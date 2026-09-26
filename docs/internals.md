# How the translation works

Notes for anyone working on the mod rather than playing with it. For installing it, see
the [README](../README.md).

## Working on it

```bash
./tools/test.sh
```

builds, deploys to `<game>/PlateUp/Mods/PlateUpArabic/`, restarts the game and prints the mod's
log. `tools/build.py` alone is enough to check a translation compiles; it needs no game.
`tools/release.sh` produces the archive that players download from Releases: the mod, both
installers and the licences, zipped under one folder.

The mod is built with `dotnet` against the game's own assemblies (`$(GameManaged)` in the
csproj). The Python tools want a virtualenv:

```bash
python3 -m venv venv && ./venv/bin/pip install -r requirements.txt
```

## How it works

PlateUp! loads mods itself through `KitchenMods.dll`, so there is no BepInEx, no Harmony and no
asset bundle. A folder under `Mods/` holding a `.dll` is enough: the loader `Assembly.Load`s it
and instantiates every `IModInitializer`. Data the mod reads at runtime sits in an `arabic/`
subfolder, because anything in the folder root is treated as a mod pack.

Five things have to happen, and the mod does them in this order.

**1. The corpus.** `Kitchen.GameData.dll` still contains the developers' own CSV round-trip
(`LocalisationContext`, `LocalisationRow`, `ILocalised.Export`). One runtime pass over
`GameData.Main.Objects` exports all 3110 strings keyed by `(SourceID, Key)` — the authoritative
list, not a guess at Odin-serialised assets. That export is checked in as
`original/strings-en.json` so builds do not need the game.

**2. The font.** Unity cannot build a `Font` from bytes at runtime, so `FontBuilder` drives
TextCore's `FontEngine` directly (much of it through reflection, since `ResetAtlasTexture` and
`TryAddGlyphsToTexture` are internal) and hand-assembles a **static** `TMP_FontAsset`. Static
matters: TMP then never tries to reload the face.

The Arabic asset is installed as a **fallback** of each font the game substitutes in, not as a
replacement. Latin text, digits and sprite tags keep PlateUp's own typeface, and the entry
survives `FontLookup.SetLocale`, which clears the base fonts' fallback tables but never touches
the substituted fonts' own.

Two faces are used, mapped by role in `fonts/fonts.json`: Lalezar for display text and Noto Sans
Arabic Black for body text.

**3. The text.** `tools/build.py` shapes each translation — joins the letters into their
presentation forms and runs the Unicode bidi algorithm — so what ships is already in *visual*
order. TextMesh Pro has neither a shaper nor a bidi implementation, so it has to arrive that way.
`Inject` then feeds the rows back through the game's own `Localisation.Import` and calls
`GameData.ReLocalise`.

Short vowels and shadda are **dropped** while shaping. TMP does no mark positioning: a combining
mark has no advance of its own, both fonts draw its ink at or right of the origin and rely on
GPOS anchors to move it over its letter, and TMP ignores those anchors — so the mark is drawn on
the boundary with the next glyph or on the next glyph outright, whichever way the line runs. It
also breaks joining, which is why `لًا` came out as two loose letters instead of a lam-alef
ligature. The translation files keep their marks so the source stays correctly spelled.

**4. Labels with no key.** A few strings are typed straight into a scene instead of going through
`Localisation` — the splash screen's "Press any button" is one. `SceneText` walks the loaded
scenes and matches those on their exact English text (`lang/scene.json`). Any it does not
recognise are reported in `dump/scene-text.json`, so the set is visible rather than guessed at.

**5. Wrapping.** Visual order is correct for one line, but when TMP runs out of width it breaks
the line itself, and its first line then holds the *end* of the sentence. `Wrap` subscribes to
`TMPro_EventManager.TEXT_CHANGED_EVENT`, lets TMP wrap, and rewrites the text with the
soft-wrapped lines of each paragraph in the opposite order and the breaks made explicit. Hard
breaks written into a translation are already in reading order, so paragraphs keep theirs and
only the lines inside one are flipped. A paragraph that was reordered is also right-aligned,
since a ragged-right Arabic paragraph reads wrong.

