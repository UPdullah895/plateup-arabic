#!/usr/bin/env bash
# Build the mod and put it straight into the copy of the game on this machine.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GAME="${PLATEUP_DIR:-$("$HERE/install.sh" --where)}"
DEST="$GAME/Mods/PlateUpArabic"

"$HERE/tools/stage.sh" "$DEST"
echo "deployed -> $DEST"
