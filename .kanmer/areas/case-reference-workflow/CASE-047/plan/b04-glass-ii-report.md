# B04 phase 2b — session store settlement (G20) and the credential page

Integrated into `task/pegasus-v1-casework` as `d5e9b7ea2` (squash of helper `b-work/glass3`: 7812a2185, 100afb1b7, 52d5f4c7a, 414622b64; base de792098c) and `0d0d5627c` (squash of `b-work/glass2`: 98c9b8bcb, 3ba5185f8; base 6a1d620ce), after G20 `6b441dea9` (merge 380f8002b), A's matcher commit 1f92131c7 (cherry-pick de792098c, A-authorised) and A's Glass DI handoff 9741f1963 (merge e98ff68a5). 2026-09-07.

## Session store (`EfGlassRepairEstimateSessionStore.cs`, tests `GlassRepairEstimatePersistenceTests.cs`)

- G20 adopted: Core's `GlassRepairEstimateSessionConflict` / `...ConflictException`; Infrastructure duplicates deleted.
- `ResultArtifactsJson`: written by Create and Save from the material, returned on read; the material is the row's whole mutable state (null writes null) — the semantics `LastError`/`EreId`/`ProviderVehicleId` already had.
- `CallbackConsumedAtUtc`: on the read session; stamped once by the first write out of the awaiting-callback states {Prepared, Launching, Active, Unknown}; never overwritten.
- Replay: `RequireSameLaunch` compares CaseId, UserId, CredentialGeneration and the canonical account key; protected state deliberately not compared; changed generation/account → `OperationKey` conflict.
- Occupancy: `AccountOccupyingStates` = Prepared, Launching, Active, Unknown, AwaitingImport, Importing; released by Completed, Failed, Expired, Cancelled. Import states hold the account because the contract has no "interactive session ended" statement.
- Concurrency: real deadlock (SQL 1205) reproduced with two different accounts — Serializable `RangeS-S` on the empty operation-key index range taken by the speculative replay read, converted to `RangeI-N` by the insert. Fix: insert first, unique index decides, replay resolved on rollback; no retry; isolation unchanged. Three consecutive clean repeat runs; no deadlock reports since.

## Credential page (`Pages/Administration/Glass/Index.cshtml(.cs)`, tests `GlassCredentialAdministrationWebTests.cs`)

`/Administration/Glass/{staffId:guid}`, Administrator policy, `AdministrationPageModel`; `OnGetAsync`, `OnPostSaveAsync(staffId, username, password, …)` → `ReplaceAsync(enabled: true)`, `OnPostClearAsync` → `ClearAsync`; password never a bound property, dropped from ModelState on read, never echoed/TempData/logged; typed catches; operation key re-minted on refusal; unknown staff → 404. Status as values (chip Enabled/Disabled/Not configured; account, username, generation, version, updated). Labels `CaseWorkspaceLabels.GlassCredential`. Eight web tests (compile standalone; run in the combined tree where A's `EfPerUserExternalCredentialStore` resolves). No disable operation by A's decision (Save + Clear is the A01 boundary). Catalogue registration for A posted (PR 672 comment 5563358612).

## Verification (Windows, PowerShell 7, Release, head 0d0d5627c)

| Check | Result |
| --- | --- |
| solution build | 0 / 0 |
| full Core | 1489 / 1489 |
| Architecture | 100 / 100 |
| `GlassRepairEstimatePersistenceTests\|GlassEstimateXmlParserTests\|ProductionCompositionTests.CasePageAndCanonicalImport…\|CaseWorkflowMigrationTests\|AssessmentPersistenceIntegrationTests` | 114 / 114 |
| `Test-UiCatalogue.ps1` | one unclassified source: the new Glass page (A registration pending) |

## Open for the gateway slice

Contract notes for A (non-blocking): no interactive-session-ended signal; `NormalizedExternalAccountKey` raw-in/hashed-out; `SaveAsync` returns `Task`; no explicit "carries the callback" marker. B: `EstimateSourceTotals` lacks a labour-money member.
