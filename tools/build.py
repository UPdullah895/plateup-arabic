#!/usr/bin/env python3
"""Turn the logical-order Arabic in lang/ into the file the mod loads.

Input  : original/strings-en.json  - the full English corpus, as the game's own
                                     Export path wrote it (SourceID, SourceName, Key)
         lang/*.json               - "SourceName|Key": "logical Arabic"
Output : build/strings.json        - every original row, plus an "ar" field holding
                                     visual-order text for TextMesh Pro

Rows without a translation keep an empty "ar" and the mod falls back to the English
text, so a partial translation is a valid build.
"""
from __future__ import annotations

import json
import sys
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from coverage import Coverage
from visual import visual

ROOT = Path(__file__).resolve().parent.parent
ORIGINAL = ROOT / "original" / "strings-en.json"
LANG = ROOT / "lang"
OUT = ROOT / "build" / "strings.json"
SUBS_IN = ROOT / "lang" / "substitutions.json"
SUBS_OUT = ROOT / "build" / "substitutions.json"
SCENE_IN = ROOT / "lang" / "scene.json"
SCENE_OUT = ROOT / "build" / "scene.json"
FONTS = ROOT / "fonts"


def load_translations(rows: list[dict]) -> dict[str, str]:
    out: dict[str, str] = {}
    for path in sorted(LANG.glob("*.json")):
        if path in (SUBS_IN, SCENE_IN):
            continue
        data = json.loads(path.read_text(encoding="utf-8"))
        for key, value in data.items():
            if key.startswith("_"):
                continue
            if key in out:
                raise SystemExit(f"{path.name}: duplicate key {key!r}")
            if "|" not in key:
                raise SystemExit(f"{path.name}: key {key!r} is not 'SourceName|Key'"
                                 " (or 'Type/SourceName|Key')")
            out[key] = value
    return resolve_recipe_keys(out, rows)


def resolve_recipe_keys(translations: dict[str, str], rows: list[dict]) -> dict[str, str]:
    """A recipe's key is the numeric ID of the dish it belongs to, which is unreadable in a
    translation file, so the dish's own name may be written instead."""
    dish_id = {row["source"]: row["id"] for row in rows if row["type"] == "Dish"}
    resolved: dict[str, str] = {}
    for key, value in translations.items():
        source, _, name = key.partition("|")
        if source.endswith("Recipe Localisation") and not name.lstrip("-").isdigit():
            if name not in dish_id:
                raise SystemExit(f"no dish named {name!r} (in {key!r})")
            key = f"{source}|{dish_id[name]}"
        resolved[key] = value
    return resolved


def main() -> int:
    rows = json.loads(ORIGINAL.read_text(encoding="utf-8"))
    translations = load_translations(rows)

    # SourceName is what a translator writes; SourceID is what the game keys on. A handful of
    # names cover more than one object ("Plant" is both a crop and a decoration effect), so
    # those are written as "Type/SourceName|Key" and anything still ambiguous is an error
    # rather than a guess.
    ids_for_name: dict[str, set] = defaultdict(set)
    for row in rows:
        ids_for_name[row["source"]].add(row["id"])

    known = {f"{row['source']}|{row['key']}" for row in rows}
    known |= {f"{row['type']}/{row['source']}|{row['key']}" for row in rows}
    unknown = sorted(set(translations) - known)
    if unknown:
        raise SystemExit("no such string in the game:\n  " + "\n  ".join(unknown))
    ambiguous = sorted(k for k in translations
                       if "/" not in k.split("|", 1)[0]
                       and len(ids_for_name[k.split("|", 1)[0]]) > 1)
    if ambiguous:
        raise SystemExit("source name covers more than one object; write it as "
                         "'Type/SourceName|Key':\n  " + "\n  ".join(ambiguous))

    roles = json.loads((FONTS / "fonts.json").read_text(encoding="utf-8"))
    font = Coverage(*sorted({str(FONTS / v) for k, v in roles.items()
                             if not k.startswith("_")}))

    done = 0
    for row in rows:
        logical = translations.get(f"{row['type']}/{row['source']}|{row['key']}")
        if logical is None:
            logical = translations.get(f"{row['source']}|{row['key']}")
        if logical is None:
            row["ar"] = ""
            continue
        try:
            row["ar"] = font.fit(visual(logical))
        except ValueError as e:
            raise SystemExit(f"{row['source']}|{row['key']}: {e}") from e
        done += 1

    # Only what the mod reads at runtime. The English is needed where there is no translation
    # yet - the game's Import rebuilds a whole object from the rows it is given, so an
    # untranslated key still has to carry its English back - but everywhere else it would just
    # be republishing the game's own script, and the object type is a build-time concern.
    shipped = [{k: v for k, v in
                (("id", r["id"]), ("source", r["source"]), ("key", r["key"]),
                 ("en", r["en"]), ("ar", r["ar"]))
                if k != "en" or not r["ar"]}
               for r in rows]
    OUT.parent.mkdir(exist_ok=True)
    OUT.write_text(json.dumps(shipped, ensure_ascii=False, indent=1), encoding="utf-8")

    # The substitution overrides are shaped the same way; their values are spliced into
    # already-visual-order text, so they have to be visual order too.
    subs = {k: font.fit(visual(v))
            for k, v in json.loads(SUBS_IN.read_text(encoding="utf-8")).items()
            if not k.startswith("_")}
    SUBS_OUT.write_text(json.dumps(subs, ensure_ascii=False, indent=1), encoding="utf-8")

    # Labels typed straight into a scene are matched on their exact English text rather than a
    # key, so this file is keyed by the English itself.
    scene = {k: font.fit(visual(v))
             for k, v in json.loads(SCENE_IN.read_text(encoding="utf-8")).items()
             if not k.startswith("_")}
    SCENE_OUT.write_text(json.dumps(scene, ensure_ascii=False, indent=1), encoding="utf-8")

    report = font.report()
    if report:
        print(report)
    translatable = sum(1 for r in rows if r["en"])
    print(f"{done} translated of {translatable} translatable strings "
          f"({done * 100 // translatable}%) -> {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
