# PR #639 — behavior diff and line-by-line preservation table

Read-only analysis (Wave 0, 2026-09-06, Opus 5 under claude-fable-c). No checkout, merge, reset, stash or write to any repository occurred; the dirty primary checkout and the PR-069 worktree were left exactly as found.

## 1. Header

| Field | Value |
| --- | --- |
| PR | `collisionengineers/pegasus#639` — "INTK-048: resolve manually linked Unidentified receipts (deferred — draft)" |
| State | OPEN, draft-by-intent, `mergeable: CONFLICTING`, base `dev`, last updated 2026-09-02T10:08:20Z |
| Branch | `task/intk-048-unidentified-manual-link` |
| Recorded tip | `51e7306c04547fda7e3f19064d4dc57c78bf7da0` |
| Live head (`gh pr view 639 --json headRefOid`) | `51e7306c04547fda7e3f19064d4dc57c78bf7da0` — unchanged; no delta |
| Integration base D | `3284f93fc3ea9fd3bbbea9405ec92dc7818378f2` = `origin/dev` |
| Merge-base(D, tip) | `fedeedf393015016dd2d39a45f3ddb0c451ec9bf` (PR #630 merge, 2026-08-29); single merge base |
| Distance | D is 255 commits ahead of the merge base; the branch is 7 commits ahead |
| Reviews / comments | `reviews: []`, `comments: []` via JSON; `--comments` fails on this token (`read:project` scope), so absence of review threads is undetermined, not assumed |

Commits (oldest first): `14e0ad6f` Resolve manually linked Unidentified receipts; `b25f9b24` merge origin/dev; `b5fd8725` Fix unidentified reconciliation after manual link changes; `0147af6b` refactor(intake): name the reconciliation automation identity once (PR-069); `054bfe08` fix(intake): key the Unidentified correction on the item, not the receipt (INTK-048); `1f036337` fix(intake): advance the Unidentified recheck watermark on every completed pass (INTK-048); `51e7306c` test(intake): record the recheck-watermark migration in the committed census (INTK-048).

Change surface: 18 files, +8797/−100; excluding the generated 7 510-line Designer file, 17 substantive files, +1287/−100.

## 2. What is already present at D, and what is absent

Verdict: none of PR 639's behavior is present at D (checked by reading D's files). `git grep` at D for `SynchronizeForReceiptAsync`, `ReopenUnidentifiedRequest`, `UnidentifiedReopenResult`, `ListResolutionsToRecheckAsync`, `MarkResolutionRecheckedAsync`, `ReconciledAssociationVersion`, `AutomationActorId`, `int Corrected`, `UnidentifiedResolutionRecheckWatermark` → 0 files each.

At D: `ReconcileUnidentifiedDestinations.cs` is 164 lines, 3-arity result (D:8–11), `ResolveForReceiptAsync` (D:87), top-of-method `if (ProcessIntake.IsUnidentifiedEligible(receipt)) return false;` (D:92–95), inline destination chain with no manual-link fallback (D:110–144), inline `ActionActor.Automation("intake-processing")` (D:154), receipt-keyed operation key `$"intake-unidentified-reconcile:{receipt.Id:N}:{receipt.Version}"` (D:155). `IUnidentifiedStore` (UnidentifiedContracts.cs:260–310) lacks `ReopenAsync`, `ListResolutionsToRecheckAsync`, `MarkResolutionRecheckedAsync`. `UnidentifiedItemEntity` (UnidentifiedEntities.cs:3–28) lacks `ReconciledAssociationVersion`. `Details.cshtml.cs` D:659 calls `ResolveForReceiptAsync` inside a blanket recoverable catch (D:661). `IntakeFunctions.cs` D:274 logs 3 placeholders. `DurableIntake.cs` D:911 calls `ResolveForReceiptAsync`.

Foundations already at D (C must not re-add): `IntakeReceipt.ManualAssociationVersion` (IntakeContracts.cs:433), `ManualLinkedCaseId` (:432), derived `CurrentCaseId` (:445–446), `CurrentCaseReference` (:470–473); `IntakeManualAssociationEntity` with `Version` (PegasusDbContext.cs:1175–1192, :1182); `UnidentifiedOperationConflictException` / `UnidentifiedVersionConflictException`; `AddScoped<ReconcileUnidentifiedDestinations>()` (DependencyInjection.cs:121); constructor injection into Web (Details.cshtml.cs:30) and Worker (IntakeFunctions.cs:153).

## 3. File / hunk preservation table (branch-side line numbers)

