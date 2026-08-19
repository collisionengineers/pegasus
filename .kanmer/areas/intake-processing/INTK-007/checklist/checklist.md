# Checklist — INTK-007

- [x] Run kanmer-docs and update protected operator truth with the confirmed Unidentified requirement.
- [x] Update PRD, FRD-01/02/03/06/08/09/10/12, design, capabilities, and index as applicable.
- [x] Classify every normative old Needs sorting use into Unidentified or a preserved distinct workflow.
- [x] Link governing docs, clear `docs_todo`, and stop if any required behavior remains contradictory.
- [x] Add the one Core Unidentified reason taxonomy with six canonical codes.
- [x] Add Open/Resolved state, single/group origin, summary/detail/history, and command/query contracts.
- [x] Add exact `U<n>` formatter/parser with positive invariant unpadded sequence.
- [x] Add authorized/versioned/idempotent register and resolve use cases.
- [x] Add EF item/sequence/history entities with exact-one-origin and all uniqueness/state constraints.
- [x] Implement transactional atomic allocation without `MAX + 1`.
- [x] Implement replay-safe register, resolve, list, detail, exact search, origin lookup, and history.
- [x] Add migration, deterministic legacy backfill, canonical reason mapping, snapshot, and required runtime grants.
- [ ] Test migration from clean and representative legacy databases.
- [x] Route ProcessIntake qualifying terminal outcomes through the one register use case.
- [x] Update mail route/classification destination without duplicating reason taxonomy.
- [ ] Preserve Triage, Blocked intake, incomplete Audit, Image Intake, and INTK-006 Image-Only semantics.
- [x] Ensure retryable work does not allocate U and terminal technical failure does.
- [ ] Update receipt, retained-mail, dashboard, Operations, and search projections.
- [x] Add exact U-reference search and reject U-reference as Case/Audit/Image Intake identity.
- [x] Build Unidentified queue and detail pages with origins, filenames, reason, state, history, and next action.
- [x] Build authorized resolution with antiforgery, expected version, operation key, required reason, and supported target.
- [ ] Update navigation, dashboard, Operations, Upload status, Intake/Mail detail, and status chips.
- [x] Update `OperatorLabels.cs` as the single presentation mapping.
- [x] Update MCP lookup/list/resolution schemas and enforce actor/version/replay rules.
- [ ] Test all six reason mappings.
- [ ] Test one U item for a grouped submission and one per ungrouped origin.
- [ ] Test concurrent allocation, retry/replay, conflict, resolution, and permanent non-reuse.
- [ ] Test open/resolved search, counts, queue removal, and permanent history.
- [ ] Test all preserved distinct workflows do not accidentally create Unidentified items.
- [x] Run the final stale-term search and document every permitted historical/compatibility residual.
- [x] Run `dotnet restore`.
- [x] Run `dotnet build --configuration Release`.
- [x] Run focused Core, persistence, migration, Web, MCP, and browser tests.
- [ ] Run full `dotnet test`.
- [x] Perform and record the dated four-lens simplification pass.
- [x] Update checklist and post-implementation report with actual evidence.

## Progress — 2026-08-19

- Prepared and took `INTK-007` on branch `intk-007-unidentified-intake` in `.worktrees/intk-007`.
- Reconciled protected operator notes, PRD/FRDs, design authority, capability inventory, current architecture, and runbook; linked governing docs and cleared `docs_todo`.
- Added Core Unidentified contract: six reason codes, Open/Resolved state, receipt/group origin, canonical U-reference parser/formatter, versioned/idempotent register/resolve ports and use cases.
- Added EF entities/store, serializable sequence allocation, unique origin/reference/operation constraints, history, migration snapshot, deterministic legacy backfill, and sequence seed.
- Routed terminal ProcessIntake outcomes through the registration use case while leaving image-only material for Image Intake processing.
- Added Web queue/detail/resolution pages, navigation, dashboard metric, operator labels/status chip, and MCP list/get/resolve tools.
- Verification so far: `dotnet restore`; Core Release build; Infrastructure Release build; Web Release build; IntegrationTests Release build; full Core test suite (592 passed); focused Unidentified test suite (12 passed); Architecture suite (96 passed); HealthEndpoint focused suite (3 passed).
- Remaining work is explicitly unchecked below: full stale-term/semantic audit, grouped-submission persistence integration with INTK-005, mail/retained-mail/Operations projection completion, migration/runtime-grant tests, and final full-suite summary.

## Progress — 2026-08-19 (takeover, claude-code)

Took over by operator decision. Merged `origin/dev` (42 commits, one conflict in `docs/capabilities.md`, resolved). Fixed the three blockers and all 14 remaining reviewer comments (dispositions in `plan.md`'s dated "Review fixes" section). Ran the final stale-term search (`git grep -rn "Needs sorting\|NeedsSorting\|needs_sorting" -- src tests docs CLAUDE.md AGENTS.md`, 176 hits at session start, 169 now) and recorded the full classification in `post-implementation-report.md`. Renamed `MailOperationalDestination.NeedsSorting` to `Unidentified` (item 6 hand-off; TICK-045 had not merged into `origin/dev` and its pushed state doesn't wire the enum into any caller yet — see plan.md and scratch-takeover.md). Found and fixed a real regression the branch's own commit introduced: three integration tests still asserted the literal rendered text "Needs sorting" after `Intake/Details.cshtml.cs`'s `DecisionLabel` was changed to show "Unidentified".

Corrected the runtime-role grants twice: first pass granted an identical matrix to both roles; the coordinator asked for per-object least privilege, so re-derived every permission from the actual caller (see plan.md's "Grant correction" note) — Web lost INSERT on `UnidentifiedItems` and all of `UnidentifiedSequences` (nothing in `Pegasus.Web` calls `IRegisterUnidentified`); Worker gained UPDATE on `UnidentifiedItems` (my own reconciliation feature resolves stale items from the Worker side). Also added the `20260819115323_UnidentifiedWork` permission census to `scripts/Invoke-AzureDatabaseBootstrap.ps1` per a mid-session coordinator addition (`Test-AzureDeploymentPlan.ps1 -Mode Local`, landing via PR #426), and kept it in exact sync with the migration's own GRANT/DENY statements through the correction.

Verification this session: `dotnet build ./Pegasus.slnx -c Release` green throughout; local copies of the not-yet-merged `Test-MigrationGrants.ps1` and `Test-AzureDeploymentPlan.ps1 -Mode Local` both pass for this ticket's own migration (the only remaining `-Mode Local` failure names `20260819104953_MailClassificationCorrectionHistory.cs`, confirmed not this ticket's — tracked by PR #426); full `Pegasus.Core.Tests` suite green (655 passed, up from 592, including 3 new tests: the two retryable-failure tests and the ambiguous-case-match reason test); the task's specified `Pegasus.IntegrationTests` filter (`Unidentified|IntakePersistenceIntegrationTests|MailWorkspaceWebTests`) green (23 passed, including a real migration applied to LocalDB and the fixed rendered-text assertions). A full `dotnet test` run across all projects is in progress; its result will be recorded in post-implementation-report.md before this ticket leaves review.

Still explicitly unchecked and honestly outstanding: full clean/legacy-fixture migration test, grouped-submission/INTK-005 integration (blocked on that ticket merging), retained-mail/Operations/search projection completion beyond the dashboard metric already wired, all-six-reason-mapping test coverage (one new case added; five untested), concurrency/replay/non-reuse integration tests, and the full-suite summary once the background run completes.
