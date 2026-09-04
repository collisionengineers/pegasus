# Plan — CASE-032 (2026-09-04, gpt-5.6-terra high; corrected after plan review)

Both halves are implementable now over existing columns. The 2026-09-04 plan
review found the Triage half was wrongly blocked: the reference and provider
vocabulary the ticket body promised does exist in Core, and both open questions
are resolved from repository authority (see `open-questions/open-questions.md`).

**Starting state:** verified read-only at `80f0ca26`.
EPIC-011 §1.4 requires image rows `ref·reg, files·custody` and Triage rows
`ref·reg, provider·assignee`. `EfImageIntakeStore.ProjectAsync` omits
`ImageIntakeEntity.CustodyState`; `EfTriageStore.ListAsync` and
`GetByOriginReceiptAsync` omit the origin draft's `ClaimNumber` and
`SuggestedPrincipalCode`.

**Governing docs:** EPIC-011 §1.4 (row shape) and §1.5 (`h1 ref; reg,
provider`); `docs/operator-notes.md:219,221` ("Work Provider — Also referred to
as the principal", "Claim Number — External reference number");
`docs/design/README.md` (exact labels, no explanatory copy, absent values render
nothing). EPIC-012 §Build policy binds: parallel build, ordered merge, no local
full-suite runs, `OperatorLabels` additions in a ticket-delimited block, no
lane edits to `TestUiSnapshotTests.cs`, CI or `scripts/*.ps1`.

**No migration.** Both halves project existing columns
(`ImageIntakes.CustodyState`, `InstructionDrafts.ClaimNumber`,
`InstructionDrafts.SuggestedPrincipalCode`). No schema change, no grant change,
no bootstrap census entry.

## 1. Core image-custody vocabulary and the two image contracts

Add `public enum ImageCustodyState { Pending, Confirmed, Merged, Failed }` to
`src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs`. Add
`ImageCustodyState? Custody` to `ImageIntakeSummary`, positioned immediately
after `RegisteredAtUtc` and **before** the defaulted `State` and
`ClosureReason`, so every construction site must supply it — the file's own
comment at `Pages/Search/Index.cshtml.cs:232-237` records what a silently
defaulted member cost last time. Add the same member to `ImageIntakeDetail`.

Null means "registered before image custody existed"; it is not a fifth state.

One list per concept: the Infrastructure constants
`ImageIntakeEntities.ImageCustodyStates` stay the sole owner of the persisted
strings and become the parse/format point for the new Core enum, exactly as
`EfTriageStore.ParseState`/`ToCode` already do for `TriageState`. Do **not**
duplicate the literals into Core and do **not** touch `EfExternalWorkStore` or
`EfQueuedCustodyProcessor`, which keep using the constants unchanged.
`OperatorLabels.CustodyState(DocumentCustodyStatus)` is not reusable — that
enum has no `Merged` member, and the file's existing remark on
`UploadRequestState` records that same rule.

Touch: `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs`.

## 2. Project image custody through the existing image reads

In `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs`, select
`CustodyState` inside the existing `ProjectAsync` select (`:857-920`) and map it
to the Core enum; do the same in `ToDetailAsync` (`:844`) from the entity it has
already loaded. `ProjectAsync` is the shared projection behind `ListAsync`,
`ListByOriginReceiptsAsync`, `ListForCaseAsync` and `SearchByRegistrationAsync`,
so all four gain the value from one edit. No new query, no per-row read.

Touch: `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs`.

## 3. Render `files·custody` on the image row

Add `OperatorLabels.ImageCustodyState(ImageCustodyState)` inside a
`CASE-032`-delimited block in
`src/Pegasus.Web/Presentation/OperatorLabels.cs` — the one place operator
labels live.

In `ImageRow` (`Pages/Cases/Index.cshtml.cs:543-559`) build the meta with the
existing `Join` helper (`:628`), which already drops empty parts:

    Join($"{fileCount} retained image…", item.Custody is { } custody
        ? OperatorLabels.ImageCustodyState(custody) : null)

A pre-custody row therefore renders the file count alone — absent renders
nothing, per `docs/design/README.md`. Add the custody pair to the row's quick
detail list beside `State`, `Registered` and `Chase`.

Update `Pages/Search/Index.cshtml.cs:238-247` to pass `byReference.Custody`
into the reconstructed summary — the value Step 1 added to
`ImageIntakeDetail`, so no second query.

Touch: `src/Pegasus.Web/Presentation/OperatorLabels.cs`,
`src/Pegasus.Web/Pages/Cases/Index.cshtml.cs` (`ImageRow` only),
`src/Pegasus.Web/Pages/Search/Index.cshtml.cs`.

## 4. Add the Triage reference and provider to `TriageSummary`

Append `string? Reference` and `string? Provider` to `TriageSummary`
(`src/Pegasus.Core/Triage/TriageContracts.cs:271-278`). No defaults: the three
construction sites are updated by the compiler.

Their owners, named per the ticket's Approach:

- **Reference** — `InstructionDraft.ClaimNumber`
  (`src/Pegasus.Core/Intake/IntakeContracts.cs:382`), the operator's "Claim
  Number — External reference number".
- **Provider** — `InstructionDraft.SuggestedPrincipalCode` (`:380`), the
  operator's "Work Provider … also referred to as the principal".
  `IntakeAllocation.cs:263` already reads exactly
  `receipt.InstructionDraft?.SuggestedPrincipalCode` as the principal code, so
  this reuses Core's existing owner rather than inventing a display string.

Both are nullable because a Triage record need not carry an instruction draft
(FRD-03: manual classification invents no Principal identity). Absent values are
dropped by `Join`; no placeholder text is invented.

Touch: `src/Pegasus.Core/Triage/TriageContracts.cs`.

## 5. Project them in both `EfTriageStore` read paths

`InstructionDraftEntity` is a nullable one-to-one on `IntakeReceiptEntity`
keyed by `IntakeReceiptId` (`PegasusDbContext.cs:258-282,1421,1442-1449`), and
`TriageEntity.OriginReceiptId` is that key. Left-join `InstructionDrafts` on
`OriginReceiptId` **inside the existing query** in `EfTriageStore.ListAsync`
(`:458-481`) and select the two columns with the row — one SQL statement, no
per-row lookup.

Apply the same join in `GetByOriginReceiptAsync` (`:438-456`), the second
`TriageSummary` construction site. Leaving it null there would reproduce the
silently-defaulted-member defect this ticket exists to fix.

Touch: `src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs`.

## 6. Render `ref·reg` and `provider·assignee` on the Triage row

In `TriageRow` (`Pages/Cases/Index.cshtml.cs:560-575`) title with
`Join(item.Reference, item.NormalizedVehicleRegistration)` and meta with
`Join(item.Provider, assignee)`. Add `Reference` and `Provider` to the quick
detail list. `LoadTriageAsync` (`:417-430`) keeps supplying the assignee through
the existing `ActorDisplayNames` resolution — unchanged, no new read.

The whole `Pages/Cases/Index.cshtml.cs` diff is confined to `ImageRow`,
`TriageRow` and their two quick-detail lists. Tabs, rail, filters, selection and
the `LoadNotReadyAsync`/`LoadTriageAsync` bodies are untouched, so CASE-042 adds
its Awaiting-instruction tab after this merges without conflict.

Touch: `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs` (`TriageRow` only).

## 7. Prove every half against seeded data

In `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`:

- Extend `NotReadyImageRowRendersRetainedImageCountAndChaseState` (`:163-183`),
  which already seeds through `RegisterImageIntakeAsync` (a registration writes
  `CustodyState = ImageCustodyStates.Pending`, `EfImageIntakeStore.cs:197`).
  Assert reference, registration, file count and the custody label separately.
- Add one Triage-row test seeded through the existing `StoreMinimalReceiptAsync`
  helper, extended to persist an `InstructionDraft` carrying a claim number and
  a principal code, then `ICreateTriageFromIntake` and `IAssignTriage` for a
  resolvable assignee. Assert reference, registration, provider and assignee
  individually — four distinct assertions, not one combined string.

Query-count proof is structural, per the research document's finding that the
page has no fixed query total: assert that both new Triage values arrive from
`ListAsync`'s own projection and that no store or PageModel call was added
inside row enumeration. Introduce no query-counting fixture.

Update the two Core test helpers whose positional construction the new members
break: `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeCasePairingTests.cs`
(`Summary`) and `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs`
(`NewTriage`).

## 8. Snapshots, simplification pass, PR

`Pages/Cases/Index.cshtml.cs` is the routed `/Cases` PageModel and
`docs/design/test-ui/pages/queues--default.html` / `queues--empty.html` are its
captured states, so CLAUDE.md's regenerate-then-verify rule applies. Neither
captured state currently contains an image or Triage row, so the artifacts may
come back byte-identical — record which happened either way, with the file's
byte size and doctype, in the post-implementation report (EPIC-012 "verify the
artifact, not the gate"). Run the scoped capture UIIMP-015 delivers, using that
ticket's actual switches as merged; at `80f0ca26`
`scripts/Update-TestUiSnapshots.ps1` still accepts only `-Verify` and
`-SkipCapture`, so do not assume a scope flag exists. Then
`./scripts/Test-UiCatalogue.ps1`. Edit no script, no CI file and no
`TestUiSnapshotTests.cs`.

Run the simplification pass over the branch diff (`/simplify` plus
`code-simplifier`, or equivalent independent lenses) and record findings and
dispositions under a dated "Simplification pass" heading in this plan before
opening the PR.

## Acceptance conditions

- The image row renders the queried file count and the queried Core custody
  value; a null-custody row renders the file count alone.
- The Triage row renders all four halves — claim reference, registration,
  principal code, assignee — from `TriageSummary`.
- One Core-owned custody vocabulary; the Infrastructure constants stay the sole
  persistence-string owner; Web emits no persistence literal and no placeholder.
- Both `EfTriageStore` read paths populate the two new members; both new Triage
  values come from the existing list SQL, with no read added inside row
  enumeration and no change to the image path beyond `ProjectAsync`.
- `TriageQueuesWebTests` asserts each new half separately against seeded data.
- No package, query type, page, service, or migration is added.
- No explanatory copy; labels only in `OperatorLabels.cs`, in a CASE-032 block.

## Local commands

    dotnet restore ./Pegasus.slnx --locked-mode
    dotnet build ./Pegasus.slnx --configuration Release --no-restore
    dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build
    dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build
    dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~TriageQueuesWebTests"

Plus the scoped snapshot capture/verify and `Test-UiCatalogue.ps1` of Step 8.
GitHub CI, not this lane, runs the full integration and browser suites.

## Stop condition

The scoped changes and the commands above pass, the snapshot artifact is
inspected and recorded, the simplification pass is recorded here, the
post-implementation report is written and a PR labelled `Kanmer: CASE-032` is
open against `dev`; then move the ticket to Review. Do not merge it.

## Plan review (2026-09-04, gpt-5.6-sol xhigh; dispositions Claude Opus)

| # | Severity | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | blocker | Steps 1/4 wrongly declared the Triage half unplannable. The reference and provider owners exist: `InstructionDraft.ClaimNumber` and `InstructionDraft.SuggestedPrincipalCode`, persisted per intake receipt and reachable from `TriageEntity.OriginReceiptId`; `operator-notes.md:219,221` defines both meanings; `IntakeAllocation.cs:263` already reads the principal code. | **Fixed.** Verified independently at `80f0ca26`. Step 1 deleted; new Steps 4–6 name those owners. Both open questions resolved from repository authority — the ticket body itself said the owners already exist in Core. |
| 2 | should-fix | The Core custody type and the null-`CustodyState` render were left to implementation ("coordinate ownership if necessary"). | **Fixed.** Step 1 names `ImageCustodyState` with four members, keeps `ImageCustodyStates` as the sole persistence-string owner (the `ParseState`/`ToCode` pattern), and Step 3 renders a null row as the file count alone via `Join`. Confirmed `OperatorLabels.CustodyState(DocumentCustodyStatus)` cannot be reused: no `Merged` member. |
| 3 | should-fix | Constructor audit incomplete — `EfTriageStore.GetByOriginReceiptAsync` (`:438-456`, target-typed `new(...)`) is a third `TriageSummary` site; `ImageIntakeDetail` carries no custody, so Search could not "preserve every member". Also: prefer required members over trailing optionals. | **Fixed.** Verified both. Step 5 covers `GetByOriginReceiptAsync`; Step 1 adds custody to `ImageIntakeDetail` and places the summary member before the defaulted parameters so no call site can silently default it. |
| 4 | should-fix | Step 5 undernamed its reuse (`StoreMinimalReceiptAsync`, `IAssignTriage`) and did not require per-half assertions. | **Fixed.** Step 7 names both and requires four separate Triage assertions and four image assertions. |
| 5 | should-fix | No Test UI snapshot refresh, no `Test-UiCatalogue.ps1`, no simplification pass, despite a routed-PageModel change. | **Fixed in substance; suggested commands rejected.** Step 8 adds the refresh, the catalogue check, the artifact inspection and the simplification pass. The reviewer's `-Scope queues -CaptureFilter …` flags do not exist on `scripts/Update-TestUiSnapshots.ps1` at `80f0ca26` (only `-Verify`, `-SkipCapture`), so Step 8 defers to UIIMP-015's merged switches rather than inventing them, and records that both captured queues states currently contain no image or Triage row. |

Reviewer's closing points accepted without change: `Pages/Search/Index.cshtml.cs`
and `OperatorLabels.cs` are justified refinements of the approximate owned
paths; the `Pages/Cases/Index.cshtml.cs` diff stays inside `ImageRow` and
`TriageRow`; the focused integration filter complies with EPIC-012; no package
or abstraction is warranted. The read-only research checkout was clean after
the review run.
