# Research — PLAT-002: one Web staff-actor root

## Question

After the user selected complete consolidation, what exact Web surface owns
staff-actor resolution and N-format operation-key generation, and how can it be
reduced to one owner without changing authorization or page behaviour?

## Findings

- Survey source: checkout c41314d9 on 2026-08-20. The read-only command
  rg -l "StaffActorFactory.TryCreate" src/Pegasus.Web --glob "*.cs" returns 20
  files containing 27 calls.
- Two calls are the current abstract owners:
  src/Pegasus.Web/Pages/Administration/AdministrationPageModel.cs and
  src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs. The administration base
  has the preferred NotNullWhen nullable-flow signature; the case base also owns
  case-specific command, lease, TempData, readiness, and logging mechanics that
  must stay below a new common root.
- Eighteen concrete actor-calling models remain: root Index; Account
  PasswordChange; Cases Create, Index, Assessment, and Documents/Download;
  ImageIntake Details; Intake Details and Source; Mail Index and Message;
  Operations Index; Triage Index and Details; Unidentified Details; Upload;
  UploadStatus; and UploadGroupStatus.
- Every actor caller is authenticated. Root Index, the case/image/operations/
  triage/unidentified/upload pages carry explicit authorization; Account,
  Intake, and Mail use the authenticated fallback policy at
  src/Pegasus.Web/Program.cs:495-496. The abstract bases add no endpoint policy.
- No anonymous page resolves a staff actor. The relevant anonymous page,
  src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs, only duplicates operation-key
  generation and must remain AllowAnonymous on PageModel.
- The shared actor signature needs one output only. Ordinary pages consume
  ActionActor directly. Account/PasswordChange and Triage/Details can preserve
  their existing staff Guid by parsing ActionActor.SubjectId locally. Inline
  callers can replace only the factory clause and retain operand order,
  short-circuiting, and existing failure results.
- The read-only command
  rg -l 'Guid\.NewGuid\(\)\.ToString\("N"\)'
  src/Pegasus.Web/Pages --glob "*.cs" returns nine application owners:
  Account/PasswordChange, AdministrationPageModel, Cases/Assessment,
  CaseMutationPageModel, Cases/Create, Operations/Index, Triage/Details, Upload,
  and anonymous Uploads/Request.
- Test-only operation-key generators create valid fixture inputs; they do not
  own application behaviour and are excluded from the application one-owner
  assertion.
- Search found no existing neutral Web owner for key generation. A separate
  helper/utility would add an abstraction for one call shape without a second
  policy or external boundary. The smallest one-owner shape is a public static
  NewOperationKey on StaffPageModel. Uploads/Request calls it by type without
  inheriting StaffPageModel.
- StaffPageModel can derive from PageModel and remain metadata-free. The two
  current bases derive from it, and the 18 concrete actor callers derive from it
  directly. This changes no constructor dependency, route, handler,
  authorization metadata, antiforgery, factory input, operation-key format,
  failure response, TempData, redirect, or Core call.
- Existing Case Details Razor references to
  CaseMutationPageModel.NewOperationKey can continue to resolve the inherited
  public static member; a build immediately after migrating the two bases is the
  early proof. If the compiler rejects that access shape, the views can name
  StaffPageModel directly without changing generated values.
- Existing repository convention supports source-based architecture assertions
  in tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs through
  FindRepositoryRoot and File.ReadAllText. The durable checks are one Web
  StaffActorFactory call, one application Pages N-format generator, and an
  anonymous RequestModel that is not a StaffPageModel.
- Focused HTTP evidence already exists for every affected family:
  ShellAndStatusPageWebTests, AdministrationSearchAccountWebTests,
  StaffSignInSecurityTests, CaseCreateWebTests, CasesIndexWebTests,
  CaseDetailsWebTests, AssessmentReportDraftWebTests, QdosCustodialWebTests,
  QdosIntakeWebTests, IntakeWebNegativeTests, ImageIntakeWebTests,
  MailWorkspaceWebTests, OperationsWebTests, QdosTriageIntegrationTests, and
  TriageQueuesWebTests.
- SIMPLI-011 created and proved CaseMutationPageModel, then explicitly deferred
  this consolidation to PLAT-002. Reuse it; do not reopen the Case Details split.
- This is internal Web composition cleanup. No PRD, FRD, ADR, Core policy,
  database, Infrastructure, Azure/live state, deployment, or product behaviour
  changes.

## Implications

Create one metadata-free StaffPageModel owning the exact existing claim
translation and key format. Migrate both bases first, then every direct actor
caller; preserve local Guid parsing and surrounding conditions. Keep the public
upload-request page outside staff inheritance and reuse only the public static
key generator. Add architecture ownership guards, run the mapped focused HTTP
tests, and refresh current architecture as an as-built consequence.

This is one mechanical unit of work: every changed page consumes the same
existing Web request-context mechanism, and leaving any application copy would
violate the user-selected complete-consolidation acceptance.

## Open questions

None. Complete consolidation was selected by the user on 2026-08-20; the narrow
alternative was rejected.
