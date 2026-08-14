# Research — TICK-012 (INT-25): Automatic case creation from definitive authorised intake

## Question

Establish the current Core policy owner, the real durable caller, the
persistence/infrastructure boundary, and the acceptance evidence for INT-25 —
and separate what mechanism is already implemented and caller-proved from what
remains (breadth, live deployment acceptance) before proposing any change.

Activation boundary (docs/capabilities.md:112): "The durable processing path
consumes every persisted typed QDOS case type and attempts one replay-safe
allocation. An Audit is definitive only when its instruction and a separate
original report are retained and the report carries exactly one literal
outcome: `repairable` or `total loss`. It then creates its Case/PO and `a.` or
`ap.` reference automatically, without staff confirmation. Unique existing-case
matches bypass allocation. Failures retain a bounded allocation outcome
separately from the processing decision and completed-work replay cannot retry;
authenticated staff may retry the frozen command with a reason after correction."

## Findings

- **Core policy owner — IMPLEMENTED.** `AllocateIntake` (interface
  `IAllocateIntake`) is the single Core owner of automatic case creation:
  `src/Pegasus.Core/Intake/IntakeAllocation.cs:174,203`. Class doc (:199-202):
  "The one Core owner for initial allocation, durable failure and reasoned staff
  retry. Completed-work replay never calls this use case." Entry points:
  `AttemptAutomaticAsync` (:213, system actor `system-worker:intake-processing`
  :210), `AttemptStaffCreateAsync` (:252, the INT-26 manual path), `RetryAsync`
  (:277). It delegates the transaction to `AcceptIntake` (`AcceptIntake.cs:12`)
  → `ICaseAcceptanceStore.AcceptAsync`.
- **Reference generation — IMPLEMENTED.** Numeric Case/PO reference minted in
  the acceptance transaction: `EfCaseAcceptanceStore.cs:252`
  `$"{principal.Code}{year % 100:00}{allocatedSequence:000}"` (e.g. `QDOS25007`),
  per principal-lineage/year sequence (:251), London-year (:231), exhaustion
  bound `>= 999` (:246). The `a.` / `ap.` audit reference is `AuditIdentity.Create`
  (`src/Pegasus.Core/Cases/CaseContracts.cs:93-108`): Repairable → `a.`,
  TotalLoss → `ap.`, returning `prefix + caseReference` (e.g. `a.QDOS25007`);
  applied at `EfCaseAcceptanceStore.cs:253-255,271`.
- **Durable processing path / real caller — IMPLEMENTED and wired (not
  feature-flag gated).** Timer `PendingWorkDispatchFunction`
  (`src/Pegasus.Worker/IntakeFunctions.cs:12-19`, `%PendingWorkDispatchSchedule%`)
  → `DispatchPendingWork` (`ExternalWorkProcessing.cs:84`) →
  `DispatchPendingIntakeWork` (`DurableIntake.cs:544`) enqueues to `intake-work`;
  queue trigger `IntakeWorkFunction` (`IntakeFunctions.cs:30-45`) →
  `ProcessQueuedIntake.ExecuteAsync` (`DurableIntake.cs:589-756`) which processes
  the source (:694), completes the evaluation (:705), associates a unique match
  (:736) and performs **one automatic allocation** via
  `allocateIntake.AttemptAutomaticAsync(...)` at `DurableIntake.cs:742-745`. Wiring:
  `WorkerDependencyInjection.cs:92-102`; Core use cases registered in
  `DependencyInjection.cs:64,131,137` with the explicit comment "allocation is no
  longer a staff action: the Worker's processing path creates the case for a
  definitive instruction" (:133-137). The old `RequireActivatedPrincipal`
  activation gate was deliberately removed (`CaseContracts.cs:56-73`); the only
  `QdosAlpha…Gate` (`CoreAssembly.cs:52`) is a release-acceptance manifest
  evaluator, not a runtime switch.
