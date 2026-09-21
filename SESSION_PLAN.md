<proposed_plan>
# Make Triage a Case Type

## Summary

- Add `Triage` as a real `CaseType`, with immutable references formatted `t.{Principal}{YY}{sequence}`, for example `t.QDOS26001`.
- Allocate Triage numbers from the same Principal-lineage/year sequence as Inspection and standalone Audit Cases.
- Preserve the specialised Triage lifecycle, findings, assignment, due target, manual chasing and later instructed-Case association as a subtype extension of the Case aggregate.
- Move the specialised workspace to `/Cases/{id}` and remove both `/Triage` and `/Triage/{id}` so they return 404.
- Replace documentation that calls Triage “pre-Case” with one coherent definition: Triage is a Case type that does not constitute definitive instruction.

## Implementation Changes

### Identity, creation and persistence

- Extend `CaseType` and every exhaustive persistence, presentation, provider and query mapping with `Triage`/`triage`.
- Add `TriageIdentity.Create(reference)` producing the lowercase `t.` prefix, analogous to `AuditIdentity`.
- Create each Triage as a `Cases` row containing its ID, Principal, sequence lineage, year, shared sequence number, `t.` reference, type, creation time and optional origin receipt.
- Replace the standalone Triage identity fields and global `TriageSequences` counter with a one-to-one Triage subtype table keyed by `CaseId`. It owns only specialised state: Triage state/version, assignee, linked instructed Case and related lifecycle data.
- Retarget findings, response evidence and history to the Triage Case ID. Rename internal request properties and persistence columns from ambiguous `TriageId` to `TriageCaseId`/`CaseId` where they now describe the Case identity.
- Keep Triage workflow state in the subtype rather than creating a second ordinary `CaseWorkflow` state. There must be one lifecycle authority: `Open`, `AwaitingInformation`, `FindingRecorded`, `Completed` and `Cancelled`.
- The forward migration may discard existing development Triage rows and dependent evidence. Drop the obsolete sequence and identity columns/tables rather than retaining compatibility or translating `T-` references.
- Update runtime database grants and the model snapshot for the new Case-backed writes.

### Creation routes and custody

- Enforce processing order as Principal identification → email/submission classification → Triage creation. A request cannot be classified into a Triage Case until its Principal is established.
- Require Principal and normalised vehicle registration before automatic, Provider API, Unidentified-resolution or manual creation can allocate a Triage reference.
- Automatic intake creation must atomically allocate the shared Case sequence, create the Case and subtype, link the source receipt, append Case/Triage history and enqueue standard Case custody.
- Add Triage to generic manual Case creation. Require only Principal and registration; record staff actor and operation provenance without inventing an intake receipt or source evidence.
- Create standard Case custody for manually created Triage Cases and expose the established Case Files upload path in the specialised workspace.
- Keep accepted route/API evidence mandatory for automatic creation; manual creation is the only source-less path.

### Workflow, linking, due work and correspondence

- Preserve existing Triage transitions and correction rules, including reversible Completed/Cancelled states and completed-finding correction.
- Retain `TriageTargetDays`, default 1. A Triage Case without a finding remains a Needs attention item due at opened time plus that target, using the existing Europe/London calendar-day rules.
- Retain manual Triage chaser correspondence and exact Sent evidence. It is not automatic, not a completion gate and is available only when a suitable mailbox-origin conversation exists.
- Retain automatic and manual association with one later definitive instructed Case. Rename the relationship to make its direction explicit, such as `LinkedInstructionCaseId`.
- A later instruction allocates its own next shared sequence number and Case/PO. Linking records both histories but does not complete, cancel, convert or renumber the Triage Case.
- Preserve unique-match, Principal agreement, contradictory-registration, archived/terminal target, active edit lease and deliberate staff unlink/reassignment safeguards.

### Routes, queues and discovery

