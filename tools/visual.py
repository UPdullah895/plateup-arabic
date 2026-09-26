"""Turn logical Arabic into the visual form TextMesh Pro draws correctly.

PlateUp! renders all its text with TextMesh Pro, which has no Arabic shaper, no
bidirectional algorithm and no RTL layout. Nothing at runtime will join letters or put
words in the right order, so every translated string is stored already in its final
visual form and made here:

1. The string is split into units. Markup (`<color=...>`, `<sprite name=...>`, ...) and
   placeholders (`{0}`, `$dish$`) are lifted out; every character remembers which
   container tags enclose it.
2. Each line is shaped into presentation forms (U+FE70-FEFC, plus lam-alef ligatures),
   which the font provides directly.
3. Each line is reordered with the Unicode bidirectional algorithm at paragraph level
   RTL, and mirrored characters are swapped.
4. Tags are re-emitted around the characters they enclosed, now in visual order.

Placeholders stay whole: the game fills them in after localisation, so their contents
cannot be shaped here, only positioned. `{0}`-style ones hold numbers and act as European
digits in the bidirectional algorithm; word-valued ones act as Arabic text.
"""
from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass, field

from arabic_reshaper.letters import FINAL, INITIAL, ISOLATED, LETTERS_ARABIC, MEDIAL

# Markup that wraps text and must be re-emitted around it, as opposed to the standalone
# tags below which are just content sitting in the line.
CONTAINER_TAGS = {"color", "size", "align", "nobr", "b", "i", "u", "s", "font", "mark",
                  "style", "sup", "sub", "lowercase", "uppercase", "smallcaps", "cspace",
                  "line-height", "indent", "margin", "voffset", "width", "pos", "gradient",
                  "rotate", "link", "allcaps", "noparse"}

# PlateUp!'s StringSubstitutor rewrites these shorthands into TMP markup at localisation
# time (see the dumped substitutions table). Doing it here instead means the tags are real
# tags while the line is being shaped and reordered, so a coloured span ends up around the
# characters it encloses rather than around whatever lands in its place.
SHORTHAND = [("{{x", '<color=#cccc77>x'), ("{{+", "<color=#55ff55>"),
             ("{{-", "<color=#ff1111>"), ("{{#", "<color=orange>"),
             ("}}", "</color>"), ("$b$", "-"), ("\\n", "\n")]

# Substitutions that expand to a word rather than an icon, and so read as text.
WORD_TOKENS = {"[thinking]", "[ordering]", "[food]", "[delivery]", "[range]"}

TAG = re.compile(r"<[^<>]*>")
BRACE = re.compile(r"\{[^{}]*\}")
DOLLAR = re.compile(r"\$[^$\s]*\$")
BRACKET = re.compile(r"\[[a-z]+\]")
TOKEN = re.compile(f"{TAG.pattern}|{BRACE.pattern}|{DOLLAR.pattern}|{BRACKET.pattern}")

LAM = "ل"
LAM_ALEF = {"آ": ("ﻵ", "ﻶ"), "أ": ("ﻷ", "ﻸ"),
            "إ": ("ﻹ", "ﻺ"), "ا": ("ﻻ", "ﻼ")}
MIRROR = dict(zip("()[]{}<>«»‹›", ")(][}{><»«›‹"))
NBSP = " "

# A placeholder holding a bare number behaves like a Latin-digit run; anything else is a
# word and behaves like Arabic text.
NUMERIC_PLACEHOLDER = re.compile(r"^\{\d+(:[^{}]*)?\}$")


@dataclass
class Unit:
    text: str            # one character, or a whole token like "{0}" or "<sprite=3>"
    tags: tuple = ()     # container tags enclosing it, outermost first
    cls: str = ""        # bidi class override, for tokens


def _tag_name(tag: str) -> str:
    m = re.match(r"</?\s*([a-zA-Z-]+)", tag)
    return m.group(1).lower() if m else ""


