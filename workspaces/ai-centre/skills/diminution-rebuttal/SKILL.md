---
name: diminution-rebuttal
description: Drafts, prepares, and reviews diminution-in-value rebuttals and defensive responses for Collision Engineers, including rebutting EVA or formula-driven third-party reports, defending CE's own diminution reports against insurer objections, and handling paint-depth, Elcometer, prior-repair, provenance, prestige-market stigma, Part 35, written-question, addendum, or testing-disclosure issues. Triggers on "diminution rebuttal", "rebuttal report", "EVA report", "diminution claim", "stigma", "challenge the diminution", "defend our diminution report", "insurer rejected diminution", "paint depth", "Elcometer", "prior repairs", "Part 35 diminution", "diminution in value", or instructions to respond to a claim that a repaired vehicle has lost market value permanently.
---

## Authority boundary

This package may produce evidence, candidates, or draft output only. `Pegasus.Core` and an authorised human own every accepted case fact, cost, category, outcome, legal position, and approval.
# Diminution Rebuttal

Produce a Part 35-related evidence-drafting experiment for authorised human review. It does not provide legal advice, guarantee compliance, serve a document, or adopt a Collision Engineers legal position.

**Foundations:** consult `collision-engineers-design` (`references/document-letterhead.md`) for the A4 letterhead layout and `ce-house-style` for voice. This skill supplies the rebuttal-specific content.

**Related:** where the rebuttal needs an evidenced market value (rather than just attacking the claimant's formula), the `vehicle-valuation` skill establishes the pre-/post-incident retail value from live comparable adverts; cite its figure as the market-grounded counter to a formula-only diminution claim.

## Workflow per case

1. **Read every document supplied** - the diminution report, repair report, estimate, invoice, PAV/valuation, photographs, emails, witness evidence, and opponent objections. Do not draft until you have reviewed everything.
2. **Run `references/evidence-reconciliation.md`** - identify duplicates, reconcile estimate/invoice/PAV figures, build the timeline, classify photos, and confirm registration, mileage, body style, and derivative.
3. **Build and validate `diminution_intake.json`.** Use `references/structured-intake-and-builder.md` and `references/diminution_intake.schema.json`. Run:
   ```bash
   python scripts/validate_diminution_intake.py diminution_intake.json
   ```
   Every attack line ID `1` to `14` must be marked `include`, `exclude`, or `needs_evidence` before drafting.
4. **Confirm Collision Engineers' role and output mode:**
   - If CE is rebutting a third-party diminution report, read `references/attack-lines.md` and `references/abi-benchmark.md`.
   - If CE is defending its own diminution report or answering insurer objections, read `references/defending-ce-diminution.md`.
   - If the output is solicitor-facing advice, an insurer-facing email, a formal rebuttal report, or an expert addendum, select the matching structure from `references/structure.md`.
5. **Load conditional references only where needed:**
   - Paint depth, Elcometer, prior repairs, first keeper, previous owner, or proving no historic damage: read `references/paint-depth-and-prior-history.md`.
   - Prestige, performance, specialist, high-value, low-volume, or provenance-sensitive vehicle: read `references/prestige-vehicle-market-stigma.md`.
   - Part 35 report/addendum, written questions, expert formalities, measurement evidence, or testing disclosure: read `references/expert-procedure-and-evidence.md`.
6. **Confirm the vehicle body style and variant** if there is any ambiguity. Wrong body style or trim undermines credibility instantly.
7. **Draft from the validated intake** using the selected structure. Include every `include` attack line, omit every `exclude` line, and resolve or omit `needs_evidence` lines before presenting. Apply `ce-house-style` voice throughout.
8. Lint for house style before presenting (zero hits required). If the `ce-house-style` skill is available, run its `scripts/lint_house_style.py` over the draft; otherwise apply the `ce-house-style` banned-terms list manually.
9. **Render formal reports and addenda with the `collisionrenderer` connector** (`templateId: diminution-rebuttal`). Build the camelCase payload per `references/structure.md`, fetch the current shape with `get_template_sample`, check it with `validate`, then `render`. Solicitor advice and insurer emails are plain text — no PDF. If the connector is unavailable in the session, present the validated payload JSON and say the render needs the connector — do not build the document any other way.
10. Be ready to iterate - sharpen, soften, add, or remove sections on request.

## Case-specific angles to look for

When reviewing a new report, check for:

- **Same-firm authorship** of repair and diminution reports → strengthens points 3 and 4
- **Unsigned Statement of Truth** → raise point 11
- **Floating-point artefacts** in the calculation (e.g. "5.8500000000000005%") → raise point 12
- **Wrong vehicle variant** in EVA's comparable evidence → strong factual hit
- **Repair cost figure mismatch** between EVA's quoted total and the underlying estimate
- **Estimate vs invoice mismatch** where a later invoice changes the repair-cost/PAV ratio
- **Duplicate documents** in the case pack that should not be counted twice
- **Comparable count at a band threshold** → sensitivity argument (small input change flips the multiplier band)
- **Mileage / condition / spec mismatches** in EVA's comparable evidence
- **Damage characterised differently** in repair report vs. diminution report
- **Paint-depth / prior-history burden shift** where the opponent asserts previous repairs without evidence
- **Prestige-market provenance sensitivity** where buyers pay for clean history, approved-used route, warranty, and specialist provenance
- **Expert-procedure defect** where a report, addendum, written question, or testing report does not meet Part 35 / PD35 requirements

## Tone

Plain-spoken professional — engineer's voice, not lawyer's. State the conclusion, then back it up. Make the case once clearly. "That cannot be right" is stronger than "that cannot, on any rational view, be correct." One "in our view" or "in our professional opinion" per section maximum.

## References

- `references/attack-lines.md` — the standard attack and defence lines with guidance on when each applies
- `references/diminution_intake.schema.json` — structured intake shape for claimant report facts and attack-line decisions
- `references/structured-intake-and-builder.md` — analyser and builder workflow for using the intake
- `references/abi-benchmark.md` — the ABI 20% inconsistency argument (strongest standalone point; include in every rebuttal)
- `references/structure.md` — document layout, section order, sign-off rules, .docx build notes
- `references/evidence-reconciliation.md` - pre-draft document, figure, chronology, duplicate, photo, and variant checks
- `references/defending-ce-diminution.md` - defensive workflow when CE's own diminution report is challenged
- `references/paint-depth-and-prior-history.md` - Elcometer, inspection, previous-repair, provenance, and burden-shift guidance
- `references/prestige-vehicle-market-stigma.md` - high-value, specialist, performance, and provenance-sensitive vehicle market points
- `references/expert-procedure-and-evidence.md` - CPR Part 35, PD35, written questions, statements of truth, addenda, and testing disclosure checklist
- `scripts/validate_diminution_intake.py` - validates that every attack line has been considered before drafting

