#!/usr/bin/env bash
# Change la version de LyaranTV partout d'un seul coup : identité de
# l'application (affichage, mise à jour), installateur et attributs de
# l'assembly. À lancer avant chaque modification publiée.
#
#   Tools/version.sh 1.1.0
#
set -eu
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

CURRENT="$(sed -n 's/.*public const string Version = "\([0-9][0-9.]*\)".*/\1/p' Sources/App/AppInfo.cs | head -1)"
VERSION="${1:-}"

if ! printf '%s' "$VERSION" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+$'; then
  echo "Usage : Tools/version.sh <majeure.mineure.correctif>   (exemple : 1.1.0)"
  echo "Version actuelle : ${CURRENT:-inconnue}"
  exit 1
fi
if [ "$VERSION" = "$CURRENT" ]; then
  echo "La version est déjà $VERSION."
  exit 0
fi

sed -i "s/public const string Version = \"[0-9][0-9.]*\";/public const string Version = \"$VERSION\";/" Sources/App/AppInfo.cs
sed -i "s/public const string Version = \"[0-9][0-9.]*\";/public const string Version = \"$VERSION\";/" Installer/Setup.cs
sed -i "s/AssemblyVersion(\"[0-9][0-9.]*\")/AssemblyVersion(\"$VERSION.0\")/" Sources/App/AssemblyInfo.cs
sed -i "s/AssemblyFileVersion(\"[0-9][0-9.]*\")/AssemblyFileVersion(\"$VERSION.0\")/" Sources/App/AssemblyInfo.cs

grep -q "public const string Version = \"$VERSION\";" Sources/App/AppInfo.cs || { echo "Échec : AppInfo.cs"; exit 1; }
grep -q "public const string Version = \"$VERSION\";" Installer/Setup.cs || { echo "Échec : Setup.cs"; exit 1; }
grep -q "AssemblyFileVersion(\"$VERSION.0\")" Sources/App/AssemblyInfo.cs || { echo "Échec : AssemblyInfo.cs"; exit 1; }

echo "Version LyaranTV : $CURRENT -> $VERSION"
echo "Installateur attendu : Dist/setup-LyaranTV-$VERSION.exe"
echo "Reste à faire : mettre à jour Docs/RELEASE-NOTES.md, puis taguer v$VERSION."
