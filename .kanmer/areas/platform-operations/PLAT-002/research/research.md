# Research — PLAT-002: one staff-actor page root

## Question

Can the two existing Razor Page bases and named private copies share one staff-actor/operation-key owner without changing authorization or identity, and does the written scope satisfy the one-root verification?

## Findings

- Cases/CaseMutationPageModel.cs and Administration/AdministrationPageModel.cs independently resolve the same name-identifier and role claims through StaffActorFactory.TryCreate. Administration uses the tidier nullable-flow signature; the case base manually assigns null on failure. Both generate Guid N-format keys.
- The case base also owns substantial case-edit and TempData policy. A new root above it fits the existing boundary: case-only behaviour remains on CaseMutationPageModel.
- Operations/Index, Triage/Details, Cases/Assessment/Index, and Cases/Documents/Download carry explicit staff-role authorization. A metadata-free root does not change their authorization.
- Account/PasswordChange has no page attribute, but Program.cs:495-496 sets an authenticated fallback policy. Its helper also returns a staff Guid; ActionActor.SubjectId carries the same identifier and can be parsed locally after shared resolution.
- Triage/Details likewise needs both staff Guid and actor. Preserve the local subject-to-Guid parse rather than add a second shared signature.
- Cases/Documents/Download is staff-authorized; being a GET/file response is not unsafe when the root adds no endpoint metadata.
- Uploads/Request is explicitly AllowAnonymous and resolves no staff actor; it only duplicates key generation. Inheriting StaffPageModel would falsely model a public page as staff. It can call an accessible shared generator without inheriting staff semantics, or planning must find a neutral existing owner.
- The scope and verification disagree. The read-only command rg -l "StaffActorFactory.TryCreate" src/Pegasus.Web --glob "*.cs" returns 20 files. Direct inline resolutions remain in root Index, Intake, Image Intake, mail, upload/status, unidentified, case-create/index, and Triage index pages. The stated one-root rg check cannot pass when only named pages change.
- Guid.NewGuid().ToString("N") appears in eight page-model files, including extra copies in Cases/Create and Upload. The written scope also cannot establish one operation-key owner without addressing or excluding them.
- SIMPLI-011 created CaseMutationPageModel as a behaviour-preserving extraction, proved the current callers on merged dev, and explicitly deferred consolidation here. Reuse that base; do not reopen the case workspace split.
- This is an internal Web simplification. No PRD/FRD/ADR behaviour, Azure resource, deployment state, or cloud write is implicated.

## Implications

The narrow shape is a metadata-free root owning nullable TryGetActor and operation-key generation. Both existing bases and named authorized pages derive from it; pages needing staff Guid retain only parsing; the anonymous upload page consumes the static generator without inheriting staff semantics.

Planning cannot define honest acceptance until scope is decided: expand to all current Web actor/key copies, or narrow the ticket and its rg check. No Azure work belongs in either option.

## Open questions

- Expand to every current Web actor/key copy, or retain the named cleanup with narrower verification?

## Expanded-scope findings — option 1 selected 2026-08-20

The user selected complete Web-wide consolidation. Survey source: current checkout c41314d9; commands were read-only.

- The 20 files contain 27 StaffActorFactory.TryCreate calls: 7 helper owners/callers and 13 files with inline claim parsing. All 20 are Razor Page models.
- Explicitly authorized actor callers: root Index; Cases/Create, Cases/Index, Cases/Assessment, Cases/Documents/Download; ImageIntake/Details; Operations/Index; Triage/Index and Details; Unidentified/Details; Upload; UploadStatus; UploadGroupStatus.
- Fallback-authenticated actor callers: Account/PasswordChange; Intake/Details and Source; Mail/Index and Message. AdministrationPageModel and CaseMutationPageModel are abstract bases whose concrete descendants remain protected by their existing endpoint metadata/policies.
- No actor caller is anonymous. Uploads/Request is the only relevant AllowAnonymous page and contains no actor lookup, only key generation. It must not derive from the staff root.
- Actor-call shapes do not require multiple shared overloads. The shared nullable ActionActor output is sufficient:
  - ordinary callers consume the actor directly;
  - Triage/Details and Account/PasswordChange locally parse ActionActor.SubjectId to the existing Guid;
  - inline conditional callers can call TryGetActor once and retain their existing surrounding condition;
  - multiple calls within Cases/Create, Intake/Details, Mail/Message, and Unidentified/Details become repeated calls to the same inherited method, not new policy.
- Nine application page files own N-format key generation: Account/PasswordChange, AdministrationPageModel, Cases/Assessment, CaseMutationPageModel, Cases/Create, Operations/Index, Triage/Details, Upload, and anonymous Uploads/Request. Test-only generators are fixture data and are not application policy; they remain out of the application-owner rg assertion.
- A single class cannot accurately represent both staff actor resolution and anonymous key generation if named StaffPageModel. The clean boundary is:
  - StaffPageModel owns TryGetActor and derives from PageModel;
  - one neutral application key generator is callable by both StaffPageModel and Uploads/Request.
  Search found no existing neutral Web owner. The simplest concrete shape is a static NewOperationKey on a neutrally named page utility only if the plan can justify that exception to the no-Helpers rule, or make the generator public on StaffPageModel and call it statically from the anonymous page while explicitly avoiding inheritance. The latter has one owner and no extra abstraction, despite the staff-oriented name.
- Deriving the 18 concrete actor-calling models directly from StaffPageModel, and the two current abstract bases from it, is a mechanical inheritance substitution. No constructor dependency, route, handler, endpoint metadata, antiforgery, actor factory input, or failure response needs to change.
- Verification should separate the two concepts:
  - StaffActorFactory.TryCreate appears once under src/Pegasus.Web, in StaffPageModel;
  - Guid.NewGuid().ToString("N") appears once in application Pages code;
  - Uploads/Request does not derive from StaffPageModel and remains AllowAnonymous.
- The expanded surface needs broader focused tests than the original ticket named. Existing owners include ShellAndStatusPageWebTests, CaseCreateWebTests, CasesIndexWebTests, ImageIntakeWebTests, QdosIntakeWebTests/IntakeWebNegativeTests, MailWorkspaceWebTests, OperationsWebTests, QdosTriageIntegrationTests/TriageQueuesWebTests, AdministrationSearchAccountWebTests, QdosCustodialWebTests, and upload/status browser or Web tests. Architecture coverage should assert ownership and the anonymous non-inheritance fact.
- No new governing product or architecture decision is needed: this is one Web implementation owner replacing byte-equivalent claim parsing and key formatting. Current architecture documentation only needs an edit if its Web composition inventory explicitly describes the old owner.

## Expanded-scope implications

Planning may now treat all actor and key copies as one mechanical slice, grouped by caller family so failures are localized. It should introduce the root first, migrate the two bases, then migrate concrete page families while keeping each family's focused tests green. The anonymous upload page consumes only the shared static key generator.

There are no unresolved research questions. The remaining choice of exact key-generator placement is an implementation-shape decision constrained by one owner, no anonymous staff inheritance, and no unnecessary abstraction; the planner can settle it from those constraints.
