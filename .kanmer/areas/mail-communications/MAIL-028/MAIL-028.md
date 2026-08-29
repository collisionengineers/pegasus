---
id: MAIL-028
type: ticket
title: Activate the production retained-mail folder mover (MAIL-07)
status: backlog
area: mail-communications
assignee: ''
profile: feature
labels:
  - mail
  - rule-14
  - requires-live-approval
  - activation
groups:
  - EPIC-011
links:
  - MAIL-025
  - MAIL-027
  - TICK-054
blocks:
  - MAIL-025
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
archived: false
created: '2026-08-29T13:05:48.271Z'
updated: '2026-08-29T13:10:32.203Z'
---

## What

Own the activation of MAIL-07's **production folder-mover caller**: compose a
real `IRetainedMailFolderMover` in the deployed Web estate so the Inbox message
decision card's "Move to X" and "Check move status" controls actually render
and work, with the Graph permission, deployment and activation evidence
recorded in `docs/operations.md`.

The alternative disposition, if the operator rules that in-app mailbox
mutation stays closed for the alpha, is to amend [[MAIL-025]]'s Verification
line to drop folder move and remove the controls — which is an operator scope
decision, not an agent's.

## Why

[[MAIL-025]] was reversed out of Done by the GPT-5.6 adjudication of
2026-08-29 on exactly this: its Verification section names folder move
("Existing handlers (classification correction, folder move, association) keep
antiforgery, version and reason behaviour") and its `proof/proof.md:291` lists
"Move to X" and "Check move status" as delivered decision-card content, graded
"Proven (build/test)" at `proof.md:323`. Under D21 a capability behind a
composition gate that is CLOSED in the deployed estate is not delivered, and
build/test green is the wrong evidence tier.

No board ticket cleanly owns activation today:

- [[MAIL-027]] owns `Core/Intake/RetainedMailFolderMove.cs` and extends
  `IRetainedMailFolderMover`, but its own text keeps the adapter "composed only
  by explicit configuration with the unavailable implementation by default" and
  states "Production activation is a separately approved live write".
- [[TICK-054]] (MAIL-13, `requires-live-approval`, preparing) covers read
  state, Outlook categories, flags and delete — **not** MAIL-07's
  designated-folder move.
- `docs/capabilities.md:218` leaves MAIL-07 "behind an unavailable-by-default
  provider composition" and records that no Graph permission/RBAC change,
  deployment, production writer activation, live Graph call or Outlook mutation
  was performed or claimed.

Evidence recorded by the audit on merged `dev` at `b92cb9a7`:

- `src/Pegasus.Infrastructure/DependencyInjection.cs:83` —
  `services.TryAddSingleton<IRetainedMailFolderMover,
  UnavailableRetainedMailFolderMover>();` is the only registration.
- `src/Pegasus.Core/Intake/RetainedMailFolderMove.cs:136` —
  `public bool IsAvailable => false;`
- `GraphRetainedMailFolderMover`
  (`src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs:1077`) is
  `internal sealed` and registered by no composition root — not Web, not
  Worker.
- `src/Pegasus.Core/Intake/RetainedMail.cs:632` gates `CanMove` on
  `folderMover?.IsAvailable == true && !isCurrentLocation`, and `:555` derives
  `SuggestedMove` from `CanMove: true`. So in every production composition
  `CanMove` is false, the "Move to X" branch at `Message.cshtml:282` and the
  dialog at `:669-713` never render, and "Check move status"
  (`Message.cshtml:267`) is unreachable because no move can start.
- `docs/current-architecture.md`: "The provider is unavailable by default and
  the control is absent in that composition; fake-HTTP/local-SQL tests supply
  it, while no production writer, Graph permission, deployment or live mailbox
  mutation is active."
- `docs/operations.md` at `b92cb9a7` carries no folder-mover activation record.

## Approach

- Sequence after [[MAIL-027]], which owns the Core mover contract — do not fork
  a second mover or a parallel Graph adapter.
- Reuse `GraphRetainedMailFolderMover` as written; the work is composition,
  permission and evidence, not a new adapter.
- Every live step is a write and needs explicit per-target approval before it
  runs: the Graph application permission grant, the deployed app-setting change
  composing the real mover, and the first live mailbox mutation. Local alpha
  work must not mutate an Outlook mailbox; a live test needs an explicitly
  approved test mailbox.
- Refresh `docs/operations.md` and `docs/current-architecture.md` in the same
  task as the deployment — a deploy that leaves either stale is unfinished.

## Verification

- [ ] The deployed Web composition registers a real `IRetainedMailFolderMover`
      whose `IsAvailable` is true, evidenced from the running revision, not
      from a test profile.
- [ ] "Move to X" renders and completes a designated-folder move against an
      approved mailbox, and "Check move status" resolves an Uncertain move.
- [ ] `docs/operations.md` records the activation — permission, revision, date
      — so a later reader can check the gate state without reading code.
- [ ] `docs/capabilities.md` MAIL-07 is updated from "behind an
      unavailable-by-default provider composition" to its activated state.
