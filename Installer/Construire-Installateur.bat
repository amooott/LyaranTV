@echo off
setlocal EnableExtensions
rem Construit setup-LyaranTV.exe : application + libVLC embarqués.
rem Usage : Construire-Installateur.bat [chemin\LyaranTV.exe]
rem Sans argument, utilise le LyaranTV.exe de la racine (compilez d'abord).

set "ROOT=%~dp0.."
set "WORK=%~dp0build"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

set "APP=%~1"
if "%APP%"=="" set "APP=%ROOT%\LyaranTV.exe"
if not exist "%APP%" (
  echo Application introuvable : %APP%
  echo Compilez d'abord Sources\Reconstruire.bat.
  exit /b 1
)
if not exist "%ROOT%\VLC\libvlc.dll" (
  echo libVLC introuvable dans %ROOT%\VLC
  exit /b 1
)

if exist "%WORK%" rmdir /s /q "%WORK%"
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

set "RSP=%WORK%\setup.rsp"
> "%RSP%" echo /nologo
>>"%RSP%" echo /target:winexe
>>"%RSP%" echo /codepage:65001
>>"%RSP%" echo /optimize+
>>"%RSP%" echo /out:"%ROOT%\setup-LyaranTV.exe"
>>"%RSP%" echo /win32icon:"%ROOT%\Sources\LyaranTV.ico"
>>"%RSP%" echo /win32manifest:"%ROOT%\Sources\LyaranTV.manifest"
>>"%RSP%" echo /resource:"%WORK%\payload.zip",payload.zip
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.FileSystem.dll"
>>"%RSP%" echo "%~dp0Setup.cs"

echo Compilation de l'installateur...
"%CSC%" "@%RSP%"
if errorlevel 1 (
  echo Echec de la compilation de l'installateur.
  exit /b 1
)
del "%RSP%" >nul 2>&1
echo Installateur genere : %ROOT%\setup-LyaranTV.exe
exit /b 0
