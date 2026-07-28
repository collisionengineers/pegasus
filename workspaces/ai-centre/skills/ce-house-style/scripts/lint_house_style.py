#!/usr/bin/env python3
"""
Collision Engineers house-style linter.

Scans text or a file for banned terms (AI tell-tales and internal workflow
words) that must not appear in any client-facing or court-facing output.

Usage:
    python lint_house_style.py <file_path>
    python lint_house_style.py --text "Some text to check"
    python lint_house_style.py --payload <payload.json>

Exit code 0 = clean. Exit code 1 = violations found.

`--payload` is FIELD-AWARE: it parses a valuation evidence-pack payload and lints
only the PDF-RENDERED prose fields (the same field list as
vehicle-valuation/scripts/validate_evidence_pack.py's _pdf_bound_text_fields).
Internal, never-rendered fields — `valuation_mode`, `guide_value`,
`guide_value_unavailable_reason`, `evidence_role`, `comparability_note`,
`differences_note`, `evidence_assessment.basis` — are deliberately NOT scanned:
a mode label inside them is correct and flagging it every run trained readers to
ignore the linter (observed live 2026-07-03). Plain-text mode is unchanged and
scans everything.
"""
import json
import sys
import re
import argparse

# ---------------------------------------------------------------------------
# Banned-term definitions
# Each entry: (pattern, category, note)
# Patterns are case-insensitive regexes.
# ---------------------------------------------------------------------------
BANNED = [
    # AI tell-tales
    (r"it is important to note", "AI tell-tale", ""),
    (r"it is worth noting", "AI tell-tale", ""),
    (r"it should be noted", "AI tell-tale", ""),
    (r"\bdelve\b", "AI tell-tale", ""),
    (r"\bseamless\b", "AI tell-tale", ""),
    (r"\beverag(e|ing|ed)\b", "AI tell-tale", "leverage/leveraging"),
    (r"in our considered opinion", "AI tell-tale", ""),
    (r"on any rational view", "AI tell-tale", ""),
    (r"it is to be noted that", "AI tell-tale", ""),
    # 'comprehensive' as filler — flag but note context may be legitimate
    (r"\bcomprehensive\b", "AI tell-tale (review)", "check context — may be legitimate"),

    # Internal workflow terms
    (r"\bguide uplift\b", "internal term", "never in external output"),
    (r"\buplift\b", "internal term (review)", "check context — financial uplift may be legitimate"),
    (r"\bmarket_only\b", "internal term", "mode label — never in external output"),
    (r"\bguide_supported\b", "internal term", "mode label — never in external output"),
    (r"\btool output\b", "internal term", ""),
    (r"\btool result\b", "internal term", ""),
    (r"\bdraft strategy\b", "internal term", ""),
    (r"\bguide valuation\b", "internal term", "never in external PDFs"),
    (r"\bguide values?\b", "internal term", "never in external PDFs"),
    (r"\bguide price\b", "internal term", "never in external PDFs"),
    (r"\bengineer value\b", "internal term", "never in external PDFs"),
    (r"\borginal eng value\b", "internal term", "never in external PDFs"),
    (r"\bcherry.pick", "internal term", "never in external documents"),
    (r"\bhighest adverts found\b", "internal term", ""),
    (r"\bselected to increase value\b", "internal term", ""),
    (r"\bclient.favour", "internal term (review)", "check context"),
    # EVA as internal system (not as opposing assessor name)
    (r"\beva system\b", "internal term", "EVA the system — not the opposing firm"),
    (r"\beva.generated\b", "internal term", ""),
    (r"\bour eva\b", "internal term", ""),

    # AI disclosure
    (r"\b(artificial intelligence|machine learning|large language model|llm|gpt|claude|openai|anthropic)\b",
     "AI disclosure", "must not appear in any external output"),

    # Sales/marketing language
    (r"\bdelighted to\b", "sales language", ""),
    (r"\bexcited to\b", "sales language", ""),
    (r"\bgame.changing\b", "sales language", ""),
    (r"\bcutting.edge\b", "sales language", ""),
    (r"\bworld.class\b", "sales language", ""),

    # Emotional language
    (r"\bfrustrating\b", "emotional language", ""),
    (r"\bunacceptable\b", "emotional language", ""),
    (r"\bshocking\b", "emotional language", ""),

    # Americanisms (spot-check)
    (r"\bcolor\b", "Americanism", "use 'colour'"),
    (r"\bfavor\b", "Americanism", "use 'favour'"),
    (r"\borganize\b", "Americanism", "use 'organise'"),
    (r"\brecognize\b", "Americanism", "use 'recognise'"),
    (r"\bauthorize\b", "Americanism", "use 'authorise'"),
    (r"\banalyze\b", "Americanism", "use 'analyse'"),
    (r"\btire\b", "Americanism (review)", "vehicle tyres — use 'tyre'"),
]


