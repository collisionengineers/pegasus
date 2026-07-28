# diminution-rebuttal

> **Status:** Current · **Last reviewed:** 2026-07-06 · **Runtime:** Structured intake; renders via the `collisionrenderer` connector

Produce CPR Part 35-compliant rebuttals to third-party diminution-in-value claims — most commonly
EVA reports, but the approach applies to any formula-driven diminution claim.

This is an expert-voice, high-judgement skill: read all case documents, decide which of the **14
standard lines of attack and defence** apply, and weave them into a coherent narrative defending Collision
Engineers' position on market-value preservation after repair.

## What it provides

- **`SKILL.md`** — the workflow (read everything first → confirm CE's role → select attack lines →
  draft → lint → render via `collisionrenderer`).
- **`references/`** — `attack-lines.md` (the 14 points + when each applies), `abi-benchmark.md`
  (the ABI 20% inconsistency — always include), `structure.md` (section order + renderer payload
  mapping).
- **`references/diminution_intake.schema.json`** and
  **`scripts/validate_diminution_intake.py`** — structured intake and coverage validation.
- **`assets/style-examples/`** — reference rebuttals for structure and formatting.

## Foundations

Letterhead/layout from `collision-engineers-design`; voice and banned terms from `ce-house-style`
(lint to zero hits before presenting).

## Status

Production-ready. Human sign-off required before any rebuttal is served.

## Layout

`README.md`/`AGENTS.md` live in `_dev/`, which `tools/pack_skill.py` excludes from the shipped
zip. See `AGENTS.md` before changing intake, rendering, or attack-line rules.
