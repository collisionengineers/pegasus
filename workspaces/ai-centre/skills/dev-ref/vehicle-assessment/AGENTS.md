# AGENTS.md — vehicle-assessment (dev wrapper)

> **Status:** Current · **Last reviewed:** 2026-07-08 · **Runtime:** Chat pack + collisionrenderer (branded PDF) + local frozen generator (Audatex/EVA) — pack always, PDFs default on full assessments

Guidance for AI agents and developers maintaining the **vehicle-assessment** skill.

## What this is

The broad Collision Engineers front door for photo/document-led vehicle assessment. The
estimate is the deliverable; documents are renderings of it. Every assessment delivers the
**engineer information pack** in chat, built around the line-by-line estimate table projected
from the validated `assessment_payload.json`; on full assessments the CE-branded PDF via the
`collisionrenderer` connector (`expert-report` / `total-loss-report` /
`repairable-contract-repair-report`) and the Audatex/EVA-compatible PDF via the frozen local
generator render by default (skipped only on narrow single-question asks or explicit opt-out).
Each deliverable closes with the key-information summary table
(`references/assessment-output-structure.md`). Dispute/addendum wording via `ce-house-style`
stays on request.

It orchestrates, it does not replace: `total-loss-assessment` (Audatex PDF as primary ask),
`manufacturer-methods-evidence`, `salvage-categorisation`, `vehicle-history-check`,
`vehicle-valuation`, `diminution-report`/`diminution-rebuttal`, and `roadworthy-report` stay
authoritative for their domains.

## Layout

```
vehicle-assessment/                  <- the CLEAN skill = ships in the zip
  SKILL.md
  agents/openai.yaml
  references/  (21 files: intake, governance, estimate construction, output structure,
                body-repair logic, system escalation, ABP 2026 data, EVA routing, disputes,
                gotchas)
  scripts/     (validators + frozen audatex_gen_v4.py)
  _dev/                              <- this wrapper — NEVER shipped (pack_skill excludes it)
    AGENTS.md  README.md  tests/
```

## Render paths — do not blur

The pack is always produced; the two PDFs are renderings of the validated estimate payload,
default-on for full assessments (skipped only on narrow single-question asks or explicit
opt-out):

1. **Pack:** chat markdown, always available, no rendering; ends with the summary table.
2. **Branded PDF — rendering of the payload:** `collisionrenderer` only — payload →
   house-style lint → `validate` →
   `render`. If the connector is unavailable, present the validated payload and STOP. Never add
   a fallback/manual render route (standing no-fallback policy).
3. **Audatex/EVA PDF — rendering of the payload:** `scripts/audatex_gen_v4.py` is **FROZEN — never modify**
   (byte-identical to the `total-loss-assessment` copy; keep the sha256s equal if that skill
   ever legitimately revs it). Payloads are validated by
   `scripts/validate_assessment_payload.py` first. Requires local Python + reportlab; staff
   Claude Desktop machines have no Python — there the skill still builds and presents the
   validated payload and surfaces the render limitation instead of pretending to render or
   silently dropping the deliverable. No CE branding on this output.

## What's editable vs frozen

- **FROZEN:** `scripts/audatex_gen_v4.py`.
- **Editable:** all references, the escalation matrix (rev `schema_version`/filename on breaking
  change), ABP data (new year = new `abp-reference-data.<year>.json` + validator repoint + prose
  update; keep old years only as date-scoped historical evidence), validators, tests.
- **Critical gotchas:** the `specialist_wu` vs `rnr` routing trap; labour rate class (wrong rate
  = 25%+ error); no final salvage/structural conclusions from photos. Read
  `references/gotchas.md` before building.

## Testing

```
python -m unittest discover vehicle-assessment/_dev/tests
```

Suite: ABP data validation, assessment-payload validation, escalation-matrix validation, skill
metadata (frontmatter/name/description/references-exist), fixture privacy (denylist of real
identifiers; synthetic-reg allowlist), and cross-skill drift (EOL-normalized sha256 equality of
the files shared with `total-loss-assessment`; VA is content-canonical). Forward-test prompts
(manual eval) are listed in `README.md`.

## Shipping

Zips are built ONLY via `tools/pack_skill.py` (repo root):
`python tools/pack_skill.py vehicle-assessment vehicle-assessment.zip`.
Zips are org-upload distribution artifacts — regenerate after skill edits.
