#!/usr/bin/env bash
#
# Install the Arabic translation of PlateUp!
#
#   ./install.sh                     find the game and install
#   ./install.sh /path/to/PlateUp    install into a folder you name
#   ./install.sh --uninstall         remove it again
#   ./install.sh --where             just print where the game is
#
# The mod is a folder inside the game; installing copies it in and uninstalling deletes it,
# and the game is untouched either way.
set -euo pipefail

APPID=1599600
MOD=PlateUpArabic
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

say()  { printf '%s\n' "$*"; }
warn() { printf '%s\n' "$*" >&2; }
die()  { printf 'error: %s\n' "$*" >&2; exit 1; }

usage() {
    sed -n '3,10p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    exit "${1:-0}"
}

# --- finding the game ------------------------------------------------------------------

# Every place a Steam install puts its root, including the Flatpak one and the Steam Deck's.
steam_roots() {
    printf '%s\n' \
        "$HOME/.local/share/Steam" \
        "$HOME/.steam/steam" \
        "$HOME/.steam/root" \
        "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam" \
        "$HOME/Library/Application Support/Steam" \
        "/usr/local/share/Steam" \
        "/usr/share/steam"
}

# Steam keeps its library list in a Valve key-value file. Pulling the "path" lines out of it
# is enough; a full parser is not worth it for one field.
library_paths() {
    local root=$1 vdf="$1/steamapps/libraryfolders.vdf"
    printf '%s\n' "$root"
    [ -f "$vdf" ] || return 0
    sed -n 's/^[[:space:]]*"path"[[:space:]]*"\(.*\)"[[:space:]]*$/\1/p' "$vdf"
}

# A folder is the game if the executable or its data folder is in it. PlateUp! ships only a
# Windows build, which on Linux runs through Proton, so PlateUp.exe is the right thing to look
# for on every platform.
is_game() {
    [ -n "${1:-}" ] && { [ -f "$1/PlateUp.exe" ] || [ -d "$1/PlateUp_Data" ]; }
}

# Steam records the folder name it installed into; inside it the game sits either at the top
# or one level down, so check both rather than assuming.
game_under() {
    local base=$1 sub
    for sub in "$base" "$base/PlateUp"; do
        if is_game "$sub"; then printf '%s\n' "$sub"; return 0; fi
    done
    return 1
}

find_game() {
    local root lib manifest installdir found
    for root in $(steam_roots); do
        [ -d "$root" ] || continue
        while IFS= read -r lib; do
            [ -d "$lib" ] || continue
            manifest="$lib/steamapps/appmanifest_$APPID.acf"
            if [ -f "$manifest" ]; then
                installdir=$(sed -n 's/^[[:space:]]*"installdir"[[:space:]]*"\(.*\)"[[:space:]]*$/\1/p' "$manifest")
                [ -n "$installdir" ] || installdir=PlateUp
                if found=$(game_under "$lib/steamapps/common/$installdir"); then
                    printf '%s\n' "$found"; return 0
                fi
            fi
            # No manifest, but the folder may still be there - a moved or copied install.
            if found=$(game_under "$lib/steamapps/common/PlateUp"); then
                printf '%s\n' "$found"; return 0
            fi
        done < <(library_paths "$root")
    done
    return 1
}

# Explains itself before giving up, because the caller runs it in a command substitution -
# where an exit would only leave the subshell - and so cannot add anything useful afterwards.
ask_for_game() {
    local answer
    warn "Could not find PlateUp! automatically."
    if [ ! -t 0 ]; then
        warn "Run: $0 /path/to/PlateUp"
        return 1
    fi
    warn "Give the folder that has PlateUp.exe in it, or press Enter to give up."
    while true; do
        printf 'Game folder: ' >&2
        IFS= read -r answer || { warn "Nothing installed."; return 1; }
        [ -n "$answer" ] || { warn "Nothing installed."; return 1; }
        answer="${answer/#\~/$HOME}"
        if answer=$(game_under "$answer"); then printf '%s\n' "$answer"; return 0; fi
        warn "There is no PlateUp.exe in that folder."
    done
}

# --- the mod itself --------------------------------------------------------------------

find_payload() {
    local dir
    for dir in "$HERE/$MOD" "$HERE/dist/$MOD"; do
        [ -f "$dir/$MOD.dll" ] && { printf '%s\n' "$dir"; return 0; }
    done
    return 1
}

# --- go --------------------------------------------------------------------------------

action=install
game=

while [ $# -gt 0 ]; do
    case $1 in
        -h|--help)    usage ;;
        -u|--uninstall) action=uninstall ;;
        -w|--where)   action=where ;;
        --game)       game=${2:?--game needs a path}; shift ;;
        -*)           warn "unknown option: $1"; usage 1 ;;
        *)            game=$1 ;;
    esac
    shift
done

if [ -n "$game" ]; then
    # Keep what was typed: the substitution below leaves $game empty when it fails.
    given="${game/#\~/$HOME}"
    game=$(game_under "$given") || die "no PlateUp.exe in $given"
    [ "$action" = where ] || say "Game: $game"
elif game=$(find_game); then
    [ "$action" = where ] || say "Found the game: $game"
else
    game=$(ask_for_game) || exit 1
fi

if [ "$action" = where ]; then
    printf '%s\n' "$game"
    exit 0
fi

target="$game/Mods/$MOD"

if [ "$action" = uninstall ]; then
    if [ -d "$target" ]; then
        rm -rf "$target"
        say "Removed. PlateUp! is back to English."
    else
        say "Nothing to remove - the mod is not installed."
    fi
    exit 0
fi

payload=$(find_payload) || die "the mod is missing from $HERE.
Download the release archive and run install.sh from inside it, or build it with tools/release.sh."

rm -rf "$target"
mkdir -p "$target"
cp -R "$payload/." "$target/"

say ""
say "Installed into $target"
say "Start PlateUp!, open Settings and choose العربية in the language list."
say "To remove it later: $0 --uninstall"
