# B04 phase 2b-i — Glass's export parser and session store

Integrated into `task/pegasus-v1-casework` as the squash of helper branch `b-work/glass1` (5f1cc4f4b, b337168be, 4722b8786, 97904f16b, 1bcaac251, 3a245e280; base `d736bae60`). Opus implementer under claude-fable-b orchestration, 2026-09-07. Four new files, no existing file modified.

## Delivered

- `src/Pegasus.Infrastructure/Glass/GlassEstimateXmlParser.cs` — `IEstimateDocumentParser` (`Route = Glasses`; `CanParse` on `.xml`/XML media types, `Parse` proves the `<Estimation>` root) and `Read(...)` → `GlassEstimateExport(ParsedEstimate, GlassEstimateIdentity, GlassEstimateAttachment?)` surfacing RegPlt/Mileage/MilUnit/TypeNo/VIN and the decoded calculation-sheet PDF. Time semantics verified against the reference exports: `Time` is decimal hours at `TimeUnit 60` (labour 488.00 = 6.10 × 80; 1,576.00 = 19.70 × 80), any other unit refused; chargeable hours = `Time − OverlapTime`, zero for `Part_InclusiveSparePart`. `PosType` owns the parts/paint split (parts: unit amount + panel hours; paint: row materials + paint hours), `RepairKind` → operation via `EstimateOperations.ToLineType`. Printed totals (`ExclVatStatisticResults`/`Result`) as evidence only. Hardening: `DtdProcessing.Prohibit`, no resolver, entity/document character caps, 16 MB document / 8 MB attachment caps, PDF prefix + `%%EOF`, BOM tolerance, unknown kinds/unreadable numbers reject whole.
- `src/Pegasus.Infrastructure/Persistence/EfGlassRepairEstimateSessionStore.cs` — `IGlassRepairEstimateSessionStore` over Foundation's `GlassRepairEstimateSessionEntity` (untouched): `NormalizeAccountKey` (trim → lower-invariant → NFKC → salted SHA-256 hex; salt is a versioned label, password never a parameter); `ActiveAccountKey` = key while live (Prepared/Launching/Active) else null, uniqueness violation translated to `GlassRepairEstimateSessionConflictException(ActiveAccount)`; operation-key replay; callback identical-replay vs conflict; `CallbackConsumedAtUtc` stamped by the write leaving the live set; protected state opaque; serializable transactions; `Version` + `ConcurrencyToken`; `UpdatedAtUtc` from `TimeProvider`; `LastError` on Failed/Unknown.
- Tests: `tests/Pegasus.IntegrationTests/GlassEstimateXmlParserTests.cs` (48; synthetic fixtures with AB12CDE only) and `GlassRepairEstimatePersistenceTests.cs` (20; LocalDB).

## Verification (Windows, PowerShell 7, Release)

| Where | Check | Result |
| --- | --- | --- |
| helper | build; full Core; Architecture; parser+session+existing parser suites | 0/0; 1489; 100; 104 |
| squashed tree | build; full Core; Architecture; same integration filter | 0/0; 1489/1489; 100/100; 104/104 |

Reference exports under `pegasus_pack/glasses-integration/` were run through `Read` locally as read-only evidence; nothing copied into the repo; `glasses.ps1` never opened; spike code not lifted.

## Handoffs and gaps

- DI lines for A: `AddSingleton<IEstimateDocumentParser, GlassEstimateXmlParser>()` before the Audatex line; `AddScoped<IGlassRepairEstimateSessionStore, EfGlassRepairEstimateSessionStore>()`; `ProductionCompositionTests` parser count 2 → 3 (A-owned test).
- Shared contract gaps (A-owned `GlassRepairEstimates.cs`): no carrier for `ResultArtifactsJson` on `GlassRepairEstimateSessionMaterial` (store does not write the column yet); no member for `CallbackConsumedAtUtc`; `NormalizedExternalAccountKey` means raw account on create and stored key on read. `EstimateSourceTotals` (B) lacks a labour-money member (Glass's `TotalAmountLabourCosts` not retained).
- Simplification pass (helper, `3a245e280`): applied; rejected hoisting work-unit bounds/field caps into Core and sharing the JSON parser's rejection helper (would edit shared Core or another parser; constants referenced, not copied).

## Remaining Glass's

2b-ii gateway (`GlassRepairEstimateGateway`: scripted HTTP mechanics for the 17 request paths, launch/resume/complete policy over the store, custody retain of XML/PDF through A's `ICaseArtifactCustody`, canonical import via `IImportRawEstimate`); 2b-iii web (Case page launch/resume handlers, `Integrations/Glass/Callback` page, `Administration/Glass` page and tests).
