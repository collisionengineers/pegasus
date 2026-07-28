# AGENTS.md — diminution-rebuttal (dev wrapper)

> **Status:** Current · **Last reviewed:** 2026-07-06 · **Runtime:** Structured intake; renders via the `collisionrenderer` connector

Guidance for AI agents and developers maintaining the **diminution-rebuttal** skill.

## What this is

A high-judgement expert-voice skill that produces **CPR Part 35-compliant rebuttals** to
third-party diminution-in-value claims (most commonly EVA, but the methodology applies to any
formula-driven report). It supplies the rebuttal-specific content; design and voice come from the
foundation skills.

## Layout

`_dev/` (this folder) is maintainer documentation and tests — `tools/pack_skill.py` excludes it
from the shipped zip. Everything else in the skill folder ships.

## What's editable vs frozen

- **Editable:** the standard attack lines and their guidance, structure, ABI benchmark notes,
  structured intake schema, and intake validator.
- **Convention:** the **ABI 20% inconsistency** point (`references/abi-benchmark.md`) is the
  strongest standalone argument — include it in every rebuttal. Plain-spoken engineer's voice;
  one "in our professional opinion" per section maximum.
- **Coverage rule:** the structured intake must consider attack-line IDs `1` through `14` before
  drafting. Do not silently omit a point; mark it `exclude` or `needs_evidence` with a rationale.

## Dependencies

- **`collision-engineers-design`** — `references/document-letterhead.md` for the A4 layout.
- **`ce-house-style`** — voice + banned terms. The linter is a **soft dependency** on
  cowork/Desktop (skills upload individually): run `ce-house-style`'s
  `scripts/lint_house_style.py` if that skill is present, otherwise apply its banned-terms list
  manually.
- **`collisionrenderer` connector** — the only render path for formal reports/addenda
  (`templateId: diminution-rebuttal`, camelCase payload; see `references/structure.md`). There is
  deliberately no DOCX or other fallback renderer — if the connector is absent, the skill hands
  over the validated payload and stops.

## Path conventions

No repo-rooted/absolute paths for skill-local files.

## Shipping

Zips are built ONLY via `tools/pack_skill.py` (repo root).
Status: production-ready.
