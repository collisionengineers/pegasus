## 2026-08-28 — structured contract, implementation reconnaissance

Ticket moved back `review` → `implementing`; worktree fast-forwarded 24 commits to
`ba3a0e92`. Open-questions rewritten with the operator's 2026-08-28 structured-schema
ruling.

### Two plan items dropped as unnecessary (good news)

1. **No acceptance actor-guard change.** `AllocateIntake.AttemptAutomaticAsync`
   already allocates as `ActionActor.SystemWorker("system-worker:intake-processing")`
   (`IntakeAllocation.cs:274`). A provider submission reaches acceptance down the same
   automatic path an e-mail instruction does, so `AcceptIntake` and
   `EfCaseAcceptanceStore` need no widening. Provider attribution already lives on the
   submission's own action history (`ProviderSubmissionPolicy.ActionHistoryAggregateType`,
   `ActionActor.Provider`), which is where FRD-09 wants it.
2. **`AddCaseNote` is not widened.** Its Staff-only guard is a recorded decision with
   stated reasoning ("letting it author a note as well would put machine text where a
   colleague's words are expected", `CaseNotes.cs:49-56`). A provider note should instead
   be its own `CaseWorkflowEvents` event type (`provider_instruction_note`) written in
   the acceptance transaction and labelled in `OperatorLabels`. Same timeline, same
   append-only guarantees, no contradiction of the existing ruling.

### Constraints confirmed

- `CaseDataSnapshotFactory.AddExtractedValue` throws unless every draft value has a
  matching `InstructionReviewField` in `FieldsJson` with exactly one unambiguous
  candidate. A declared instruction must therefore emit review fields too, under the
  exact intake field names the factory looks up ("Claimant name", "Claim number",
  "Vehicle registration", "Vehicle make", "Vehicle model", "Vehicle mileage",
  "Accident circumstances", "Date of incident", "Instruction date", "Inspection date",
  "Inspection address").
- `IntakeEvidenceSource` needs a `ProviderDeclaration` member; the factory's
  `StaffCorrection ? StaffCorrection : IntakeEvidence` source mapping needs a third arm
  onto the new `CaseDataSourceKind.ProviderApi`.
- `CaseDataSnapshotFactory` asserts `request.AcceptedInspectionDeadline ==
  receipt.InstructionDraft?.InspectionDate`. **Drop `inspection.deadline` from the wire
  schema** — the deadline is the inspection date, exactly as the mail route has it.
- Triage needs no `DurableIntake` change at all: `CreateTriageIfQualifyingAsync`
  (`DurableIntake.cs:1084`) qualifies purely on a draft registration plus exactly one
  Strong `AcceptedTriageMatch` evidence with a matcher key and version. A declared
  triage just emits that evidence.
- `AllocateIntake` reads the case type from `receipt.MailClassificationDecision?.CaseType`;
  it needs an `EstablishedCaseTypeAsync` symmetric to the existing
  `EstablishedPrincipalCodeAsync`.

### Open design fork — one receipt or many?

`IGroupedIntakeSubmission` creates **one staged receipt per file**
(`GroupedIntakeMemberToken.Create`: ordinal 0 carries the bare submission token,
siblings get `:{ordinal}`). The declared instruction belongs to the submission, not to
one file, so binding it to every member would allocate a case per file.

The blocker is Audit: `RecordAutomaticAuditEvidenceAsync` resolves the original report
from `receipt.AssetRecords` — the *same* receipt. With one receipt per file the report
is a sibling receipt and is not reachable. Options recorded in the message to the
operator; awaiting the ruling before writing code.

## 2026-08-28 — structured contract implemented (Core, Infrastructure, Web build green)

Operator ruled **B — one receipt for the whole submission**. The retained source is
the request body exactly as sent (`instruction.json`, `application/json`), and the
submitted files are that receipt's attachments — the e-mail shape. This is what makes
a declared Audit able to find its original report among its own assets.

### What is written

- **`Pegasus.Core/ProviderApi/ProviderInstruction.cs`** — wire vocabulary in one place:
  `inspection|audit|auditreport|triage`, `repairable|total-loss`, file roles, and the
  mapping onto `CaseType` (`auditreport` → `InspectionAndAudit`; `triage` → no Case/PO).
- **`ProviderInstructionPolicy.cs`** — the declared instruction, its normalisation, the
  `InstructionDraft` projection, the review fields carrying
  `IntakeEvidenceSource.ProviderDeclaration`, and the Triage/declaration evidence.
  Bounds are the case store's own, not tighter wire numbers — a contract that refuses a
  50-character claimant name the database would have stored refuses real work.
- **`ProviderInstructionJson.cs`** — the wire schema and its single parser, used by both
  the endpoint (incoming request) and the intake reader (retained body). One owner, so
  the two cannot disagree about what a submission said.
- **`ProcessIntake.AssessAsync`** — one substitution: a `provider_api` source with a
  binding returns a declared assessment and never touches mail routing, classification
  or the extraction policy. Everything downstream is unchanged.
- **`IntakeAllocation`** — case type from the declaration, principal from the binding,
  and the provider's note written onto the created case.
- **`ProviderApiIntakeSourceReader`** (Infrastructure) — decorates the ordinary reader,
  answers only for its own channel, recovers the files as attachments.
- **`AddCaseNote`** — Staff-only guard replaced (operator decision). A Provider actor
  needs `SubmitProviderInstruction`; Staff and Automation still need `PerformCasework`.
- New case data: `claimant_contact_number`, `claimant_address`, and
  `CaseDataSourceKind.ProviderApi`. The file handler reuses the existing Contact block.
- `InstructionDraft` gained the eight declared-only fields; `InstructionDrafts` gained
  their columns.

### Corrections made while building

- **`auditreport` carries no original report and no verdict.** FRD-01 § Case types:
  Inspection + Audit is Collision Engineers auditing its *own* report, and its reference
  is the ordinary Inspection Case/PO with no `a.`/`ap.` prefix. Only a standalone
  `audit` has another firm's report to attach and a verdict to state.
- **`inspection.deadline` dropped from the wire schema.** `CaseDataSnapshotFactory`
  asserts `AcceptedInspectionDeadline == InstructionDraft.InspectionDate`; the deadline
  is the inspection date, exactly as it is for the mail route.
- **`CaseEditableData` and `CaseDataSnapshotFactory` construct positionally.** New record
  parameters are appended, never inserted, or every value after them shifts silently.
- **Envelope bound** (plan item C5): `MaximumProviderApiEnvelopeLength` 30 MiB decoded,
  `MaximumProviderApiRequestLength` 42 MiB of JSON, enforced while reading the body
  rather than trusted from `Content-Length`. Still wants the operator's confirmation.

### Remaining

Migrations + grants + bootstrap census; test fakes and new tests; FRD-01/03/09 and
capabilities; simplification pass.

## 2026-08-28 — declared contract working end to end (commit 2804ebb6)

All 8 `ProviderApiSubmissionTests` pass against SQL: a declared JSON instruction
creates a real Case/PO, a declared `total-loss` Audit takes the `ap.` prefix, a
declared `triage` opens a Triage and allocates no case, and a body naming another
Principal is 403 with a recorded security event. All 1110 Core tests pass.

### Two defects found while building, both fixed

1. **`IntakeEvidenceSource` had two persisted code maps, already drifted** —
   `EfIntakeReceiptStore` and `InspectionAddressResolutionStore` each carried their
   own copy. Adding `ProviderDeclaration` to one meant receipts wrote it happily and
   the address-resolution snapshot then refused to read it back, failing case
   allocation with an *unclassified* fault whose safe reason says only "The case
   could not be created." Now one owner, `IntakeEvidenceSourceCodes`. This is exactly
   the failure mode the "one list per concept" rail exists to prevent, and it was
   already latent on `dev` before this ticket.
2. **The scaffolded migration re-added `CaseWorkflows.EditLeaseHolderKind`.**
   `20260828110108_CaseEditLeaseHolderKind` reached this branch by merge and carries
   an *earlier* timestamp than this branch's own migrations, so the last Designer
   snapshot here (`GrantProviderSubmissions`, 11:17) predates it and the model diff
   saw the column as missing. Every integration test failed at migration time with
   "Column name 'EditLeaseHolderKind' ... specified more than once". Removed by hand
   and the reason recorded in the migration's `<remarks>`.
   **This will recur** on any branch that merges `dev` and then scaffolds a
   migration; worth a board item of its own.

### Notes for review

- No new grant migration: the change creates no table, only columns on already-granted
  tables and two recreated check constraints. `Test-MigrationGrants.ps1` passes
  (84 files checked).
- `dotnet ef migrations remove` reverts the model snapshot to the *previous
  migration's* Designer file, which is what exposed defect 2. Regenerating rather
  than hand-editing is still right; the hand edit was to the generated output.
- The envelope bound (30 MiB decoded / 42 MiB body) is my recommendation under plan
  item C5 and still wants the operator's confirmation. It is enforced while reading
  the body, not trusted from `Content-Length`.
- `docs/operator-notes.md` deliberately untouched: it is protected, and whether the
  declared-verdict ruling belongs there as well as in FRD-01/FRD-09 is still an open
  question for the operator.
