# Proof — ENG-026: Named estimates on a Case with per-estimate VAT and a Current estimate

## What was verified, and where

Verified by reading merged `dev` at `b92cb9a7` in the primary checkout
`C:/Users/PC/Documents/GitHub/pegasus`, read-only. ENG-026 landed as PR
**#595**, merge commit **`c4b89840`** (2026-08-28T18:52:31Z), branch
`task/eng-026-estimates`, five commits: `bcee2ae2` (implementation),
`d57bc60b` (tests), `a0daecd9` (simplification pass), `9c0d9181` and
`c97889f1` (CI fixes). `git merge-base --is-ancestor c4b89840 b92cb9a7`
returns 0, so everything below is present at the gate SHA.
`git diff --stat c4b89840^1 c4b89840` reports 27 files, 9,951 insertions
and 114 deletions, and every file named in the ticket's **Owns** list is
in it.

## Evidence

### Core/Assessment/Estimates.cs exists and owns estimate money

Tier: build/test.

`src/Pegasus.Core/Assessment/Estimates.cs` is new in `bcee2ae2`, 484
lines. `EstimateTotals.Compute` is at `Estimates.cs:92`:

```csharp
var parts = estimate.Lines.Sum(line => (line.Price ?? 0m) * (line.Quantity ?? 1));
var labourHours = estimate.Lines.Sum(line => line.WorkUnits ?? 0m);
var paintHours = estimate.Lines.Sum(line => line.PaintWorkUnits ?? 0m);
var labour = labourHours * (details.LabourRate ?? 0m);
var paint = paintHours * (details.PaintLabourRate ?? 0m) + (details.PaintMaterials ?? 0m);
var other = details.OtherCosts ?? 0m;
var subtotal = parts + labour + paint + other;
var vat = decimal.Round(subtotal * details.VatPercent / 100m, 2, MidpointRounding.AwayFromZero);
```

It is the only estimate-summing implementation on `dev`. `grep -rn
"EstimateTotals" src/` finds one definition and three consumers —
`Estimates.cs:269` (`EstimatePolicy.BasisFor`),
`Reports/AssessmentReportProjection.cs:89` (`CostsOf`) and
`Pegasus.Web/Mcp/AssessmentMcpTools.cs:528` (the MCP list result).
`grep -rn "Lines.Sum" src/` returns four hits: the three inside `Compute`
and one at `AssessmentReportProjection.cs:91`, which re-derives the same
labour-hours figure to fill the report's `LabourHours`/`HourlyRate` pair.

Also delivered as claimed: `EstimateOperations` (`Estimates.cs:37`) is the
single Replace/Repair/R&I/Paint/Other to `EstimateLineCodes` mapping;
`EstimatePolicy` (`:116`); the requests and the five use cases
`SaveEstimate`, `DuplicateEstimate`, `DiscardEstimate`,
`SetCurrentEstimate`, `ListCaseEstimates` (`:387`–`:484`).
`RepairSpecificationVersion` (`Assessment/RepairSpecifications.cs:47`)
gained `EstimateDetails Details`, `IsCurrent`, `AiJobId` and
`DiscardReason`; `CaseEstimateLineRecord` and `EstimateLineInput` in
`Assessment/AssessmentContracts.cs` gained `PaintWorkUnits` and
`Quantity`.

### D9 — the Current estimate's VAT overrides the report's built-in rule

Tier: build/test. This is the ticket's load-bearing claim and it holds.

The built-in rule lives at `Core/Reports/AssessmentReportRendering.cs:105`
and is conditional on there being no override:

```csharp
public decimal Vat => VatOverride ?? decimal.Round(
    (RepairerVatRegistered ? Subtotal : Parts + PaintMaterials) * 0.20m,
    2,
    MidpointRounding.AwayFromZero);
```

`AssessmentReportProjection.CostsOf` (`AssessmentReportProjection.cs:87`)
always fills that override from the estimate's own percentage:

```csharp
var totals = EstimateTotals.Compute(estimate);
return new(
    LabourHours: estimate.Lines.Sum(line => line.WorkUnits ?? 0m),
    HourlyRate: estimate.Details.LabourRate ?? 0m,
    Parts: totals.Parts,
    PaintMaterials: totals.Paint,
    SpecialistOther: totals.Other,
    RepairerVatRegistered: totals.VatPercent > 0,
    VatOverride: totals.Vat);            // AssessmentReportProjection.cs:97
```

