# AGENTS.md — total-loss-assessment (dev wrapper)

> **Status:** Current · **Last reviewed:** 2026-06-11 · **Runtime:** Local frozen PDF generator

Guidance for AI agents and developers maintaining the **total-loss-assessment** skill.

## What this is

A document skill that generates **Audatex-format damage-assessment PDFs** from photos —
bypassing per-job Audatex costs on clear total losses, minor jobs, and transcription work.
Output mimics the Audatex format for EVA import; it is **not** CE-branded.

## Layout (wrapper vs upload)

```
total-loss-assessment-dev/           <- this dev shell — NEVER uploaded
  AGENTS.md   README.md
  total-loss-assessment/             <- the CLEAN skill = ships to cowork/Desktop
    SKILL.md
    references/  (labour-rates, extras-package, eva-routing, damage-cataloguing, gotchas)
    scripts/assessment_payload.schema.json
    scripts/validate_assessment_payload.py
    scripts/audatex_gen_v4.py        <- deterministic PDF generator (FROZEN)
    scripts/requirements.txt
```

## Two-stage architecture — do not blur

1. **Claude** identifies the vehicle/damage and builds the Python operations dict (judgement).
2. **`scripts/validate_assessment_payload.py`** validates the dict before generation.
3. **`scripts/audatex_gen_v4.py`** turns that dict into the byte-identical EVA PDF (deterministic).

## What's editable vs frozen

- **FROZEN: `scripts/audatex_gen_v4.py` — never modify.** Note: its `__main__` demo block
  hardcodes `/home/claude/work/ours_v4.pdf`, but `build_pdf(output_path, …)` is fully
  parameterised, so real use is unaffected — left intentionally untouched.
- **Editable:** references (labour rates, extras package, routing traps), payload schema, and
  validator rules.
- **Critical gotchas:** the `specialist_wu` vs `rnr` routing trap; labour rate (wrong rate =
  25%+ error). Read `references/gotchas.md` before building.
- **Render gate:** keep local rendering as default. A `collisionrenderer:render` path
  (`templateId: total-loss-report`) is allowed only if it invokes the frozen generator unchanged
  and passes golden-PDF `sha256` byte-equality tests.

## What's NOT applied

- **No brand layer.** Do **not** apply `collision-engineers-design` styling — the PDF must
  look like Audatex. `ce-house-style` applies only to the chat summary, not the PDF.

## Path conventions

The build script Claude writes runs from the skill root; it adds scripts via
`sys.path.insert(0, 'scripts')`. No repo-rooted or absolute paths.

## Shipping

Zips are built ONLY via `tools/pack_skill.py` (repo root).
Status: production-ready.
