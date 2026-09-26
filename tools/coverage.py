"""Fit shaped Arabic to a font that only covers part of the presentation-form blocks.

Display faces very often draw the isolated shape of a letter at the letter's own
codepoint and never add the U+FE70..U+FEFC alias for it. Lalezar, for one, ships 271
presentation forms but not a single ISOLATED FORM, so a shaper's output would come out
full of holes even though every shape it asked for is in the file.

The presentation-form codepoints are compatibility characters, so Unicode itself records
what each one is a form of. Anything the font is missing falls back to that base
character, which is the same drawing.
"""
from __future__ import annotations

import unicodedata

from fontTools.ttLib import TTFont


def _is_arabic(codepoint: int) -> bool:
    return (0x0600 <= codepoint <= 0x06FF or 0x0750 <= codepoint <= 0x077F
            or 0xFB50 <= codepoint <= 0xFDFF or 0xFE70 <= codepoint <= 0xFEFF)


def _base_of(codepoint: int) -> int | None:
    """The letter an ISOLATED presentation form is a form of, per its decomposition.

    Only the isolated form, and only when it decomposes to a single letter. A font draws
    its isolated shape at the letter's own codepoint, so that swap is the same drawing -
    but an initial, medial or final form has no such stand-in, and quietly putting the
    isolated letter in its place would break the word open at that letter. A connected
    form the fonts do not have is a real gap and has to be reported as one.
    """
    decomposition = unicodedata.decomposition(chr(codepoint)).split()
    if not decomposition or decomposition[0] != "<isolated>":
        return None
    parts = [int(p, 16) for p in decomposition[1:]]
    # Lam-alef ligatures decompose to two letters and have no single stand-in.
    return parts[0] if len(parts) == 1 else None


class Coverage:
    """What a set of fonts can all draw, and the nearest thing they can for what they cannot.

    The same translated string is rendered by every font the mod installs, so a character
    is only safe if all of them have it; a form only one font is missing is replaced
    everywhere. The replacement is the same drawing in the fonts that did have the form,
    so nothing is lost by doing it uniformly.
    """

    def __init__(self, *paths: str):
        if not paths:
            raise ValueError("no fonts given")
        self.paths = paths
        self.cmap = set.intersection(*(set(TTFont(p, lazy=True).getBestCmap()) for p in paths))
        self.substituted: dict[str, str] = {}
        self.missing: set[str] = set()

    def fit(self, text: str) -> str:
        return "".join(self._fit_char(c) for c in text)

    def _fit_char(self, c: str) -> str:
        # Latin, digits, punctuation and markup are drawn by the game's own typeface; these
        # fonts are only ever reached for Arabic, so only Arabic is checked here.
        if not _is_arabic(ord(c)) or ord(c) in self.cmap:
            return c
        base = _base_of(ord(c))
        if base is not None and base in self.cmap:
            self.substituted[c] = chr(base)
            return chr(base)
        self.missing.add(c)
        return c

    def report(self) -> str:
        lines = []
        if self.substituted:
            lines.append(f"{len(self.substituted)} presentation form(s) missing from at least "
                         f"one font, drawn with the base letter instead")
        if self.missing:
            lines.append("NOT COVERED AT ALL: "
                         + " ".join(f"U+{ord(c):04X} ({c})" for c in sorted(self.missing)))
        return "\n".join(lines)