### Core

| File | Hunk / lines | Behavior | Disposition | Target at D / reasoning |
| --- | --- | --- | --- | --- |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | 911 | Rename call `ResolveForReceiptAsync` → `SynchronizeForReceiptAsync` | needs-C-port | D:911; land with the Core rename |
| `ReconcileUnidentifiedDestinations.cs` | 8–12 | Result gains `int Corrected` as third positional member (`Candidates, Resolved, Corrected, Failures`) | needs-C-port | D:8–11; every construction site and assert moves together |
| " | 13–27 | XML doc stating the inverse rule (reopen/re-target on later manual association change) | needs-C-port | D:13–24; PR-069 wording preferred |
| " | 41–46 | `public const string AutomationActorId = "intake-processing"` + `ReconciliationActor` — names the automation identity once so the EF store selects exactly this owner's resolutions | needs-C-port | Replaces D:154; keep public (single-owner rule) |
| " | 57, 81 | `var corrected = 0;` and open-loop call renamed | needs-C-port | D:41, D:65 |
| " | 92–127 | The recheck sweep: `ListResolutionsToRecheckAsync(maximumItems)`; per row load receipt, `SynchronizeForReceiptAsync` (corrected++ when it wrote), then unconditionally `MarkResolutionRecheckedAsync(item.Id, receipt.ManualAssociationVersion)`; failures counted, never fatal | needs-C-port (heart of the rule) | After D:74. Load-bearing: (a) mark is written even when nothing else was, else the row starves the bounded page; (b) version recorded is the one this pass read |
| " | 128 | `return new(candidates, resolved, corrected, failures);` | needs-C-port | D:76 |
| " | 139–209 `SynchronizeForReceiptAsync` | (1) `GetByOriginAsync` for every receipt; eligibility short-circuit re-gated inside the Open branch as `receipt.CurrentCaseId is null && IsUnidentifiedEligible(receipt)` (155–159) so a manually linked still-NeedsSorting receipt resolves — INTK-048's headline rule; (2) item resolved by any actor other than Automation/intake-processing is never touched (172–177); (3) destination differs → `ReopenAsync` then `ResolveAsync` if a destination remains (179–208) | needs-C-port with §5 resolution | D:87–163. The eligibility re-gate at 155–159 is the single most important line; PR-069 dropped it |
| " | 211–252 `DestinationForAsync` | Precedence chain: CaseCreated+CurrentCaseId → InstructionCase; ImageIntakeRegistered → ImageIntake; IsTriageRequest+triage → Triage; then trailing `receipt.CurrentCaseId is { } linkedCaseId` → InstructionCase (245–250) | needs-C-port | Replaces D:107–144; uses `CurrentCaseReference` instead of D:114's `AcceptedCaseReference ?? ManualLinkedCaseReference` |
| " | 254–270 `ResolveAsync` | Resolve via existing `IResolveUnidentified` owner with `ReconciliationActor` and `OperationKey("reconcile", item)` | needs-C-port | one owner, no second reconciler |
| " | 272–287 `OperationKey(transition, item)` | `$"intake-unidentified-{transition}:{item.Id:N}:{item.Version}"` — item-keyed; each transition from a distinct item version is a distinct key; a retry of the same transition rebuilds the same key | needs-C-port (behavioral) | Replaces D:155; without it opening a Triage for an already-linked receipt strands the item Open |
| " | 289–292 | `private sealed record UnidentifiedDestination(Kind, Id, Reference)` | needs-C-port | internal |
| `Intake/Unidentified/UnidentifiedContracts.cs` | 256–268 | `ReopenUnidentifiedRequest(UnidentifiedItemId, ExpectedVersion, Actor, OperationKey, Reason, ReopenedAtUtc)`, `UnidentifiedReopenResult(Item, History, IsReplay)` | needs-C-port | after D:253/258 |
| " | 284–286 | `IUnidentifiedStore.ReopenAsync` (Resolved → Open; withdrawn resolution stays in history) | needs-C-port | after `ResolveAsync` D:270 |
| " | 310–336 | `ListResolutionsToRecheckAsync(int maximum)`, `MarkResolutionRecheckedAsync(Guid, long associationVersion)` with starvation docs | needs-C-port | after `ListAsync` D:292–301 |
| " | 481–493 | `UnidentifiedValidation.ValidateReopen` | needs-C-port | mirrors `ValidateResolve` D:416 |

### Infrastructure