def _token_class(token: str) -> str:
    """Bidi class for a non-character unit."""
    if token.startswith("<") or token.startswith("$"):
        return "ON"                     # a sprite or a spacer: takes the surrounding run
    if NUMERIC_PLACEHOLDER.match(token):
        return "EN"
    return "R"                          # a word the game substitutes in, e.g. [thinking]


def _add_text(units: list[Unit], text: str, stack: list[str]) -> None:
    """Append characters, dropping the short vowels and shadda.

    TextMesh Pro does no mark positioning. A combining mark has no advance of its own, and
    both fonts here draw its ink at or to the right of the origin and rely on GPOS anchors
    to move it over its letter - anchors TMP ignores. So a mark is drawn on the boundary
    with the next glyph, or on the next glyph outright, whichever way the line is ordered.
    There is no arrangement of the string that puts it in the right place.

    It also breaks the word open where it stands: lam, fathatan, alef stops looking like a
    lam-alef ligature to the joining rules below, and comes out as two loose letters.

    Removing the marks leaves ordinary undiacritised Arabic, which is how the language is
    normally written and how nearly every string here is written already. The translation
    files keep their marks so the text stays correctly spelled at the source.
    """
    for c in text:
        if unicodedata.category(c) == "Mn":
            continue
        units.append(Unit(c, tuple(stack)))


def _parse(line: str, stack: list[str]) -> list[Unit]:
    """Split one line into units. `stack` carries open tags across lines and is updated."""
    units, pos = [], 0
    for m in TOKEN.finditer(line):
        _add_text(units, line[pos:m.start()], stack)
        token = m.group()
        name = _tag_name(token)
        if token.startswith("</") and name in CONTAINER_TAGS:
            if not stack or _tag_name(stack[-1]) != name:
                raise ValueError(f"unbalanced {token} in {line!r}")
            stack.pop()
        elif token.startswith("<") and name in CONTAINER_TAGS:
            stack.append(token)
        else:
            units.append(Unit(token, tuple(stack), _token_class(token)))
        pos = m.end()
    _add_text(units, line[pos:], stack)
    return units


# --- shaping ---------------------------------------------------------------

def _forms(c: str):
    return LETTERS_ARABIC.get(c)


def _joins_next(c: str) -> bool:
    f = _forms(c)
    return bool(f and f[INITIAL])


def _joins_prev(c: str) -> bool:
    f = _forms(c)
    return bool(f and f[FINAL])


def _shape(units: list[Unit]) -> list[Unit]:
    """Presentation forms; lam+alef become one ligature unit."""
    out: list[Unit] = []
    i = 0
    while i < len(units):
        u = units[i]
        c = u.text
        if u.cls or not _forms(c):
            out.append(u)
            i += 1
            continue
        prev = units[i - 1] if i else None
        prev_joins = bool(prev) and not prev.cls and _joins_next(prev.text) and _joins_prev(c)
        nxt = units[i + 1] if i + 1 < len(units) else None
        if c == LAM and nxt and not nxt.cls and nxt.text in LAM_ALEF:
            out.append(Unit(LAM_ALEF[nxt.text][1 if prev_joins else 0], u.tags))
            i += 2
            continue
        next_joins = bool(nxt) and not nxt.cls and _joins_prev(nxt.text) and _joins_next(c)
        f = _forms(c)
        if prev_joins and next_joins and f[MEDIAL]:
            form = f[MEDIAL]
        elif prev_joins:
            form = f[FINAL]
        elif next_joins:
            form = f[INITIAL]
        else:
            form = f[ISOLATED]
        out.append(Unit(form, u.tags))
        i += 1
    return out


# --- bidirectional algorithm ----------------------------------------------

def _classes(units: list[Unit]) -> list[str]:
    return [u.cls or unicodedata.bidirectional(u.text) or "L" for u in units]


