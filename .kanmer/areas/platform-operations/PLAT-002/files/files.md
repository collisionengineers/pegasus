# Files — PLAT-002

Surveyed before planning on 2026-08-20. The named surface follows the ticket; the unresolved scope decision may expand it.

## Where the change lands

| Path | Why |
|---|---|
| src/Pegasus.Web/Pages/StaffPageModel.cs (new) | Proposed metadata-free owner for claim-to-actor resolution and N-format operation keys. |
| src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs | Derive from the root; remove only actor/key copies. Keep case command, lease, TempData, readiness, and logging here. |
| src/Pegasus.Web/Pages/Administration/AdministrationPageModel.cs | Derive from the root; retain administration-only key validation. |
| src/Pegasus.Web/Pages/Operations/Index.cshtml.cs | Derive from root; preserve role metadata and lease flow. |
| src/Pegasus.Web/Pages/Triage/Details.cshtml.cs | Derive from root; preserve local SubjectId-to-Guid parsing. |
| src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs | Derive from root; remove private actor/key copies. |
| src/Pegasus.Web/Pages/Account/PasswordChange.cshtml.cs | Derive under authenticated fallback; preserve local staff-id parsing. |
| src/Pegasus.Web/Pages/Cases/Documents/Download.cshtml.cs | Derive from root; explicit staff authorization remains. |
| src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs | Keep AllowAnonymous; consume only an accessible shared key generator without staff-root inheritance. |
| tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs or existing page-model architecture owner | Protect intended common-base/one-owner shape without duplicating a string inventory. |
| Existing focused integration/browser tests named by ticket | Verify unchanged real callers: CaseDetailsWebTests, AdministrationSearchAccountWebTests, OperationsWebTests, QdosCustodialWebTests, ImageIntakeWebTests. |

## Context files

| Path | What it tells the implementer |
|---|---|
| src/Pegasus.Web/Program.cs | Authenticated fallback protects PasswordChange; endpoint authorization does not belong on the root. |
| StaffActorFactory definition under Pegasus.Core | Canonical claim-to-actor translation; call unchanged. |
| src/Pegasus.Core/Identity/IdentityContracts.cs | ActionActor.SubjectId preserves the staff Guid source. |
| src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs | Case-only mechanics must not move upward. |
| src/Pegasus.Web/Pages/Administration/AdministrationPageModel.cs | Preferred nullable signature; key validation is a separate concern. |
| tests/Pegasus.IntegrationTests/AdministrationSearchAccountWebTests.cs | Policy and PasswordChange route coverage. |
| tests/Pegasus.IntegrationTests/OperationsWebTests.cs | Real HTTP coverage for actor-bound Operations actions. |
| tests/Pegasus.IntegrationTests/QdosCustodialWebTests.cs | Password-change and custodial/download-adjacent coverage. |
| tests/Pegasus.IntegrationTests/CaseCapabilityPagesTestSupport.cs | Shared case-page actor/refusal support; do not duplicate it. |
| docs/engineering.md | One-owner and proportional-plan constraints. |
| docs/current-architecture.md:591 | Current CaseMutationPageModel as-built description. |
| SIMPLI-011 ticket documents | Proven origin, boundary, tests, and explicit deferral. |

## Ripple effects

Base changes are compile-time composition only. Routes, handlers, authorization, antiforgery, claims, key format, TempData, redirects, and Core ports remain unchanged. Namespace/using cleanup follows. Architecture tests protect ownership; focused integration tests prove callers. No database, migration, Infrastructure, Bicep, Azure, deployment, PRD, FRD, or ADR artifact follows.

If scope expands, add the other direct actor-resolution and key-generation page files from the rg survey and map their owning tests before planning.

## Out of scope

No Core policy change, authorization redesign, key validation redesign, case workspace behaviour, public upload workflow change, Azure/live check or write, deployment, or documentation taxonomy change. Research does not implement or plan the consolidation.
