---
id: INTK-012
type: ticket
title: Fix the ordinal-0 member-token ambiguity in FindForMemberSourceAsync
status: done
area: intake-processing
order: 1470
assignee: group-lane
profile: fix
stageEntered:
  implementing: '2026-08-20T04:45:32.554Z'
  review: '2026-08-20T04:55:14.407Z'
  verifying: '2026-08-20T05:10:29.012Z'
  done: '2026-08-20T12:44:19.220Z'
labels:
  - defect
  - grouped-upload
  - identity
links:
  - INTK-011
  - INTK-005
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
commits:
  - fe48c239
  - 3e6a452f
prs:
  - '454'
deployment: production
archived: false
created: '2026-08-20T00:42:19.151Z'
updated: '2026-08-25T01:27:00.536Z'
---

## What

`EfIntakeSubmissionGroupStore.FindForMemberSourceAsync` can never recognise an **ordinal-0** group member from its own source identity: INTK-005's token scheme deliberately gives ordinal 0 the parent token verbatim (`token`, not `token:0`), so a lookup keyed on the member-token shape cannot distinguish the first member from the group itself. A pre-existing encoding ambiguity, distinct from the INTK-011 race (the production straggler was ordinal 1, so this gap was not what the incident exhibited).

## Why

Found by [[INTK-011]] during the atomic-group-outcome work and deliberately left out of that fix's scope rather than silently uncovered (recorded in its plan and post-implementation report). Any future path that resolves a member by source identity — reconciliation, replay, the upload confirmation surface — will mis-handle first members until this is closed.

## Approach

- Decide the fix at the identity level, not the query level: either the lookup learns the ordinal-0 convention (parent token ⇒ ordinal 0 of its group), or membership resolution keys on `StagedReceiptId` instead of token shape. Prefer whichever keeps one owner for the token convention (`GroupedIntakeMemberToken`, added by INTK-006's merge, is that owner).
- Add the test INTK-011 could not: resolve an ordinal-0 member by its own identity and assert it finds its group.

## Verification

- [ ] Ordinal-0 members resolve to their group in every caller of `FindForMemberSourceAsync`.
- [ ] The token convention still has exactly one owner.
- [ ] Focused integration suites green.

## Outcome