and `Project` selects it whenever a Current estimate is present
(`AssessmentReportProjection.cs:159`):

```csharp
var costs = input.CurrentEstimate is { } estimate ? CostsOf(estimate) : input.Costs!;
var lines = input.CurrentEstimate?.Lines ?? assessment.EstimateLines;
```

So the built-in 20 % rule can only run for a caller that supplies `Costs`
without an estimate. The percentage itself is free per estimate:
`EstimateDetails.VatPercent` is a plain `decimal`, bounded 0–100 by
`EstimatePolicy.ValidateDetails` and by the database check constraint
`CK_CaseRepairSpecifications_VatPercent`.

The override is pinned by a test that uses a non-default rate:
`tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs`,
`TheCurrentEstimateSuppliesTheCostBlockAndTheListsWithItsOwnVat` builds an
estimate with `VatPercent: 5m` and asserts

```csharp
// 5 % from the estimate, not the built-in 20 % rule.
Assert.Equal(totals.Vat, costs.Vat);
Assert.Equal(decimal.Round(totals.Subtotal * 0.05m, 2), costs.Vat);
```

### Production caller — the report reads the Current estimate

Tier: build/test. The path is wired end to end on `dev`; it is not
deployed (see Scope).

Every hop, quoted from merged `dev`:

1. Rendered control:
   `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml:235` —
   `<form method="post" asp-page-handler="GenerateReportDraft" asp-route-id="@caseId">`
   and `:243`, the `PreviewReportDraft` link.
2. Handler: `Index.cshtml.cs:334` `OnPostGenerateReportDraftAsync` →
   `:352 result = await generateReportDraft.ExecuteAsync(id, actor, cancellationToken);`
   `:384 OnGetPreviewReportDraftAsync` calls the same use case at `:393`.
3. Use case: `Core/Reports/AssessmentReportProjection.cs:345`
   `GenerateCaseAssessmentReportDraft` → `:361 source.GetAsync(...)` →
   `:367 AssessmentReportProjection.Project(input)`.
4. Port implementation, registered in DI at
   `src/Pegasus.Infrastructure/DependencyInjection.cs:484`
   (`AddScoped<IAssessmentReportProjectionSource, EfAssessmentReportProjectionSource>()`),
   supplies the estimate —
   `Persistence/EfAssessmentReportProjectionSource.cs:110`:

   ```csharp
   Costs: null,
   CurrentEstimate: workspace.AcceptedSpecification);   // :111
   ```

5. `workspace.AcceptedSpecification` is the Current estimate, not merely
   the accepted one — `Persistence/EfAssessmentWorkspaceSource.cs:92`:
   `var acceptedEntity = specificationEntities.SingleOrDefault(item => item.IsCurrent);`

The page's own readiness condition uses the same inputs, at
`Index.cshtml.cs:319`:

```csharp
ReportDraftPreparation = AssessmentReportProjection.Prepare(
    Assessment,
    costs: null,
    currentEstimate: AcceptedSpecification);
```

