---
id: TICK-085
type: ticket
title: Complete Glass's repair-estimate import from a representative export
status: backlog
area: engineering-assessment
assignee: ''
profile: feature
labels:
  - capability
  - EXT-12
  - requires-live-approval
  - now
  - evidence-required
groups:
  - HZN-002
  - EPIC-009
links:
  - ENG-002
blocks:
  - ENG-025
refs:
  - docs/frd/frd-07-eva-and-external-engineering-handoff.md
archived: false
created: '2026-08-12T15:05:40.242Z'
updated: '2026-08-29T13:10:09.661Z'
---

## What

Complete the remaining Glass's repair-estimate import only after a representative export is supplied.

## Why

EXT-12 is now allocated to Now / 0.1.0-alpha.1. [[ENG-002]] delivered and production-proved the first variant: deterministic Audatex full-report PDF import with retained source custody, total validation, and draft repair-specification lines. ENG-002's report records that the Glass's route, custody and landing are ready but its parser is parked because no real Glass's export sample exists.

## Approach

- Treat the delivered Audatex path as complete; do not reimplement or wrap it.
- Obtain a representative Glass's export before defining its supported layout or field mapping.
- Reuse the existing EXT-12 custody, source-version and draft-specification path.
- Reject the whole import on missing, ambiguous or internally inconsistent evidence.
- Leave estimate-to-report cost derivation with EXT-09.

## Verification

- [ ] Representative Glass's variants and mappings are accepted from real supplied evidence.
- [ ] The retained source, parsed version and draft lines are linked and hash/provenance-backed.
- [ ] Unsupported or ambiguous variants fail closed without partial lines.

## Outcome
