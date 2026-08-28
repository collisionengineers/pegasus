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
