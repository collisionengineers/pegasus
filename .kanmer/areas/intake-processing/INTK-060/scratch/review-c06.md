---
verdict: needs-changes
ticket: INTK-060
slice: C06 — current principal, organization and address directory
head: 556a26b1a
review_head: c94e3dddc (556a26b1a merged with C branch 15518699c)
slice_diff: git diff 930440465..556a26b1a (13 C06 commits: 30a5196c5, 485978bf1,
  0d738e6fe, bd4610a75, 1aac8416d, 622b2c3ad, f72d157de, 0ebf9bd58, d1686d7d1,
  0f3bec931, e038d5085, 9b6d774bf, 556a26b1a)
worktree: C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c06
branch: c06-directory
ownership: PASS
frozen_signatures: PASS
stop_conditions: none tripped
lanes_seen:
  - {lane: 1-build, exit: 0, result: PASS, summary: "0 Warning(s) 0 Error(s)"}
  - {lane: 2-core, exit: 0, result: PASS, summary: "Failed 0, Passed 54"}
  - {lane: 3-integration, exit: 1, result: FAIL, summary: "Failed 2, Passed 29, Total 31"}
  - {lane: 4-host, exit: 0, result: PASS, summary: "Failed 0, Passed 27"}
  - {lane: 5-architecture, exit: 0, result: PASS, summary: "Failed 0, Passed 100"}
findings: 2 blocker, 3 major, 10 minor
---

# C06 review attestation — head 556a26b1a

Verdict is `needs-changes`: wave 15 lane 3 (integration) failed 2 of 31, and both
failures are C06 defects in the repurposed Principal settings page, not fixture
noise. Everything else in the slice reads sound; the remaining findings are
correctness and coverage gaps, not rework of the design.

## Ownership — PASS

All 26 files touched by the 13 C06 commits are inside "### C06 files" plus new
C06 test files:

- Core: `Cases/OrganizationAdministration.cs`, `Cases/OrganizationDirectory.cs`,
  `Cases/ClaimSourceAdministration.cs`
- Infrastructure: `EfOrganizationAdministration.cs`, `EfClaimSourceAdministration.cs`,
  `EfOrganizationDirectory.cs`, `InspectionAddressChoicesQueries.cs`
- Web: `Principals/Create.*`, `Principals/EvaSubmission.*`, `Principals/Index.cshtml`,
  `ClaimSources/Index.*`, `ClaimSources/Edit.*`
- Tests: Core `OrganizationAdministrationTests.cs`, `OrganizationDirectoryTests.cs`,
  `ClaimSourceAdministrationTests.cs`; Integration `OrganizationAdministrationPersistenceTests.cs`,
  `OrganizationAdministrationWebTests.cs`, `OrganizationDirectoryPersistenceTests.cs`,
  `OrganizationDirectoryWebTests.cs`, `ClaimSourceAdministrationTests.cs`,
  `InspectionAddressSuggestionTests.cs`, `C06AdapterRegistrations.cs`

No A-owned file is touched by any C06 commit: `DependencyInjection.cs`,
`Migrations/*`, `PegasusDbContextModelSnapshot.cs`, `V1FoundationEntities.cs`,
`V1FoundationModelConfiguration.cs` and the grants are untouched. The
`DependencyInjection.cs` and `V1PlatformFoundation.cs` edits visible in the
two-dot range come from `2b6b5ed37` (Stream A composition patch) and `99f48a459`
(shared G13), not from C06. No `.csproj`/package change; `.worktrees/kanmer` and
`kanmer-board` untouched.

## Frozen signatures — PASS

`Cases/OrganizationDirectory.cs` and `Cases/ClaimSourceAdministration.cs` are
append-only below the frozen records/interfaces (`OrganizationDirectoryRecord`,
`OrganizationDirectoryQuery`, `IOrganizationDirectoryQueries`,
`ClaimSourceRecord`, `IClaimSourceAdministration`, `IClaimSourceQueries`) — not
one frozen line changed. `Address/InspectionAddressResolution.cs` (the frozen
`IInspectionLocationChoices`, `InspectionLocationChoice`,
`InspectionLocationChoicesQuery`) is not modified at all, and the internal
20-row cap lives once in `InspectionLocationMatchPolicy.MaximumResultLimit`,
clamped for callers by `ClampLimit` — no caller can raise it. Assumption 2
(implementing item 3 against the G1 names rather than the plan's superseded
`InspectionAddressSuggestion*` names) is the right call and is recorded.

## Stop conditions — none tripped

