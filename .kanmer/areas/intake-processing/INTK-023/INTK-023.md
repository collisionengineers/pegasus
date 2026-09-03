---
id: INTK-023
type: ticket
title: 'Extract claimant, vehicle and incident date from the real QDOS letter shapes'
status: done
area: intake-processing
order: 1360
assignee: ''
profile: fix
stageEntered:
  done: '2026-08-22T03:46:07.059Z'
labels:
  - regression
  - extraction
  - corpus
links: []
commits:
  - c5b3247f2cd6130b4bb785a4133157b386be4d55
prs:
  - '494'
deployment: production
archived: false
created: '2026-08-21T10:45:24.375Z'
updated: '2026-09-03T09:06:49.419Z'
---

## What

Live cases QDOS26005–26007 arrived without claimant name, vehicle make/model,
or incident date although all sit in the instruction letters (operator report
2026-08-21, issues 1–3). Root causes, live-verified in `FieldsJson` and the
letters: typographic apostrophes broke the claimant lookahead and the
vehicle-description labels; the incident date's two spellings always
conflicted; the `TP Registration:` third-party row polluted VRM candidates;
the letters' wrapped details block produced truncated prefix candidates.

Delivered by PR #494 (merged to dev, squash `c5b3247f`): apostrophe
normalization at the engine's line split; `TP `-prefix lookbehind;
typed-value canonicalization (dates, VRMs) and within-fragment prefix
subsumption in conflict resolution; ordinal-date parsing;
`Claimant's Vehicle` description label; policy version 2 → 3. Genuinely
distinct candidates still fail closed.

Tests: 5 new unit facts per defect shape; `QdosMappingExtractionTests` pins
the full documented field set per operator-supplied mapping email
(`corpus/qdosmapping`, skip-if-absent) — org claimant, inline-letter body,
private plates, double-slash refs, report-sourced odometer all covered.

Held for the mapping sign-off (R2 checkpoint): report-shape labels and the
accident-circumstances paragraph rule.

## Verify

Proof after the next release: a fresh instruction email lands claimant /
make / model / incident date as Facts, and "Re-evaluate with current policy"
is available for older receipts (v3 marker).

Deviation note: subagents barred by operator directive — self-reviewed;
board write-up backfilled (Kanmer MCP was disconnected when the work ran).
