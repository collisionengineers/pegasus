---
id: PLAT-057
type: ticket
title: >-
  EfEvaSubmissionWorkStore has no test coverage — claim, lease-loss and outcome
  paths are unexercised
status: backlog
area: platform-operations
order: 780
assignee: ''
profile: chore
labels:
  - backend
  - tests
groups:
  - EPIC-011
links:
  - PLAT-053
archived: false
created: '2026-08-29T08:03:36.063Z'
updated: '2026-09-03T15:15:28.476Z'
---

## What

`grep -rn --include=*.cs "EfEvaSubmissionWorkStore" tests/` returns zero hits.
The class is a production queue writer against `ExternalWorkItems`: it holds
the optimistic claim loop, the lease-token authority check, and the terminal
duplicate-delivery short-circuit for every automatic EVA submission. None of
those paths is exercised anywhere in the repository.

`EvaSubmissionPersistenceTests` is not coverage for it — that class seeds
`context.EvaSubmissions` only and never touches `ExternalWorkItems` or any
`IEvaSubmissionWorkStore` implementation.

## Paths worth pinning

- `ClaimProcessingAsync` returns null for a terminal row, returns null for a
  not-yet-due `pending` row, throws on a live `processing` lease, and throws
  `InvalidDataException` on an unknown persisted state word.
- The optimistic claim loop retries when a concurrent writer moves the row
  between read and update (AGENTS.md rule 11 — a swallowed conflict here
  records a submission against the wrong attempt).
- `RecordOutcomeAsync` commits and returns on an already-terminal row, throws
  when the lease token no longer matches, and persists `RetryScheduled` as the
  `pending` word with `CompletedAtUtc` cleared.

## Why it was not done in PLAT-053

Pre-existing gap, not introduced by that ticket, and outside its three-file
"Owns" list. PLAT-053 closed the immediate risk by reverting its own
semantic restructure of this class back to literal-for-constant
substitution, so nothing it shipped depends on untested behaviour — but the
class itself remains untested.

Raised in the PLAT-053 adversarial verification (2026-08-29).
