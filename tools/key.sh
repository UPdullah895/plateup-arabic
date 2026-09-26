#!/usr/bin/env bash
# Send keys to PlateUp!. Wayland, so input goes through ydotool's uinput device, which
# delivers to whatever is focused - hence the check rather than a focus dispatch (this
# Hyprland's dispatcher API is lua and its spelling moves between versions).
set -euo pipefail
active="$(hyprctl activewindow -j | python3 -c 'import json,sys;print(json.load(sys.stdin).get("class",""))')"
if [ "$active" != "steam_app_1599600" ]; then
  echo "PlateUp! is not focused (active: ${active:-none}); click it first" >&2
  exit 1
fi
for k in "$@"; do
  ydotool key "$k"
  sleep 0.4
done
