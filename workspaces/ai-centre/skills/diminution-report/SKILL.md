---
name: diminution-report
description: Drafts and reviews Collision Engineers claimant-side diminution-in-value reports, solicitor advice notes, and post-repair market-stigma assessments. Use when the user asks for a diminution report, inherent diminution assessment, post-repair stigma review, claimant diminution advice, disclosure/market perception analysis, or whether a repaired vehicle has suffered a defensible permanent loss in market value. For rebutting an opponent's diminution report, use `diminution-rebuttal` instead.
---

# Diminution Report

## Overview

Prepare claimant-side diminution opinions for Collision Engineers. This skill is separate from
`diminution-rebuttal`: use it to build CE's own opinion, not to attack a third-party formula report.

Use `vehicle-valuation` whenever the diminution conclusion depends on a pre-incident value,
post-repair value, market comparables, guide evidence, or live advert support. Use
`collision-engineers-design` and `ce-house-style` for formal report layout and voice.

## Output Modes

- **Full expert diminution report:** produce a structured report for a repaired vehicle where CE is
  giving its own diminution opinion.
- **Solicitor advice note:** assess whether the evidence supports a diminution claim and list gaps
  before a report is commissioned.
- **Post-repair market-stigma review:** explain whether disclosure, repair scope, provenance, and
  buyer perception support a residual value loss.

## Workflow

1. Read the instruction, repair report, estimate, invoice, photos, post-repair evidence, history
   checks, valuation evidence, sale/purchase evidence, and correspondence. Do not draft from a bare
   repair cost and PAV.
2. Read `references/evidence-checklist.md` and identify missing material. If evidence is too thin,
   produce an advice note with gaps rather than a confident report.
3. Read `references/report-structure.md` for the selected output mode.
4. Read `references/physical-vs-market-condition.md` where repair quality, inspection, paint depth,
   or physical detectability is in issue.
5. Read `references/market-perception-and-disclosure.md` for disclosure, provenance, stigma, buyer
   behaviour, prestige sensitivity, and market evidence.
6. Read `references/common-defendant-arguments.md` before finalising conclusions so the report
   answers foreseeable objections without sounding defensive.
7. Use `vehicle-valuation` for market figures where needed. Do not invent pre- or post-repair value
   adjustments.
8. Draft in CE house style. Avoid formula-only conclusions, legal advocacy, and guarantees about how
   a hypothetical buyer would behave.
9. For formal court-facing reports, include Part 35-style expert formalities only where the user
   requested a court/expert report and the required expert details are available.
10. Render formal reports with the `collisionrenderer` connector using `templateId: expert-report`
    (camelCase payload; fetch the current shape with `get_template_sample`, `validate`, then
    `render`). Advice notes are plain text. If the connector is unavailable, present the validated
    payload and say the render needs it — do not build the document another way.

## Boundaries

- Do not use this skill to rebut an opponent report; use `diminution-rebuttal`.
- Do not assert diminution merely because a repair occurred. Tie the conclusion to evidence.
- Do not treat paint-depth readings as the sole determinant of market stigma.
- Do not import raw historic templates, registrations, signatures, or case details into new output.

## References

- `references/evidence-checklist.md` - required evidence and gap handling
- `references/report-structure.md` - report and advice-note structure
- `references/market-perception-and-disclosure.md` - buyer disclosure and stigma reasoning
- `references/physical-vs-market-condition.md` - physical repair condition versus market condition
- `references/common-defendant-arguments.md` - anticipated objections and concise answers
