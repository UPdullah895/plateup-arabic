# Releasing

The installers are the product, so every release ends with an install test, not a
`git push`.

## Before you build

1. `VERSION` — bump it for any change that ships.
2. `SUPPORTED_GAME` — the exact build of the game you tested against. The README
   row «اللعبة المدعومة» must contain that string; the docs check enforces it.
3. `python3 tools/check_docs.py` must print `docs ok`. CI runs it on every push to
   `main`, so do not tag while it fails.

## Build the archive

```bash
tools/release.sh
```

It builds the payload, stages `dist/PlateUpArabic-<VERSION>/`, normalises the
Windows launchers to CRLF inside the archive, keeps `install.sh` at 0755, and
writes `dist/PlateUpArabic-<VERSION>.zip.sha256`.

## Test it before anyone downloads it

```bash
mkdir -p /tmp/fake/PlateUp && touch /tmp/fake/PlateUp/PlateUp.exe
cd "dist/PlateUpArabic-<VERSION>"
./install.sh "/tmp/fake/PlateUp"
find "/tmp/fake/PlateUp" -maxdepth 3
./install.sh --uninstall "/tmp/fake/PlateUp"
```

The mod must land in `Mods/PlateUpArabic`. If it lands anywhere else, the
installer found the wrong folder and the release is not ready.

## Publish

- `git tag -a v<VERSION> -m "PlateUpArabic <VERSION>" && git push origin v<VERSION>`
- Attach `dist/PlateUpArabic-<VERSION>.zip` **and** the `.sha256` file.
- Notes: what changed, install steps for Windows / Linux / Steam Deck, what is
  deliberately left in English, and what to do after a game update.
- `packaging/README.txt` is what players see inside the zip: edit it in the same
  commit as the change, or the archive ships yesterday's instructions.

## After publishing

- `python3 tools/check_docs.py` still prints `docs ok`.
- Open the raw README on `main` and one `screenshots/` URL: both must load.
- If the game version moved, update `SUPPORTED_GAME`, the README row, and the
  release title together.
