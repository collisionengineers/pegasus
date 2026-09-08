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

## Merged-base preparation refresh — 2026-09-08

Read-only baseline a022fc4b2db87d4d2eeb14437b41f6d8344d63e6 (INTK-063
integrated, still Verifying/taken). This supersedes stale implementation-shape
assumptions above, not the current-principal/transaction/manual-intent guards.
No declared research sources apply; current linked FRD-02/03 and EPIC-014 read.

### Exact existing owners and necessary composition

There is NO ITriageLifecycle type or registration on this SHA. TriageContracts
defines ITriageStore, ICreateTriageFromIntake and separate ILinkTriageCase /
IUnlinkTriageCase; TriageLifecycle.cs implements those commands separately.
Infrastructure/DependencyInjection.cs:139–160 registers the store and each
command. Calling a nonexistent lifecycle registration is not an option.

The smallest coherent automatic owner is one concrete TriageCasePairing
command in existing TriageLifecycle.cs, registered once beside those commands.
It shares ITriageStore's transaction and the existing EvaluateIntakeCaseMatch
eliminator, not a second matcher or service hierarchy. Do not overload the
manual Staff/lease-bearing command or put the automatic policy in Worker.

CreateTriageFromIntake.ExecuteAsync currently returns store.CreateAsync at
lines 5–15. Calling pairing after that create/replay covers ProcessQueuedIntake
and every existing creation caller, so DurableIntake.cs need not change.
Preserve the creation command's historical replay return contract. Acceptance
calls pairing after AcceptAsync, including duplicate acceptance, alongside
INTK-063's existing image pairing block (AcceptIntake.cs:118–147).
StagedArtifactReconciliationFunction:179–267 already owns the scheduled
recovery pass; add the Triage call there without another timer or queue.
No existing automatic Triage caller was found.

### Current matching, identity and manual boundaries

CaseMatchEntities.cs:188–264 still creates a separate context for each
FindByAnyKeyAsync and FindByCaseIdAsync. Both need the SAME context-bound form
of that query owner inside the existing Triage serializable transaction.
EvaluateIntakeCaseMatch.ExecuteDeclaredAsync already takes CaseMatchSourceData
and the fifteen registered principal policies; no change to its algorithm or
public contract is required.

Use the persisted Triage principal, accepted origin/evaluation identity and
typed original draft fields. Preserve the Triage registration; a present
contradictory origin draft registration refuses, not silently substitutes.
Check current origin identity/evaluation and principal again in the transaction.
Compare the final target Case.PrincipalId with known Triage.PrincipalId,
including replacement redirects. A historical provider-scoped candidate alone
does not establish the replacement's principal.

EfTriageStore.ChangeCaseLinkAsync:973–1096 owns serializable replay/hash,
history and CaseWorkflowEvent writes. Manual ValidateCaseLink remains
Staff-authorized and lease-bearing. The automatic entry is distinct and
SystemWorker-only; it rechecks complete candidate uniqueness, current
Triage/Case state and version and live lease. Prior deliberate manual unlink
or relink blocks automatic reattachment; no persistent new exclusion flag.
Cancelled refuses; Completed can link without reopening or changing findings.

### Permissions and test ripple

No new schema/grant is currently justified. RuntimeRoleReconciliation gives
Worker Triage SELECT/INSERT/UPDATE and TriageHistory SELECT/INSERT.
AddWorkerCaseCreationGrants already supplies CaseHistory SELECT/INSERT,
Principals SELECT/UPDATE and current CaseMatchIndex access; CaseWorkflows and
CaseWorkflowEvents permissions also exist. Use the existing restricted
ConnectedContextFactory / EXECUTE AS harness to prove the actual command.

Important: Worker has no TriageFindings SELECT. ITriageQueries.GetAsync reads
the full detail including findings and is NOT the recovery input. A narrow
existing-store automatic candidate projection reads only needed Triage,
history, origin/draft and principal data; findings are not needed to link and
remain untouched. Do not broaden grants just to reuse an unnecessarily broad
UI query.

QdosTriageCaseAssociationIntegrationTests:177 currently names and asserts
DoesNotAutoLinkOrCloseTriage. The no-link expectation is obsolete under this
ticket; retain its genuine QDOS source/hash and its unchanged-state/reference/
finding/no-extra-allocation assertions while proving the new link.
Existing ITriageStore test implementations are ReplayStore in TriageReplayTests
and NoteStore in AddTriageNoteTests: the latter was missing from the old map
and needs only new-contract fixture implementation. Existing QdosTriageReplay,
TriageFromIntake and AzureSqlRuntimeRoleMigrationTests remain the focused
acceptance owners. No new fixture host or fabricated domain evidence.

### Ownership and conclusion

TICK-035 is Done/released. INTK-063 is integrated but not closed/released;
TICK-085 still owns Infrastructure/DependencyInjection.cs. The composition
edit remains necessary, so execution must wait for both exact file releases
and root sequencing. This refresh is preparation only: no take, branch,
source change, build, test, claim transfer or provider write occurred.
