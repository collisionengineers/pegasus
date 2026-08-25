---
id: CASE-023
type: ticket
title: Stop a workflow event from consuming the version slot the next note needs
status: backlog
area: case-reference-workflow
assignee: ''
profile: fix
labels:
  - review-finding
  - notes
  - durability
links: []
archived: false
created: '2026-08-24T14:15:05.282Z'
updated: '2026-08-25T06:38:46.217Z'
---

## The defect

`CaseWorkflowEvents` has a unique index on `(CaseId, AfterVersion)`
(`CaseWorkflowModelConfiguration.cs:62`). Sixteen writers claim a version slot
by writing their event at the version their mutation just bumped to. An operator
note claims no version at all: `EfCaseNoteStore.AddAsync` writes
`BeforeVersion = AfterVersion = workflow.Version` and leaves the workflow row
untouched, deliberately, because "a note records itself and changes nothing".

Those two designs are incompatible. Whenever the most recent version-claiming
event sits at the case's *current* version, the next operator note collides on
the index, `SaveChanges` throws, and the page reports "The note was not added."

## Why it matters now

Before [[DOCS-012]] the collision was incidental — it needed a mutation to write
an event at the current version with nothing bumping the version afterwards.
Production shows the benign shape: case `9fb604f8` has `vehicle_lookup_current`
at `after=1` and an `operator_note` at `before=2, after=2`, which survived only
because something else had moved the case to 2 first.

DOCS-012 writes a `case_document_removed` event at the version its own removal
just claimed. So after removing a file the case sits at exactly that version,
and **the next note fails deterministically** — on the sequence the feature most
invites, since the redesigned panel hides removed rows and points at Notes to
explain what changed.

## Not DOCS-012's invention

DOCS-012 follows the same convention as all sixteen writers. The tension is
between that convention and CASE-017's version-neutral notes, and it predates
this ticket. Verified read-only against production: zero document removals so
far, so nothing live has hit it.

## What to decide

The `(CaseId, OperationKey)` unique index is the documented replay guard —
`EfCaseNoteStore` says so in as many words. The `(CaseId, AfterVersion)`
uniqueness is undocumented, has no test naming it, and is already conceptually
violated by version-neutral notes. Most likely it should become a plain
non-unique index, with a migration (permitted pre-cutover by
[[PLAT-042]] / ADR-0030). Confirm what it was guarding before dropping it.

## Verify

Remove a file from a case, then add an ordinary note. The note lands on the
Notes tab. Replay protection still refuses a resubmitted note.
