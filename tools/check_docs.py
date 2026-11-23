#!/usr/bin/env python3
"""Check one translation repo's docs before it ships.

Run from anywhere: python3 tools/check_docs.py

It guards the things that drifted between the five repos before: the README
section order, relative links and screenshots, numbered screenshot names, the
line-ending rules, the archive README, and the supported-game string. Exits 1
with a list of what is wrong, so CI fails on the push that broke it.
"""
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# The shared README order. Extra sections are allowed anywhere; these must be
# present and in this order, so the five repos keep reading as one author.
REQUIRED = [
    "في سطور",
    "المتطلبات",
    "التنزيل",
    "التثبيت على ويندوز",
    "التثبيت على لينكس",
    "التثبيت على ستيم ديك",
    "تشغيل التعريب",
    "الإزالة",
    "بعد تحديث اللعبة",
    "إن لم يعمل",
    "أسئلة سريعة",
    "ما ينبغي معرفته",
    "للمهتمين تقنيًا",
    "الخطوط والتراخيص",
    "الإبلاغ والمساهمة",
]

LINK = re.compile(r"!?\[[^\]]*\]\(([^)]+)\)")
# GitHub resolves these against the repo page, not the file system.
REMOTE = ("http://", "https://", "#", "mailto:", "../../")

errors: list[str] = []


def fail(msg: str) -> None:
    errors.append(msg)


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def check_readme() -> str:
    path = ROOT / "README.md"
    if not path.is_file():
        fail("README.md is missing")
        return ""
    text = read(path)
    if "قيد الإعداد" in text:
        return text  # placeholder repo: links only

    for target in LINK.findall(text):
        target = target.strip()
        if target.startswith(REMOTE):
            continue
        local = target.split("#")[0]
        if local and not (ROOT / local).exists():
            fail(f"README.md links to a missing file: {target}")

    sections = [line[3:].strip() for line in text.splitlines() if line.startswith("## ")]
    position = 0
    for wanted in REQUIRED:
        try:
            position = sections.index(wanted, position) + 1
        except ValueError:
            fail(f"README.md section missing or out of order: «{wanted}»")

    if not re.search(r"!\[[^\]]*\]\(screenshots/", text):
        fail("README.md shows no screenshot from screenshots/")

    supported = ROOT / "SUPPORTED_GAME"
    if supported.is_file():
        game = supported.read_text(encoding="utf-8").strip()
        if game and game not in text:
            fail(f"README.md does not mention the supported game version: {game}")
    return text


def check_screenshots() -> None:
    folder = ROOT / "screenshots"
    if not folder.is_dir():
        if "قيد الإعداد" not in read(ROOT / "README.md"):
            fail("screenshots/ is missing")
        return
    for name in sorted(os.listdir(folder)):
        if not re.fullmatch(r"\d\d-[a-z0-9-]+\.png", name):
            fail(f"screenshots/{name}: expected a numbered name like 01-menu.png")


def check_line_endings() -> None:
    path = ROOT / ".gitattributes"
    if not path.is_file():
        fail(".gitattributes is missing")
        return
    rules = read(path)
    for pattern, eol in (("*.bat", "crlf"), ("*.ps1", "crlf"), ("*.sh", "lf")):
        if not re.search(rf"^\s*{re.escape(pattern)}\s+\S*\s*eol={eol}\b", rules, re.M):
            fail(f".gitattributes: no `{pattern}` eol={eol} rule")


def check_archive_readme() -> None:
    if not (ROOT / "install.sh").is_file():
        return  # 9 Kings ships standalone installers, no zip
    archive = ROOT / "packaging" / "README.txt"
    if not archive.is_file():
        fail("packaging/README.txt is missing")
        return
    text = read(archive)
    for heading in ("ما هذا", "التثبيت على ويندوز", "التثبيت على لينكس", "الإزالة"):
        if heading not in text:
            fail(f"packaging/README.txt: no «{heading}» section")


def main() -> int:
    check_readme()
    check_screenshots()
    check_line_endings()
    check_archive_readme()
    name = ROOT.name
    if errors:
        print(f"{name}: {len(errors)} problem(s)")
        for error in errors:
            print(f"  - {error}")
        return 1
    print(f"{name}: docs ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
