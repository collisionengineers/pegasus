#!/usr/bin/env python3
"""Readability and fidelity checker for plain-language rewrites.

Usage:
    python readability.py draft.md
    python readability.py original.md draft.md
    python readability.py original.md draft.md --json

With one file it reports readability stats for that file. With two files it
reports stats for both, plus every number / date / percentage / duration that
appears in the original but not in the draft. Missing numbers are the most
common fidelity bug in a rewrite, so treat each one as a defect until you have
confirmed it was safe to drop.

No dependencies beyond the standard library.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

# ---------------------------------------------------------------------------
# Text preparation
# ---------------------------------------------------------------------------

FENCE_RE = re.compile(r"```.*?```", re.S)
INLINE_CODE_RE = re.compile(r"`[^`]*`")
LINK_RE = re.compile(r"\[([^\]]*)\]\([^)]*\)")
HTML_RE = re.compile(r"<[^>]+>")
HEADING_RE = re.compile(r"^\s{0,3}#{1,6}\s*", re.M)
BULLET_RE = re.compile(r"^\s*(?:[-*+]|\d+[.)])\s+", re.M)
TABLE_SEP_RE = re.compile(r"^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$", re.M)
EMPHASIS_RE = re.compile(r"(\*\*|__|\*|_)(?=\S)(.+?)(?<=\S)\1")
BLOCKQUOTE_RE = re.compile(r"^\s*>\s?", re.M)


def strip_markdown(text: str) -> str:
    """Reduce markdown to prose-ish text so sentence stats aren't skewed."""
    text = FENCE_RE.sub(" ", text)
    text = INLINE_CODE_RE.sub(" ", text)
    text = HTML_RE.sub(" ", text)
    text = LINK_RE.sub(r"\1", text)
    text = TABLE_SEP_RE.sub(" ", text)
    text = HEADING_RE.sub("", text)
    text = BLOCKQUOTE_RE.sub("", text)
    text = BULLET_RE.sub("", text)
    text = EMPHASIS_RE.sub(r"\2", text)
    # Table rows: treat cell separators as sentence breaks.
    text = text.replace("|", ". ")
    return text


ABBREV = {
    "e.g", "i.e", "etc", "vs", "mr", "mrs", "ms", "dr", "prof", "sr", "jr",
    "st", "no", "inc", "ltd", "co", "corp", "approx", "min", "max", "sec",
    "fig", "vol", "dept", "est", "u.s", "u.k",
}

SENT_SPLIT_RE = re.compile(r"(?<=[.!?])\s+(?=[\"'(\[]?[A-Z0-9])")


def split_sentences(text: str) -> list[str]:
    text = re.sub(r"\s+", " ", text).strip()
    if not text:
        return []
    raw = SENT_SPLIT_RE.split(text)
    sentences: list[str] = []
    buf = ""
    for piece in raw:
        buf = (buf + " " + piece).strip() if buf else piece
        last = buf.rstrip(".!?\"')").split()
        last_word = last[-1].lower().rstrip(".") if last else ""
        if last_word in ABBREV:
            continue
        sentences.append(buf)
        buf = ""
    if buf:
        sentences.append(buf)
    # Drop fragments with no letters (e.g. lone numbers from tables).
    return [s for s in sentences if re.search(r"[A-Za-z]{2,}", s)]


WORD_RE = re.compile(r"[A-Za-z][A-Za-z'\-]*")


def words_in(text: str) -> list[str]:
    return WORD_RE.findall(text)


def count_syllables(word: str) -> int:
    w = word.lower().strip("'-")
    if not w:
        return 0
    if len(w) <= 3:
        return 1
    w = re.sub(r"(?:[^laeiouy]es|ed|[^laeiouy]e)$", "", w)
    w = re.sub(r"^y", "", w)
    groups = re.findall(r"[aeiouy]+", w)
    return max(1, len(groups))


# ---------------------------------------------------------------------------
# Readability
# ---------------------------------------------------------------------------