def _levels(units: list[Unit]) -> list[int]:
    """UBA for one line with paragraph level 1 and no explicit formatting characters."""
    t = _classes(units)
    n = len(t)
    prev = "R"                                   # W1: NSM takes the previous type (sos = R)
    for i in range(n):
        if t[i] == "NSM":
            t[i] = prev
        prev = t[i]
    # W2 (EN after an Arabic letter becomes AN) is skipped on purpose: every number here
    # is a Latin-digit game value, and as AN it would lose its sign and percent sign
    # (W5 only joins ET to EN), so "+50%" would come out as "%50+".
    t = ["R" if x == "AL" else x for x in t]     # W3
    for i in range(n - 1):                       # a sign right before a number is part of
        if t[i] == "ES" and t[i + 1] == "EN" and (i == 0 or t[i - 1] != "EN"):   # it: "+50"
            t[i] = "EN"
    for i in range(1, n - 1):                    # W4
        if t[i] == "ES" and t[i - 1] == t[i + 1] == "EN":
            t[i] = "EN"
        elif t[i] == "CS" and t[i - 1] == t[i + 1] and t[i - 1] in ("EN", "AN"):
            t[i] = t[i - 1]
    i = 0                                        # W5
    while i < n:
        if t[i] == "ET":
            j = i
            while j < n and t[j] == "ET":
                j += 1
            if (i > 0 and t[i - 1] == "EN") or (j < n and t[j] == "EN"):
                t[i:j] = ["EN"] * (j - i)
            i = j
        else:
            i += 1
    t = ["ON" if x in ("ES", "ET", "CS") else x for x in t]   # W6
    strong = "R"                                 # W7
    for i in range(n):
        if t[i] in ("L", "R"):
            strong = t[i]
        elif t[i] == "EN" and strong == "L":
            t[i] = "L"
    neutral = ("B", "S", "WS", "ON", "BN", "LRI", "RLI", "FSI", "PDI")
    i = 0                                        # N1, N2
    while i < n:
        if t[i] in neutral:
            j = i
            while j < n and t[j] in neutral:
                j += 1
            before = "R" if i == 0 else ("R" if t[i - 1] in ("R", "EN", "AN") else "L")
            after = "R" if j == n else ("R" if t[j] in ("R", "EN", "AN") else "L")
            t[i:j] = [before if before == after else "R"] * (j - i)
            i = j
        else:
            i += 1
    levels = [1 if x == "R" else 2 for x in t]   # I2 (paragraph level 1)
    j = n                                        # L1: trailing whitespace to paragraph level
    while j > 0 and not units[j - 1].cls and unicodedata.bidirectional(units[j - 1].text[0]) in ("WS", "S"):
        j -= 1
        levels[j] = 1
    return levels


def _reorder(units: list[Unit]) -> list[Unit]:
    levels = _levels(units)
    order = list(range(len(units)))
    for level in (2, 1):                         # L2
        i = 0
        while i < len(order):
            if levels[order[i]] >= level:
                j = i
                while j < len(order) and levels[order[j]] >= level:
                    j += 1
                order[i:j] = order[i:j][::-1]
                i = j
            else:
                i += 1
    out = []
    for k in order:                              # L4
        u = units[k]
        if levels[k] % 2 and not u.cls and u.text in MIRROR:
            u = Unit(MIRROR[u.text], u.tags, u.cls)
        out.append(u)
    return out


def _emit(units: list[Unit]) -> str:
    s: list[str] = []
    open_: list[str] = []
    for u in units:
        common = 0
        while common < min(len(open_), len(u.tags)) and open_[common] == u.tags[common]:
            common += 1
        for tag in reversed(open_[common:]):
            s.append(f"</{_tag_name(tag)}>")
        s += list(u.tags[common:])
        open_ = list(u.tags)
        s.append(u.text)
    s += [f"</{_tag_name(tag)}>" for tag in reversed(open_)]
    return "".join(s)


# --- entry point -----------------------------------------------------------

def visual(text: str) -> str:
    r"""Logical Arabic (with tags, placeholders, \n) -> visual-order text for TMP."""
    for shorthand, tag in SHORTHAND:
        text = text.replace(shorthand, tag)
    stack: list[str] = []
    out = []
    for line in text.split("\n"):
        out.append(_emit(_reorder(_shape(_parse(line, stack)))))
    if stack:
        raise ValueError(f"unclosed {stack} in {text!r}")
    return "\n".join(out)
