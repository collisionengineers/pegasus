---
id: ADR-0056
status: accepted
date: 2026-09-24
supersedes: [ADR-0051, ADR-0002]
superseded_by: []
related_capabilities: [CASE-04, CASE-07, CASE-08, TRI-01, TRI-08]
related_frd: [frd-01, frd-03, frd-05, frd-11, frd-13, frd-16, frd-18, frd-22]
tags: [identity, case, audit, triage, custody, box, migration]
---

# ADR-0056: One Case with per-work data; Triage as a Case type

## Status

Accepted, recording the operator's decisions of 23 and 24 September 2026
(the v29 planning round, issue 814 and PR 803's approved Triage plan). It
supersedes ADR-0051 in full. It partially supersedes ADR-0002's Case
reference allocation clause only where that clause describes the Audit of an
Inspection + Audit case as a linked Audit sharing the original sequence; the
transactional, unique-protected sequence and the single numbering
implementation are unchanged. FRD-01 owns Case identity, FRD-03 Triage,
FRD-13 Create audit and FRD-16 the Case page views.

## Context

ADR-0051 made Create audit insert a second, linked Audit Case: its own row,
page, lifecycle, queue rows and Box root under the original's folder. The
operator's review of that design (issue 814) found the Audit is the same
piece of work as the Inspection. It belongs on the same Case, with one state,
one Files and one Notes, while its report, reference, fee and values stay its
own.

Triage was a separate record with a global `T-` reference, its own sequence
table and its own `/Triage/{id}` page. It had no Case/PO, no Case custody and
no upload. PR 803's approved plan makes Triage a Case type.

Both changes alter Case identity, the reference rules and the Case page, so
they ship as one rework with one destructive migration.

## Decision

1. **One Case, one or two works.** A Case owns its identity, state,
   Engineers, Files, Notes, history, custody, intake links, due work and
   chasing once. Its working values belong to a *work* of the Case: Case data
   and its fields, assessment fields, repair specifications with their lines
   and revision snapshots, guide valuations and applied valuations, report
   wording and open AI field proposals. Every Case has a primary work. An
   Inspection + Audit Case gains one Audit work through Create audit, and
   never a second.
2. **The primary work's Id is the Case Id, supplied by the persistence
   context.** Every existing per-Case row and every report snapshot already
   frozen therefore stays valid under the per-work key. The persistence
   context adds the primary work whenever it adds a Case, so no Case creator
   adds it and no guard throws. The database fails closed: a primary work's
   Id must equal its Case Id, a Case has at most one work of each kind, and
   every per-work table has a foreign key to its work.
3. **The current work.** The current work is the Audit work when one exists,
   otherwise the primary work. Every write targets the current work,
   resolved inside the writer's transaction after its version, lease and
   archive guards. Create audit takes the same workflow lock and advances the
   Case version, so a write prepared before it is refused. Reads name the
   current or the primary work; only the Inspection view reads the primary
   work once an Audit exists. The Case-level mirrors (the accepted inspection
   deadline, Due by, the completeness gate and the match index) follow the
   primary work only. Lists, queues, Search rows and intake matching read the
   primary work.
4. **A report per work.** Each report generation belongs to one work, and
   current, superseded and stale are decided per work. A generation of a work
   that is no longer current can be opened and downloaded, never generated
   again, approved or sent. The Audit report's reference is `a.{Case/PO}`,
   held once on the Case; it is the Audit report's Our Ref, file name and
   email subject. The re-send suffix counts sends of the same work. At Create
   audit the report approval and Sent evidence move from the Case workflow to
   the primary work, and the Audit's Sent evidence must follow the Audit's
   creation. Fees and MI count the first confirmed report of each work.
5. **The `a.` folder.** Each Case document records its custody folder: the
   Case folder or its audit folder. An Audit-work report is filed in the
   `a.{Case/PO}` Box subfolder under the Case folder, which Pegasus creates
   through the custody work that Create audit queues. A document bound for
   that folder stays pending until the folder exists. Every other file,
   including images, stays in the Case folder and is shared by both works.
6. **The prefix rule.** A Case/PO is the base reference
   `{principal code}{YY}{sequence}` with the type's prefix: none for
   Inspection and Inspection + Audit, `a.` for a standalone Audit and `t.`
   for Triage. One formatter owns the rule and one allocator the number.
   Allocation locks the Principal/year sequence row first and retries a
   deadlock or duplicate. Every Case type consumes one number from the shared
   sequence. The Audit reference of an Inspection + Audit Case consumes no
   number and is not a Case/PO.
7. **Triage is a Case subtype.** A Triage Case is a Case of type Triage with
   a `t.` Case/PO and one Triage record keyed by the Case Id. That record
   keeps only the specialised state: the Triage state and version, assignee,
   findings, response evidence, history, notes, origin and the linked
   instruction Case. The Triage state (Open, Awaiting information, Finding
   recorded, Completed, Cancelled) is its one lifecycle authority, so a
   Triage Case has no Case workflow, initial Case state, data snapshot, match
   index entry, Case due work, vehicle lookup or Case intake link.
   `/Cases/{id}` is its only page. The global Triage sequence and the `T-`
   reference are removed.
8. **The Principal gate.** A Triage Case needs an established Principal and a
   registration before its number is allocated. Automatic intake asks a Core
   port whether the receipt's Principal is established. Without one the
   request is not a qualifying Triage and becomes Unidentified, and Open the
   Triage is not offered for it. The creating store refuses a Principal-less
   request as a backstop.
9. **Triage edit-scope custody authority.** Standard Case custody and upload
   association apply to a Triage Case. Wherever that path needs the Case
   edit lease or the Case workflow, a Triage Case uses its Triage edit scope
   and Triage version instead: custody completion, artifact retention, upload
   association and custody retry. A system custody completion never
   invalidates a staff edit scope.
10. **Automatic Triage creation stays on accepted route evidence.** Intake
    creates a Triage Case from the accepted route classification or the
    Principal's Provider API declaration, the same trust as instruction
    allocation. A review advisory on PR 803 suggested an approval step; none
    is added. Staff creation (Create case, Open the Triage) is the only
    source-less path.
11. **Staff linking of uploads reaches any Case.** Staff Link to case (the
    Upload, Unidentified and Inbox association destinations) offers every
    Case and every Triage Case in any state. Automatic association keeps its
    own rules (FRD-22).
12. **A forward-only destructive migration.** One migration introduces the
    works, moves the per-work keys to them, gives report generations their
    work and documents their custody folder, turns Triage into a Case
    subtype, and drops the linked-Audit column, the engineer-finding table
    and the Triage sequence table. It refuses to run while any linked Audit
    Case, Audit reference, Triage-type Case, Triage row, engineer-finding row
    or orphan applied valuation remains, and converts nothing: the approved
    test-estate wipe runs first. Its downgrade is refused; recovery is an
    approved backup. The release follows the destructive route of ADR-0046.

## Consequences

- ADR-0051's linked Audit Case, its nested custody root operation and the
  ribbon links between an Audit Case and its original are removed. Standalone
  Audit Cases are unchanged.
- The Case page shows a Views card once an Audit exists, and a read-only
  Inspection view (FRD-16). Search lists an Inspection + Audit Case with an
  Audit twice; queues list it once with its Inspection values (FRD-15).
- A Triage Case gains Case custody, Files and upload, appears in Search and
  the Workflow queues, and is counted by the Triages metric. MCP Triage tools
  identify it by its Case id (FRD-10).
- Set principal is removed from Triage. Correct principal is not extended to
  Triage Cases by this decision.
- The runtime roles gain their grants on the works table, and Web gains
  insert on the Triage table for Open the Triage.

## Links

- [FRD-01 — Case identity and types](../frd/frd-01-case-identity-and-lifecycle.md)
- [FRD-03 — Triage](../frd/frd-03-triage.md)
- [FRD-05 — Documents, extraction and custody](../frd/frd-05-documents-extraction-and-custody.md#custody-and-derived-reads)
- [FRD-13 — Case lifecycle and workflow](../frd/frd-13-case-lifecycle-and-workflow.md#create-audit)
- [FRD-16 — Case record workspace](../frd/frd-16-case-record-workspace.md#case-workspace)
- [ADR-0002](0002-dotnet-modular-monolith-on-azure.md#case-reference-allocation)
- [ADR-0045](0045-document-custody-and-derived-caches.md)
- [ADR-0046](0046-destructive-migration-runtime-shutdown.md)
- [ADR-0051](0051-linked-audit-case-identity-and-custody.md) (superseded)
- Provenance: [v29 notes](../../design/planning-and-old-designs/v29_planning/current/v29-notes.md)
