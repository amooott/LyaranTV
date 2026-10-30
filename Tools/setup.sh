#!/usr/bin/env bash
# Construit Dist/setup-LyaranTV-<version>.exe : application + libVLC embarqués
# dans une charge utile compressée, installateur compilé avec csc. Écrit aussi
# les empreintes SHA-256 publiées avec la Release.
set -eu
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
CSC="/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
FW="/c/Windows/Microsoft.NET/Framework64/v4.0.30319"

# La version vient de Sources/App/AppInfo.cs : une seule source de vérité.
VERSION="$(sed -n 's/.*public const string Version = "\([0-9][0-9.]*\)".*/\1/p' Sources/App/AppInfo.cs | head -1)"
if [ -z "$VERSION" ]; then
  echo "VERSION_INTROUVABLE dans Sources/App/AppInfo.cs"; exit 1
fi
SETUP="setup-LyaranTV-$VERSION.exe"
echo "Version : $VERSION"

# Jeton GitHub injecté à l'installation : variable d'environnement ou fichier
# local build/update-token.txt (jamais versionné).
TOKEN_VALUE="${LYARANTV_UPDATE_TOKEN:-}"
if [ -z "$TOKEN_VALUE" ] && [ -f build/update-token.txt ]; then
  TOKEN_VALUE="$(tr -d '\r\n' < build/update-token.txt)"
fi
mkdir -p build
cat > build/UpdateToken.cs <<EOF
namespace LyaranTV.Setup
{
    internal static class UpdateToken
    {
        public const string Value = "$TOKEN_VALUE";
    }
}
EOF
echo "Jeton de mise à jour : $([ -n "$TOKEN_VALUE" ] && echo intégré || echo absent)"

bash Tools/build.sh >/dev/null
rm -rf build/stage build/payload.zip
rm -f Dist/setup-LyaranTV*.exe Dist/setup-LyaranTV*.exe.sha256
mkdir -p build/stage/VLC Dist
cp Dist/LyaranTV.exe build/stage/LyaranTV.exe
cp -r VLC/. build/stage/VLC/
cp Installer/lisezmoi.txt build/stage/ 2>/dev/null || true

echo "Compression de la charge utile…"
powershell -NoProfile -Command "Compress-Archive -Path 'build\\stage\\*' -DestinationPath 'build\\payload.zip' -CompressionLevel Optimal -Force" >/dev/null
ls -lh build/payload.zip | awk '{print "payload:", $5}'

ROOTW="$(cygpath -w "$ROOT")"
FWW="$(cygpath -w "$FW")"
cat > build/setup.rsp <<EOF
/nologo
/target:winexe
/codepage:65001
/optimize+
/out:"$ROOTW\\Dist\\$SETUP"
/win32icon:"$ROOTW\\Sources\\LyaranTV.ico"
/win32manifest:"$ROOTW\\Sources\\LyaranTV.manifest"
/resource:"$ROOTW\\build\\payload.zip",payload.zip
/reference:"$FWW\\System.dll"
/reference:"$FWW\\System.Core.dll"
/reference:"$FWW\\System.Windows.Forms.dll"
/reference:"$FWW\\System.Drawing.dll"
/reference:"$FWW\\System.IO.Compression.dll"
/reference:"$FWW\\System.IO.Compression.FileSystem.dll"
/reference:"$FWW\\System.Security.dll"
/reference:"$FWW\\System.Web.Extensions.dll"
"$ROOTW\\build\\UpdateToken.cs"
"$ROOTW\\Installer\\Setup.cs"
EOF

rm -f "Dist/$SETUP"
MSYS_NO_PATHCONV=1 "$CSC" "@$(cygpath -w "$ROOT/build/setup.rsp")" 2>&1 | grep -v "only supports language versions" || true
if [ -f "Dist/$SETUP" ]; then
  echo "SETUP_OK $(stat -c %s "Dist/$SETUP") octets -> Dist/$SETUP"
else
  echo "SETUP_FAILED"; exit 1
fi

# Empreintes publiées dans la Release (format « hash  nom.exe »).
cd Dist
for f in "$SETUP" LyaranTV.exe; do
  powershell -NoProfile -Command "(Get-FileHash '$f' -Algorithm SHA256).Hash.ToLower()" | tr -d '\r' | awk -v n="$f" '{print $1"  "n}' > "$f.sha256"
done
echo "EMPREINTES: $(cat "$SETUP.sha256")"
