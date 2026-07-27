# 8.8 Estimate and ABP Review Pack

## Purpose

Create a structured review pack for Audatex/estimate documents and ABP-rate checks that supports, but does not replace, technical judgement.

## Why this matters

Estimate review is high-value but sensitive. The system can extract parts/labour/paint/materials, flag unusual lines and present comparison information, but an engineer or competent reviewer must make the final assessment.

## Step-by-step plan

### Step 1 — Define estimate data model

1. Estimate source type.
2. Estimate reference/date.
3. Labour total.
4. Paint total.
5. Parts total.
6. Materials total.
7. Total net/gross.
8. VAT.
9. Line items.
10. Repair/replace indicators.
11. ADAS, geometry, SRS and specialist items.
12. Supplementary estimate marker.

### Step 2 — Parse supplied estimate files only

1. Import Audatex/estimate PDFs or documents provided with the case.
2. Do not scrape or recreate external estimating-system data.
3. Preserve the source estimate file.
4. Link every parsed line back to the estimate source.
5. Route unparseable estimates to manual review.

### Step 3 — Build review flags

1. High labour hours.
2. High paint/materials ratio.
3. Replace instead of repair indicators.
4. Multiple estimate versions.
5. Supplementary or amended estimate.
6. Missing VAT status context.
7. Lines requiring specialist review.
8. Repair cost near potential total-loss threshold.

### Step 4 — Add ABP/market-rate context cautiously

1. Store approved internal rate references and notes.
2. Present comparison context, not a final judgement.
3. Require reviewer selection/approval for any challenge wording.
4. Keep evidence for rates used.
5. Version rate guidance so old cases can be interpreted historically.

### Step 5 — Generate estimate review summary

1. Summarise the estimate totals.
2. List flagged lines.
3. Show source snippets or page references.
4. Add reviewer notes.
5. Mark unresolved issues.
6. Feed the summary into the engineer pack.

### Step 6 — Measure usefulness

1. Track how often flags are accepted, dismissed or corrected.
2. Identify providers/repairers with frequent estimate issues.
3. Update parsing rules and flag thresholds based on reviewer feedback.
4. Avoid black-box scoring.

## Deliverables

- Estimate data model.
- Estimate parser.
- Review-flag taxonomy.
- ABP/rate-context library.
- Estimate review summary template.

## Acceptance criteria

- Supplied estimates can be parsed into totals and line items where text extraction allows.
- Flags are visible but not treated as final conclusions.
- Reviewers can accept, dismiss or annotate each flag.
- Engineer packs include a clear estimate summary.

## Risks and controls

| Risk | Control |
|---|---|
| Breaching third-party estimating-system terms | Parse only supplied files; pursue approved integrations separately. |
| Automation makes technical judgements | Keep reviewer/engineer approval mandatory. |
| Misleading thresholds | Treat thresholds as triage flags, not conclusions. |

## Suggested priority

P2. Build after core intake/review and before advanced analytics.
