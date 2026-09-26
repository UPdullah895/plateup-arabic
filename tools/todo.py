#!/usr/bin/env python3
"""List strings that still need translating, so they can be worked through by area.

    tools/todo.py               counts per object type
    tools/todo.py Dish          every untranslated Dish string
    tools/todo.py Dish --json   the same, as a skeleton to paste into lang/
"""
from __future__ import annotations

import json
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from build import LANG, ORIGINAL, load_translations


def main(argv: list[str]) -> int:
    rows = [r for r in json.loads(ORIGINAL.read_text(encoding="utf-8")) if r["en"]]
    done = load_translations(rows)
    todo = [r for r in rows
            if f"{r['source']}|{r['key']}" not in done
            and f"{r['type']}/{r['source']}|{r['key']}" not in done]

    if not argv:
        counts = Counter(r["type"] for r in todo)
        total = Counter(r["type"] for r in rows)
        for kind, n in sorted(counts.items(), key=lambda kv: -kv[1]):
            print(f"{n:5d} left of {total[kind]:5d}  {kind}")
        print(f"{len(todo):5d} left of {len(rows):5d}  TOTAL")
        return 0

    kind = argv[0]
    picked = [r for r in todo if r["type"] == kind]
    if "--json" in argv:
        print(json.dumps({f"{r['source']}|{r['key']}": r["en"] for r in picked},
                         ensure_ascii=False, indent=2))
        return 0
    for r in sorted(picked, key=lambda r: (r["source"], r["key"])):
        print(f"{r['source']}|{r['key']}\t{r['en']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
