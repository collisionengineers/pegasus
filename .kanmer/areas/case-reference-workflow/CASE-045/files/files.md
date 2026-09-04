# Files — CASE-045

## File map — CASE-045 delta after CASE-032 and CASE-042

## Pegasus.Core

`src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` — add nullable `PrincipalId` (and any derived display value) to the record contract and `ImageIntakeSummary` per D51/ticket Approach; no new matching or creation policy.

## Pegasus.Infrastructure

`src/Pegasus.Infrastructure/Persistence/ImageIntakeEntities.cs` — add nullable `PrincipalId` column to `ImageIntakeEntity`.

`src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` and the ImageIntake model configuration — persist/read the new column and project it (with the confirmed `Principal.Code` join for display) in the existing bulk `ProjectAsync` query, without N+1 reads.

`src/Pegasus.Infrastructure/Persistence/Migrations/<ts>_ImageIntakePrincipal.cs` + Designer + `PegasusDbContextModelSnapshot.cs` — new nullable-column migration; regenerate at merge prep to sort after `dev`'s tail if it moved (queue-serialized per EPIC-012 Build policy).

`scripts/Invoke-AzureDatabaseBootstrap.ps1` — census entry only if verification during planning shows a new grant is needed for the column (a column on an existing table typically needs none; confirm, don't assume).

## Pegasus.Web — shared ordered-merge paths

`src/Pegasus.Web/Pages/Cases/Index.cshtml.cs` — change CASE-042’s Awaiting `ImageRow`/quick-detail fact construction to add `Principal` (recorded code, or the exact `Not known` label) using the projected optional value.

`src/Pegasus.Web/Pages/Cases/Index.cshtml` — change only if CASE-042’s generic quick-detail markup needs a conditional principal-specific rendering path; current generic facts may make this unnecessary.

## Pegasus.Web — image-initiated detail page

`src/Pegasus.Web/Pages/ImageIntake/Details.cshtml` — add a `Principal` definition-list fact (recorded code or `Not known`) and a staff select from the active principals list (default `Not known`), reusing the existing detail-page mutation/lease/replay convention.

`src/Pegasus.Web/Pages/ImageIntake/Details.cshtml.cs` — add the handler to set `PrincipalId` from the active principals list, through the existing mutation guard.

`src/Pegasus.Web/Presentation/OperatorLabels.cs` — add the CASE-045-delimited block with the exact `Not known` label (per ticket Approach: "Labels in `OperatorLabels` only").

## Tests

`tests/Pegasus.Core.Tests` — record-contract test for the optional `PrincipalId`.

`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` — add the new migration to the applied-migrations list (chronological; queue-position serialized).

An ImageIntake persistence test (Infrastructure/Core test project — exact location resolved in planning) for the nullable column round-trip.

`tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` — Awaiting-tab and quick-view assertions for a recorded principal and for `Not known` when none exists, based on CASE-042’s merged test shape.

`tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs` — detail-page display and staff-select assertions.

## Explicitly not changed on current evidence

`src/Pegasus.Core/Intake/IntakeDecisionPolicy.cs` — no case-creation or intake-decision change; a principal is never required to create or retain an image-initiated case.

## Researcher's file-map caveats (see research.md wrapper note)

The researcher's version of this file map gated most of the above on
"only if the operator confirms a canonical optional principal projection" and
listed the migration/grants/OperatorLabels rows as **not** needed on current
evidence. That gating reflects the researcher not fully reconciling the D51
decision and the ticket's own Approach section (which already specify the
nullable-column design). This document restates the file map against the
ticket's actual Approach; the plan should verify the researcher's other,
unrelated evidence (CASE-032 diff contents, CASE-042 packet shape, existing
detail-page mutation convention, absence of any principal-authenticated
intake route) rather than its principal-storage conclusion.