- Spreadsheet-workflow seed assumptions: no import path; the seed test reads
  A's migration rows only.
- Merged roles: `OrganizationDirectoryRole`, `ClaimSourceRecord` and `Principal`
  stay separate record types end to end; the directory role is a separate
  column, and ClaimSource carries no principal/route/location identity.
- Second principal catalog: none — the slice reads A's seeded `Principals`.
- External address provider: no HTTP client, no new package, no fuzzy or
  geographic term anywhere in the four new/extended source files.
- Automatic EVA reintroduced: no. C's create/index/settings surfaces and the
  Ef command hashing no longer read or offer it, and
  `PlanPrincipalEvaSubmissionUpdate` forces it false (and counts a persisted
  `true` as a change so the version moves).

Seeded principals verified against `handoffs/C-foundation-requirements.json`:
the 15 code/GUID pairs in `FreshDatabaseSeedsExactlyTheFifteenFrozenPrincipalsOnce`
match the frozen `principalSeeds` exactly (QDOS…c001 through BC…c00f), and the
test asserts count 15, each frozen id exactly once with its exact code, no
`HDUK` principal, and distinct codes and ids.

Seeded-estate fixes (dispatch item 8) — PASS.
`ReplacementDisablesAndLinksPredecessorWithoutChangingAllocatedCaseIdentity`
keeps every assertion it had: lineage id, `PredecessorId`, successor active,
predecessor inactive with `SuccessorId`, code still `QDOS`, allocated counts
1 vs 0, `Cases.PrincipalId` and `Reference` unchanged, 2 `ActionHistory` rows,
1 operation receipt, then operation-conflict and stale-version. Only the
fixture changed (seeded row instead of a fresh "QDOS" create, and
`predecessor.OrganizationId` instead of the deleted throwaway organization);
`Assert.Single` calls are all predicate-filtered, so extra seeded principals in
that organization are harmless. Assumption 7's second fix ("qdos" → "alpha" in
`CreateReplayConflictDuplicateAndBoundedProjectionsUseCoreAndEf`) is correct:
`CreatePrincipalOnceAsync` checks the code with no organization filter.

## Findings

### C06-R-1 — BLOCKER — the Principal settings page 500s on this branch

`src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml.cs:28-31`

Lane 3 failure: `OrganizationAdministrationWebTests.AdministratorRoutesAreDiscoverableAndPostThroughCoreEfCallers`
— "Expected: OK, Actual: InternalServerError" from `IntakeWebTestSupport.cs:544`,
called at `OrganizationAdministrationWebTests.cs:107` (a GET of
`/Administration/Principals/EvaSubmission/{org}/{principal}`).

`EvaSubmissionModel` takes `IUpdatePrincipalDefaultInspectionLocation` as a
required constructor dependency, and nothing registers it: the only C06-adjacent
line in `DependencyInjection.cs` is `IInspectionAddressChoicesQueries`
(line 346). The page model cannot be activated, so a pre-existing page that
returned 200 before this slice now returns 500 for every user on this branch.
Correction round 1 bridged `InspectionAddressChoicesQueries` but the report's
claim that `OrganizationAdministrationWebTests` "needed no composition — neither
resolves any of the four bridged interfaces" is wrong: that test drives this
page, and page models are activated per request, so the failure is a page 500
rather than a host startup failure.

Fix: apply the same optional-resolution bridge to the page model
(`IUpdatePrincipalDefaultInspectionLocation? updatePrincipalDefaultInspectionLocation = null`,
with the default-location form hidden or refused when it is absent), so the
pre-existing page keeps working until Stream A's registration lands; and give
`OrganizationAdministrationWebTests` `WithC06Adapters()` for the full-behaviour
proof.

### C06-R-2 — BLOCKER — neither settings form can ever save

`src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml.cs:43-70`
plus the two forms in `EvaSubmission.cshtml`

Lane 3 failure: `OrganizationDirectoryWebTests.PrincipalSettingsPageSavesDefaultLocationAndManualEvaIndependently`
at line 89 — the `UpdateLocation` POST returned 200 (redisplay) instead of 302.
DI is composed in that test, so this is validation, not resolution.

The page now carries two non-nullable pairs — `EvaReason`/`EvaOperationKey` and
`LocationReason`/`LocationOperationKey` — and each form posts only its own pair.
With nullable reference types on and no
`SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` in `Program.cs`
(line 306), MVC's implicit required validation marks the absent pair invalid, so
`if (ModelState.IsValid)` is false on every post of either form. The repo's own
multi-form precedent does the opposite: `Administration/Automation/Index.cshtml.cs:49-73`
has three handlers sharing one non-nullable `Reason`/`OperationKey` and keeps
every form-specific field nullable.

