# Files — PLAT-002

Surveyed before planning on 2026-08-20 at c41314d9. Option 1 is final:
complete Web-wide consolidation.

## Where the change lands

| Path | Why |
|---|---|
| src/Pegasus.Web/Pages/StaffPageModel.cs (new) | Metadata-free Razor Page root. Own the single protected nullable TryGetActor implementation and the single public static NewOperationKey implementation. |
| src/Pegasus.Web/Pages/Administration/AdministrationPageModel.cs | Derive from StaffPageModel; delete actor/key copies; retain administration-only IsOperationKeyValid. Its descendants reuse the inherited methods without edits. |
| src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs | Derive from StaffPageModel; delete actor/key copies; retain all case command, lease, TempData, readiness, and logging behaviour. Its descendants reuse inherited methods without edits. |
| src/Pegasus.Web/Pages/Index.cshtml.cs | Derive from StaffPageModel; replace one inline actor lookup. |
| src/Pegasus.Web/Pages/Account/PasswordChange.cshtml.cs | Derive from StaffPageModel; replace its helper/key copy; preserve local SubjectId-to-Guid parsing. |
| src/Pegasus.Web/Pages/Cases/Create.cshtml.cs | Derive from StaffPageModel; replace two inline actor lookups and key copy. |
| src/Pegasus.Web/Pages/Cases/Index.cshtml.cs | Derive from StaffPageModel; delete its actor helper. |
| src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs | Derive from StaffPageModel; delete actor/key copies. |
| src/Pegasus.Web/Pages/Cases/Documents/Download.cshtml.cs | Derive from StaffPageModel; delete its actor helper; retain explicit staff authorization. |
| src/Pegasus.Web/Pages/ImageIntake/Details.cshtml.cs | Derive from StaffPageModel; replace one inline actor lookup. |
| src/Pegasus.Web/Pages/Intake/Details.cshtml.cs; src/Pegasus.Web/Pages/Intake/Source.cshtml.cs | Derive from StaffPageModel; replace five inline actor lookups while preserving fallback authentication and each handler's response. |
| src/Pegasus.Web/Pages/Mail/Index.cshtml.cs; src/Pegasus.Web/Pages/Mail/Message.cshtml.cs | Derive from StaffPageModel; replace three inline actor lookups. |
| src/Pegasus.Web/Pages/Operations/Index.cshtml.cs | Derive from StaffPageModel; delete actor/key copies while preserving lease flow. |
| src/Pegasus.Web/Pages/Triage/Index.cshtml.cs; src/Pegasus.Web/Pages/Triage/Details.cshtml.cs | Derive from StaffPageModel; replace actor/key copies; Details retains local SubjectId-to-Guid parsing. |
| src/Pegasus.Web/Pages/Unidentified/Details.cshtml.cs | Derive from StaffPageModel; replace two inline actor lookups without changing conditional action logic. |
| src/Pegasus.Web/Pages/Upload.cshtml.cs; src/Pegasus.Web/Pages/UploadStatus.cshtml.cs; src/Pegasus.Web/Pages/UploadGroupStatus.cshtml.cs | Derive from StaffPageModel; replace three actor lookups and Upload's key copy. |
| src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs | Remain AllowAnonymous and on PageModel; delete its key copy and call StaffPageModel.NewOperationKey statically. |
| tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs | Add a source-based one-owner guard for StaffActorFactory.TryCreate and N-format application key generation, plus reflection assertions that Uploads/Request remains AllowAnonymous and does not inherit StaffPageModel. Reuse FindRepositoryRoot. |
| docs/current-architecture.md | Refresh the Web implementation map to name StaffPageModel as the shared request-context owner above administration, case mutation, and direct staff pages. |

## Context files

| Path | What it tells the implementer |
|---|---|
| src/Pegasus.Core/Actors/StaffActorFactory.cs | Canonical claim-to-actor policy; the shared Web root calls this unchanged. |
| src/Pegasus.Core/Identity/IdentityContracts.cs | ActionActor.SubjectId is the existing source for PasswordChange and Triage staff Guid parsing. |
| src/Pegasus.Web/Program.cs | The authenticated fallback policy protects pages without explicit Authorize metadata; authorization must not move into the root. |
| src/Pegasus.Web/Pages/Administration/AdministrationPageModel.cs | Supplies the preferred NotNullWhen nullable-flow signature and the administration-only validation precedent. |
| src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs | Case-only state and command mechanics stay below the new root. |
| tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs | Existing repository-source assertion convention and FindRepositoryRoot helper. |
| tests/Pegasus.IntegrationTests/AdministrationSearchAccountWebTests.cs | Administration policy and PasswordChange route coverage. |
| tests/Pegasus.IntegrationTests/CaseCreateWebTests.cs; tests/Pegasus.IntegrationTests/CasesIndexWebTests.cs; tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs | Case create/list/shared-base HTTP callers. |
| tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs; tests/Pegasus.IntegrationTests/QdosCustodialWebTests.cs | Assessment and document/password caller coverage. |
| tests/Pegasus.IntegrationTests/QdosIntakeWebTests.cs; tests/Pegasus.IntegrationTests/IntakeWebNegativeTests.cs; tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs | Intake and image-intake actor/fail-closed coverage. |
| tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs; tests/Pegasus.IntegrationTests/OperationsWebTests.cs | Mail and Operations HTTP callers. |
| tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs; tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs | Triage and Unidentified queue/action callers. |
| tests/Pegasus.IntegrationTests/ShellAndStatusPageWebTests.cs; tests/Pegasus.IntegrationTests/StaffSignInSecurityTests.cs | Root dashboard, upload status, authentication, and forced-password paths. |
| docs/engineering.md | One-owner, existing-convention, no-unneeded-abstraction, test-support, and simplification-pass rules. |
| Kanmer SIMPLI-011 documents | Proven origin of CaseMutationPageModel and explicit deferral to PLAT-002. |

## Ripple effects

The change is compile-time Web composition. Remove obsolete Security.Claims,
Actors, and Identity usings only where the compiler proves they are unused.
Routes, handlers, authorization metadata, antiforgery, factory inputs, operation
key format, failure responses, TempData, redirects, and Core calls remain
unchanged. Existing Razor references to CaseMutationPageModel.NewOperationKey
continue to resolve the inherited public static member.

Focused integration tests cover every caller family; architecture tests protect
the one-owner result. Current architecture is refreshed because the as-built
owner changes.

## Out of scope

No change to StaffActorFactory, Core policy, authorization policies or roles,
operation-key validation, page behaviour, test-only key generators, database,
Infrastructure, Bicep, Azure/live state, deployment, PRD, FRD, or ADR. No new
utility/helper abstraction and no new project or top-level directory.
