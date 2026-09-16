---
id: ADR-0053
status: accepted
date: 2026-09-16
supersedes: []
superseded_by: []
related_capabilities: []
related_frd: [frd-12]
tags: [notifications, persistence]
---

# ADR-0053: Personal staff notification store

## Status

Accepted, recording the operator's 13 September 2026 decision (v26 planning,
Work Centre D10) that Stage 2 implemented. FRD-12 owns the causes, the
dialog and the retention period; this record owns the store.

## Context

The shell's bell used to render the first Needs attention rows, so it was a
second copy of office-wide work. The operator decided the bell is personal:
a list of things that happened that concern the signed-in person and nothing
else. That needs durable per-person rows with a read state, which no existing
record holds: action history is per Case and office-wide, the AI job ledger is
per job, and the Work Centre is computed on read.

## Decision

Pegasus keeps one **StaffNotifications** table in the application database,
owned by `Pegasus.Core` through a notification store port and written only by
the Core actions that raise a cause (an AI draft ready, a Case assignment, a
change on an assigned Case by someone else). Each row names the person, the
Case or Unidentified reference, the cause, the relative route the shell opens,
when it was raised, who raised it and when it was read. Rows are never edited
except to set the read time; rows older than the FRD-12 retention window are
not listed and may be removed. No notification is delivered by e-mail, push or
any channel outside the application, and the Automation Actor cannot read or
write the store.

## Consequences

- One additive migration and one Core port; no new runtime, queue or
  background process. Raising a notification happens inside the transaction
  of the act that causes it.
- The Work Centre's Mine view and the bell agree because both read from the
  same causes; the bell reads the store, the Work Centre computes.
- A store failure degrades the bell only: the page renders with
  `Notifications unavailable.` and the failure is logged (FRD-12).
- Adding a cause is an FRD-12 decision and a Core change, not a schema change.

## Links

- [FRD-12 — the bell](../frd/frd-12-operator-experience.md)
- Provenance: [v26 planning, Notifications](../../design/planning-and-old-designs/v26_planning/pages/work-centre/dialogs/notifications/how-it-should-work.md)