Fix (any one): share a single `Reason`/`OperationKey` pair across both forms
(the Automation convention); or declare the four properties `string?` and rely
on the `IsNullOrWhiteSpace` checks the handlers already make; or
`ModelState.Remove` the other form's two keys at the top of each handler. Also
assert the redisplayed validation text in the test so the next failure names its
own cause instead of only a status code.

### C06-R-3 — MAJOR — the default-location override does not keep the fact it replaced

`src/Pegasus.Infrastructure/Persistence/EfOrganizationAdministration.cs:493-503`

`UpdatePrincipalDefaultInspectionLocationOnceAsync` calls
`AddHistory(..., before: null, after: result)`. The two sibling update commands
in the same file both pass a real `before` snapshot
(`organization_roles_updated` at ~line 224, `principal_eva_submission_updated`
at ~line 395). Item 6 requires a staff override to carry a reason and keep both
facts; with `before: null` the audit row cannot show the default location and
source it replaced, so only the new fact survives.

Fix: capture `ToSummary(entity, allocatedCaseCount)` before mutating the entity
and pass it as `before`, exactly as the sibling commands do.

### C06-R-4 — MAJOR — item 6 has no test, and "QDOS defaults to Image Based Assessment" is unasserted

`tests/Pegasus.Core.Tests/Cases/OrganizationAdministrationTests.cs:374-403`

The stub gained `DefaultInspectionLocationUpdates` but nothing asserts it, and
nothing anywhere calls
`OrganizationAdministrationPolicy.Normalize(UpdatePrincipalDefaultInspectionLocationRequest)`.
Untested: Image Based Assessment clears label/address/postcode/source, a
physical choice requires an address, a reason is required, a non-Administrator
is denied, an undefined `Kind` is rejected. And no test asserts the plan's and
handoff's named expected output — `handoffs/C-foundation-requirements.json`:
"QDOS defaults to the Image Based Assessment location". That matters more than
usual here because Image Based Assessment is represented as *all* `Default*`
columns null (`EvaSubmission.cshtml.cs:230`, `Principals/Index.cshtml:129-133`),
which is indistinguishable from "never set": QDOS reads as Image Based
Assessment only because the migration leaves those columns null.

Fix: add Core tests for the `Normalize` overload, assert the captured
`DefaultInspectionLocationUpdates` in a command test, and add one assertion that
the seeded QDOS principal resolves to Image Based Assessment on the settings
page or the Principals index.

### C06-R-5 — MAJOR — the prior-principal-location source is nondeterministic and silently incomplete

`src/Pegasus.Infrastructure/Persistence/InspectionAddressChoicesQueries.cs:154-167`

`.Take(500)` has no `OrderBy`, and the prefix filter is applied in memory
afterwards (line 241 inside `AddIfMatches`). `CaseDataFields` is keyed
`(CaseId, FieldName, ValueKind)`, so 500 rows means 500 prior cases for the
principal; past that, which prior locations are considered varies between runs
(SQL Server guarantees no order without `ORDER BY`) and matching addresses are
dropped before they are ever compared. Items 3-4 require deterministic prefix
matches from the local sources, and QDOS will pass 500 cases.

Fix: push a coarse predicate into the query and order the bounded fetch —
`.Where(field => field.Value.StartsWith(trimmedPrefix)).OrderByDescending(field => field.ConfirmedAtUtc).Take(...)`
— then normalize and rank in memory as now.

### C06-R-6 — MINOR — a physical default can never be source-linked in production

`src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml.cs:193-195`

The request, policy and store all carry source kind/record id/version, but the
only production caller hardcodes `SourceKind: "manual", SourceRecordId: null,
SourceVersion: null`, and the form has no suggestion picker. The plan's expected
output "reasoned repairer override changes location but not CE assessment method
and remains source-linked" is therefore unreachable through the delivered
surface (the "changes location, not the CE method" half is satisfied — the
command writes only the `Default*` columns and never `InspectionMode` or any B
assessment field). Fix: let the form choose a suggestion through
`IInspectionLocationChoices`/`IOrganizationDirectoryQueries` and carry its
source triple, or record the deferral as an explicit assumption and handoff.

### C06-R-7 — MINOR — no test proves the bridge's absent-directory path

`tests/Pegasus.IntegrationTests/InspectionAddressSuggestionTests.cs` (all five
tests use `WithC06Adapters`)

