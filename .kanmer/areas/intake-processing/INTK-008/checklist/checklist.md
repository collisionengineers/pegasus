# Checklist — INTK-008

- [x] Re-read files.md and record implementation boundary in scratch.
- [x] Add Core Image-initiated lifecycle states, records, commands, and ports.
- [x] Add transition policy, actor/reason validation, terminal/replay/conflict rules.
- [x] Add persistence projection and append-only lifecycle events.
- [x] Add DbContext mapping and additive migration with AwaitingInstruction backfill.
- [x] Implement transactional lifecycle transitions and query projections.
- [x] Invoke merge projection from successful reverse formal-Case pairing.
- [ ] Add VRM-reference custody target through existing Box/local adapter boundary. — **Reversed on takeover.** No caller existed anywhere in the repo; wiring one with real integration coverage is a feature-sized addition, not a bug fix, so the seam (interface, both adapters, DI registration) was removed instead of shipped dark. `git diff origin/dev -- src/Pegasus.Core/Custody src/Pegasus.Infrastructure/Custody src/Pegasus.Infrastructure/DependencyInjection.cs` is empty. Matching claims removed from ADR-0029, FRD-05, design/README.md.
- [x] Update ImageIntake list/search labels and lifecycle filters.
- [x] Update details with state, filenames/group evidence, merge history, and staff-close form. (Custody state display dropped with the custody-seam reversal above; not a gap, there is nothing to display.)
- [x] Update formal Case search/details with Image-initiated reference and merge history.
- [x] Add Core transition/replay/pairing tests.
- [x] Add SQL/web/authorization/search/merge/closure tests.
- [ ] Add architecture/composition custody test and prove no formal Case row. — No dedicated custody architecture test (custody seam removed, nothing to test). "No formal Case row" is covered: `ImageIntakePersistenceTests.CloseValidatesBeforePersistingAndReplayRejectsAMismatchedCommand` asserts `SELECT COUNT(*) FROM Cases` is 0 after registering an unmatched Image intake.
- [x] Amend PRD, FRD-01/02/05/06/12, design, capabilities, index, CONTEXT.
- [x] Add ADR-0029 and supersede ADR-0013 without editing its accepted body.
- [x] Run simplification pass and record dispositions. (See dated section below.)
- [x] Run restore, Release build, focused tests, integration tests, architecture tests, and full test.
- [x] Write post-implementation-report with governing-doc traceability and verification commands. (Corrected via a dated addendum rather than rewritten in place — see the report's "Takeover correction" section.)
- [ ] Push branch, open PR targeting dev with Kanmer: INTK-008, record PR, and move to Review. — PR #423 already open and already in Review; this takeover pushed further commits to it, did not move the stage, and must not merge (operator instruction).

## Implementation progress — 2026-08-19 (original agent, superseded in part)

- [x] Re-read files.md and kept the implementation on the existing ImageIntake/Formal Case seams.
- [x] Added Core Image-initiated states, merge/close commands, history records, and transition validation.
- [x] Added SQL lifecycle projection/events, migration 20260819112914_ImageInitiatedLifecycle, replay/CAS transition, and formal Case merge history.
- [x] Wired reverse accepted-Case pairing to record the Image-initiated merge projection.
- [x] Updated Image-initiated list/detail labels, state/history presentation, search wording, and reasoned staff closure.
- [x] Reconciled PRD, FRD-01/02/05/06/12, design, capabilities, index, CONTEXT, operator notes, and ADR index; added ADR-0029 and superseded ADR-0013 frontmatter.
- [x] Core lifecycle/pairing tests pass: 40 tests.
- ~~[ ] VRM-keyed Box adapter invocation and custody state presentation still need final implementation/verification before PR.~~ — superseded: the custody seam was removed, not finished (see above).
- ~~[x] Added the distinct IImageIntakeCustody target and local/Box adapters...~~ — **superseded, this claim was false**: no caller ever existed for it (confirmed by repo-wide search before removal). Left struck through rather than deleted so the record of what was claimed, and why it was wrong, survives.

## Takeover — 2026-08-19 (claude-code, DELIV-012, operator decision)

Took over PR #423 mid-review. Merged `origin/dev` (resolved one conflict in
`IntakePersistenceIntegrationTests.cs` — kept both dev's newer
`VersionedRepairSpecifications` and this branch's `ImageInitiatedLifecycle`
migration entries, in timestamp order). Fixed all five numbered blockers plus
items 6–12 from the takeover brief, addressed the operator's two-branch
ruling on `docs/operator-notes.md`, and closed all 13 outstanding Codex PR
review comments (see Simplification pass section below for the disposition
table). Added the release-route census entry the coordinator flagged
mid-task (`scripts/Invoke-AzureDatabaseBootstrap.ps1` for
`ImageIntakeLifecycleEvents`).

## Simplification pass — 2026-08-19 (takeover)