def lint_text(text):
    """Return list of (line_number, matched_text, category, note) tuples."""
    hits = []
    lines = text.splitlines()
    for lineno, line in enumerate(lines, start=1):
        for pattern, category, note in BANNED:
            for m in re.finditer(pattern, line, re.IGNORECASE):
                hits.append((lineno, m.group(0), category, note, line.strip()))
    return hits


def pdf_bound_text_fields(payload):
    """(field_path, text) pairs for every PDF-RENDERED prose field.

    Mirror of vehicle-valuation/scripts/validate_evidence_pack.py's
    _pdf_bound_text_fields — keep the two lists in sync. Fields not listed here
    (valuation_mode, guide_value*, evidence_role, comparability_note,
    differences_note, evidence_assessment.basis, …) never reach the PDF and are
    exempt from banned-term scanning by design.
    """
    fields = []

    def append(path, value):
        if isinstance(value, str) and value.strip():
            fields.append((path, value))

    subject = payload.get("subject_vehicle")
    if isinstance(subject, dict):
        for key in [
            "registration", "vehicle_description", "make", "model", "derivative",
            "body_type", "fuel", "transmission", "engine", "first_registered",
            "mileage", "colour", "vehicle_history", "vin",
        ]:
            append(f"subject_vehicle.{key}", subject.get(key))

    for key in ["intro", "market_research", "conclusion", "vat_note", "search_summary"]:
        append(key, payload.get(key))

    commentary = payload.get("valuation_commentary")
    if isinstance(commentary, list):
        for index, paragraph in enumerate(commentary, start=1):
            append(f"valuation_commentary[{index}]", paragraph)

    adverts = payload.get("adverts")
    if isinstance(adverts, list):
        for index, advert in enumerate(adverts, start=1):
            if not isinstance(advert, dict):
                continue
            for key in [
                "source", "price", "make", "model", "derivative_or_engine",
                "registration_year", "mileage", "fuel", "transmission",
                "body_style", "seller_type", "location", "report_comment",
            ]:
                append(f"adverts[{index}].{key}", advert.get(key))

    return fields


def lint_payload(payload):
    """Field-aware lint: (field_path, matched, category, note, context) tuples."""
    hits = []
    for path, value in pdf_bound_text_fields(payload):
        for pattern, category, note in BANNED:
            for m in re.finditer(pattern, value, re.IGNORECASE):
                hits.append((path, m.group(0), category, note, value.strip()))
    return hits


def main():
    # Windows consoles often default to cp1252, which cannot encode the ✓/✗
    # markers — reconfigure stdout to UTF-8 (best-effort) so the linter never
    # crashes on its own output.
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

    parser = argparse.ArgumentParser(description="CE house-style linter")
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("file", nargs="?", help="Path to file to lint")
    group.add_argument("--text", help="Inline text to lint")
    group.add_argument(
        "--payload",
        help="Path to a valuation evidence-pack payload JSON; lints only PDF-rendered prose fields",
    )
    args = parser.parse_args()

    if args.payload:
        try:
            with open(args.payload, encoding="utf-8") as f:
                payload = json.load(f)
        except FileNotFoundError:
            print(f"ERROR: file not found: {args.payload}", file=sys.stderr)
            sys.exit(2)
        except json.JSONDecodeError as exc:
            print(f"ERROR: not valid JSON: {args.payload} ({exc})", file=sys.stderr)
            sys.exit(2)
        if not isinstance(payload, dict):
            print(f"ERROR: payload root must be a JSON object: {args.payload}", file=sys.stderr)
            sys.exit(2)

        payload_hits = lint_payload(payload)
        if not payload_hits:
            print(f"✓ {args.payload}: clean — no banned terms in any PDF-rendered field.")
            sys.exit(0)
        print(f"✗ {args.payload}: {len(payload_hits)} violation(s) in PDF-rendered fields.\n")
        for path, matched, category, note, context in payload_hits:
            note_str = f"  [{note}]" if note else ""
            print(f"  {path}  [{category}]  '{matched}'{note_str}")
            print(f"           {context[:120]}")
            print()
        sys.exit(1)

    if args.text:
        text = args.text
        source = "<inline>"
    else:
        try:
            with open(args.file, encoding="utf-8") as f:
                text = f.read()
            source = args.file
        except FileNotFoundError:
            print(f"ERROR: file not found: {args.file}", file=sys.stderr)
            sys.exit(2)

    hits = lint_text(text)

    if not hits:
        print(f"✓ {source}: clean — no banned terms found.")
        sys.exit(0)

    print(f"✗ {source}: {len(hits)} violation(s) found.\n")
    for lineno, matched, category, note, context in hits:
        note_str = f"  [{note}]" if note else ""
        print(f"  Line {lineno:4d}  [{category}]  '{matched}'{note_str}")
        print(f"           {context[:120]}")
        print()

    sys.exit(1)


if __name__ == "__main__":
    main()