The other half of the bridge proof is genuine:
`OrganizationDirectoryWebTests.PrincipalsIndexStillRendersWhenNoC06RegistrationsArePresent`
(line 135) uses a plain `IntakeWebApplicationFactory` with no composition and
asserts the page renders — verified, it really starts the host without the
registrations. But nothing exercises `SearchAsync` with `_directory == null`, so
"the other three sources still work and nothing fakes a result" is only proved
by reading. Fix: one test constructing `InspectionAddressChoicesQueries` without
the directory and asserting the claimant/repairer/storage/prior rows still come
back and no directory row is invented.

`C06AdapterRegistrations.WithC06Adapters` does mirror the DI list handed to A
exactly (the five interfaces plus both concrete registrations), including the
`RemoveAll<IInspectionAddressChoicesQueries>()` needed so both interfaces share
one instance — verified against the report's handoff section and
`DependencyInjection.cs:346`.

### C06-R-8 — MINOR — repairer, storage and the choice-level ordering are untested

`InspectionAddressChoicesQueries.cs:133-150` and `:204-213`;
`tests/Pegasus.IntegrationTests/InspectionAddressSuggestionTests.cs`

The suggestion tests cover claimant, prior-principal and directory only —
`InspectionLocationSourceKind.Repairer` and `.Storage` have no coverage.
Exact-before-prefix ordering and the name/postcode/id tiebreaks are tested only
at the directory adapter (`OrganizationDirectoryPersistenceTests.cs:78`), never
at `IInspectionLocationChoices`, and `SourceRecordId`/`SourceVersion` (item 5)
are never asserted. The dedupe assertion
(`Assert.Equal(choices.Count, distinct count)`) is trivially true after
`DistinctBy` and proves nothing. Fix: seed a repairer and a storage address in
the union test and assert order plus the full source triple.

### C06-R-9 — MINOR — an unfiltered-result path and a dead guard

`InspectionAddressChoicesQueries.cs:103-104, 152, 241-246`

`namePrefix` is always at least as long as `postcodePrefix` (collapsing interior
whitespace can never remove more characters than deleting all of it), so
`nameQualifies` is necessarily true once line 105 is passed: the `if
(nameQualifies)` guard and the `nameQualifies` parameter are dead. If it ever
were false, `AddIfMatches` skips the prefix test entirely and adds every case
address unfiltered. Fix: drop the parameter and always apply the prefix test.

### C06-R-10 — MINOR — the exact-before-prefix rule lives twice, and the bounded fetch can cut it

`EfOrganizationDirectory.cs:48-60`, `InspectionAddressChoicesQueries.cs:204-222`

The rank-exact-first rule is implemented independently in both adapters, while
the minimum length and the cap correctly live once in
`InspectionLocationMatchPolicy` — one owner per rule wants the same for the
ranking. And `EfOrganizationDirectory` fetches `Take(limit * 4)` ordered by
normalized name only, then re-ranks in memory: an exact *postcode* match whose
name sorts late is dropped before the ranking once more than 80 active entries
match the predicate. Fix: move the exact-match predicate into
`InspectionLocationMatchPolicy` and rank in the database ordering before `Take`.

### C06-R-11 — MINOR — `EvaAutomaticSubmission` still on two C administration requests

`src/Pegasus.Core/Cases/CaseContracts.cs:272-279` (`CreatePrincipalRequest`) and
`:288-296` (`UpdatePrincipalEvaSubmissionRequest`)

