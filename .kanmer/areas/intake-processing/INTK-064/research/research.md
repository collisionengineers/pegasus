# Research — INTK-064

## Question and authority

The operator explicitly requires Triage to remain a Case with its distinct
reference and to link automatically to a formal Case where identity is unique.
FRD-03 already requires the formal instruction to link the existing Triage;
its residual pre-Case wording must agree with this current direction without
changing Triage reference/finding/completion semantics. FRD-02 owns current
source identity and manual reversal. EPIC-014 applies.

This supplemental fix links [[INTK-060]], [[INTK-033]], [[INTK-035]],
[[INTK-059]] and [[TICK-035]] without taking their claims. Root owns its separate
one-ticket frozen run; the original 218-ticket roster is unchanged. Preparation
only. Source inspected at baafa29e0f7002b8235aa43bf333f5d9bb172828 and relevant
callers rechecked on dev 19e6f523bf6760cab39104b4dca3674b0ac8a512.

## Actual caller and transaction evidence

- Core/Triage/TriageLifecycle.cs CreateTriageFromIntake delegates to CreateAsync;
  Core/Intake/DurableIntake.cs:1109 creates a qualified Triage but never links it.
  AcceptIntake calls only image pairing. ILinkTriageCase has manual Web/MCP
  callers, not an automatic production caller or scheduled recovery.
- TriageLifecycleRules.ValidateCaseLink:441 requires PerformCasework and a real
  Staff Case edit lease. Do not weaken that or create a fake Staff actor.
- EfTriageStore.ChangeCaseLinkAsync:973 has the existing serializable transaction,
  operation hash/replay, CaseMutationGuard, Triage version/history and Case
  workflow event. Extend this store's automatic path, not another link table.
- TriageRecord already stores optional PrincipalId; create persists resolved
  identity and queries project the code. INTK-059's Preparing description is
  stale relative to code; do not add another principal field or invent one when
  null. Record that discrepancy for root reconciliation, not another migration.
- EvaluateIntakeCaseMatch.ExecuteDeclaredAsync derives accepted typed keys
  through current principal policy then uses the existing eliminator. Origin
  receipt and accepted Triage VRM supply evidence; CaseMatchIndex is current,
  principal-scoped and includes Created-in-error redirect semantics.
- EfCaseMatchIndex in Persistence/CaseMatchEntities.cs opens a new DbContext
  per query. Rechecking only the target Case version cannot detect a new
  competing Case. The smallest necessary adapter refinement is a context-bound
  form of this SAME query owner for the existing Triage serializable transaction;
  reuse the Core evaluator, never copy its candidate/eliminator policy.
- RuntimeRoleReconciliation grants Worker Triage SELECT/INSERT/UPDATE,
  TriageHistory SELECT/INSERT, Cases/CaseWorkflows SELECT/UPDATE and
  CaseWorkflowEvents SELECT/INSERT. CaseMatchIndex Worker SELECT is established
  by its existing migration. Verify the real caller with the restricted role;
  no grant expansion is currently justified.

## Eligibility and recovery

Manual link/unlink still has real Staff authorization, reason and lease.
Automatic entry requires ExecuteSystemWork, current known principal, exactly
one noncontradictory current match, no active Staff lease and no deliberate
prior unlink/relink override. Recheck Triage current state/version, complete
candidate set and target currentness in the same transaction before writing
deterministic automatic history. Cancelled is ineligible. Completed Triage may
still link to a later formal instruction: completion closes Triage response
work, not its permanent association history. Do not copy image's pre-report
eligibility restriction into Triage without a requirement; preserve existing
Triage Case terminal/archive guards.

Invoke after Triage creation/replay, after formal acceptance/replay, and from
existing StagedArtifactReconciliationFunction. Bounded oldest eligible reads
must not let unknown-principal/no-match/manual-overridden rows starve valid
work. No new queue/schema or completion mutation; failures leave durable
unlinked state for retry and surface existing operational outcomes.

## Proof boundary

Existing QdosTriageCaseAssociationIntegrationTests, TriageFromIntakeIntegrationTests
and TriageReplayTests provide intake/history fixtures. Add both arrival orders,
timer-only recovery after failure, replay exactly-one history, current Case
correction/new competing candidate, known-principal conflict/unknown principal,
manual reversal, lease/stale version, Cancelled refusal and Completed linking.
Assert unchanged Triage identity/findings and no extra Case/PO or copied definitive
Case findings. No tests or provider writes ran during this research.