- **"Every persisted typed QDOS case type" — IMPLEMENTED.** The type fed to
  allocation is `receipt.MailClassificationDecision?.CaseType`
  (`IntakeAllocation.cs:225`), one of `Inspection | Audit | InspectionAndAudit`
  (`CaseContracts.cs:30-35`), set by `QdosMailClassificationPolicy` (:148-164).
- **"Replay-safe, one allocation" — IMPLEMENTED.** Guard
  `IntakeAllocation.cs:220-223` (`CurrentCaseId is not null || Decision !=
  CaseCreated → return null`), operation-key `BeginAsync` with
  `IsReplay`/`IsSuppressed` (:355-377), command hashing (:535-555), success-in-
  transaction (`EfCaseAcceptanceStore.cs:162,422`), duplicate short-circuit with
  exact-replay check (:151-178, `EnsureExactReplay` :518-546). Idempotency
  documented at `DurableIntake.cs:644-650`.
- **Definitive-Audit gating — IMPLEMENTED (three layers).** (1) Classifier
  `EvaluateStandaloneAuditReport` (`QdosMailClassificationPolicy.cs:180-215`)
  requires ≥2 distinct attachments with exactly one instruction (:201) and the
  non-instruction report carrying exactly one XOR outcome (:206-214), matched by
  literal/negation-aware regexes (:259-268 — excludes "unrepairable", "not a
  total loss"). (2) Fail-closed gate `ProcessIntake.cs:155-166` forces
  `NeedsSorting` when an Audit lacks its standalone report. (3) Acceptance-time
  re-validation `AcceptIntake.cs:60-72` + `ResolveStandaloneAuditEvidenceAsync`
  (`EfCaseAcceptanceStore.cs:440-493`). Automatic evidence captured by
  `RecordAutomaticAuditEvidenceAsync` (`ProcessIntake.cs:243-269`) →
  `EfStandaloneAuditEvidenceStore` (:40-124).
- **Unique existing-case match bypass — IMPLEMENTED.** `EvaluateIntakeCaseMatch`
  (`EvaluateIntakeCaseMatch.cs:13`) with `QdosCaseMatchPolicy`; one survivor →
  `UniqueMatch` (:94-102), several → `Ambiguous` (:103-111, downgrades to
  `NeedsSorting` at `ProcessIntake.cs:367-372`). Bypass:
  `AssociateCaseIfUnambiguousAsync` (`DurableIntake.cs:792-833`) sets
  `CurrentCaseId` before the allocation call, so `AttemptAutomaticAsync` no-ops
  (association at :736 ordered before allocation at :742).
- **Failure / replay-safety — IMPLEMENTED.** The receipt carries the processing
  `Decision` (`IntakeContracts.cs:345`) separately from
  `IntakeAllocationState? AllocationState` (:367). The bounded failure taxonomy
  `IntakeAllocationFailureKind` (`IntakeAllocation.cs:23-30`:
  PrincipalUnavailable, ConcurrencyConflict, SequenceExhausted,
  CaseTypeUnavailable, Unexpected) is classified in `Classify` (:488-508) and
  persisted via `CompleteFailureAsync` (:469-486) without touching the decision.
  Completed-work replay cannot retry (invariant :200-201; `RetryAsync`
  suppresses a `Succeeded` attempt :304-307, waits on `Pending` :308-317).
  Staff retry reuses the **frozen** stored command (:332-339), requires a staff
  actor with `PerformCasework` + non-empty reason ≤500 chars (:510-533). Web
  surface `Intake/Details.cshtml.cs OnPostRetryAllocationAsync` (:113-160), gated
  by `CanRetryAllocation` (:72), `Forbid()` for non-staff (:126,147).
- **Tests — broad IMPLEMENTED coverage.** Core:
  `AllocateDefinitiveIntakeTests.cs`
  (`AutomaticAllocationUsesPersistedTypedCaseType` :10,
  `FailedAutomaticAttemptIsDurableAndIsNotRetriedInBackground` :25,
  `AutomationActorCannotInvokeStaffRetry` :42,
  `SequenceExhaustionIsBlockedAndUnexpectedFailureIsSafe` :61);
  `DefinitiveIntakeCaseTypeTests.cs`; `Qdos/QdosMailClassificationPolicyTests.cs`
  (:44,:71,:90,:115 — the definitive-Audit triple condition + negation);
  `CaseMatching/EvaluateIntakeCaseMatchTests.cs`. Integration:
  `QdosAllocationRecoveryTests.cs`
  (`UniqueExistingCaseAssociationBypassesNewAllocationExactlyOnce` :280,
  `MissingPrincipalFailurePersistsAndReasonedStaffRetryAllocatesExactlyOnce` :339,
  `ReceivedProjectionSeparatesProcessingDecisionFromFailedAllocation`
  in `IntakeAllocationConsumerTests` :909, and more), `CaseAcceptanceReplayTests.cs`,
  `QdosIntakeWebTests.cs`, browser `QdosAllocationRecoveryBrowserTests.cs`.
  Wiring: `WorkerCompositionTests.cs:56,59,137`.
- **Requirement text (verbatim).** `docs/requirements.md` "Matching conflicts
  and reversible association" (:199): "Definitive authorised intake creates
  exactly one instructed Case idempotently. A definitive match to an existing
  instructed Case allocates no duplicate. A new instructed Case enters `Not
  ready` … The allocation decision adds no universal manual acceptance gate."
  (:258) and "One source occurrence has at most one current Case association.
  Every automatic or manual association records the exact source and Case
  identities, evidence, actor, time, policy/version, and reason where required…"
  (:260). `Not ready`/`Review` initial state at `EfCaseAcceptanceStore.cs:258-260`.

## Implications

- INT-25's **mechanism is complete and test-covered end-to-end** through the
  real durable caller. This is a plan-and-accept ticket, not a build ticket;
  plan.md must not re-implement or "improve" a working, invariant-bearing path.
  The task deliverable is the contract/caller/failure/test record the ticket's
  Verification asks for, plus an activation-criteria decision.
- The ticket's two Verification boxes are answerable now: (1) the exact feature
  contract, caller, failure behaviour and required tests are enumerated above
  and will be captured in plan.md; (2) the **local caller-proof activation tier
  is satisfied** — the tests cited are the evidence, runnable now.
- Residual gaps are breadth and operations, not mechanism, and should be
  recorded as accepted boundaries / follow-ups rather than fixed under this
  ticket: (a) **QDOS-only** — only `Qdos*` provider policies are registered
  (`DependencyInjection.cs:123-130`); non-QDOS needs a human key
  (`CaseContracts.cs:68-72`); (b) automatic Audit outcome is **text-literal
  only** — scanned/OCR-needed PDFs fail closed to `NeedsSorting`
  (`ProcessIntake.cs:354-358`), bounding how many Audits auto-create; (c)
  mailbox-origin latency depends on the configured cron + a running Worker
  (`IntakeFunctions.cs:14`) — operational, not a code gap.
- The tier-5 live/deployment acceptance (an enabled Worker caller actually
  creating a case against the deployed estate) is `requires-live-approval` and
  is NOT provable now — consistent with NOW.md's "No browser journey has
  exercised … an enabled Worker caller against the deployed estate."
- INT-25 blocks DOC-01 (TICK-017): the immutable Case/PO reference DOC-01 names
  its Box folder from is exactly what `EfCaseAcceptanceStore.cs:252` mints; the
  contract handoff (reference format, immutability, the `create_case_custody`
  enqueue) is owned here.

## Open questions

- For Verification box 2, is "activation criteria satisfied or explicitly
  accepted" meant to be closed at the **local caller-proof tier** (tests green),
  with the live/deployment tier tracked as an explicitly-accepted deferral — or
  does the operator want a live deployed case-creation journey before this
  ticket can reach the final stage?
- Should the QDOS-only breadth and the OCR-literal Audit bound be filed as their
  own capability/follow-up tickets, or recorded as accepted alpha scope here?
- Confirm no separate acceptance-manifest step (`QdosAlpha…Gate`,
  `CoreAssembly.cs:52`) must be re-evaluated as part of "activation criteria" for
  this capability, versus it being a release-record concern owned elsewhere.
