---
id: DELIV-038
type: ticket
title: Four documentation gaps release 37 exposed but did not close
status: backlog
area: delivery-repository
order: 240
assignee: ''
profile: chore
labels:
  - documentation
groups:
  - EPIC-011
links:
  - DELIV-037
refs:
  - docs/operations.md
  - docs/open-decisions.md
archived: false
created: '2026-08-30T17:03:24.178Z'
updated: '2026-09-03T15:15:27.389Z'
---

## What

Four findings from the independent verification of [[DELIV-037]] that are real
but sit outside a release-evidence pass.

## 1. An open decision now contradicts shipped code

`docs/open-decisions.md`'s *Future AI Operations boundary* still opens *"The
future AI job catalogue and AI Viewer remain unresolved and unimplemented"* and
asks to *"decide the permitted job types and eligibility, request and execution
lifecycle"*.

Release 37 shipped `src/Pegasus.Core/AiWork/AiJobOperations.cs` with an
`AiJobKind` enum of four permitted kinds and a request/execution lifecycle. So
the register asks for a decision that the code has already made — either the
decision is settled and should be recorded, or the shipped implementation
outran it and that gap needs naming. **Do not simply delete the entry**; work
out which of those two it is.

## 2. The upload-link surface has no local-vs-live boundary row

`operations.md`'s evidence table gained a Provider API row at release 37 but
none for the newly composed document-upload-link surface — **the higher-risk of
the two gates opened**, because it admits anonymous internet callers while the
Provider API admits nobody. INT-31 has a capabilities row but nothing saying
what is proved locally versus what needs live evidence.

## 3. Nothing records how release 37 differed from release 36's failures

Release 36's entry records two permanent hand-made prerequisites (the
`eva-client-id` and `eva-client-secret` Key Vault secrets, and two secret-scoped
grants) plus a live deviation (the Worker identity's vault-scope *Key Vault
Secrets User* grant). Release 37's entry says nothing about them, so a reader
cannot tell whether those still stand, were superseded, or were simply not
needed this time. Record the answer once rather than leaving each future release
to re-derive it.

## 4. Telemetry blindness is recorded but not actioned

Release 37 records that App Insights ingestion stopped at 12:41Z on the 0.5 GB
daily cap and therefore does not cover the deploy. Nothing says whether the cap
will be raised, whether the release's own traffic contributed to exhausting it,
or what compensating observation covers the window to the 03:00Z reset.

**The cap was raised from 0.1 GB only at release 35 and is already being hit
before mid-afternoon.** That is a recurring blindness on a production system,
not a one-off circumstance, and it deserves a decision rather than a note in
each release entry. See [[pegasus-production-observability-gaps]] context in
`docs/operations.md`.

## Verification

- [ ] The AI-operations entry either records the settled decision or names the
      gap between it and shipped code
- [ ] The upload-link surface has a boundary row stating local versus live
      evidence
- [ ] Release 36's hand-made prerequisites are recorded as still standing,
      superseded, or unnecessary
- [ ] The telemetry cap has a decision, not a per-release note
