#!/usr/bin/env bash
# Assemble the mod into a folder laid out the way it is installed, and nothing else.
#
# PlateUp!'s loader treats every file in a mod folder's root as a mod pack - it Assembly.Loads
# the .dll and tries to read anything else as an AssetBundle - so only the assembly may sit at
# the top and everything it reads at runtime goes in arabic/ beside it.
set -euo pipefail
DEST="${1:?usage: stage.sh <folder>}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

for f in strings.json substitutions.json scene.json; do
  [ -f "$HERE/build/$f" ] || { echo "missing build/$f - run tools/build.py first" >&2; exit 1; }
done

dotnet build -v q --nologo "$HERE/src/PlateUpArabic/PlateUpArabic.csproj"

mkdir -p "$DEST/arabic"
# Drop what we put there last time, so a renamed font or a removed data file does not linger.
rm -f "$DEST/arabic"/*.ttf "$DEST/arabic"/*.otf "$DEST/arabic"/*.json
cp "$HERE/src/PlateUpArabic/bin/Debug/PlateUpArabic.dll" "$DEST/PlateUpArabic.dll"
cp "$HERE/build/strings.json" "$HERE/build/substitutions.json" "$HERE/build/scene.json" \
   "$DEST/arabic/"
cp "$HERE"/fonts/*.ttf "$DEST/arabic/"
cp "$HERE/fonts/fonts.json" "$DEST/arabic/fonts.json"
