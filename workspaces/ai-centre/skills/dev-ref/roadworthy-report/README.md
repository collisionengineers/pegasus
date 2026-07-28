# roadworthy-report

> **Status:** Current · **Last reviewed:** 2026-07-06 · **Runtime:** Deterministic local DOCX renderer (single path)

Transform an engineer's accident-damage assessment into an **HS (Hackney Solutions) roadworthy /
re-insurance report** for taxi and private-hire licensing authorities.

This is a deterministic template transformation: **14 fields change**, nothing else. The workflow
is payload validation → prepared-template render. There is no fallback route: if
`render_roadworthy.py` fails, the skill surfaces the error and stops.

## What it provides

- **`SKILL.md`** — the extract → payload → render workflow and the header-date special case.
- **`references/field-mapping.md`** — the 14 fields, their sources, and fallbacks, with a worked
  example.
- **`scripts/render_roadworthy.py`** — validates the 14-field payload and renders the prepared HS
  DOCX template, failing closed if the real template is absent.
- **`assets/HS_roadworthy_report_template.docx`** — the fixed template when supplied locally
  (always render a copy; do not invent a substitute).
- **`assets/style-examples/`** — reference layouts.

## Status

Production-ready. Generates from the engineer's report alone — no clarifying questions. Remind the
user to drag vehicle images in manually.

## Layout

`README.md`/`AGENTS.md` live in `_dev/`, which `tools/pack_skill.py` excludes from the shipped
zip. See `AGENTS.md` before changing the renderer or field mapping.
