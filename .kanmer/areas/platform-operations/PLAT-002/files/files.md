# Files — PLAT-002

Surveyed on 2026-08-20 at `c41314d9`. Option 1 is final: complete
Web-wide actor and operation-key consolidation.

## Where the change lands

| Path | Change and risk |
|---|---|
| src/Pegasus.Web/Pages/StaffPageModel.cs (new) | Metadata-free Razor Page root; own the single protected nullable TryGetActor and public static NewOperationKey. |
| src/Pegasus.Web/Pages/Administration/AdministrationPageModel.cs | Derive from StaffPageModel; remove actor/key copies; retain administration-only IsOperationKeyValid. |
| src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs | Derive from StaffPageModel; remove actor/key copies; retain all case-only mechanics. |
| src/Pegasus.Web/Pages/Index.cshtml.cs | Derive from StaffPageModel; replace one inline actor lookup. |
| src/Pegasus.Web/Pages/Account/PasswordChange.cshtml.cs | Derive from StaffPageModel; replace helper/key copy; preserve local SubjectId-to-Guid parsing. |
| src/Pegasus.Web/Pages/Cases/Create.cshtml.cs | Derive from StaffPageModel; replace two actor lookups and its operation-key generation. |
| src/Pegasus.Web/Pages/Cases/Index.cshtml.cs | Derive from StaffPageModel; remove actor helper. |
| src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs | Derive from StaffPageModel; remove actor/key copies. |
| src/Pegasus.Web/Pages/Cases/Documents/Download.cshtml.cs | Derive from StaffPageModel; remove actor helper; retain explicit staff authorization. |
| src/Pegasus.Web/Pages/ImageIntake/Details.cshtml.cs | Derive from StaffPageModel; replace one actor lookup. |
| src/Pegasus.Web/Pages/Intake/Details.cshtml.cs; src/Pegasus.Web/Pages/Intake/Source.cshtml.cs | Derive from StaffPageModel; replace five actor lookups while preserving fallback authentication and handler responses. |
| src/Pegasus.Web/Pages/Mail/Index.cshtml.cs; src/Pegasus.Web/Pages/Mail/Message.cshtml.cs | Derive from StaffPageModel; replace three actor lookups. |
| src/Pegasus.Web/Pages/Operations/Index.cshtml.cs | Derive from StaffPageModel; remove actor/key copies while preserving lease flow. |
| src/Pegasus.Web/Pages/Triage/Index.cshtml.cs; src/Pegasus.Web/Pages/Triage/Details.cshtml.cs | Derive from StaffPageModel; replace actor/key copies; Details keeps local SubjectId-to-Guid parsing. |
| src/Pegasus.Web/Pages/Unidentified/Details.cshtml.cs | Derive from StaffPageModel; replace two actor lookups without changing conditional action logic. |
| src/Pegasus.Web/Pages/Upload.cshtml.cs | Derive from StaffPageModel and replace its actor lookup. Keep ExternalReceiptToken generation local: it is an intake replay/receipt identity, not an operation key. |
| src/Pegasus.Web/Pages/UploadStatus.cshtml.cs; src/Pegasus.Web/Pages/UploadGroupStatus.cshtml.cs | Derive from StaffPageModel; replace their actor lookups. |
| src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs | Remain AllowAnonymous on PageModel; remove its operation-key copy and call StaffPageModel.NewOperationKey statically. |
| tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs | Reuse FindRepositoryRoot/source inspection. Guard the sole actor-factory owner, the two intentional GUID-N application sites (StaffPageModel operation key and Upload receipt token), and RequestModel's anonymous/non-staff boundary. |
| docs/current-architecture.md | Name StaffPageModel as the shared request-context owner. |

## Context files

| Path | What it establishes |
|---|---|
| src/Pegasus.Core/Actors/StaffActorFactory.cs | Canonical claim-to-actor policy; call unchanged. |
| src/Pegasus.Core/Identity/IdentityContracts.cs | ActionActor.SubjectId supplies the two local staff-Guid parses. |
| src/Pegasus.Web/Program.cs | Authenticated fallback policy; authorization must not move into the root. |
| src/Pegasus.Web/Pages/Administration/AdministrationPageModel.cs | Preferred nullable-flow signature and administration-only validation. |
| src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs | Case-only state and command mechanics remain below the root. |
| src/Pegasus.Web/Pages/Upload.cshtml.cs | ExternalReceiptToken is the grouped-intake replay/receipt identity and must remain a separate concept. |
| tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs | Existing source-assertion and repository-root convention. |
| tests/Pegasus.IntegrationTests/AdministrationSearchAccountWebTests.cs | Administration and PasswordChange coverage. |
| tests/Pegasus.IntegrationTests/CaseCreateWebTests.cs; tests/Pegasus.IntegrationTests/CasesIndexWebTests.cs; tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs | Case create/list/base coverage. |
| tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs; tests/Pegasus.IntegrationTests/QdosCustodialWebTests.cs | Assessment and document/password coverage. |
| tests/Pegasus.IntegrationTests/QdosIntakeWebTests.cs; tests/Pegasus.IntegrationTests/IntakeWebNegativeTests.cs; tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs | Intake, upload, and image-intake coverage. |
| tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs; tests/Pegasus.IntegrationTests/OperationsWebTests.cs | Mail and Operations coverage. |
| tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs; tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs | Triage and Unidentified coverage. |
| tests/Pegasus.IntegrationTests/ShellAndStatusPageWebTests.cs; tests/Pegasus.IntegrationTests/StaffSignInSecurityTests.cs | Root, upload status, authentication, and forced-password coverage. |
| docs/engineering.md | One-owner, abstraction, plan-sizing, and four-lens requirements. |
| Kanmer SIMPLI-011 documents | Proven CaseMutationPageModel origin and explicit deferral. |

## Ripple effects

This is compile-time Web composition. Remove obsolete usings only where the
compiler proves them unused. Preserve routes, handlers, authorization metadata,
antiforgery, factory inputs, operation/receipt formats, validation, failure
responses, TempData, redirects, and Core calls. Existing Razor references to
CaseMutationPageModel.NewOperationKey should resolve the inherited member.

Focused integration tests cover all caller families. Architecture tests protect
ownership and the deliberate anonymous/receipt exceptions. The as-built
architecture snapshot changes.

## Out of scope

No change to StaffActorFactory, Core policy, authorization, operation-key or
receipt-token semantics/validation, page behaviour, test-only generators,
database, Infrastructure, Bicep, Azure/live state, deployment, PRD, FRD, or ADR.
No neutral key utility, new project, or top-level directory.
