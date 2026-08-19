# Checklist — INTK-007

- [x] Run kanmer-docs and update protected operator truth with the confirmed Unidentified requirement.
- [ ] Update PRD, FRD-01/02/03/06/08/09/10/12, design, capabilities, and index as applicable.
- [ ] Classify every normative old Needs sorting use into Unidentified or a preserved distinct workflow.
- [x] Link governing docs, clear `docs_todo`, and stop if any required behavior remains contradictory.
- [x] Add the one Core Unidentified reason taxonomy with six canonical codes.
- [x] Add Open/Resolved state, single/group origin, summary/detail/history, and command/query contracts.
- [x] Add exact `U<n>` formatter/parser with positive invariant unpadded sequence.
- [x] Add authorized/versioned/idempotent register and resolve use cases.
- [x] Add EF item/sequence/history entities with exact-one-origin and all uniqueness/state constraints.
- [x] Implement transactional atomic allocation without `MAX + 1`.
- [x] Implement replay-safe register, resolve, list, detail, exact search, origin lookup, and history.
- [ ] Add migration, deterministic legacy backfill, canonical reason mapping, snapshot, and required runtime grants.
- [ ] Test migration from clean and representative legacy databases.
- [x] Route ProcessIntake qualifying terminal outcomes through the one register use case.
- [ ] Update mail route/classification destination without duplicating reason taxonomy.
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
- [ ] Run the final stale-term search and document every permitted historical/compatibility residual.
- [x] Run `dotnet restore`.
- [x] Run `dotnet build --configuration Release`.
- [x] Run focused Core, persistence, migration, Web, MCP, and browser tests.
- [ ] Run full `dotnet test`.
- [ ] Perform and record the dated four-lens simplification pass.
- [ ] Update checklist and post-implementation report with actual evidence.

## Progress — 2026-08-19

- Prepared and took `INTK-007` on branch `intk-007-unidentified-intake` in `.worktrees/intk-007`.
- Reconciled protected operator notes, PRD/FRDs, design authority, capability inventory, current architecture, and runbook; linked governing docs and cleared `docs_todo`.
- Added Core Unidentified contract: six reason codes, Open/Resolved state, receipt/group origin, canonical U-reference parser/formatter, versioned/idempotent register/resolve ports and use cases.
- Added EF entities/store, serializable sequence allocation, unique origin/reference/operation constraints, history, migration snapshot, deterministic legacy backfill, and sequence seed.
- Routed terminal ProcessIntake outcomes through the registration use case while leaving image-only material for Image Intake processing.
- Added Web queue/detail/resolution pages, navigation, dashboard metric, operator labels/status chip, and MCP list/get/resolve tools.
- Verification so far: `dotnet restore`; Core Release build; Infrastructure Release build; Web Release build; IntegrationTests Release build; full Core test suite (592 passed); focused Unidentified test suite (12 passed).
- Remaining work is explicitly unchecked below: full stale-term/semantic audit, grouped-submission persistence integration with INTK-005, mail/retained/Operations projections, migration/runtime-grant tests, full test run, and simplification pass.
