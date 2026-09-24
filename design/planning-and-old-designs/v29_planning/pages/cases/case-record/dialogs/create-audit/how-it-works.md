# Create audit — how it works today

Read from the live source on 23 September 2026 (origin/dev 446c3ce2f).

Create audit is an Actions-menu item on an Inspection + Audit Case. It opens
a compact dialog and, on confirmation, creates a second, linked Case of type
Audit. Paths are under `src/` unless stated.

## What this dialog does not show

- No reason field. The dialog's own comment: "No reason is asked; the action
  is its own record."
- No choice of reference, Engineer or state. The reference is always
  `a.{original reference}`; the Audit Case starts in Review with the
  original's Engineer.
- No list of what will be copied, and no warning that later changes on the
  original do not reach the Audit Case.
- No refusal before submit when no outcome is recorded. The item and the
  dialog are offered without an outcome; the dialog then has no Audit
  reference line, and Core refuses on submit.
- No mention of the Box folder the Audit Case will get.
- Nothing at all outside an edit session: `CanCreateAudit` requires
  `IsEditing`, so neither the menu item nor the dialog is rendered.

## Governing documentation

| Document | What it settles for this dialog |
| --- | --- |
| [FRD-01](../../../../../../../../docs/frd/frd-01-case-identity-and-lifecycle.md) | Inspection + Audit: once a report has been generated, Create audit (Actions menu, inside an edit session) creates one linked Audit Case with the same Principal and sequence number, type Audit, reference `a.{Case/PO}` whatever the outcome, the original's Engineer, starting in Review, carrying Case data, assessment and estimate, sharing files by reference, with a Box subfolder under the original's and a two-way link. A second Create audit is refused. |
| [ADR-0051](../../../../../../../../docs/adr/0051-linked-audit-case-identity-and-custody.md) | The same decision with its reasons: one sequence, two records; shared bytes; nested custody root created by Pegasus; the parent is the persisted relationship; intake routes unchanged. |
| [FRD-16](../../../../../../../../docs/frd/frd-16-case-record-workspace.md) | Create audit is an Actions item "on an Inspection + Audit Case once a report has been generated". |
| [FRD-14](../../../../../../../../docs/frd/frd-14-record-edit-leases.md) | The Case edit lease the command carries. |
| [FRD-05](../../../../../../../../docs/frd/frd-05-documents-extraction-and-custody.md) | Custody and the logical occurrence and version boundary the shared files use. |
| [CONTEXT.md](../../../../../../../../CONTEXT.md) | Audit and Inspection + Audit ("Once a report has been generated there, Create audit creates the Audit Case as `a.` plus the same number"). |

## Source

| Layer | File | What it owns |
| --- | --- | --- |
| Web | `Pegasus.Web/Pages/Cases/Details.cshtml` | The menu item (`offersCreateAudit`, `data-create-audit`) in the Hold group. |
| Web | `Pegasus.Web/Pages/Cases/Details.Frame.cs` | `CanCreateAudit`, `ProposedAuditReference`, `OnPostCreateAuditAsync`, the `AuditCase` and `OriginalCase` reads. |
| Web | `Pegasus.Web/Pages/Cases/Shared/_CaseDialogs.cshtml` | `case-create-audit-dialog`: its facts, hidden fields and buttons. |
| Web | `Pegasus.Web/Presentation/CaseWorkspaceLabels.cs` (`Frame`) | "Create audit", "Original case", "Audit case". |
| Web | `Pegasus.Web/Pages/Cases/Shared/_CaseHistory.cshtml` | How the history line reads on Notes. |
| Core | `Pegasus.Core/Lifecycle/CreateAuditCase.cs` | `CreateAuditCase` (the checks), `AuditCaseRefusal` and its messages, `AuditCasePolicy` (outcome mapping and history lines). |
| Core | `Pegasus.Core/Cases/CaseContracts.cs` | `AuditIdentity.Create` (`"a." + reference`), `CaseType`, `AuditAssessment`. |
| Infrastructure | `Pegasus.Infrastructure/Persistence/EfCreateAuditCaseStore.cs` | The transaction that creates and fills the Audit Case, the link queries and `HasGeneratedReportAsync`. |
| Infrastructure | `Pegasus.Infrastructure/Persistence/AutomaticEvaReviewSubmissionScheduling.cs` | The automatic EVA submission a Review entry can schedule. |
| Infrastructure | `Pegasus.Infrastructure/Persistence/EfRecentCaseQueries.cs` | Counts `audit_case_created` as a creation event for the Work Centre's New cases. |

## Behaviours

### When the item is offered

`CanCreateAudit` (`Details.Frame.cs`) is true only when all hold:

- the Case type is `InspectionAndAudit`;
- `AuditCase` is null (no Case has this one as its `AuditOfCaseId`);
- `OriginalCase` is null;
- the state is not Created in error;
- the Case is not archived;
- `HasGeneratedReport`: some report generation on the Case has an artifact in
  state `Confirmed` (`EfCreateAuditCaseStore.HasGeneratedReportAsync`);
- `IsEditing`: this browser holds the edit lease.

The recorded outcome is not part of the Web gate.

### The dialog

`_CaseDialogs.cshtml` renders `case-create-audit-dialog` whenever
`CanCreateAudit` is true. It is `dialog--compact`, titled "Create audit",
and shows a definition list:

- **Original case** — the Case reference.
- **Outcome** — `AssessmentValue(outcome)` with underscores turned to
  spaces, for example "total loss"; "Not recorded" when none is shown.
