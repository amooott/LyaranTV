@echo off
setlocal EnableExtensions
set "ROOT=%~dp0.."
if not exist "%ROOT%\Dist" mkdir "%ROOT%\Dist"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "WPF=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Compilateur .NET Framework introuvable.
  exit /b 1
)
if not exist "%WPF%\PresentationFramework.dll" (
  echo Assemblies WPF introuvables.
  exit /b 1
)

set "RSP=%TEMP%\lyarantv-build-%RANDOM%-%RANDOM%.rsp"
> "%RSP%" echo /nologo
>>"%RSP%" echo /target:winexe
>>"%RSP%" echo /codepage:65001
>>"%RSP%" echo /optimize+
>>"%RSP%" echo /out:"%ROOT%\Dist\LyaranTV.exe"
>>"%RSP%" echo /win32icon:"%~dp0LyaranTV.ico"
>>"%RSP%" echo /win32manifest:"%~dp0LyaranTV.manifest"
>>"%RSP%" echo /reference:"%WPF%\PresentationFramework.dll"
>>"%RSP%" echo /reference:"%WPF%\PresentationCore.dll"
>>"%RSP%" echo /reference:"%WPF%\WindowsBase.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.Serialization.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Web.Extensions.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Security.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Xml.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Xml.Linq.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.dll"
>>"%RSP%" echo /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.FileSystem.dll"

for /r "%~dp0" %%F in (*.cs) do >>"%RSP%" echo "%%~fF"

echo Compilation de LyaranTV...
"%CSC%" "@%RSP%"
set "RESULT=%ERRORLEVEL%"
del "%RSP%" >nul 2>&1
if not "%RESULT%"=="0" (
  echo.
  echo Echec de la compilation.
  exit /b %RESULT%
)
echo Application generee : %ROOT%\Dist\LyaranTV.exe
exit /b 0