def readability(text: str) -> dict:
    prose = strip_markdown(text)
    sentences = split_sentences(prose)
    words = [w for s in sentences for w in words_in(s)]
    n_sent = max(1, len(sentences))
    n_words = max(1, len(words))
    syllables = sum(count_syllables(w) for w in words)
    long_words = [w for w in words if count_syllables(w) >= 3]

    asl = n_words / n_sent
    asw = syllables / n_words
    flesch = 206.835 - 1.015 * asl - 84.6 * asw
    fk_grade = 0.39 * asl + 11.8 * asw - 15.59

    per_sentence = [(len(words_in(s)), s) for s in sentences]
    longest = sorted(per_sentence, key=lambda t: -t[0])[:5]
    over_30 = [s for n, s in per_sentence if n > 30]

    return {
        "words": len(words),
        "sentences": len(sentences),
        "avg_sentence_length": round(asl, 1),
        "avg_syllables_per_word": round(asw, 2),
        "pct_long_words": round(100 * len(long_words) / n_words, 1),
        "flesch_reading_ease": round(flesch, 1),
        "fk_grade_level": round(fk_grade, 1),
        "sentences_over_30_words": len(over_30),
        "longest_sentences": [{"words": n, "text": s} for n, s in longest],
    }


# ---------------------------------------------------------------------------
# Fidelity: numbers, dates, durations, percentages
# ---------------------------------------------------------------------------

NUM_TOKEN_RE = re.compile(
    r"""
    (?<![\w.])
    (?:
        \d{1,2}:\d{2}(?::\d{2})?                      # times 14:07
      | \d{4}-\d{2}-\d{2}                             # ISO dates
      | \d{1,2}/\d{1,2}/\d{2,4}                       # slash dates
      | [$€£]\s?\d[\d,]*(?:\.\d+)?[kKmMbB]?           # money
      | \d[\d,]*(?:\.\d+)?\s?%                        # percentages
      | \d[\d,]*(?:\.\d+)?(?:x|ms|s|m|h|d|kb|mb|gb|tb)\b  # units attached
      | \d[\d,]*(?:\.\d+)?                            # plain numbers
    )
    (?![\w.]|\s*\))
    """,
    re.X | re.I,
)

WORD_NUMBERS = {
    "one": 1, "two": 2, "three": 3, "four": 4, "five": 5, "six": 6,
    "seven": 7, "eight": 8, "nine": 9, "ten": 10, "eleven": 11, "twelve": 12,
    "fifteen": 15, "twenty": 20, "thirty": 30, "forty": 40, "forty-five": 45,
    "fifty": 50, "sixty": 60, "ninety": 90, "hundred": 100,
}


def normalize_number(tok: str) -> str:
    t = tok.strip().lower().replace(",", "").replace(" ", "")
    # 1.50 -> 1.5, 95.0 -> 95
    m = re.fullmatch(r"([$€£]?)(\d+)\.(\d+)(.*)", t)
    if m:
        frac = m.group(3).rstrip("0")
        t = f"{m.group(1)}{m.group(2)}{('.' + frac) if frac else ''}{m.group(4)}"
    return t


def number_tokens(text: str) -> dict[str, list[str]]:
    """Map normalized number -> list of surface forms with a bit of context."""
    text = FENCE_RE.sub(" ", text)
    found: dict[str, list[str]] = {}
    for m in NUM_TOKEN_RE.finditer(text):
        tok = m.group(0)
        key = normalize_number(tok)
        # ignore markdown list/footnote counters and section numbers like "3.1" is kept
        start = max(0, m.start() - 30)
        end = min(len(text), m.end() + 30)
        ctx = re.sub(r"\s+", " ", text[start:end]).strip()
        found.setdefault(key, []).append(ctx)
    # Spelled-out numbers count too ("sixty (60) days" is common in contracts).
    for w, v in WORD_NUMBERS.items():
        if re.search(rf"(?<![\w-])\b{re.escape(w)}\b(?![\w-])", text, re.I):
            found.setdefault(str(v), []).append(f"spelled out: '{w}'")
    return found


def number_variants(key: str) -> set[str]:
    """Forms under which a number may legitimately reappear in the draft."""
    variants = {key}
    bare = re.sub(r"[^\d.]", "", key)
    if bare:
        variants.add(bare)
        if "." in bare:
            variants.add(bare.split(".")[0])
    return variants


SECTION_REF_RE = re.compile(r"^\d{1,2}(?:\.\d{1,2})+$")