Honest attribution: that call site was written by **ENG-025**, commit
`5d3b658c` ("fix(assessment): condition the report-draft controls on the
Current estimate"), not by ENG-026's own diff. It is present at
`b92cb9a7`. ENG-026 shipped the Infrastructure half of the caller
(`EfAssessmentReportProjectionSource`), which is production code with a
DI registration and a live consumer.

### Production caller — what makes an estimate Current

Tier: build/test.

`IsCurrent` is written in exactly two places in
`Persistence/EfRepairSpecificationStore.cs`: `:171` inside `AcceptAsync`
(`entity.IsCurrent = true;` after `Accept(...)`) and `:384` inside
`SetCurrentEstimateAsync`.

Only the first is reachable from production code:
`Pages/Cases/Assessment/Index.cshtml:406`
(`asp-page-handler="AcceptSpecification"`) → `Index.cshtml.cs:640`
`OnPostAcceptSpecificationAsync` → `:707
await repairSpecifications.AcceptAsync(...)`. The estimate reaching that
form comes from the import control at `Index.cshtml:480` →
`:477 OnPostImportEstimateAsync` → `:598 repairSpecifications.StartDraftAsync(...)`.

`ISetCurrentEstimate` — the "Use estimate" control — has **no production
consumer**: `grep -rn "ISetCurrentEstimate" src/ tests/` returns only
`Estimates.cs` and `DependencyInjection.cs:324`. The same is true of
`IDuplicateEstimate` (`:322`) and `IDiscardEstimate` (`:323`). Those are
tier-1 registrations only. See Outstanding.

### Migration 20260828112103_NamedEstimates and its grants

Tier: build/test.

The file is at
`src/Pegasus.Infrastructure/Persistence/Migrations/20260828112103_NamedEstimates.cs`.
The ticket text says `src/Pegasus.Infrastructure/Migrations/…`; the real
path is under `Persistence/`. Its `Up` contains 17 `AddColumn`
operations, one `Sql` backfill, two `CreateIndex`, six
`AddCheckConstraint`, and **no `CreateTable` and no `GRANT`** — verified
by `grep -n "CreateTable\|GRANT" …` returning nothing.

The columns land on two pre-existing tables that already carry
table-level runtime grants, which in SQL Server cover columns added
later:

- `20260819112640_VersionedRepairSpecifications.cs:134` —
  `GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseRepairSpecifications] TO [pegasus_web_runtime_role];`
- `20260803205759_SendToAiAssessmentToolset.cs:189` —
  `GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[CaseEstimateLines] TO [{WebRole}];`

So the post-implementation report's claim — no new table, therefore no
grant and no bootstrap-census entry — checks out.

The one-Current-per-case rule is enforced in the database, replacing the
old one-Accepted filter:

```csharp
migrationBuilder.CreateIndex(
    name: "IX_CaseRepairSpecifications_CaseId",
    table: "CaseRepairSpecifications",
    column: "CaseId",
    unique: true,
    filter: "[IsCurrent] = 1");
```

with `CK_CaseRepairSpecifications_Current` (`[IsCurrent] = 0 OR [State] =
'Accepted'`), `CK_CaseRepairSpecifications_VatPercent` (`BETWEEN 0 AND
100`), the widened state check (`+ 'Discarded'`), the widened source-route
check (`+ 'Json', 'AiDraft'`), and `CK_CaseEstimateLines_Quantity`.
Existing rows are backfilled in the same migration:

```sql
UPDATE [CaseRepairSpecifications]
SET [Name] = CONCAT('Estimate ', [Version]),
    [VatPercent] = 20,
    [IsCurrent] = CASE WHEN [State] = 'Accepted' THEN 1 ELSE 0 END;
```

The migration is registered in the applied-migrations census:
`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs:112`
lists `"20260828112103_NamedEstimates"` as the last entry, and the same
test asserts `Assert.Empty(await context.Database.GetPendingMigrationsAsync())`.

### MCP pegasus_estimate_save / pegasus_estimate_list

Tier: registration + build/test — **not** deployed.

Defined at `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs:163` and `:244`,
injected via `ISaveEstimate` (`:157`) and `IListCaseEstimates` (`:158`),
and the tool type is registered at
`src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs:120`
(`.WithTools<AssessmentMcpTools>()`). The endpoint is composition-gated:
`Program.cs:682` `if (automationMcpOptions is not null)` guards
`AddPegasusAutomationMcp`, and `Program.cs:1027` guards
`MapPegasusAutomationMcp`. `docs/operations.md:122` records that gate as
enabled in production since release 9 — but the newest deployed release
is 19 (2026-08-22, source `42125b34`, `docs/operations.md:808`), which
predates this merge, so these two tools are in no deployed image.

The AiDraft-only and job-citation rules are enforced in Core
(`EstimatePolicy.ValidateSave`, `Estimates.cs:161`; `ValidateCitedJob`,
`:198`) and exercised over real HTTP by
`tests/Pegasus.IntegrationTests/AutomationAssessmentIngressTests.cs:651`
`EstimateSaveRequiresTheHeldEstimateJobAndLandsAsAnUnconfirmedAiDraft`.
The tool inventory assertion in `AutomationMcpIngressTests.cs` was
extended to 43 tools by `9c0d9181`.

### JSON estimate parser

Tier: registration + build/test. **No production consumer.**

`src/Pegasus.Infrastructure/Assessment/JsonEstimateParser.cs:40`
implements `IEstimateDocumentParser` with `Route = Json`, and is
registered as a concrete singleton at `DependencyInjection.cs:320`. The
only implementation bound to the interface is still
`AudatexEstimatePdfParser` (`DependencyInjection.cs:317`), which is what
`Index.cshtml.cs:44 IEstimateDocumentParser estimateParser` resolves.
`grep -rn "JsonEstimateParser" src/ tests/` finds no consumer other than
`tests/Pegasus.IntegrationTests/JsonEstimateParserTests.cs:14`, which
news it up directly. The DI comment names the missing caller: "the import
dialog selects the parser by the chosen source route" — that dialog is
ENG-028's.

### Build and test

Tier: build/test. Cited from the canonical gate evidence for merged `dev`
at `b92cb9a7`; not re-run here.

```
dotnet restore ./Pegasus.slnx --locked-mode                 -> exit 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore
  -> Build succeeded. 0 Warning(s), 0 Error(s).
dotnet test ... --filter 'Category!=Corpus&Category!=Browser'
  -> Pegasus.ArchitectureTests   Passed:  100 / 100
  -> Pegasus.Core.Tests          Passed: 1133 / 1133
  -> Pegasus.IntegrationTests    Passed: 1022, Skipped: 2 / 1024
```

The two skips are pre-existing and unrelated to estimates
(`QdosMappingExtractionTests`, `CustodyOutboxIntegrationTests`). ENG-026's
own tests are inside those totals:
`tests/Pegasus.Core.Tests/Assessment/EstimateTests.cs` (13 tests,
including `TotalsFollowTheFrdFormulaWithVatRoundedToPence`,
`EveryLineTypeMapsToExactlyOneOperationAndBack` and
`MakingCurrentIsTheEngineersAcceptanceWithTheTotalsOwnersBasis`),
`AssessmentPersistenceIntegrationTests.cs:415`
`NamedEstimatesSaveDuplicateDiscardSetCurrentAndListWithOneCurrentPerCase`,
`JsonEstimateParserTests.cs` (four test methods, one an 11-case Theory),
and the two report-projection tests quoted above.

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| Totals have one owner | Proven (build/test) | `EstimateTotals.Compute` at `Estimates.cs:92` is the only estimate sum on `dev`; three consumers, no second implementation |
| Report costs come from the Current estimate | Proven (build/test) | `AssessmentReportProjection.cs:159` selects `CostsOf(estimate)`; `EfAssessmentReportProjectionSource.cs:111` supplies `CurrentEstimate: workspace.AcceptedSpecification`; `EfAssessmentWorkspaceSource.cs:92` resolves that by `IsCurrent` |
| D9 — free VAT % per estimate | Proven in the model; **not operator-settable** | `EstimateDetails.VatPercent` is free and bounded 0–100, but its only production writer is MCP `pegasus_estimate_save`; the shipped Accept form types no percentage — see Outstanding |
| D9 — Current estimate's VAT overrides the report rule | Proven (build/test) | `ReportRepairCosts.Vat => VatOverride ?? <built-in>` (`AssessmentReportRendering.cs:105`); `CostsOf` always sets `VatOverride` (`AssessmentReportProjection.cs:97`); asserted at 5 % in `TheCurrentEstimateSuppliesTheCostBlockAndTheListsWithItsOwnVat` |
| Migration and grants | Proven (build/test) | `20260828112103_NamedEstimates.cs` adds columns only; both tables already granted at `20260819112640:134` and `20260803205759:189`; census entry at `IntakePersistenceIntegrationTests.cs:112` |
| Existing import/accept callers keep their behaviour | Proven (build/test) | `Index.cshtml.cs:598` and `:707` untouched by this diff; `AssessmentEstimateImportWebTests.cs` green in the gate run |
| Automation may only save AiDraft estimates citing a held job | Proven (build/test) | `EstimatePolicy.ValidateSave` `Estimates.cs:161`, `ValidateCitedJob` `:198`; over-HTTP test `AutomationAssessmentIngressTests.cs:651` |
| Any deployed behaviour | **Unproven** | Newest release is 19 (2026-08-22, `docs/operations.md:808`), predating merge commit `c4b89840` (2026-08-28) |

## Outstanding

- **`ISetCurrentEstimate`, `IDuplicateEstimate` and `IDiscardEstimate` are
  registered but unreachable.** `DependencyInjection.cs:322`–`:324` are
  their only mentions outside `Estimates.cs`. Tier 1 only. Disclosed at
  review in the post-implementation report; **owned by ENG-028** (the
  estimate editor's Use estimate / Duplicate / Delete controls).
- **`JsonEstimateParser` has no production consumer.** Registered at
  `DependencyInjection.cs:320`; the page still resolves
  `AudatexEstimatePdfParser`. **Owned by ENG-028** (the import dialog's
  source-route selector).
- **The per-estimate VAT % is not settable from any shipped screen.**
  `StartDraftAsync` seeds `VatPercent = EstimatePolicy.DefaultVatPercent`
  (20) at `EfRepairSpecificationStore.cs:94`, and
  `OnPostAcceptSpecificationAsync` (`Index.cshtml.cs:640`) still collects
  the legacy `vat` money figure and `repairerVatRegistered` answer rather
  than a percentage. The override mechanism is real and runs, but the
  value it overrides with is 20 % for every estimate the shipped UI can
  create. **Owned by ENG-028** (the editor's VAT % field).
- **A report draft still cannot be generated through the shipped UI.**
  `Prepare` now also requires `currentEstimate.Details.LabourRate is > 0`
  (`AssessmentReportProjection.cs:143`, `LabourRateRequirement`), but the
  only production path that creates an estimate leaves `LabourRate` null:
  `StartRepairSpecificationDraftRequest` (`RepairSpecifications.cs:222`)
  carries no rates, `StartDraftAsync` sets
  `LabourRate = predecessor?.LabourRate`
  (`EfRepairSpecificationStore.cs:96`), and `AcceptAsync` never touches
  it. The only writer is `SaveEstimateAsync`
  (`EfRepairSpecificationStore.cs:489`), reachable only through MCP. This
  is not a regression — before this merge the production source passed
  `Costs: null` and the draft failed closed on `RepairCostRequirement` —
  but the end-to-end report claim stays unproven until ENG-028 ships the
  rate fields. **Owned by ENG-028.**
- **`RepairSpecificationPolicy.PolicyVersion` was bumped to 2** with the
  docstring "the calculation basis of an estimate made Current is derived
  by `EstimateTotals`" (`RepairSpecifications.cs:75`–`:80`), **but the one
  production accept path still hand-types the basis**
  (`Index.cshtml.cs:698`). `EstimatePolicy.BasisFor` (`Estimates.cs:267`),
  which does derive it, is called only from `ValidateSetCurrent` and
  `SetCurrentEstimate`, neither of which has a production caller. The
  report is unaffected — its costs come from `CostsOf`, not from
  `CalculationBasis` — so this is a provenance-label mismatch, not a money
  defect. Raise against **ENG-028**, which wires the path that would make
  the docstring true.
- **No deployed evidence for anything in this ticket.** Nothing here has
  been exercised against a running environment; every claim above is
  tier 1 or tier 2.
- **No browser/layout walk.** ENG-026 ships no Razor markup, so the
  1580/1100/760 check does not apply to it; `UIIMP-010` owns that walk for
  the epic.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not
been promoted; the exact-SHA `dev` → `main` promotion happens at wave 5.

## 2026-08-29 — Reversed out of Done under the strict rule 14 (D20/D21)

The operator settled rule 14 in favour of the strict reading after this proof was
written, and separately ruled that a disabled control or a closed feature gate is
never a delivered capability (D21). An independent GPT-5.6 audit, adjudicated
against this ticket's own What/Owns/Verification scope, found the following named
capabilities are not delivered on merged `dev` at `b92cb9a7`:

| Capability | Why it does not qualify | Wired by |
| --- | --- | --- |
| `IDuplicateEstimate` — named in the What ("Use cases … `IDuplicateEstimate` …") | `git grep -n "IDuplicateEstimate" -- src/` returns exactly three hits: interface `src/Pegasus.Core/Assessment/Estimates.cs:367`, implementation `Estimates.cs:406`, DI `src/Pegasus.Infrastructure/DependencyInjection.cs:322`. No route, handler, MCP tool or reachable consumer. D21's "Registered in DI with no reachable consumer — No" row. | [[ENG-028]] — its What names the editor "Duplicate" action and its Owns claims `src/Pegasus.Web/Pages/Cases/Assessment/**` |
| `IDiscardEstimate` and state `+Discarded` — both named in the What | Identical three-hit shape: `Estimates.cs:372` / `:418` / `DependencyInjection.cs:323`. `RepairSpecificationState.Discarded` reaches the store (`EfRepairSpecificationStore.cs:322`) and the migration check constraints, but only through `IDiscardEstimate`, so it has no production entry point. | [[ENG-028]] — its What names the editor "Delete" action and the related dialogs |
| `ISetCurrentEstimate` — named in the What | `Estimates.cs:377` / `:440` / `DependencyInjection.cs:324`. The legacy Accept handler can set an initial `IsCurrent` but supplies no Current-switching capability. | [[ENG-028]] — its What names "Use estimate/Current chip" |
| JSON estimate parser and source route `+Json` — named in the What ("routes `+Json`", "JSON estimate parser beside the Audatex parser") and in Owns (`src/Pegasus.Infrastructure/Assessment/JsonEstimateParser.cs`) | Registered only as a concrete singleton (`DependencyInjection.cs:320`); `IEstimateDocumentParser` binds solely to `AudatexEstimatePdfParser` (`:317`), which is what the page injects (`Pages/Cases/Assessment/Index.cshtml.cs:44`). The import form accepts PDF only (`Index.cshtml:488`). The DI comment states the intent that never shipped — "the import dialog selects the parser by the chosen source route" — and there is no such dialog. | [[ENG-028]] — its What names the "Import estimate dialog (name, source Audatex PDF / JSON / Other, file)" |
| Line-operation mapping Replace/Repair/R&I/Paint/Other ↔ `EstimateLineCodes` — named in the What | `EstimateOperations.FromLineType` occurs only at its definition (`Estimates.cs:50`) and in `tests/Pegasus.Core.Tests/Assessment/EstimateTests.cs` — test-only, which rule 14 names explicitly. `ToLineType`/`TryParse` are consumed only by `JsonEstimateParser.cs:134`/`:136`, so their sole consumer is itself unreachable. | [[ENG-028]] — the lines-table operation column in its editor |
| Report costs rendered from the Current estimate — named in the What ("the Current estimate feeds `ReportRepairCosts`") and in Verification ("report costs come from the Current estimate") | Readiness *is* wired (`Index.cshtml.cs:319` calls `AssessmentReportProjection.Prepare(…, currentEstimate: AcceptedSpecification)` on every load), but the rendered-cost half cannot be exercised. `Prepare` requires `currentEstimate.Details.LabourRate is > 0` (`src/Pegasus.Core/Reports/AssessmentReportProjection.cs:142`); the only reachable writer seeds `LabourRate = predecessor?.LabourRate` (`EfRepairSpecificationStore.cs:96` — null on first import), and the Accept handler's form fields populate the calculation basis, not `Details.LabourRate`. The MCP save tool can write a rate but produces unconfirmed lines (`Estimates.cs:610`), which acceptance rejects (`RepairSpecifications.cs:162`). `proof.md:335` concedes it — "A report draft still cannot be generated through the shipped UI" — against `proof.md:112` "The path is wired end to end on dev". | [[ENG-028]] — the rates editor plus "Use estimate" |

Nothing in the proof above is withdrawn — it remains accurate at the tier it claims.
What changed is the bar, not the evidence. The honest disclosure this proof already
carries ("registered but unreachable" at `:319`, "No production consumer" at `:262`)
is precisely what D20 says no longer earns Done: "A registered-but-unreachable port
does not qualify, however honestly it is disclosed and ticketed."

`disabledOrGated` is empty for this ticket — its failures are unwiring, not gating.
`Features:AutomationMcp`, the gate over the two MCP tools this ticket names, is OPEN:
`docs/operations.md:139` records `Features__AutomationMcp=true` in production since
release 9 (2026-08-18). So `pegasus_estimate_save`/`pegasus_estimate_list`,
`ISaveEstimate`, `IListCaseEstimates`, `EstimateTotals.Compute`, per-estimate VAT %,
`PaintWorkUnits`, the `AiDraft` route and the migration columns are genuinely
delivered.

### Findings that were NOT counted against this ticket

- Permanently inert `Glass's` button,
  `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml:211` — a D7 seam and a
  D21 "No" row, but the file belongs to [[ENG-025]] (`waves.md` wave 2 lane F
  owns `Pages/Cases/Assessment/**`); this ticket's Owns section claims no page
  file. Its supplier is [[TICK-085]].
- Permanently inert `Audatex` integration button,
  `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml:214` — same D7 seam, same
  owner [[ENG-025]]. Distinct from the active retained-PDF import, which works.
  Now owned by [[ENG-030]].
- `Features:AutomationMcp` over `pegasus_estimate_save`/`pegasus_estimate_list` —
  checked and cleared, not a finding: the gate is OPEN in production per
  `docs/operations.md:139`, so these callers are real under D21's OPEN-gate row.