Item 7 asks for the field's removal from C's administration contracts. Both
request records still carry it, and the store no longer reads either
(`EfOrganizationAdministration.cs:249-253` and `:353` dropped it from the
request hash and the policy call), so a caller can set it and be silently
ignored — safe, but dead and misleading. `CaseContracts.cs` is outside
"### C06 files", so removing it needs an A/B handoff, and that handoff is in
neither the seven assumptions nor the report's DI/handoff section. Fix: record
it as an explicit handoff item beside the A-side column/store removals the plan
already assigns. (For the record: the remaining automatic-EVA surface —
`PrincipalEntity.EvaAutomaticSubmission`, `EfAutomaticEvaSubmissionStore`,
`EfEvaSubmissionModeStore`, the Worker path and the migration's `0` seed value —
is A/B-owned per the plan's C06 handoff note and correctly untouched here.)

### C06-R-12 — MINOR — `ClaimSourceAdministrationPolicy.RequireFound` is unreachable

`src/Pegasus.Core/Cases/ClaimSourceAdministration.cs:84-92`

Assumption 1's single create-or-update `SaveAsync` means the store never calls
it, so `ClaimSourceAdministrationError.ClaimSourceNotFound` can never be thrown,
yet both page models map a message for it (`ClaimSources/Edit.cshtml.cs:140`,
`Index.cshtml.cs:123`) and a unit test covers the helper. Fix: remove the helper
and the enum member, or say in the doc comment that they are reserved and
unreachable today.

### C06-R-13 — MINOR — the page-level stale-write path has no test

`tests/Pegasus.IntegrationTests/ClaimSourceAdministrationTests.cs:64-84`

The concurrency fix itself is real and correct — verified at
`ClaimSources/Edit.cshtml.cs:85-92` and `:133-135`: the posted `ExpectedVersion`
reaches the store untouched and is refreshed only on the redisplay path,
matching `Organizations/Edit.cshtml.cs`. The round-trip test would indeed have
caught the old code, because the old `LoadAsync(initializeFields: true)` also
overwrote `Name` before the save, so the renamed-row assertion at line 84 would
have failed. What is missing is a test for the behaviour the fix restores: no
test posts a *stale* `ExpectedVersion` and asserts the store refuses it, though
"stale writes fail" is a named plan expected output (only the Core
`RequireCurrentVersion` unit test covers it). A future refactor could
reintroduce the pre-save load with `initializeFields: false` and pass every
current test.

Related, and deliberately not raised as a C06 defect: the redisplay refresh of
`ExpectedVersion` and `OperationKey` is inert, because ModelState still holds
the posted values and the tag helpers render those — so a retry after a
stale-version error re-posts the stale version and the consumed key. The copied
templates do exactly the same (`Organizations/Edit.cshtml.cs:88,122`,
`Organizations/Index.cshtml.cs:87`) while other admin pages call
`ModelState.Remove(nameof(OperationKey))` (`Roles/Index.cshtml.cs:121`,
`Access/Index.cshtml.cs:88`, `Accounts/Edit.cshtml.cs:70`). C06 followed its
named template, so this belongs to the template's owner, not to this slice.

### C06-R-14 — MINOR — create is not idempotent under a replayed operation key

`src/Pegasus.Web/Pages/Administration/ClaimSources/Index.cshtml.cs:79`

The handler mints `Guid.NewGuid()` on every attempt and the request hash
includes `Id`, so a resubmitted create form (same `OperationKey`, new id) is an
`OperationConflict` rather than a replay of the original create. It fails safe —
no duplicate row — but item 8's "idempotent operation key" is not met for
create. Fix: derive the id deterministically from the operation key, or mint it
alongside the key and bind it as a hidden field.

### C06-R-15 — MINOR / carried — the directory has no writer

`src/Pegasus.Infrastructure/Persistence/EfOrganizationDirectory.cs`

`OrganizationDirectoryEntryEntity` rows, and their `NormalizedName`/
`NormalizedPostcode` values, are written only by tests. Nothing guarantees a
production row is normalized the way `InspectionLocationMatchPolicy` expects,
and the directory source contributes nothing in production. Assumption 4 records
this honestly and asks for a follow-up ticket; carrying it forward as recorded
rather than as a new defect.

## Verified good

- Items 3-5 rules implemented as specified: two-character minimum on the
  normalized prefix, internal cap 20 with no caller override, exact-before-prefix
  then name, postcode, stable id, `DistinctBy(Id)` dedupe (safe — the single
  confirmed `CaseDataFields` row per case means the deterministic
  `(caseId, role)` id cannot collide, and identical prior addresses across cases
  are grouped by value first), the four local sources unioned, `Active` filter
  on the directory, and every result carrying source kind, record id and version.
  No network, package, fuzzy or geographic inference.
- Item 6 store behaviour: writes only the `Default*` columns, forces the address
  fields null for Image Based Assessment, requires an address and a reason for a
  physical choice, expected-version and idempotent with a receipt, and never
  touches `InspectionMode` or any B assessment field.
- Item 7 on C's surfaces: no automatic control on Create, Index or the settings
  page; the policy forces it false and counts a persisted `true` as a change.
- Item 8: Administrator-only pages, expected version, reason and operation key
  on every write, disable-as-Active-toggle preserving history, no password or
  mailbox control anywhere in the two new pages, concurrency and conflict
  messages mapped for all three error states.
- Assumptions 1-7 are all recorded on `scratch/c06-notes`, each with a reason
  and rejected alternatives, and none of them compounds into a second undecided
  dependent decision.
