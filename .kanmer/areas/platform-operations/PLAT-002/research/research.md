# Research — PLAT-002: one Web staff-actor root

## Question

After the user selected complete consolidation, what exact Web surface owns
staff-actor resolution and operation-key generation, and how can it be reduced
to one owner without changing authorization, identity semantics, or page
behaviour?

## Verified findings

- Survey checkout: `c41314d9` on 2026-08-20.
- `rg -n "StaffActorFactory\.TryCreate" src/Pegasus.Web/Pages` returns 26 calls
  in 20 files: the two existing abstract bases and 18 concrete page models.
- The bases are
  `Pages/Administration/AdministrationPageModel.cs` and
  `Pages/Cases/CaseMutationPageModel.cs`. The administration base has the
  preferred `[NotNullWhen(true)] out ActionActor?` signature. Case-only command,
  lease, TempData, readiness, and logging mechanics stay in the case base.
- The 18 concrete actor callers are: root Index; Account/PasswordChange; Cases
  Create, Index, Assessment/Index, and Documents/Download; ImageIntake/Details;
  Intake/Details and Source; Mail/Index and Message; Operations/Index;
  Triage/Index and Details; Unidentified/Details; Upload; UploadStatus; and
  UploadGroupStatus.
- Every actor caller is authenticated. Explicit authorization protects root
  Index and the case/image/operations/triage/unidentified/upload pages; Account,
  Intake, and Mail use the authenticated fallback policy in
  `src/Pegasus.Web/Program.cs:495-496`. The common bases add no endpoint policy.
- No anonymous page resolves a staff actor. `Pages/Uploads/Request.cshtml.cs`
  is `[AllowAnonymous]`, derives from `PageModel`, and owns only an operation-key
  generator; it must remain outside staff inheritance.
- PasswordChange and Triage/Details need both `ActionActor` and a staff `Guid`.
  They can call the shared actor method and preserve their existing local
  `ActionActor.SubjectId` parsing. Inline callers can replace only the factory
  clause, retaining operand order, short-circuiting, and failure results.
- `rg -n 'Guid\.NewGuid\(\)\.ToString\("N"\)' src/Pegasus.Web/Pages --glob
  '*.cs'` returns nine files, but they do **not** represent one concept:
  eight generate operation keys (PasswordChange, AdministrationPageModel,
  Assessment/Index, CaseMutationPageModel, Cases/Create, Operations/Index,
  Triage/Details, and Uploads/Request); `Upload.cshtml.cs` generates
  `ExternalReceiptToken`, the durable intake replay/receipt identity.
- Therefore complete consolidation means one operation-key owner plus one
  separate receipt-token generation site. Moving `ExternalReceiptToken`
  generation into `StaffPageModel.NewOperationKey` would conflate identities
  and put intake receipt ownership at the wrong altitude merely because both use
  the GUID N representation.
- Test-only generators create fixture inputs and do not own application
  behaviour.
- Search found no neutral existing operation-key owner. A separate utility would
  add an abstraction for one call shape without a second policy or external
  boundary. The smallest shape is public static `NewOperationKey` on the new
  `StaffPageModel`; anonymous Uploads/Request calls it by type without
  inheriting the staff root.
- A metadata-free `StaffPageModel : PageModel` changes no constructor
  dependency, route, handler, authorization metadata, antiforgery, factory
  input, operation-key format, failure response, TempData, redirect, or Core
  call.
- Existing Razor references to `CaseMutationPageModel.NewOperationKey` should
  resolve the inherited public static member. A build immediately after base
  migration is the early proof; if Razor compilation rejects that access, name
  `StaffPageModel` directly.
- `DependencyDirectionTests.cs` already uses `FindRepositoryRoot` and source
  inspection. A proportional guard should assert: the sole Web actor-factory
  call is in StaffPageModel; application Pages contain exactly two GUID-N
  generation sites, StaffPageModel for operation keys and Upload for its
  distinct receipt token; and RequestModel remains anonymous and outside
  StaffPageModel inheritance.
- Existing integration classes cover each affected family:
  ShellAndStatusPageWebTests, AdministrationSearchAccountWebTests,
  StaffSignInSecurityTests, CaseCreateWebTests, CasesIndexWebTests,
  CaseDetailsWebTests, AssessmentReportDraftWebTests, QdosCustodialWebTests,
  QdosIntakeWebTests, IntakeWebNegativeTests, ImageIntakeWebTests,
  MailWorkspaceWebTests, OperationsWebTests, QdosTriageIntegrationTests, and
  TriageQueuesWebTests.
- SIMPLI-011 created and proved CaseMutationPageModel, then deferred this
  consolidation to PLAT-002. Reuse it; do not reopen the Case Details split.
- This is internal Web composition cleanup. No PRD, FRD, ADR, Core policy,
  database, Infrastructure, Azure/live state, deployment, or product behaviour
  changes.

## Fresh-base reconciliation (execution)

- Execution started from `origin/dev` at `bc0646a6`, not the earlier survey SHA.
- The total remained 20 actor-owning files and 26 calls, but merged work had
  introduced `UploadConfirmationPageModel` as the shared base for UploadStatus
  and UploadGroupStatus, and `Intake/Image.cshtml.cs` as an actor caller.
- The executable shape is therefore three existing bases plus 17 direct actor
  callers. Reusing UploadConfirmationPageModel avoids editing its two concrete
  descendants and is simpler than the earlier file map; scope and behaviour are
  unchanged.

## Implications

Create one metadata-free StaffPageModel owning claim-to-actor translation and
operation-key generation. Migrate both bases, then all 18 direct actor callers.
Keep anonymous Uploads/Request on PageModel while reusing the static operation
key. Keep Upload's receipt token local as a different concept. Add narrow
ownership guards, run mapped HTTP tests, and refresh the as-built architecture.

## Open questions

None. Complete consolidation was selected on 2026-08-20; “complete” applies to
all staff-actor lookups and all operation-key generators, not unrelated values
that happen to share the same string representation.
