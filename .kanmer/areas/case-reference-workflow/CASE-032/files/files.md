# Files — CASE-032

Revised 2026-09-04 after the plan review: the Triage half is no longer
conditional — its reference and provider owners exist in Core (see
`open-questions/open-questions.md`).

## Pegasus.Core

`src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` — add the
`ImageCustodyState` enum, the nullable `Custody` member on `ImageIntakeSummary`
(before its defaulted parameters) and the same member on `ImageIntakeDetail`.

`src/Pegasus.Core/Triage/TriageContracts.cs` — append nullable `Reference` and
`Provider` to `TriageSummary`.

`tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeCasePairingTests.cs` —
compatibility update: its `Summary` helper constructs `ImageIntakeSummary`
positionally.

`tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs` —
compatibility update: its `NewTriage` helper constructs `TriageSummary`.

## Pegasus.Infrastructure

`src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` — select
`CustodyState` in the existing `ProjectAsync` select and in `ToDetailAsync`,
mapping through the existing `ImageCustodyStates` constants; no extra read.

`src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs` — left-join
`InstructionDrafts` on `OriginReceiptId` inside both `ListAsync` and
`GetByOriginReceiptAsync`, and construct the two new summary members there.

`src/Pegasus.Infrastructure/Persistence/ImageIntakeEntities.cs` — no change.
`ImageCustodyStates` stays the sole owner of the persisted strings;
`EfExternalWorkStore` and `EfQueuedCustodyProcessor` keep using it unchanged.

`src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` — no change.

`src/Pegasus.Infrastructure/Persistence/Migrations/**` — no change.

## Pegasus.Web

`src/Pegasus.Web/Pages/Cases/Index.cshtml.cs` — `ImageRow` and `TriageRow` and
their two quick-detail lists only. Shared with CASE-042, which adds the
Awaiting-instruction tab after this merges; tabs, rail, filters, selection and
the `LoadNotReadyAsync`/`LoadTriageAsync` bodies stay untouched.

`src/Pegasus.Web/Presentation/OperatorLabels.cs` — one
`ImageCustodyState` mapping in a CASE-032-delimited block.

`src/Pegasus.Web/Pages/Search/Index.cshtml.cs` — pass the new `Custody` member
through the exact-reference summary reconstruction at `:238-247`.

## Pegasus.IntegrationTests

`tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` — extend the image-row
test for custody and add the seeded Triage-row test asserting reference,
registration, provider and assignee individually.

## docs

`docs/design/test-ui/pages/queues--default.html`,
`docs/design/test-ui/pages/queues--empty.html` — regenerated artifacts for the
routed `/Cases` PageModel change. Neither captured state currently contains an
image or Triage row, so they may come back byte-identical; the
post-implementation report records which.

## Not touched

No new query type, page, service, package, migration, test project or file is
justified. `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`,
`.github/workflows/ci.yml` and `scripts/*.ps1` are off-limits to this lane
(EPIC-012 build policy).

## No migration expected

Both halves project columns that already exist —
`ImageIntakes.CustodyState`, `InstructionDrafts.ClaimNumber` and
`InstructionDrafts.SuggestedPrincipalCode` — so no migration, grant change or
bootstrap census entry rides this diff.
