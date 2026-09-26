#!/usr/bin/env bash
# One round trip: build, deploy, restart the game, wait for the mod to report in.
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MOD="${PLATEUP_DIR:-$("$HERE/install.sh" --where)}/Mods/PlateUpArabic"

"$HERE/tools/game.sh" stop
"$HERE/venv/bin/python" "$HERE/tools/build.py" || exit 1
# Keep the build's own errors: a silent deploy failure here just looks like the
# game never started.
"$HERE/tools/deploy.sh" >/dev/null || { "$HERE/tools/deploy.sh"; exit 1; }
rm -f "$MOD/arabic.log"
"$HERE/tools/game.sh" start
for _ in $(seq 120); do
  grep -qE "Applied Arabic|Failed:|nothing to do" "$MOD/arabic.log" 2>/dev/null && break
  sleep 2
done
cat "$MOD/arabic.log"
