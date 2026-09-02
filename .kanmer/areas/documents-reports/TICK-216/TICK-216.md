---
id: TICK-216
type: ticket
title: >-
  Decide whether unaccepted wording and signature assets may ship behind a
  closed gate
status: done
area: documents-reports
order: 2180
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-19T08:59:06.874Z'
  implementing: '2026-08-25T06:51:56.962Z'
  review: '2026-08-25T06:51:57.247Z'
  verifying: '2026-08-25T06:52:17.475Z'
  done: '2026-08-25T06:52:17.817Z'
labels:
  - now
  - source-now
groups:
  - EPIC-004
links:
  - SIMPLI-015
  - SIMPLI-014
blocks: []
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
deployment: n/a
archived: true
created: '2026-08-12T15:08:06.048Z'
updated: '2026-09-01T22:03:55.014Z'
---

## What

Decide whether unaccepted wording and signature assets may ship behind a closed gate.

## Decision

No. Exact assessment wording and engineer identity/signature evidence are callable only as complete accepted tuples. The sole complete tuple is `A Patterson | M.Inst.IAEA | andy_patterson`.

Ed Mawdsley and Neil O'Reilly have governed signature images, but their accepted qualifications are absent. Neither may be embedded or selected until an exact qualification is supplied and accepted. Missing, unknown, mismatched, substituted, custom, placeholder, and otherwise unaccepted content fails closed. Draft generation never implies human approval or issue.

## Outcome

Closed as a corrected no-code acceptance slice on 2026-08-25. The earlier ticket text claiming all three tuples were accepted was unsupported and has been replaced throughout the pipeline. [[SIMPLI-014]] already implemented and proved the narrower authoritative boundary in PR #415 at `b548b674e31d05de6f43eeb285a25dedd7d2a768`: one Core tuple and one embedded Andy asset, with Ed/Neil absent. No TICK-216 repository diff, PR, deployment, or cloud action was required.

Superseded 2026-09-01 by operator decision D18 (EPIC-011 `decisions/2026-09-01-work-pack.md`): any user in the Engineer role may issue a report and reports render typed Engineer identity only; signature assets and qualification strings are no longer required. The FRD-11 tuple paragraph is rewritten by [[DELIV-040]]. Archived by the Claude controller with the operator's authorisation; its `blocks` edge to [[TICK-081]] is removed because the decision it guarded no longer exists.
