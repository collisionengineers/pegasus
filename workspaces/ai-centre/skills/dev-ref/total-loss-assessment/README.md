# total-loss-assessment

> **Status:** Current · **Last reviewed:** 2026-06-11 · **Runtime:** Local frozen PDF generator

Generate the Audatex-format EVA-import PDF where it is the sole ask and the outcome is already
decided — clear total losses, decided minor jobs, transcription of existing estimates, and
cost-targeted builds — without incurring the per-job Audatex cost. Opinion work (a repair
estimate or damage assessment from photos or a brief) belongs to `vehicle-assessment`, which
renders this same PDF inside its estimate-first pack.

**Output is not CE-branded.** It deliberately mimics the Audatex layout so it can be imported
into EVA.

## How it works

1. Claude reviews the photos, identifies the vehicle, catalogues damage, picks repair vs renewal,
   and builds a Python operations dict (this is where engineering judgement lives).
2. `scripts/validate_assessment_payload.py` validates the operations dict before rendering.
3. `scripts/audatex_gen_v4.py` deterministically renders the byte-identical EVA PDF from that dict.

## What it provides

- **`SKILL.md`** — the workflow and the operations-dict template.
- **`references/`** — ABP 2026 labour rates, default extras package, EVA routing rules (incl. the
  `specialist_wu` trap), damage-cataloguing guidance, and a gotchas log.
- **`scripts/assessment_payload.schema.json`** — the operations-dict JSON shape.
- **`scripts/validate_assessment_payload.py`** — validation gate before PDF generation.
- **`scripts/audatex_gen_v4.py`** — the deterministic generator (do not modify).

## Status

Production-ready. Human sign-off required before any assessment is used.

The `collisionrenderer:render` path (`templateId: total-loss-report`) remains gated: server-side
rendering must call the same frozen generator unchanged and pass golden-PDF `sha256` byte-equality
checks before it can become default.

## Layout

`README.md`/`AGENTS.md` live in this `total-loss-assessment-dev/` wrapper; the uploadable skill is the
nested `total-loss-assessment/` folder.