| File | Hunk / lines | Behavior | Disposition | Target at D / reasoning |
| --- | --- | --- | --- | --- |
| `Persistence/EfUnidentifiedStore.cs` | 205–270 `ReopenAsync` | Serializable tx; op-key replay probe → `UnidentifiedOperationConflictException` when key recorded against a different request; `UnidentifiedVersionConflictException` on stale version or non-Resolved; clears every resolution field including `ReconciledAssociationVersion = null` (250); `Version++`; history row Resolved → Open | needs-C-port | after `ResolveAsync` (~D:203). Nulling the watermark on reopen is required |
| " | 311–350 `ListResolutionsToRecheckAsync` | Join `UnidentifiedItemEntity.OriginId == IntakeManualAssociationEntity.IntakeReceiptId`, filter `State == Resolved && ResolvedByActorKind == Automation && ResolvedByActorSubjectId == AutomationActorId && OriginKind == Receipt && (ReconciledAssociationVersion == null \|\| != association.Version)`, `orderby ResolvedAtUtc, Sequence`, `Take(maximum)` | needs-C-port | Freshness is the association Version, never a timestamp (fix from `1f036337`) |
| " | 353–371 `MarkResolutionRecheckedAsync` | `ExecuteUpdateAsync` scoped `Id == itemId && State == Resolved`, sets watermark, deliberately no concurrency token | needs-C-port | preserve with its comment |
| `Persistence/UnidentifiedEntities.cs` | 25–32 | `public long? ReconciledAssociationVersion { get; set; }` on `UnidentifiedItemEntity`, off the domain record | foundation-A (C-F01) | after `ResolutionTargetReference` D:24 |
| `Migrations/20260829222702_UnidentifiedResolutionRecheckWatermark.cs` | new, 77 lines | `AddColumn<long>` nullable bigint, no backfill, no new GRANT, SQL-Server-only assertion that `pegasus_worker_runtime_role` holds object-level UPDATE (`class = 1`, `minor_id = 0`) on `dbo.UnidentifiedItems` else `THROW 51000`; Down drops column | foundation-A (C-F01); file itself reject-stale-churn | id sorts before seven D migrations (newest `20260905010654_CaseSignOffEngineer`); A regenerates on D, preserving the content decisions |
| `…Designer.cs` | new, 7 510 lines | generated snapshot, stale | reject-stale-churn | never port |
| `PegasusDbContextModelSnapshot.cs` | @@ -5886 | `b.Property<long?>("ReconciledAssociationVersion").HasColumnType("bigint");` | foundation-A (C-F01) | regenerate against D |

### Web / Worker

| File | Hunk / lines | Behavior | Disposition | Target at D / reasoning |
| --- | --- | --- | --- | --- |
| `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs` | 7 | `using Pegasus.Core.Intake.Unidentified;` | needs-C-port | D:4–9 |
| " | 654–678 | (a) `CloseUnidentifiedForTriageAsync` calls `SynchronizeForReceiptAsync`; (b) `UnidentifiedOperationConflictException` excluded from the advisory catch (673–675) — permanently taken key surfaces instead of a 302 over lost work; version conflict stays advisory | needs-C-port ((b) behavioral) | D:653–664 (call D:659, catch D:661); no state inferred in Web; view untouched |
| `src/Pegasus.Worker/IntakeFunctions.cs` | 193–197; LoggerMessage | threads `Corrected` into the existing log; `{Corrected}` in template; same timer, no new schedule | foundation-A with needs-C-port dependency on the result record | D:193–197, D:273–279; PR-069's wording (updates INTK-018 comment D:188–192) preferred |

### Tests

