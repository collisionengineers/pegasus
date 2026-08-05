# Intake allocates without a gate — task plan

Branch `task/intake-allocates-without-a-gate`. The behavioural half of the
Inbox page work in the UI implementation programme (`NOW.md`), taken first
because the Inbox screen's queue, chips and counts all describe the outcome of
this change.

Closes defect **B4** and the "DraftReady is not a business state" section of
`docs/ui-work/defects-and-non-functional.md`, and implements **INT-25 /
CAP-008**.

## The finding

`IAcceptIntake` had exactly one caller in the solution — the staff
`OnPostAcceptAsync` form handler — so every case in Pegasus waited on a person
pressing "Accept and allocate case reference". `requirements.md` forbids
exactly that: definitive authorised intake creates one instructed Case
idempotently, and "the allocation decision adds no universal manual acceptance
gate". `IntakeDecision.DraftReady` existed only to name the wait.

The gate was also ceremony: everything the form asked for, processing had
already established — route accepted, extraction policy `Applicable`, case
match not ambiguous — and the page prefilled the principal from the same draft.

## What changed

| Layer | Change |
|---|---|
| Core | `IntakeDecision.DraftReady` → `CaseCreated`, with the removal reasoned in the enum's own documentation. `IntakeQueueCounts` drops the draft count. |
| Core | `ProcessIntake` yields `CaseCreated` for the definitive path, and routes standalone Audit work to `Needs sorting` — its case cannot be justified until the original report is confirmed. |
| Core | `ProcessQueuedIntake.AllocateCaseIfDefinitiveAsync` calls `IAcceptIntake` at processing time, replay-safe through the evaluation-scoped operation key and non-blocking on failure. |
| Infrastructure | `IAcceptIntake` registered in Infrastructure, because the Worker composes only Infrastructure and allocation is no longer a staff action. |
| Infrastructure | `EfCaseAcceptanceStore` admits `case_created`, legacy `draft_ready`, and `needs_sorting` (INT-26), and refuses everything else, so the fail-closed boundary does not depend on the caller. |
| Infrastructure | `GetCountsAsync` and `ListAsync` exclude receipts that produced a case. |
| Web | The acceptance form moves from the definitive receipt to `Needs sorting`, which is what INT-26 always was. Label maps and filter tokens follow. |

## Decisions worth recording

- **The case enters `Not ready` with nothing confirmed.** That is the
  requirement's own answer to thin ordinary detail. Staff confirm completeness
  on the case, where the evidence is.
- **`draft_ready` stays readable** and resolves to `CaseCreated` — the same
  processing outcome, minus the wait. No migration, and round-tripping stays
  exact because `case_created` is the only code written.
- **Audit fails closed.** An automatic path cannot supply confirmed
  original-report evidence, so audit-classified instructions wait for a person.
- **Case type is `Inspection`** on the automatic path. Nothing in the extracted
  draft carries an instruction type, and the only other type, `Audit`, is
  withheld above.

## Verification

- `dotnet build --configuration Release` — clean
- Core **441/441**, architecture **73/73**, integration **400 passed, 0 failed**
- `InstructionDraftWebTests` renamed and extended: one case, one sequence, the
  workflow state `NotReady`, and the `CaseIntakeLinks` row — from an upload,
  with no staff action. It previously asserted zero cases, which was the gate.
- Nine other tests updated where they encoded the gate rather than a
  requirement; each is commented with what changed and why.

## Follow-up

The Inbox screen itself — direction tabs, filter chips, the merged
received/sent projection, row shape and retry — is the next task in the
programme and consumes what this one produced.
