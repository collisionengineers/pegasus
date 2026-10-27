---
id: ADR-0086
status: accepted
date: 2026-10-27
supersedes: []
superseded_by: []
related_capabilities: [UI-04, UI-05]
related_frd: [frd-15, frd-10, frd-27]
tags: [work-centre, read-model]
---

# ADR-0086: Work Centre views are computed on read

## Status

Accepted, recording the operator's 27 October 2026 ruling on item AC (Design A —
Streamlined ledger) and on Dismiss: Needs attention stays as a fourth view, and
Dismiss stays the existing office-wide `WorkCentreDismissals`. No new table.

Related: [ADR-0053](0053-personal-staff-notification-store.md), which already
treats the Work Centre as computed on read and keeps the bell as a separate
personal store.

## Context

The Work Centre lists what the office should see: new Cases, what Pegasus
itself did, AI deliverables, and work that needs a person. Those are not one
ledger of events. Each is a question over records that already exist — a Case,
an intake association, a workflow event, a query, an AI job, a repair
specification. A second event table would have to be kept in step with those
writes, and a dismissal would then have two homes.

## Decision

1. The four Work Centre views are computed on read. There is no System Actions
   event table and no other new table for these views.
2. System Actions is a read over existing records, keyed by the underlying
   record id: the receipt for an auto-matched e-mail and for a matched
   cancellation, the image intake id for a pairing, and the query id for a
   query opened by the Worker or the Automation actor.
3. Dismissal semantics are unchanged. `WorkCentreDismissals` remains the one
   office-wide store, and a dismissal is of that underlying record id. Dismissing
   an action therefore does not hide the Case's New cases row, whose record id
   is the Case. `pegasus_work_centre_dismiss` covers the new record ids because
   it already dismisses by record id.
4. AI Actions reads the job ledger, including jobs that completed inside the
   same window. "Taken by" an Engineer is a repair specification with source
   route `AiDraft` and that job's id, not the job state Taken.

## Consequences

FRD-15 owns the four views, the sub-filters and the search. FRD-27 owns the
widened AI list. FRD-10 notes that the dismiss tool's record id may now be a
receipt, an image intake or a query as well as a Case, an Unidentified item or
an AI job. A later view that cannot be answered from existing records needs its
own decision; this one does not add a table for it.
