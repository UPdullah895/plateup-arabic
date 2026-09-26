# Fonts

Two faces are shipped with the mod and installed as TextMesh Pro fallbacks, mapped to the
game's own fonts by role in `fonts.json`:

| | |
|---|---|
| [Lalezar](https://github.com/BornaIz/Lalezar) | display text — the main menu, titles, large labels |
| [Noto Sans Arabic](https://github.com/notofonts/arabic) (Black) | body text — cards, tooltips, everything else |

Both are under the SIL Open Font License 1.1; the licence for each is in `LICENCES/`, and the
release archive carries them too.

`fonts.json` maps a game font's name to the file that should back it, with `*` as the default.
`tools/coverage.py` reports any Arabic codepoint the installed faces cannot all draw.