Reuse/duplication/efficiency/altitude review over the branch's own diff
(`git diff origin/dev...HEAD`), done by hand plus an independent
`code-simplifier` agent pass restricted to `src/`+`tests/` (docs excluded).
Findings and dispositions:

1. **Duplication removed — lifecycle-merge triggering.** Before this
   takeover, only the reverse pairing path (`PairAcceptedCaseAsync`)
   attempted the merge transition, inline. The forward automatic path
   (`ImageIntakeAutomation.TryAssociateAsync`) and the manual staff path
   (`LinkIntake.ExecuteAsync`) never did. Rather than add two more inline
   copies, added one method (`ImageIntakeCasePairing.SyncMergeAfterLinkAsync`)
   that all three call — "one owner for the concept" per the repo's
   simplicity rail, not three.
2. **Duplication removed — lifecycle state labels.** `Index.cshtml.cs`'s
   `OutcomeLabel` had its own literal switch over `ImageInitiatedCaseState`
   duplicating what belongs in `OperatorLabels`. Reduced to compose from the
   one `OperatorLabels.ImageIntakeLifecycleState` mapping plus a small
   dash-continuation lowercase helper, so the state→words table exists in
   exactly one place.
3. **Reuse — request-fingerprint replay pattern.** The new
   `RequestFingerprint` column/comparison on `ImageIntakeLifecycleEvents`
   mirrors the existing pattern on `ImageIntakes.RequestFingerprint`
   (`EnsureRegisterReplay`) and on `IntakeMutationHistory` in
   `EfIntakeMutationStore.cs` exactly — the established convention, not a
   new one.
4. **Reuse — case reference resolution.** Instead of asking every merge
   caller to supply and validate a `CaseReference` string (which the manual
   `LinkIntake` path has no cheap way to obtain), `MergeImageInitiatedCaseRequest`
   dropped the field; `EfImageIntakeStore.TransitionAsync` resolves it once,
   inside the transaction, from `context.Cases` — fewer inputs to validate,
   no possibility of a caller-supplied stale/mistyped reference.
5. **Efficiency — accepted, not fixed.** `PairAcceptedCaseAsync` now calls
   `ListAsync(associated: null, ...)` (all Image intakes) and filters to
   `AwaitingInstruction` in memory, rather than a targeted SQL filter. At
   this product's stated scale (OPS-20: ~8 concurrent staff, ~2,000 new
   cases/month) the Awaiting set is small; a dedicated store-level state
   filter would be premature optimisation for a table with no expected
   volume problem. Revisit if `ImageIntakeQueries.ListAsync` grows a
   state-filter overload for another reason.
6. **Altitude — accepted.** The lifecycle Designer.cs/ModelSnapshot.cs
   updates for the new `RequestFingerprint` column were hand-edited (matched
   to the existing generated shape) rather than regenerated via `dotnet ef
   migrations remove`/`add`, to avoid the risk of an unrelated EF-tooling
   diff across the whole migration history under the sandbox's tooling. Risk
   is judged acceptable because the build and every migration-touching
   integration test pass, exercising the generated snapshot for real.
7. code-simplifier agent pass: findings recorded here once its report lands;
   see its notification for exact file:line detail if it proposed further
   changes beyond what is listed above.

### Codex PR review comment dispositions (13/13 addressed)