def looks_like_section_ref(key: str, contexts: list[str]) -> bool:
    """'3.1 The Initial Term...' or 'Section 9.2' are clause numbers, not values."""
    if not SECTION_REF_RE.fullmatch(key):
        return False
    for ctx in contexts:
        if re.search(rf"(?:Section|Sections|§|see|under)\s+{re.escape(key)}\b", ctx, re.I):
            return True
        if re.search(rf"(?:^|[.*\n\s]){re.escape(key)}\s+(?:[A-Z][a-z]|[A-Z]{2,})", ctx):
            return True
    return False


def missing_numbers(original: str, draft: str) -> tuple[list[dict], list[str]]:
    """Return (missing values, missing section-style refs) for the draft."""
    orig = number_tokens(original)
    draft_tokens = set(number_tokens(draft).keys())
    draft_bare = {re.sub(r"[^\d.]", "", k) for k in draft_tokens}
    draft_text_lower = draft.lower()
    missing: list[dict] = []
    section_refs: list[str] = []
    for key, contexts in orig.items():
        present = any(v in draft_tokens or v in draft_bare for v in number_variants(key))
        if not present:
            # last resort: literal substring (catches "300s" written as "300 seconds")
            bare = re.sub(r"[^\d.]", "", key)
            if bare and re.search(rf"(?<![\d.]){re.escape(bare)}(?![\d.])", draft_text_lower):
                present = True
        if present:
            continue
        if looks_like_section_ref(key, contexts):
            section_refs.append(key)
        else:
            missing.append({"number": key, "contexts": contexts[:3]})
    return missing, section_refs


# ---------------------------------------------------------------------------
# Reporting
# ---------------------------------------------------------------------------

def read(path: str) -> str:
    if path == "-":
        return sys.stdin.read()
    return Path(path).read_text(encoding="utf-8", errors="replace")


def fmt_stats(label: str, st: dict) -> str:
    lines = [
        f"== {label} ==",
        f"  words: {st['words']}   sentences: {st['sentences']}",
        f"  avg sentence length: {st['avg_sentence_length']} words   (target < 20)",
        f"  FK grade level: {st['fk_grade_level']}   Flesch ease: {st['flesch_reading_ease']}   (target grade ~7-9)",
        f"  long words (3+ syllables): {st['pct_long_words']}%   sentences over 30 words: {st['sentences_over_30_words']}",
    ]
    if st["longest_sentences"]:
        lines.append("  longest sentences:")
        for item in st["longest_sentences"][:3]:
            txt = item["text"]
            if len(txt) > 160:
                txt = txt[:157] + "..."
            lines.append(f"    ({item['words']}w) {txt}")
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("files", nargs="+", help="draft.md, or original.md draft.md")
    ap.add_argument("--json", action="store_true", help="emit JSON instead of text")
    args = ap.parse_args(argv)

    if len(args.files) not in (1, 2):
        ap.error("pass one file (draft) or two files (original draft)")

    result: dict = {}
    if len(args.files) == 1:
        draft = read(args.files[0])
        result["draft"] = readability(draft)
    else:
        original = read(args.files[0])
        draft = read(args.files[1])
        result["original"] = readability(original)
        result["draft"] = readability(draft)
        ow = max(1, result["original"]["words"])
        result["length_ratio"] = round(result["draft"]["words"] / ow, 2)
        miss, refs = missing_numbers(original, draft)
        result["missing_numbers"] = miss
        result["missing_section_refs"] = refs

    if args.json:
        print(json.dumps(result, indent=2))
        return 0

    if "original" in result:
        print(fmt_stats("ORIGINAL", result["original"]))
        print()
    print(fmt_stats("DRAFT", result["draft"]))
    if "original" in result:
        print()
        print(f"length ratio (draft/original): {result['length_ratio']}   (rewrites usually land 0.3-0.6)")
        miss = result["missing_numbers"]
        print()
        if not miss:
            print("numbers check: every number/date/percentage in the original appears in the draft.")
        else:
            print(f"numbers check: {len(miss)} value(s) from the original do NOT appear in the draft.")
            print("  Confirm each was safe to drop (section numbers and page refs usually are; deadlines and thresholds are not).")
            for m in miss:
                print(f"  - {m['number']}")
                for ctx in m["contexts"][:2]:
                    print(f"      ...{ctx}...")
        refs = result["missing_section_refs"]
        if refs:
            print(f"  (also absent, but these look like clause/section numbers: {', '.join(refs)})")
    return 0


if __name__ == "__main__":
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except (AttributeError, ValueError):
        pass
    sys.exit(main())
