---
id: CASE-046
type: ticket
title: >-
  One spelling for the readiness envelope across reopen, return, assignment and
  EVA handoff forms
status: backlog
area: case-reference-workflow
assignee: ''
profile: chore
labels:
  - case
  - eva
  - readiness
  - follow-up
links:
  - CASE-040
archived: false
created: '2026-09-05T16:08:12.281Z'
updated: '2026-09-05T16:08:12.281Z'
---

## What

`_ReadinessHiddenFields.cshtml` (typed to `DetailsModel`) spells the
readiness envelope (`instructionsComplete`, `imagesComplete`,
`evidenceReference="case-completeness-projection"`) once; `_EvaHandoff.cshtml`
(typed to `EvaHandoffViewModel`) re-spells the same three values inline
instead of reusing that partial, and `_CaseWorkflow.cshtml` builds a third
copy as a C# dictionary for its fetch-based post. Values are identical today
so nothing is broken, but the partial's own comment ("One spelling, because
the reopen, return and assignment forms must never drift apart") is no
longer true.

## Why

Raised by CASE-040 review (should-fix 4, 2026-09-05): `_EvaHandoff.cshtml`
is new in that PR and could not cheaply reuse `_ReadinessHiddenFields`
because the partial is typed to `DetailsModel` and `_EvaHandoff` is typed to
`EvaHandoffViewModel`, and retyping it touches `DetailsModel.cs` outside
CASE-040's narrow CASE-038 hand-off ownership of that file — out of scope
for that ticket.

## Approach

Retype `_ReadinessHiddenFields.cshtml` to a small readiness-only model (a
two-bool record, or an interface both `DetailsModel` and
`EvaHandoffViewModel` implement) so `_EvaHandoff.cshtml` and
`_CaseWorkflow.cshtml`'s dictionary construction can all consume the one
partial instead of re-spelling its three fields.

## Verification

- [ ] `git grep -n "evidenceReference"` shows exactly one literal spelling
      (the partial) plus its callers passing a value into it.
- [ ] Existing reopen/return/assignment/EVA-handoff/re-send tests still pass
      unchanged.
