@echo off
setlocal EnableExtensions
rem Construit l'installateur versionne avec l'application et libVLC embarques.
rem La version est lue dans Sources\App\AppInfo.cs.
rem Usage : Construire-Installateur.bat [chemin\LyaranTV.exe]
rem Sans argument, utilise Dist\LyaranTV.exe (compilez d'abord).

set "ROOT=%~dp0.."
set "WORK=%~dp0build"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

set "APP=%~1"
if "%APP%"=="" set "APP=%ROOT%\Dist\LyaranTV.exe"
if not exist "%APP%" (
  echo Application introuvable : %APP%
  echo Compilez d'abord Sources\Reconstruire.bat.
  exit /b 1
)
if not exist "%ROOT%\VLC\libvlc.dll" (
  echo libVLC introuvable dans %ROOT%\VLC
  exit /b 1
)

rem Version exacte, lue dans AppInfo.cs (source unique de vérité).
set "VERSION="
for /f "tokens=6 delims= " %%V in ('findstr /c:"public const string Version" "%ROOT%\Sources\App\AppInfo.cs"') do set "VERSION=%%V"
set "VERSION=%VERSION:"=%"
set "VERSION=%VERSION:;=%"
if "%VERSION%"=="" (
  echo Version introuvable dans Sources\App\AppInfo.cs
  exit /b 1
)
set "SETUP_NAME=setup-LyaranTV-%VERSION%.exe"
echo Version : %VERSION%

if exist "%WORK%" rmdir /s /q "%WORK%"
if not exist "%ROOT%\Dist" mkdir "%ROOT%\Dist"
del /q "%ROOT%\Dist\setup-LyaranTV*.exe" >nul 2>&1
del /q "%ROOT%\Dist\setup-LyaranTV*.exe.sha256" >nul 2>&1
mkdir "%WORK%\stage\VLC"
copy /y "%APP%" "%WORK%\stage\LyaranTV.exe" >nul
xcopy /e /i /q /y "%ROOT%\VLC" "%WORK%\stage\VLC" >nul
if exist "%~dp0lisezmoi.txt" copy /y "%~dp0lisezmoi.txt" "%WORK%\stage\" >nul

echo Compression de la charge utile...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%WORK%\stage\*' -DestinationPath '%WORK%\payload.zip' -CompressionLevel Optimal -Force"
if not exist "%WORK%\payload.zip" (
  echo Echec de la compression.
  exit /b 1
)

rem Aucun jeton personnel n'est embarque dans l'installateur.
> "%WORK%\UpdateToken.cs" echo namespace LyaranTV.Setup
>>"%WORK%\UpdateToken.cs" echo {
>>"%WORK%\UpdateToken.cs" echo     internal static class UpdateToken
>>"%WORK%\UpdateToken.cs" echo     {
>>"%WORK%\UpdateToken.cs" echo         public const string Value = "";
>>"%WORK%\UpdateToken.cs" echo     }
>>"%WORK%\UpdateToken.cs" echo }
echo Aucun jeton personnel n'est integre ; l'utilisateur pourra le saisir dans Parametres.

set "RSP=%WORK%\setup.rsp"
> "%RSP%" echo /nologo
>>"%RSP%" echo /target:winexe
>>"%RSP%" echo /codepage:65001
>>"%RSP%" echo /optimize+
>>"%RSP%" echo /out:"%ROOT%\Dist\%SETUP_NAME%"
>>"%RSP%" echo /win32icon:"%ROOT%\Sources\LyaranTV.ico"
>>"%RSP%" echo /win32manifest:"%ROOT%\Sources\LyaranTV.manifest"
>>"%RSP%" echo /resource:"%WORK%\payload.zip",payload.zip
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.FileSystem.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Security.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Web.Extensions.dll"
>>"%RSP%" echo "%WORK%\UpdateToken.cs"
>>"%RSP%" echo "%~dp0Setup.cs"

echo Compilation de l'installateur...
"%CSC%" "@%RSP%"
if errorlevel 1 (
  echo Echec de la compilation de l'installateur.
  exit /b 1
)
del "%RSP%" >nul 2>&1
echo Installateur genere : %ROOT%\Dist\%SETUP_NAME%
exit /b 0