Two details that cost time and are easy to hit again:

- Assigning `label.text` from inside TMP's own generation callback leaves the label dirty at a
  point where the canvas has already decided what to rebuild, and the change is silently dropped.
  The rewrite is queued and applied from `LateUpdate` instead.
- `LateUpdate`, not `Update`: some labels are assigned afresh every frame, so the correction has
  to land *after* that frame's assignment.

A label a **view** reassigns on every refresh cannot be corrected this way at all — the view wins
the race about as often as we do, and the text visibly flips between readings. `JOIN_PROMPT` and
`SELECT_PROFILE_PROMPT` (both written by `PlayerColourView.UpdateData`) are the two in this game,
and they carry an explicit line break in `lang/ui.json` so they never soft-wrap in the first
place. If a new label ever flickers between two line orders, that is the fix: break it by hand.

### The carrier locale

The `Locale` enum is closed and has no Arabic member, so Arabic is written into the English slot
and the language menu's English entry is relabelled العربية. The other entries in that menu are
each language's own name for itself and are deliberately left alone.

## Layout

| | |
|---|---|
| `lang/*.json` | the translation, `"SourceName\|Key": "logical-order Arabic"` |
| `original/strings-en.json` | the English corpus, as the game exported it |
| `docs/glossary.md` | fixed terminology and style rules — read this before translating |
| `fonts/` | the two faces and the role map |
| `tools/build.py` | shapes and reorders the translation into `build/` |
| `tools/visual.py` | the shaper and bidi algorithm |
| `tools/coverage.py` | which codepoints the installed fonts actually have |
| `tools/todo.py` | what is still untranslated, by object type |
| `src/PlateUpArabic/` | the mod |
| `install.sh`, `install-windows.*` | the installers shipped in the release archive |
| `tools/stage.sh` | lays the mod out the way it is installed; used by both deploy and release |

Keys are `SourceName|Key`. Where one name covers two objects ("Plant" is both a crop and a
decoration effect) write `Type/SourceName|Key`; anything still ambiguous is an error rather than
a guess. Recipes are keyed by the numeric ID of their dish, so the dish's name may be written
instead and the build resolves it.

## State

2223 of 2234 translatable strings. The eleven left are the language menu's endonyms, which are
correct untranslated.

Verified in game: splash, main menu, profile, options, advanced settings, pause menu, the
control legend, and the franchise HQ — which between them cover world-space and canvas labels,
wrapped paragraphs, hard-broken paragraphs and embedded `<size>` tags.

Not yet verified in game: the in-day screens (cards, shop, recipe book, day HUD). Reaching them
needs the chef walked to the planning board and a dish chosen, which the keyboard automation here
could not drive. They use the same rendering path as everything above, so this is untested rather
than known-broken.

Dropping the marks changed what some words say, so 113 strings were reworded: the imperative of
طها is اطهُ and `اطه` is not a word (the whole cook family moved to the طبخ root), `صِل` strips to
`صل` which reads as "pray", `أنهِ` to `أنه` — "that he", `كُل` to `كل` — "all". Shadda and tanwin
on an alef strip harmlessly. The rule of thumb is in `docs/glossary.md`: never let a diacritic
carry a meaning the consonants do not.

Known limits:

- Single-line labels the layout left-aligns are still left-aligned; only wrapped paragraphs are
  moved. Mirroring the whole UI was not attempted.
- A rich-text tag that spans one of TMP's own line breaks is closed and reopened per line when
  the lines are reordered. Nesting deeper than the tags PlateUp actually uses is untested.
- Modded runs are excluded from speedrun leaderboards (`ModPreload.IsModded` → `SModdedRun`).
  Achievements and multiplayer are unaffected; there is no mod handshake.
