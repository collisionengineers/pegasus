---
id: INTK-035
type: ticket
title: Promote an Unidentified triage request once its registration is known
status: implementing
area: intake-processing
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-24T10:07:06.107Z'
taken_at: '2026-08-24T10:07:10.292Z'
branch: task/intk-035-open-triage
worktree: ../pegasus-worktrees/intk-035-open-triage
labels:
  - triage
  - unidentified
  - deferred-from-INTK-033
links: []
refs:
  - docs/frd/frd-03-triage.md
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/operator-notes.md
  - docs/design/README.md
archived: false
created: '2026-08-24T08:33:08.297Z'
updated: '2026-08-24T10:07:10.293Z'
---

## Why

`operator-notes.md` § Stage 0: *"keep it as **Unidentified** … **until a vehicle
registration is known, then open the Triage**"*.

[[INTK-033]] wired both ends of that rule and the supersession that closes the
stale Unidentified item once a Triage exists — but the only thing that can open
the Triage is intake processing re-running over the same receipt. In practice
that means a staff **Re-evaluate**, and only when re-evaluation happens to read
a registration it missed before.

There is no operator action that says "here is the registration, open the
Triage". `ICreateTriageFromIntake` has exactly one caller
(`ProcessQueuedIntake.CreateTriageIfQualifyingAsync`); there is no manual
creation page and no MCP tool for it.

So the branch works when extraction eventually succeeds, and dead-ends when it
never does — for example a triage request that states its registration only
inside an image.

## Scope

The destination plumbing already exists and INTK-033 uses it:
`UnidentifiedResolutionTargetKind.Triage`, `UnidentifiedValidation.ValidateResolve`,
and `ITriageQueries.GetByOriginReceiptAsync`. What is missing is the staff
action that supplies a registration and opens the Triage, with the resolution
recorded against the Unidentified item.

## Verify

An Unidentified triage request with no extracted registration can be given one
by a member of staff, opens a Triage, and closes its Unidentified item with the
resolution recorded.
