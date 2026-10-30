#!/usr/bin/env bash
# Construit setup-LyaranTV.exe : application + libVLC embarqués dans une charge
# utile compressée, installateur compilé avec csc.
set -eu
cd "$(dirname "$0")"
CSC="/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
FW="/c/Windows/Microsoft.NET/Framework64/v4.0.30319"

bash tmp-build.sh >/dev/null
rm -rf build/stage build/payload.zip
mkdir -p build/stage/VLC
cp build/LyaranTV.exe build/stage/LyaranTV.exe
cp -r VLC/. build/stage/VLC/
cp Installer/lisezmoi.txt build/stage/ 2>/dev/null || true

echo "Compression de la charge utile…"
powershell -NoProfile -Command "Compress-Archive -Path 'build\stage\*' -DestinationPath 'build\payload.zip' -CompressionLevel Optimal -Force" >/dev/null
ls -lh build/payload.zip | awk '{print "payload:", $5}'

cat > build/setup.rsp <<EOF
/nologo
/target:winexe
/codepage:65001
/optimize+
/out:"$(cygpath -w "$PWD")/setup-LyaranTV.exe"
/win32icon:"$(cygpath -w "$PWD")/Sources/LyaranTV.ico"
/win32manifest:"$(cygpath -w "$PWD")/Sources/LyaranTV.manifest"
/resource:"$(cygpath -w "$PWD")/build/payload.zip",payload.zip
/reference:"$(cygpath -w "$FW")/System.dll"
/reference:"$(cygpath -w "$FW")/System.Core.dll"
/reference:"$(cygpath -w "$FW")/System.Windows.Forms.dll"
/reference:"$(cygpath -w "$FW")/System.Drawing.dll"
/reference:"$(cygpath -w "$FW")/System.IO.Compression.dll"
/reference:"$(cygpath -w "$FW")/System.IO.Compression.FileSystem.dll"
"$(cygpath -w "$PWD")/Installer/Setup.cs"
EOF

MSYS_NO_PATHCONV=1 "$CSC" "@$(cygpath -w "$PWD/build/setup.rsp")" 2>&1 | grep -v "only supports language versions" || true
if [ -f setup-LyaranTV.exe ]; then
  echo "SETUP_OK $(stat -c %s setup-LyaranTV.exe) octets"
else
  echo "SETUP_FAILED"; exit 1
fi
