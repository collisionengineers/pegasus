---
id: PLAT-066
type: ticket
title: 'Establish the approved 2,000-case capacity cohort and measured peak burst'
status: backlog
area: platform-operations
assignee: ''
profile: spike
labels:
  - capacity
  - tier-10
  - deferred
  - outside-programme
links: []
refs:
  - docs/prd/pegasus-product.md
  - docs/capabilities.md
  - docs/engineering.md
deployment: n/a
archived: false
created: '2026-09-01T21:54:35.732Z'
updated: '2026-09-01T21:54:35.732Z'
---

## What

Obtain and approve the immutable 2,000-case dataset and hash that `scripts/Invoke-QdosAlphaAcceptance.ps1` requires, run the tier-10 cohort and soak evidence (about eight concurrent operators, 2,000 new cases a month, 2–20+ files per case, burst and soak), and record the measured figures.

## Why

Operator decision D27 (2026-09-01): the capacity tier is excluded from the EPIC-011 programme and is recorded as "not run", never as passing. `docs/capabilities.md` OPS-20 and `docs/boundaries.md` record the exclusion; this spike is the owner of the evidence when it is scheduled. Per-ticket concurrency tests still run inside the programme.

## Approach

- Reuse the existing runner and its cohort requirement rather than a new harness.
- The dataset owner is an open decision (`docs/open-decisions.md`, performance dataset ownership).

## Verification

- [ ] Measured throughput, latency and failure figures are recorded with the cohort hash and environment.
- [ ] No release claim is made from this spike.

## Outcome
