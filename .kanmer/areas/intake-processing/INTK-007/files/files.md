# Files — INTK-007

## Governing-document changes (hard prerequisite)

| Path | Exact responsibility |
|---|---|
| `docs/operator-notes.md` | Protected update: record U-reference, group behavior, reason, resolution/history, and replace the prior operator meaning of Needs sorting without erasing Triage/Blocked/Audit distinctions. |
| `docs/prd/pegasus-product.md` | Define Unidentified product outcome, scope, boundaries, and success criteria. |
| `docs/frd/frd-01-case-identity-and-lifecycle.md` | Reclassify incomplete/ambiguous Audit and pre-Case material; prohibit U-reference as Case/Audit identity. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Canonical Unidentified aggregate, origin/group rule, reference, reason, resolution, replay, and migration behavior. |
| `docs/frd/frd-03-triage.md` | Specify missing-VRM/pre-Triage behavior without collapsing Triage into Unidentified. |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Route completed vehicle groups per INTK-006 and technical failures per Unidentified rules. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Replace mail destination/abstention behavior and folder/queue wording. |
| `docs/frd/frd-09-provider-and-intermediary-routes.md` | Map ambiguous/no-match routes to canonical Unidentified behavior where appropriate. |
| `docs/frd/frd-10-mcp-automation-and-actor-boundary.md` | Define automation query/mutation exposure for U-references if tool behavior changes. |
| `docs/frd/frd-12-operator-experience.md` | Define queue, detail, counts, exact search, reasons, history, and resolution affordance. |
| `docs/design/README.md` and `docs/design/system/**` | Replace current operator/design examples and preserve settled visual semantics. |
| `docs/capabilities.md`, `docs/index.md`, `docs/current-architecture.md`, `docs/runbook.md` | Reconcile capability owner/navigation/as-built/operator-runbook references where applicable. |
| `docs/adr/0006-provider-neutral-intake-with-contained-qdos-policy.md` | Read as historical context; do not edit wording unless a new ADR formally supersedes a changed technical decision. |

## Core changes

| Path | Exact responsibility |
|---|---|
| New focused files under `src/Pegasus.Core/Intake/Unidentified/` only if existing Intake files would become unwieldy | Define aggregate, reason/state enums, reference formatter/parser, commands, queries, store port, authorization/version/replay validation. This is not a new project/top-level boundary. |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | Replace or re-project `IntakeDecision.NeedsSorting` according to the updated FRD; preserve one canonical meaning. |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | Map unreadable, unsupported, no identification, conflict, ambiguous destination, and terminal technical outcomes to the one Unidentified creation port after custody. |
| `src/Pegasus.Core/Intake/IntakeDecisionPolicy.cs` | Prevent Unidentified/U-reference from satisfying Case acceptance. |
| `src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs` | Map mail abstention to the canonical destination without duplicating reasons. |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailRoutePolicy.cs` | Preserve route evidence and return the updated abstention result defined by FRD-09. |
| `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` and contracts | Send only documented terminal image technical/unidentified outcomes here; completed vehicle groups follow INTK-006. |
| `src/Pegasus.Core/Triage/` relevant lifecycle/intake files | Preserve Triage separation and apply the updated missing-registration mapping. |
| `src/Pegasus.Core/Operations/DashboardCounts.cs` | Rename/reshape counts to Unidentified with no duplicate legacy count. |
| `src/Pegasus.Core/Search/` relevant contracts | Add exact U-reference search result type and prevent cross-reference confusion. |

## Infrastructure and migration

| Path | Exact responsibility |
|---|---|
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | Add Unidentified item, sequence, history/resolution entities; constraints, indexes, relationships, versions, and delete restrictions. |
| New `src/Pegasus.Infrastructure/Persistence/EfUnidentifiedStore.cs` | Implement atomic allocation, idempotent register/resolve/replay, list/detail/search, history, and origin/group lookup. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` | Join/project the U-reference and canonical reason on receipt detail/list and migrate decision filters. |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Project Unidentified destination/reference for retained messages. |
| `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` | Count open Unidentified items, not legacy decision strings. |
| `src/Pegasus.Infrastructure/Persistence/EfOperationsStore.cs` | Supply Unidentified queue/filter/detail data and freshness metadata. |
| `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` | Update old decision assumptions; do not allocate U-reference for successful INTK-006 groups. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_UnidentifiedWork.cs` | Add schema, dedicated sequence seed, backfill every legacy record once, map reasons deterministically, indexes/FKs/grants, and rollback strategy. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | EF model snapshot. |
| `src/Pegasus.Web/Program.cs` | Register the one Core Unidentified port/store/use cases. |

## Web, MCP, and presentation

| Path | Exact responsibility |
|---|---|
| New `src/Pegasus.Web/Pages/Unidentified/Index.cshtml(.cs)` | Operator queue with U-reference, original filename/source, received time, reason, state, and next action. |
| New `src/Pegasus.Web/Pages/Unidentified/Details.cshtml(.cs)` | Full origin/group, reason/detail, retained-source links, history, and authorized resolution form. |
| `src/Pegasus.Web/Pages/Index.cshtml` | Replace dashboard Needs sorting metric/link with Unidentified. |
| `src/Pegasus.Web/Pages/Operations/Index.cshtml(.cs)` | Replace count/queue/filter and retain refresh/freshness behavior. |
| `src/Pegasus.Web/Pages/Intake/Details.cshtml(.cs)` | Show linked U-reference/reason/history and route to Unidentified detail. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Show/link Unidentified projection for mail. |
| `src/Pegasus.Web/Pages/Triage/Index.cshtml(.cs)` | Remove legacy filter wording while preserving Triage-specific states. |
| `src/Pegasus.Web/Pages/Shared/_StatusChip.cshtml` | Render canonical Unidentified state through labels. |
| `src/Pegasus.Web/Pages/Search/Index.cshtml(.cs)` | Parse/search exact `U<n>`, render distinct result type and link. |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml(.cs)` and INTK-005 group view | Display U-reference/reason for qualifying receipt/group outcomes. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Single mapping for Unidentified state/reasons; remove operator-facing legacy label. |
| `src/Pegasus.Web/Mcp/IntakeMcpTools.cs` | Return/query U-reference, state, reason, origin, and history; enforce actor/version/idempotency for resolution; never accept U as Case reference. |
| Navigation/layout files found by final search | Add Unidentified queue navigation using existing shell conventions. |