| File | Hunk | Behavior | Disposition |
| --- | --- | --- | --- |
| `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs` | 312–350 | fake `IUnidentifiedStore` grows `ReopenAsync` (throws), `ListResolutionsToRecheckAsync` (empty), `MarkResolutionRecheckedAsync` (no-op) | needs-C-port (compile-forced; the empty list keeps the sweep exercised) |
| `tests/Pegasus.Core.Tests/AiWork/AiJobTests.cs`, `Operations/DashboardBoundaryTests.cs`, `tests/Pegasus.IntegrationTests/OperationsWebTests.cs`, `UploadOutcomeQueriesTests.cs` | fake members | same three fake members, `NotSupportedException` | needs-C-port (compile-forced, zero behavior) |
| `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | 113 | census literal `"20260829222702_…"` | reject-stale-churn (literal) / foundation-A (obligation: regenerated migration must be in the census) |
| `tests/Pegasus.Core.Tests/Intake/ReconcileUnidentifiedDestinationsTests.cs` | arity churn + key assertion `intake-unidentified-reconcile:{item.Id:N}:0` | needs-C-port (key assertion behavioral) |
| " | 63–86 `ManuallyLinkedUnidentifiedReceiptResolvesToTheInstructionCase` | INTK-048 headline case | needs-C-port (PR-069 lacks it) |
| " | 125–391 | eight new tests: `ChangedManualAssociationReopensAnAutomationResolvedItemWhenTheLinkIsRemoved`, `…ReopensAndRetargetsAnAutomationResolvedItem`, `SuccessiveCorrectionsAtAnUnchangedReceiptNeverShareAnOperationKey`, `ARetriedCorrectionRebuildsTheResolveKeyItsFailedAttemptUsed`, `ChangedManualAssociationNeverReopensAStaffResolution`, `UnchangedAutomationResolutionIsANoOp`, `ManuallyLinkedImageIntakeReceiptKeepsImageIntakePrecedence`, `ManuallyLinkedTriageRequestKeepsTriagePrecedence` | needs-C-port |
| " | harness | `FakeResolveUnidentified` mutates the stored item (Version+1); `FakeUnidentifiedStore` gains `RecheckItems`, `ReopenRequests`, `RecheckMarks`, `Replace`, version-advancing `ReopenAsync`; `AddAutomationResolvedCaseItem` | needs-C-port (without a version-advancing fake the uniqueness tests pass vacuously) |
| `tests/Pegasus.IntegrationTests/UnidentifiedReconciliationTests.cs` | 92–449 | three real-SQL tests: `SweepFollowsAManuallyLinkedReceiptThroughUnlinkAndRelink`; `StaffOpeningTheTriageOfALinkedReceiptRetargetsTheRecordedDestination` (POST `/Received/{id}?handler=OpenTriage`, receipt Version unchanged, all history keys distinct, two all-zero sweeps); `ACompletedRecheckStopsHoldingTheHeadOfTheRecheckPage` | needs-C-port (highest value: only tests exercising the real SQL predicate) |

## 4. Retained behaviors as testable statements

Core: (1) NeedsSorting + active manual association resolves the open item to that case with the case reference; (2) ImageIntakeRegistered + manual link → Image intake precedence; (3) Triage request + manual link → Triage precedence; (4) no destination → stays Open; (5) staff-resolved items never reopened/retargeted; (6) destination withdrawn → reopen, all resolution fields cleared, no re-resolve; (7) destination changed → reopen and re-resolve in the same pass, withdrawn destination stays in history; (8) two successive corrections at an unchanged receipt version produce four distinct operation keys (`intake-unidentified-{transition}:{item.Id:N}:{item.Version}`); (9) reopen committed but re-resolve failed → retry presents the same resolve key and replays: first sweep `(0,0,0,1)`, second `(1,1,0,0)`; (10) a no-change recheck still records the watermark (`Assert.Single(RecheckMarks)`); (11) the version recorded is the one this pass read; (12) a failed correction increments Failures and writes no watermark.

Real SQL: (13) link → sweep resolves to A → `IReverseIntakeLink` under a real edit lease → sweep reopens (Resolved → Open history row) → relink B → sweep re-resolves to B; (14) following sweep `(0,0,0,0)` and stays so; (15) OpenTriage handler 302, receipt Version unchanged, item Resolved to the Triage, every history op key distinct, two consecutive all-zero sweeps; (16) a completed no-change recheck is absent from `ListResolutionsToRecheckAsync(50)`; (17) with page size 1 the next stale row becomes the head; (18) predicate exercised against the real query.

Architecture/contract: (19) one `ReconcileUnidentifiedDestinations` owner, one registration, no destination inference in Web/Worker; (20) automation identity exists once (`AutomationActorId`) and is read by the EF store; (21) Worker timer unchanged, log carries `{Candidates} {Resolved} {Corrected} {Failures}`; (22) `/Received/{id}` advisory catch excludes `UnidentifiedOperationConflictException`; (23) `ReconciledAssociationVersion` lives on the entity only.

## 5. Conflict between PR 639 and the PR-069 correction

PR-069 worktree `../pegasus-worktrees/pr-069-unidentified-link-reversal`, HEAD `82651f365` (commits `b95877aa9`, `bd60fb638`, `4040710eb`, merge `82651f365`), 177 commits behind D, dirty with +576 uncommitted test lines (left untouched). Same file set as PR 639 minus the census file plus `20260902030930_UnidentifiedResolutionRecheckWatermark`.

Conflicts and resolution for the port:
1. Eligibility guard — PR-069 keeps D's top-of-method guard verbatim (worktree :181–185) so a non-image NeedsSorting receipt returns false before any lookup; INTK-048's live case is not fixed. Resolve for PR 639 (re-gate `CurrentCaseId is null && IsUnidentifiedEligible` inside the Open branch).
2. Manual-link destination branch — PR-069 has no trailing `CurrentCaseId → InstructionCase` fallback. Resolve for PR 639.
3. Case reference — PR 639 `CurrentCaseReference` vs PR-069 `AcceptedCaseReference ?? ManualLinkedCaseReference`. Resolve for PR 639.
4. Candidates accounting — PR-069 `candidates++` per recheck row. Resolve for PR-069; PR 639's integration assertions change from `(0,0,1,0)` to `(1,0,1,0)` on a correcting sweep (second sweep `(0,0,0,0)` under both).
5. Ownership predicates — PR-069 `IsOwnResolution`/`Records` with `StringComparison.Ordinal`. Resolve for PR-069.
6. Reopen replay — PR 639 returns the row as it stands; PR-069 returns the state the reopen produced (`Version = ExpectedVersion + 1`). Resolve for PR-069 (PR 639's form is a latent op-key collision).
7. Migration id/census — neither; A regenerates on D.
8. Reopen reason text — PR 639 two variants ("withdrawn"/"changed"), PR-069 one; either is correct if stable per transition. Prefer PR-069's single string unless the deterministic regeneration is asserted.
`AutomationActorId` (PR 639) vs `AutomationSubjectId` (PR-069): same constant; pick one.

Both directions are required by the plan rule; the branches are complements. Everything else (item-keyed `OperationKey`, reopen transition, watermark, `ExecuteUpdateAsync` without token, Web conflict exclusion, `{Corrected}` log) is identical in substance and retained once.

## 6. Field / mapping inventory for Stream A (handoff C-F01)

| Item | Value |
| --- | --- |
| Entity | `Pegasus.Infrastructure.Persistence.UnidentifiedItemEntity`, `src/Pegasus.Infrastructure/Persistence/UnidentifiedEntities.cs`, after `ResolutionTargetReference` (D:24) |
| Table | `dbo.UnidentifiedItems` |
| Field | `ReconciledAssociationVersion` (identical in PR 639 and PR-069) |
| CLR / column | `long?` / `bigint NULL` |
| Semantics | NULL = resolved, never yet rechecked (every pre-existing row); no default, no backfill |
| Index | none in either branch (see open risk) |
| EF configuration | none; convention-mapped |
| Domain exposure | none (never on the `UnidentifiedItem` record) |
| Snapshot | `b.Property<long?>("ReconciledAssociationVersion").HasColumnType("bigint");` in the `UnidentifiedItemEntity` builder |
| Migration name | `UnidentifiedResolutionRecheckWatermark`; discard both timestamps (`20260829222702`, `20260902030930`); generate after `20260905010654_CaseSignOffEngineer` from a D snapshot |
| Down | `DropColumn` |
| Runtime grant | no new GRANT; retain the SQL-Server-only assertion that `pegasus_worker_runtime_role` holds object-level (`class = 1`, `minor_id = 0`) UPDATE on `dbo.UnidentifiedItems` in state G/W, else `THROW 51000` |
| Census obligation | append the regenerated migration to `CommittedMigrationCreatesTheSqlServerSchema` in `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` (ends D:124) |
| Writer / reader | `EfUnidentifiedStore.MarkResolutionRecheckedAsync` (ExecuteUpdateAsync, no token); nulled by `ReopenAsync`; read by `ListResolutionsToRecheckAsync` |

Open risk for A: the recheck query joins `UnidentifiedItems.OriginId` to `IntakeManualAssociations.IntakeReceiptId` filtered on State/ResolvedByActorKind/ResolvedByActorSubjectId/OriginKind and the watermark inequality, ordered by `ResolvedAtUtc, Sequence`, Take 50, on a 10-second timer; neither branch adds a supporting index. Whether existing indexes cover it must be answered against D's `PegasusDbContext.cs`/snapshot before C-F01 closes.

Determinability: live head equals recorded tip; review threads not enumerable with this token; merge base obtained read-only from the PR-069 worktree (`pegasus-guard` rule 8 blocks `git merge-base` in the primary checkout) and cross-checked with `rev-list --boundary`; the Designer file was not analysed line by line; PR-069's uncommitted test work is noted as in progress, not as retained behavior.
