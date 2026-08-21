---
id: INTK-029
type: ticket
title: Unlink must clear the case link and warn when it cancels the case
status: preparing
area: intake-processing
assignee: ''
profile: fix
labels:
  - regression
  - qdos26008
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-21T18:17:18.865Z'
updated: '2026-08-21T18:17:18.865Z'
---

## Why

The operator unlinked an email from QDOS26008. The inbox still showed it linked to that case, and offered no further action — a dead end.

**Root cause.** `EfIntakeMutationStore.ReverseLinkAsync` only deactivates the `ManualAssociation` row. The mail projection then falls back to allocation state: `linkedCase?.CaseId ?? allocationState?.CaseId` (`EfRetainedMailboxMessageStore.cs:747-755`). The automatic allocation attempt still names the case it created, so the link never visibly clears.

## Operator-directed behaviour

When the email being unlinked is the one that *spawned* the case, that stands to reason as cancelling the case. Warn the operator before the mutation — naming the case reference and saying plainly that the case will be cancelled — and on confirmation close it through the existing closure path with outcome `CaseClosureOutcome.CreatedInError` (`CaseLifecycle.cs:466`). No new lifecycle mechanism: `EvaluateIntakeCaseMatch` already redirects a `CreatedInError` survivor to its replacement.

Unlinking a non-spawning receipt keeps today's behaviour and leaves the case open. After any unlink the message must offer the next action.

## How to verify

Unlink the spawning email of a scratch case: warning appears, case closes as `CreatedInError`, the inbox no longer shows the link, next action offered.
