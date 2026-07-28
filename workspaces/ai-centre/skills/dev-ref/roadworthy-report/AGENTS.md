# AGENTS.md — roadworthy-report (dev wrapper)

> **Status:** Current · **Last reviewed:** 2026-07-06 · **Runtime:** Deterministic local DOCX renderer (single path)

Guidance for AI agents and developers maintaining the **roadworthy-report** skill.

## What this is

A low-complexity transformation skill that turns an engineer's accident-damage assessment into an
**HS (Hackney Solutions) roadworthy / re-insurance report** for taxi and private-hire licensing
authorities. Only **14 fields** change in a fixed `.docx` template.

## Layout

`_dev/` (this folder) is maintainer documentation and tests — `tools/pack_skill.py` excludes it
from the shipped zip. Everything else in the skill folder ships.

## What's editable vs frozen

- **Hard rule:** **only the 14 mapped fields ever change.** Never alter wording, engineer names,
  qualifications, paragraphs, footer, or fonts. **Our Ref is always the vehicle registration.**
- **Hard rule:** do **not** ask the user clarifying questions — generate from the report alone.
- Always work on a **copy** of the template; never edit the original.
- `scripts/render_roadworthy.py` is allowed to replace only explicit placeholders in
  `word/header1.xml` and `word/document.xml`. If the real HS template is missing or lacks those
  placeholders, the script must fail closed. Do not create an invented HS template to make tests pass.

- There is deliberately **no fallback render route** (no manual XML editing, no docx unpack/pack
  workflow). A failed render is surfaced, not worked around.

## Dependencies

- Consults `collision-engineers-design` for letterhead context and `ce-house-style` for the short
  confirmation message.

## Path conventions

All skill-local paths are relative to the skill root.

## Shipping

Zips are built ONLY via `tools/pack_skill.py` (repo root).
Status: production-ready.