- **Audit reference** — `a.{reference}`, only when `ProposedAuditReference`
  is not null, which needs an outcome that `AuditCasePolicy.AssessmentFor`
  maps: `total_loss` to total loss; `repairable`, `cash_in_lieu` and
  `contract_repair` to repairable.

The Outcome row reads the shown value, which hides an AI proposal awaiting
review on a decision field (`ShownAssessment`). The Audit reference line and
Core read the recorded field as it is. With only an unconfirmed AI outcome,
the dialog shows Outcome "Not recorded" beside an Audit reference.

The form posts `id`, `expectedVersion`, `operationKey` and `editLeaseToken`
to `/Cases/{id}?handler=CreateAudit`. The buttons are Cancel and **Create
audit** (primary, initial focus).

### The checks in Core

`CreateAuditCase.ExecuteAsync` requires a case id, a non-negative version, an
operation key and an edit lease token, then `PerformCasework`, then a Staff
actor. It refuses, in this order, with these messages:

| Refusal | Message |
| --- | --- |
| `NotStaff` | Only a member of staff can create an audit case. |
| `NotInspectionAndAudit` | Only an Inspection + Audit case can have an audit created from it. |
| `SourceCreatedInError` | A case recorded as created in error cannot have an audit created from it. |
| `SourceArchived` | An archived case cannot have an audit created from it. |
| `AuditCaseAlreadyExists` | This case already has its audit case. |
| `NoGeneratedReport` | Create audit is available once a report has been generated on the case. |
| `NoRecordedOutcome` | The case has no recorded assessment outcome to derive the audit reference from. |

It then hands the store the source identity, the mapped assessment, the
reference from `AuditIdentity.Create` and the original's assigned Engineer.

### What the store creates

`EfCreateAuditCaseStore.CreateAsync` works in one serializable transaction.
It checks the version and the lease on the original and refuses a second
Audit Case. It then adds:

- **A Case row** with the original's Principal, sequence lineage, year and
  sequence; `Reference` and `AuditReference` both `a.{reference}`; type
  `audit`; initial state `review`; custody `pending`; `AuditOfCaseId` set to
  the original; the mapped assessment in `StandaloneAuditAssessment`; the
  original's accepted inspection deadline; instruction and images both
  complete.
- **Case data**: a new snapshot with every field of the original's copied
  with its source, policy and confirmation; completeness marked satisfied;
  the claim source contact overrides copied.
- **Assessment**: every `CaseAssessmentFields` row copied with its recorder
  and confirmer.
- **Estimate**: the original's accepted specification, or else its latest
  draft, copied as version 1 with its lines, keeping its state and whether it
  is current.
- **Files**: for each current, not-removed document version, a new document
  and version 1 naming the same Box file and Box version, the same pending
  storage key and custody status; each of its occurrences copied with its
  role, report preparation role, order, rotation and crop. Nothing is copied
  in Box.
- **Workflow**: state Review, the original's assigned Engineer and Sign-off
  Engineer, version 0.
- **Automatic EVA**: a pending automatic submission, only when the
  Principal's policy is `EvaAutomaticApiOnReview`.
- **Custody work**: one `CreateCaseCustody` work item. The store's comment
  says the processor sees the parent and roots the folder under the
  original's.

The store writes nothing else. It does not copy valuation records
(`CaseValuations`), report wording (`CaseReportWordings`), report
generations, Sent evidence or the original's history.

The original's version goes up by one and its lease is cleared
(`CaseMutationGuard.Complete`).

### History lines

The same line is written on both Cases, event `audit_case_created`:
"Audit case {a.reference} created by {user name} from {reference} — total
loss" (or "— repairable") (`AuditCasePolicy.SourceHistoryLine`,
`AuditHistoryLine`). The name is the staff account's user name, or the
subject id when none is found. Notes prints it as "{actor} — {event} —
{line}"; the event code has no entry in `OperatorLabels.HistoryEvent` and
reads through `Humanise`.

### Where it lands

On success `OnPostCreateAuditAsync` clears the page's lease state, sets
"Case {a.reference} was created." and redirects to `/Cases/{new id}`. The new
Case shows the Audit chip, the Original report section and an "Original case
{reference}" link; the original shows "Audit case {a.reference}". Neither
offers Create audit again. The Audit Case appears in the Work Centre's New
cases, because `EfRecentCaseQueries` counts `audit_case_created` as a
creation event. Being in Review, it needs Hand to Engineer to move on.

A refusal or a lost lease returns to the original with Core's message, or
"The audit case was not created because the case changed or edit mode was
lost." A replay with the same operation key returns the committed Audit
Case; the same key with different content is a conflict.

## Things the FRD does not settle

- Whether an outcome must be recorded first, and whether an unconfirmed AI
  outcome counts. FRD-01 says the reference does not depend on the outcome;
  Core refuses without one and reads the raw field.
- What "carries the Case data, assessment and estimate forward" (FRD-01) and
  "Case data, assessment, figures, files and estimate" (ADR-0051) include.
  The store does not copy valuation records or report wording, and it copies
  the Sign-off Engineer, which neither document names.
- What happens on the Audit Case when the original changes after creation.
  Nothing links the copies.
- Whether the Audit Case's Review entry should schedule an automatic EVA
  submission under an `EvaAutomaticApiOnReview` Principal.
- What the dialog should say, or whether it should be offered, when no
  outcome is recorded.
