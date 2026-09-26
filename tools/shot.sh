#!/usr/bin/env bash
# Grab the PlateUp! window. Hyprland, so the geometry comes from hyprctl rather than xrandr.
set -euo pipefail
OUT="${1:?usage: shot.sh <file.png>}"
GEOM="$(hyprctl clients -j | python3 -c '
import json,sys
for c in json.load(sys.stdin):
    if c["class"] == "steam_app_1599600":
        print("%d,%d %dx%d" % (c["at"][0], c["at"][1], c["size"][0], c["size"][1])); break
else:
    sys.exit("PlateUp window not found")')"
grim -g "$GEOM" "$OUT"
echo "$OUT ($GEOM)"
