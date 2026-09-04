# Plan — CASE-032 (2026-09-04, gpt-5.6-terra high)

CASE-032 cannot proceed to implementation yet: both required Triage values lack
defined business semantics. Do not invent a reference format or provider display
vocabulary. The image-custody half is fully plannable; the Triage half remains
blocked pending the two operator decisions below.

**Starting state:** verified at `80f0ca26`. EPIC-011 §1.4 requires
image `ref·reg, files·custody` and Triage `ref·reg, provider·assignee`.
`ProjectAsync` and `EfTriageStore.ListAsync` currently omit those values; the
page has no additional query port suitable for per-row lookup.

**Governing docs:** EPIC-011 §1.4 defines the row shape; FRD-03 defines
Triage's supported origins; `docs/design/README.md` binds exact labels,
no explanatory copy, and absent-versus-disabled behaviour.

1. **Resolve the two Triage projection contracts before coding that half.**
   Reuse `ICreateTriageFromIntake`/`ITriageStore` and the persisted
   `TriageEntity.OriginReceiptId` only as evidence trails; neither currently
   provides an operator-facing Triage reference or a provider display value.
   Record the operator's selected authoritative source, immutable/reference
   rule, and absent-value rule in the ticket's open questions, then refresh
   this plan. No code file is touched in this step.

   The current `MailRouteSelection.WorkProviderCode` is a route code, not a
   universal provider display name, and it does not cover Provider API or
   manual Triage. If the answer requires new persisted values, that is a named
   schema/migration dependency and must be separately authorised; do not
   silently expand this projection-only ticket.

2. **Expose image custody through the existing image-summary projection.**
   Reuse `ImageIntakeSummary` and `EfImageIntakeStore.ProjectAsync`, which is
   already shared by every image summary list path. Add a Core-facing,
   non-presentation custody value to the summary; extend the existing EF
   projection's single select to materialize `ImageIntakeEntity.CustodyState`
   and map it to that value. Preserve the existing query shape — no additional
   PageModel call and no per-row custody lookup.

   Touch only:

   - `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs`
   - `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs`

   Named dependency, not a step: the existing Infrastructure-only
   `ImageCustodyStates` list is currently used by other persistence writers.
   If promoting its vocabulary to Core is necessary to keep one vocabulary,
   coordinate ownership of
   `src/Pegasus.Infrastructure/Persistence/ImageIntakeEntities.cs` and its
   existing consumers; do not duplicate state literals. No migration is
   expected because `CustodyState` already exists.

3. **Carry the extended image summary through existing constructors and render
   the row.** Reuse `Search/Index`'s exact-reference reconstruction and the
   existing `ImageRow` builder. Preserve every summary member, render the
   existing file-count text plus the custody label as the row's meta, and add
   the sole image-custody label mapping to `OperatorLabels`. The Web page must
   receive a Core value, never persistence literals or a computed placeholder.

   Touch only:

   - `src/Pegasus.Web/Pages/Search/Index.cshtml.cs`
   - `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs`
   - `src/Pegasus.Web/Presentation/OperatorLabels.cs`

4. **After Step 1 is resolved, extend the Triage list projection and row.**
   Reuse `TriageSummary`, `EfTriageStore.ListAsync`, `IListTriage`/`ListTriage`,
   `LoadTriageAsync`, and `TriageRow`. Add the operator-approved reference and
   provider values to the Core summary, fold their authoritative retrieval into
   the existing list read, then render `ref·reg` as the title and
   `provider·assignee` as the meta. Do not add a Web display string, a second
   query path, or per-row origin lookups.

   Touch only after the source rules are approved:

   - `src/Pegasus.Core/Triage/TriageContracts.cs`
   - `src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs`
   - `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs`

   If the approved source is not already available from the existing list
   read, stop and name the required persistence/migration work as a dependency
   rather than broadening this step.

5. **Prove populated rendered halves with the existing queue web test.**
   Reuse `TriageQueuesWebTests`, `IntakeWebApplicationFactory`,
   `RegisterImageIntakeAsync`, and the DI-resolved
   `ICreateTriageFromIntake` port. Extend the existing image-row test to seed
   and assert its custody label. After Step 1, add one Triage-row scenario
   seeded through the approved source path and assert reference, registration,
   provider, and assignee individually. No fitting Triage fixture exists in
   this test class, so add only the minimum local setup needed for the approved
   source.

   Touch only:

   - `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`

   Update the two listed Core test helpers only if their positional summary
   construction no longer compiles:

   - `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeCasePairingTests.cs`
   - `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs`

**Acceptance conditions**

- The image row renders the queried file count and queried Core custody value.
- The custody state has one Core-owned vocabulary and one
  `OperatorLabels` mapping; no raw persistence state is emitted by Web.
- The image path remains `ProjectAsync` plus the existing image-count read;
  no new PageModel/store call or row-specific custody lookup is added.
- Once the operator answers both questions, Triage renders all four halves
  from `TriageSummary`, using one existing list projection and no N+1 path.
- `TriageQueuesWebTests` proves each rendered populated value.
- No packages, new query types, pages, services, or migrations are added
  unless the operator explicitly chooses a new persisted Triage value.
- Keep labels concise; add no explanatory copy, disabled placeholder, or
  alternate state vocabulary. Core owns the contract/policy.

**Local commands**

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build
dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~TriageQueuesWebTests"
```

GitHub CI, not this plan, runs the full integration and browser suites.

**Stop condition:** after both operator answers are recorded, the scoped changes
and commands pass, the implementation report is written, and a PR labelled
`Kanmer: CASE-032` is open against `dev`; move the ticket to Review. Until the
answers arrive, keep it Preparing and do not ship only the image half under this
two-half ticket.
