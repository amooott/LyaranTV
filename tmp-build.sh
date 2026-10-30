#!/usr/bin/env bash
# Compile LyaranTV vers build/LyaranTV.exe (copie non verrouillée), avec les
# mêmes références et le même manifeste que le script officiel.
set -u
cd "$(dirname "$0")"
mkdir -p build
cat > _build-temp.bat <<'EOF'
@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "WPF=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "RSP=%TEMP%\lyarantv-build-%RANDOM%-%RANDOM%.rsp"
> "%RSP%" echo /nologo
>>"%RSP%" echo /target:winexe
>>"%RSP%" echo /codepage:65001
>>"%RSP%" echo /optimize+
>>"%RSP%" echo /out:"%ROOT%build\LyaranTV.exe"
>>"%RSP%" echo /win32icon:"%ROOT%Sources\LyaranTV.ico"
>>"%RSP%" echo /win32manifest:"%ROOT%Sources\LyaranTV.manifest"
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
for /r "%ROOT%Sources" %%F in (*.cs) do >>"%RSP%" echo "%%~fF"
echo Compilation de LyaranTV...
"%CSC%" "@%RSP%"
set "RESULT=%ERRORLEVEL%"
del "%RSP%" >nul 2>&1
if not "%RESULT%"=="0" exit /b %RESULT%
echo BUILD_OK
exit /b 0
EOF
rm -f build/LyaranTV.exe
LOG=$(cmd //c "$(cygpath -w "$PWD/_build-temp.bat")" 2>&1 | grep -v "only supports language versions" | grep -v '^$')
rm -f _build-temp.bat
echo "$LOG"
if echo "$LOG" | grep -q "BUILD_OK" && [ -f build/LyaranTV.exe ]; then
  echo "BINAIRE: $(stat -c %s build/LyaranTV.exe) octets"
else
  echo "BUILD_FAILED"; exit 1
fi
