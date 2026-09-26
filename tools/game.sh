#!/usr/bin/env bash
# Start / stop / tail PlateUp!. The Proton process tree is matched narrowly so this never
# touches the shell it runs from.
set -uo pipefail
APPID=1599600
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GAME="${PLATEUP_DIR:-$("$HERE/install.sh" --where)}"
# The game runs under Proton, so its log lands in the prefix's Windows-side AppData, and the
# prefix lives in the steamapps folder of whichever library the game was installed into.
# Walk up to find it rather than counting directories: the game sits one or two levels under
# common/ depending on how it was installed.
STEAMAPPS="$GAME"
while [ "$STEAMAPPS" != / ] && [ "$(basename "$STEAMAPPS")" != steamapps ]; do
  STEAMAPPS="$(dirname "$STEAMAPPS")"
done
LOG="$STEAMAPPS/compatdata/$APPID/pfx/drive_c/users/steamuser/AppData/LocalLow/It's Happening/PlateUp/Player.log"

# steam.sh is in the main Steam root, which is not the same place as a second library.
steam_sh() {
  local root
  for root in "$HOME/.local/share/Steam" "$HOME/.steam/steam" "$HOME/.steam/root" \
              "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam"; do
    [ -x "$root/steam.sh" ] && { printf '%s\n' "$root/steam.sh"; return 0; }
  done
  command -v steam
}

# Match the Windows-style path Proton passes, without letting the pattern match this
# script or the shell that runs it.
pids() { pgrep -f 'PlateUp[.]exe -appid'; }

case "${1:-}" in
  start)
    rm -f "$LOG"
    setsid "$(steam_sh)" -applaunch $APPID >/dev/null 2>&1 &
    echo "launching..."
    ;;
  stop)
    found="$(pids | tr '\n' ' ')"
    for p in $found; do kill "$p" 2>/dev/null; done
    for _ in $(seq 40); do pids >/dev/null || break; sleep 0.5; done
    echo "stopped: ${found:-nothing was running}"
    ;;
  log)   cat "$LOG" ;;
  path)  echo "$LOG" ;;
  status) pids >/dev/null && echo running || echo stopped ;;
  *) echo "usage: game.sh start|stop|log|path|status" >&2; exit 2 ;;
esac