| # | File | Disposition |
| --- | --- | --- |
| 1 | Migration backfill (`...ImageInitiatedLifecycle.cs`) | Fixed — backfill SQL added for pre-existing associated rows. |
| 2 | `ImageIntakeCasePairing.cs` (atomic merge) | Fixed via `SyncMergeAfterLinkAsync` + deterministic replay-safe retry (explicitly retryable, not transactional — the two writes are in separate DbContexts/transactions by existing store design). |
| 3 | `ImageIntakeCasePairing.cs` (manual link wiring) | Fixed — `LinkIntake.ExecuteAsync` now calls `SyncMergeAfterLinkAsync`. |
| 4 | `CustodyContracts.cs` (dead custody seam) | Fixed by removal (chose remove over wire — see checklist item above and the plan's original step 7, now reversed). |
| 5 | `Index.cshtml.cs` (exact-reference search) | Fixed — passes through real state/closure reason. |
| 6 | `ImageIntakeContracts.cs` (queue filter) | Fixed at its real bug site, `ImageIntakeCasePairing.PairAcceptedCaseAsync` (state-based filter; the `associated` parameter's broader meaning elsewhere in the port was left alone as a separate, legitimate concept). |
| 7 | `EfImageIntakeStore.cs` (validators bypassed) | Fixed — `ValidateMerge`/`ValidateClose` called before persisting. |
| 8 | `EfImageIntakeStore.cs` (replay mismatch) | Fixed — `RequestFingerprint` comparison added. |
| 9 | `Details.cshtml.cs` (stale close 500) | Fixed — `DbUpdateConcurrencyException` caught as a normal conflict. |
| 10 | `Details.cshtml` (raw enum/snake_case) | Fixed via `OperatorLabels`. |
| 11 | `capabilities.md` (normative leakage + broken table) | Fixed — narrative moved to owning FRDs, table made contiguous again. |
| 12 | `adr/README.md` (superseded ADR still listed current) | Fixed — ADR-0013 moved to "Superseded and relocated". |
| 13 | `CONTEXT.md` (normative leakage + broken table) | Fixed — narrative removed (already covered in FRDs), table made contiguous again. |

### QdosAllocationRecoveryTests — CASE-005 (per coordinator)

`DistinctParallelRetriesResolveToOneCaseAggregate` is filed as CASE-005:
confirmed pre-existing and intermittent on clean `dev` (coordinator found it
failing at `4f67a83e` before any release-12 branch existed, and on two
unrelated PRs; two symptoms, a SQL deadlock and the `Pending` vs `Succeeded`
assertion). Not re-investigated further per coordinator instruction. Evidence
this branch's optional `IImageIntakeStore` parameter on `ImageIntakeCasePairing`
does not make it worse: full `QdosAllocationRecoveryTests` class — 15/15
passed on this branch; the specific test run standalone twice more — 2/2
passed; the full `Pegasus.IntegrationTests` suite (576 tests, 562 passed, 14
skipped, 0 failed) also exercised it as part of the whole run.

### Release-route census verification method (for the reviewer)

`scripts/Invoke-AzureDatabaseBootstrap.ps1` gained the
`ImageIntakeLifecycleEvents` census entries this migration's GRANT/DENY
requires. `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` on the
real worktree throws on the pre-existing, unrelated
`20260819104953_MailClassificationCorrectionHistory` gap (lands via PR #426,
not this ticket) before it ever reaches this branch's own migration
assertion. To verify this branch's entry anyway: copied `scripts/`, `infra/`,
`azure.yaml`, and `src/.../Persistence/Migrations/*.cs` to a disposable
scratch directory outside the repo, added a throwaway diagnostic-only stub
comment naming `20260819104953_MailClassificationCorrectionHistory` (not
committed anywhere, no tracked file touched), reran `-Mode Local` there —
"Azure deployment plan validation passed" end to end — then deleted the
scratch directory. `pwsh ./scripts/Test-MigrationGrants.ps1` could not be run
at all: it does not exist on this worktree or on `origin/dev`, only on
`origin/task/deliv-012-grant-and-docs-fixes` (an unmerged, unrelated task
branch) — not imported, per "never touch work that is not yours"; flagged to
the coordinator instead. Coordinator confirmed both calls were correct and
that `Test-MigrationGrants.ps1` lands via that branch's own PR #426, which
will merge before this one; recheck `-Mode Local` after merging `dev` post
`#426` — it should then name nothing, and if it names this branch's own
migration at that point, that is a real regression to fix.

### code-simplifier agent pass — result (2026-08-19)

Item 7 above referenced this pending; it has landed. 11 findings applied, all
behaviour-preserving, all folded into commit `b264a36a` ("INTK-008 apply the
code-simplifier pass over this branch's diff") — see that commit message for
the itemised list, or the plan document's dated Simplification pass section
for the same list. Highlights: `ImageIntakeDetail` stopped re-declaring six
fields `ImageIntakeRecord` already carried; a duplicate private
`ListHistoryAsync` was deleted; the terminal-state rule moved from the EF
store to `ImageIntakeLifecycleRules.RequireTransitionable` in Core;
`ImageIntakeCasePairing` now takes one required `IImageIntakeStore` instead
of a second optional-with-silent-disable one (the exact smell CLAUDE.md
names); and `OperatorLabels.ImageIntakeLifecycleStateContinuation` fixed a
real bug the takeover's own `Details.cshtml` edit had introduced (a plain
`.ToLowerInvariant()` silently mangling "Instruction-initiated Case"'s
capitalisation).

Not applied (design changes, not cleanup): a SQL-level state filter for the
Awaiting-instruction scan (already dispositioned above as accepted at
current scale), projecting `SyncMergeAfterLinkAsync`'s existence check
instead of a full detail load, and unifying `DbUpdateConcurrencyException`
vs `IntakeVersionConflictException` across stores (already genuinely split
by convention).

Re-verified after applying: `dotnet build Pegasus.slnx -c Release` — 0
warnings/errors. `dotnet test tests/Pegasus.Core.Tests -c Release` —
644/644. `dotnet test tests/Pegasus.IntegrationTests -c Release --filter
"FullyQualifiedName~ImageIntake|FullyQualifiedName~IntakePersistenceIntegrationTests|FullyQualifiedName~QdosAllocationRecoveryTests"`
— 33/33. `dotnet test tests/Pegasus.ArchitectureTests -c Release` — 97/97.
Pushed as `b264a36a`.
