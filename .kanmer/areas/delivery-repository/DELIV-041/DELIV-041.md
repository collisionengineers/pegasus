---
id: DELIV-041
type: ticket
title: >-
  Record the 2026-09-02 Case workspace decisions D29–D43 in the governing
  documents
status: review
area: delivery-repository
assignee: claude-fable-5.1/c4ea9419/deliv-041
profile: chore
stageEntered:
  preparing: '2026-09-02T20:34:59.540Z'
  review: '2026-09-02T20:52:04.770Z'
taken_at: '2026-09-02T20:35:24.037Z'
branch: task/deliv-041-case-workspace-decisions
worktree: .worktrees/deliv-041
labels:
  - docs
  - governing-docs
  - phase-0
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
blocks:
  - CASE-038
  - ENG-034
  - ENG-035
  - ENG-036
  - CASE-039
  - CASE-040
  - PLAT-068
  - CASE-041
  - AUTO-018
  - PLAT-069
  - CASE-042
  - DOCS-018
  - DOCS-017
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-04-parties-accounts-and-access.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/frd/frd-12-operator-experience.md
  - docs/design/README.md
  - docs/capabilities.md
  - docs/boundaries.md
  - docs/open-decisions.md
  - docs/engineering.md
  - docs/frd/frd-07-eva-and-external-engineering-handoff.md
commits:
  - 632ec0c436e301023f3aa6a5e1f4e0e149a192b5
prs:
  - '647'
archived: false
created: '2026-09-02T20:31:38.561Z'
updated: '2026-09-02T21:55:22.131Z'
---

## What

Transcribe operator decisions D29–D43 (single-scroll Case record, Engineer workbench as Case sections, sign-off Engineer, Engineer notes, inspect-at fast update, one vehicle lookup with suggestion chips, AI market research via the external Cowork connector, Send to EVA re-send, Service health placement, Awaiting instruction queue, damage map, valuation sources, settlement fields, fee note preview, corpus-derived test fixtures) into the FRDs, the design authority, capabilities, boundaries, open-decisions and engineering.md, and append them to EPIC-011 `context.md`.

## Why

The mockup `Downloads/Pegasus_UI_v2.html` contradicts the current authority in several places (sections as tabs, D11, D18, the prototype-fixture rule). Every EPIC-012 ticket leaves Backlog on these documents. The exact wording is in EPIC-012 `context.md` §Shared decisions.

## Approach

- One PR, docs only.
- Supersede, never delete: D18 marked superseded by D31; the tabs rule scoped to non-Case records; D43 states plainly that the fixture values carry real claimant names and phone numbers.
- `docs/operator-notes.md` is protected and is not touched.

## Verification

- [ ] Each of D29–D43 appears in EPIC-011 context.md and in the owning FRD or engineering section.
- [ ] `docs/design/README.md` lists the new component classes and the Case record scroll rule.
- [ ] Docs-only CI validates.

## Outcome
