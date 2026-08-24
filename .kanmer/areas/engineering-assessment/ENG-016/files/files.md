# Files — ENG-016

*Re-surveyed after the operator's 2026-08-24 route and eligibility clarification. This replaces the earlier permissive-export file map.*

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Eva/CaseEvaMapping.cs` | Keep one strict EVA export mapping. Remove `MapForOperatorExport`, empty-field/default-date behavior, `EvaOperatorExport` and related permissive vocabulary. Retain/reuse the accepted-evidence and provenance checks currently owned by `MapForProduction`; name the surviving method for the one Export act if that improves clarity without duplicating policy. |
| `src/Pegasus.Core/Eva/EvaBundleSchema.cs` | Keep the shared package writer and eligible-image policy. Retain the existing Core lifecycle/custody eligibility rule instead of deleting it. Add an operation key to `ExportCaseBundleRequest` and remove `UnrecordedFields` from the result because an incomplete case no longer exports. |
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` | Make the surviving Export call the strict mapping and server-side Review/current-version/custody/Audit-custody/mapping/image policy. Build the archive only when all gates pass. In one database transaction, append one attributed action-history event per distinct successful Export operation and record the first-sent proxy only when absent. Exact operation-key replay must not duplicate either record or silently return a different package. |
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffEntities.cs` | Keep ENG-016's deletion of revision/operation/download entities and the obsolete proxy columns. The once-per-Case proxy remains. |
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffModelConfiguration.cs` | Keep deletion of the three dead table mappings and the proxy FK/index/columns; preserve both no-delivery/no-assignment constraints. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | Keep removal of the three dead `DbSet` properties; `ActionHistory` and `EvaFirstHandoffProxies` remain. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260824123336_DropEvaHandoffTables.cs` and generated Designer/snapshot | Retain the direct pre-cutover drop permitted by ADR-0030. Correct comments: the migration temporarily affects the Case workspace running on the old revision; production recovery is roll-forward, while `Down()` is only a scratch/disposable-schema check and does not preserve proxy data after new exports exist. Regenerate the Designer/snapshot after the merge if the model changed. |
| `scripts/Invoke-AzureDatabaseBootstrap.ps1` | Reconcile ENG-016's removed-table/grant expectations on top of current `dev`; preserve all newer migration census changes. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | Keep the visible Review-gated POST Export control, add a hidden operation key, and continue treating the UI state as a reflection of the Core rule rather than the enforcement. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | Supply a fresh Export operation key using the page's existing operation-key convention. |
| `src/Pegasus.Web/Pages/Cases/Documents/Export.cshtml.cs` | Accept/validate the operation key, return the archive only after strict Core success/history commit, remove incomplete-field continuation, and restore the `Content-Digest` header from the archive SHA-256. |
| `src/Pegasus.Core/Cases/CaseQueries.cs`, `src/Pegasus.Web/Pages/Cases/Eva/*`, `Vehicle.cshtml.cs`, `_CaseWorkflow.cshtml`, `AssessmentMcpTools.cs`, `DependencyInjection.cs` | Keep ENG-016's deletion of the duplicate hand-off projection, pages, handler, panel, MCP tools and registrations after resolving against current `dev`. |
| `docs/operator-notes.md` | Protected operator truth, explicitly authorised by the operator in this conversation: state the three routes and that today's manual Export is the send-to-Engineer act and fails closed until ready. |
| `docs/frd/frd-07-eva-and-external-engineering-handoff.md` | Replace the two-act strict/permissive model with one strict manual Export/handoff; specify its gates, proxy/history semantics and the future EVA API/direct-integration routes. |
| `docs/capabilities.md` | Reconcile CASE-21, CASE-30, EXT-03 and any affected API/replacement rows with one strict Export act; remove the claim that missing fields export empty. |
| `docs/current-architecture.md` | Describe the actual one-route implementation, strict server-side gate, first proxy, per-export history and removed tables. |
| `docs/design/README.md` | Preserve its already-correct strict Sent-to-Engineer framing; remove only stale revision/two-act wording if the final code no longer has it. |
| `docs/open-decisions.md` | Keep the current manual route as the default while EXT-04 waits for a working EVA API, and keep EVA replacement/direct estimating integrations separately gated. Edit only if the final wording still implies a permissive second export act. |
| `tests/Pegasus.Core.Tests/Qdos/QdosBoundaryContractTests.cs` | Reverse the branch test that deliberately proves an incomplete case exports; assert the exact missing evidence blocks. |
| `tests/Pegasus.Core.Tests/Qdos/CaseOperatorExportTests.cs` and `EvaHandoffPolicyTests.cs` | Pin all thirteen required fields, accepted provenance, Review/current version/custody/Audit custody/mapping/images, and no defaulted missing inspection date. |
| `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` | Prove every successful Export writes attributed structured `ActionHistory`, the first Export writes one proxy, later distinct exports write history but no second proxy, and exact operation replay duplicates neither. |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | Prove POST/antiforgery/operation-key wiring, disabled non-Review UI, server-side refusal even when directly posted, and the restored `Content-Digest` response header. |
| `tests/Pegasus.IntegrationTests/EvaHandoffPersistenceTests.cs` | Resolve the modify/delete conflict by first transferring any current-`dev` strict-gate/package assertions to surviving suites, then delete tests whose tables/use cases are intentionally removed. |
| `tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs`, `IntakePersistenceIntegrationTests.cs`, `ProductionCompositionTests.cs`, `ReadinessEndpointTests.cs`, architecture and Automation/browser suites | Reconcile current `dev` expectations with the deleted tables/routes/tools and the surviving strict staff Export caller. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/frd/frd-04-parties-accounts-and-access.md` | ACC-09 requires permanent history for every export, including actor, caller, time, policy/version, structured evidence and outcome. |
| `src/Pegasus.Infrastructure/Persistence/DocumentActionHistory.cs` | Existing helper for attributed succeeded events and exact operation replay; reuse it rather than create another history format. |
| `src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs` | Existing document-export transaction/replay/history convention. |
| `docs/adr/0030-non-additive-schema-changes-before-cutover.md` | Accepted authority for direct destructive migration, roll-forward recovery and the obligation to name the old-revision impact. |
| `.github/workflows/ci.yml` | The `changes` job owns downstream build fan-out and has a five-minute full-history checkout timeout; this explains the cancelled current runs. Do not change it in ENG-016 unless separately scoped. |
| `docs/engineering.md` and root `AGENTS.md` | Merge current `origin/dev` into the pushed task branch; do not rebase/force-push; preserve unrelated work and require independent review plus green CI. |
| `CASE-019` Kanmer ticket | Historical permissive-download decision that the operator has now superseded for the one Export/send act. |

## Ripple effects

- The dashboard's Sent-to-Engineer count continues to read the once-per-Case proxy; action history is additive evidence, not a replacement for that tile.
- Removing permissive export means suggested-only or empty fields, stale accepted evidence, non-Review state, incomplete custody and missing eligible images all block both package creation and proxy/history writes.
- The future EVA API route and later direct estimating integrations share the readiness concept but remain separately scoped capabilities; this PR adds no external adapter.
- The normal merge from `origin/dev` must discard stale stacked copies of already-merged ENG-014/ENG-015/DOCS-013 work and preserve current unrelated `dev` changes.
- The current local staged ignore/untrack change for `.codex/config.toml` and `.mcp.json` is not ENG-016 scope and must not be committed into PR #539.

## Out of scope

- No EVA API call, credentials, vendor contract or activation.
- No Audatex, Glass's or other estimating-system adapter and no EVA replacement implementation.
- No expand/contract compatibility path or production rollback support before cutover.
- No CI workflow timeout change unless the checkout-only cancellation repeats after the resolved branch is pushed and is handled by its owning CI work.
- No deployment, database mutation, Azure write or release in this planning task.