## Tests

| Path | Required coverage |
|---|---|
| New `tests/Pegasus.Core.Tests/Intake/Unidentified*` | Reference format/parser, taxonomy, registration, resolution, authorization, version, replay, group-origin, and Case-reference rejection. |
| Existing ProcessIntake/mail route/classification/Triage/ImageIntake Core tests | Reclassify every old producer by the migration table; no blind expected-string replacement. |
| New/updated migration tests in `tests/Pegasus.IntegrationTests` | Empty/current DB, legacy backfill, deterministic order, concurrent allocation, retry/replay, uniqueness, rollback, runtime grants. |
| `IntakePersistenceIntegrationTests.cs`, `RetainedMailPersistenceTests.cs`, `MailboxIntakeIntegrationTests.cs` | Durable origin/reference/reason/history and mail projection. |
| `OperationsPersistenceTests.cs`, `OperationsWebTests.cs` | Accurate open Unidentified count/filter/link/freshness. |
| `IntakeWebNegativeTests.cs`, `MultiFormatIntakeWebTests.cs`, `QdosIntakeWebTests.cs` | All unreadable/unsupported/ambiguous mappings and visible U-reference. |
| `ImageIntakePersistenceTests.cs`, `ImageIntakeWebTests.cs` | Successful vehicle grouping bypasses Unidentified; terminal documented failures enter it once. |
| `MailWorkspaceWebTests.cs`, `QdosTriageIntegrationTests.cs` | Mail/Triage distinctions remain correct. |
| `Browser/OperatorJourneyTests.cs` | Queue→detail→resolution, search by U-reference, history, keyboard/validation. |
| Health/case acceptance/search tests containing old term | Update semantic expectations and prove no U-reference can allocate/find a Case. |

## Final stale-term audit

Run `rg -n "NeedsSorting|Needs sorting|needs_sorting" src tests docs`. Every remaining match must be one of:
- historical migration compatibility;
- historical accepted ADR text;
- an explicit backward-compatibility parser/mapping test;
- a migration comment explaining the legacy value.

Record every allowed residual path in the post-implementation report. No operator-facing markup, current governing behavior, MCP schema, current enum label, or design example may retain the old term.

## Out of scope

- Deleting retained intake or reusing U-references.
- Treating U-reference as Case/PO/Audit/Image Intake identity.
- Replacing Triage, Blocked intake, Audit, Image Intake, or INTK-006 Image-Only routing.
- Generic workflow/reference frameworks, new runtime/store service, mailbox mutation, cloud deployment.