- Make `/Cases/{id}` the only Triage Case detail route. Dispatch `CaseType.Triage` to a specialised Case rendering that reuses the Case ribbon, working-set integration and Files conventions while retaining Triage determinations, state actions, notes, evidence and edit-scope behavior.
- Remove the Triage index redirect and old detail Razor endpoints entirely; do not add redirects, aliases or `410` compatibility endpoints.
- Keep a dedicated Triage queue, but move it from “Pre-Case work” into the Workflow group.
- Add an exact `Triages` counter to the Work Centre metric strip, counting active Triage Cases (`Open`, `AwaitingInformation`, `FindingRecorded`) and linking to the Triage queue. Completed and Cancelled are excluded.
- Include Triage Cases in global Case search by `t.` Case/PO, registration, Principal and available Case fields; results open `/Cases/{id}`.
- Keep Triage excluded from the existing “New cases today” measure and ordinary Case-stage counts because it has its own counter and specialised lifecycle.
- Update the Cases rail total, working-set record kind, command palette, labels and route generation to use the Case-backed identity without duplicating Triage in standard stage queues.

### Documentation

- Rewrite the glossary, PRD, FRD-01 and FRD-03 so Triage is a Case type with a `t.` Case/PO, established Principal, specialised workflow and no definitive-instruction meaning.
- Update FRD-02/09/22 for Principal-first classification, Case allocation, custody and later Case-to-Case association.
- Update FRD-12/15/16 and design documentation for the canonical `/Cases/{id}` specialised workspace, removal of `/Triage` routes, Workflow queue placement, Case search and fifth Work Centre counter.
- Correct FRD-03, FRD-16, capabilities and administration documentation to state that Triage has a configurable due target and supports manual chaser correspondence.
- Update FRD-14/21 for Triage Case edit authority and Sent evidence, and revise architecture/operations documentation only where their owned source or observed estate changes.

## Public Interfaces and Types

- `CaseType.Triage`.
- `TriageIdentity.Create(string caseReference)`.
- Case-backed Triage creation results return normal `CaseIdentity`.
- Triage commands and MCP tools continue exposing specialised operations but identify a Triage Case by its Case ID and `t.` reference.
- Search and queue projections expose Triage through the normal Case identity fields while retaining specialised state and actions.
- No old `T-00001` parser, global Triage reference formatter, legacy route or compatibility alias remains.

## Test and Verification Plan

- Core tests:
  - shared sequence allocation across Inspection, Audit and Triage;
  - exact lowercase `t.QDOS26001` formatting and expansion past sequence 999;
  - Principal and registration creation gates;
  - manual creation with only Principal and registration;
  - existing lifecycle, finding correction, replay and edit-scope rules;
  - due calculation and active/completed/cancelled counter inclusion;
  - later Case linking without state or identity mutation.
- Persistence/integration tests:
  - atomic Case/subtype/source/custody creation;
  - sequence concurrency and replay;
  - Case search by Triage reference;
  - standard custody and uploads for manual and intake-origin Triage Cases;
  - runtime-role grants and destructive migration on an estate containing disposable old Triage data.
- Web/MCP tests:
  - `/Triage` and `/Triage/{id}` return 404;
  - `/Cases/{id}` renders the specialised Triage Case in read, edit, conflict, completed and cancelled states;
  - dedicated Workflow queue and Triages counter;
  - manual creation validation;
  - chaser send/reconcile and optional response evidence;
  - Case association requiring both edit authorities.
- Update exhaustive `CaseType` tests and run focused Core, integration, architecture and Web suites, followed by the full solution verification because `CaseType`, Case identity and shared allocation are cross-cutting.
- Visually verify the Work Centre and specialised Case workspace at 1580×1000 and smaller desktop width, plus 760px for the changed shared metric layout. Check read/edit, empty files, upload, locked colleague, validation and closed states in both Scroll and Tabs modes.

## Assumptions

- The repository is unreleased; no `T-` reference or old route compatibility is required.
- Existing Triage development data is disposable and will not be converted.
- “Triages counter” is the authoritative new-case visibility for this type, so “New cases today” remains limited to definitive instructed Cases.
- The exact prefix is lowercase `t.`; the Principal code remains uppercase and the standard two-digit year/three-digit-minimum shared sequence format is unchanged.
</proposed_plan>
