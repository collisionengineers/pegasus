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

---
verdict: needs-changes
supersedes: C06 review attestation — head 556a26b1a
ticket: INTK-060
slice: C06 — current principal, organization and address directory
head: 8384e28bb
reviewed_in_worktree_at: 0be584782 (merge; C06 files byte-identical to 8384e28bb)
correction_diff: git diff c94e3dddc..8384e28bb (8 commits: 6614b8de9, 010f8131b,
  b5b1a2de3, d2440dcae, 2192f1b49, db953ffca, 0d3d76e70, 8384e28bb)
worktree: C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c06
branch: c06-directory
review_round: 2 (targeted re-review of C06-R-1…R-15)
independent: true
ownership: PASS
frozen_signatures: PASS
stop_conditions: none tripped
lanes_seen:
  - {lane: 1-build, exit: 0, result: PASS, summary: "Build succeeded. 0 Warning(s), 0 Error(s)"}
  - {lane: 2-core, exit: 0, result: PASS, summary: "Failed 0, Passed 61, Total 61"}
  - {lane: 3-integration, exit: 1, result: FAIL, summary: "Failed 2, Passed 34, Total 36"}
  - {lane: 4-host, exit: 0, result: PASS, summary: "Failed 0, Passed 32, Total 32"}
  - {lane: 5-browser, exit: 0, result: PASS, summary: "Failed 0, Passed 2, Total 2"}
  - {lane: 6-architecture, exit: 0, result: PASS, summary: "Failed 0, Passed 100, Total 100"}
findings: 1 blocker, 0 major, 4 minor (open); 12 of 15 prior findings closed
---

# C06 review attestation (round 2, superseding) — head 8384e28bb

Verdict is `needs-changes`. Twelve of the fifteen prior findings are genuinely
closed and the three deferrals are acceptable as recorded. But wave 20 lane 3
still fails 2 of 36, and both failures are the *same* defect C06-R-2 named: the
blocker was only half fixed. The residual root cause is identified below with
file and line so round 3 is not blind. Nothing else in the correction round
reads unsound; the four other open findings are minor.

## Ownership — PASS

The eight commits touch exactly 14 files, every one inside "### C06 files" or a
C06 test file already in the map: Core `Cases/ClaimSourceAdministration.cs`,
`Cases/OrganizationDirectory.cs`; Infrastructure `EfOrganizationAdministration.cs`,
`EfOrganizationDirectory.cs`, `InspectionAddressChoicesQueries.cs`; Web
`Administration/ClaimSources/Index.cshtml(.cs)`,
`Administration/Principals/EvaSubmission.cshtml(.cs)`; tests
`Pegasus.Core.Tests/Cases/OrganizationAdministrationTests.cs`, Integration
`ClaimSourceAdministrationTests.cs`, `InspectionAddressSuggestionTests.cs`,
`OrganizationAdministrationWebTests.cs`, `OrganizationDirectoryWebTests.cs`.

Verified by explicit probe that no A-owned path is in the range:
`DependencyInjection.cs`, `Migrations/*`, `*ModelSnapshot*`,
`V1FoundationEntities.cs`, `V1FoundationModelConfiguration.cs`, any `.csproj`,
`Pegasus.slnx` and `Program.cs` are all untouched. No package change;
`.worktrees/kanmer` and `kanmer-board` untouched.

## Frozen signatures — PASS

`OrganizationDirectory.cs` gains only `InspectionLocationMatchPolicy.IsExactMatch`;
`OrganizationDirectoryRecord`, `OrganizationDirectoryQuery` and
`IOrganizationDirectoryQueries` are byte-identical. `ClaimSourceAdministration.cs`
gains only a doc comment above `RequireFound`; `ClaimSourceRecord`,
`IClaimSourceAdministration` and `IClaimSourceQueries` are untouched.
`Address/InspectionAddressResolution.cs` is not modified at all. The 20-row cap
still lives once, in `MaximumResultLimit`, clamped by `ClampLimit`.

## Prior findings — per-finding disposition

| Finding | Claimed | Verified | Verdict |
|---|---|---|---|
| C06-R-1 | Fixed `6614b8de9` | Yes | **CLOSED** |
| C06-R-2 | Fixed `6614b8de9` | **No — half fixed** | **OPEN → C06-R-16** |
| C06-R-3 | Fixed `010f8131b` | Yes | **CLOSED** |
| C06-R-4 | Fixed `2192f1b49` | Yes | **CLOSED** |
| C06-R-5 | Fixed `b5b1a2de3` | Yes, with a new narrowing | **CLOSED**, see C06-R-17 |
| C06-R-6 | Deferred | Deferral sound | **ACCEPTED (deferred)** |
| C06-R-7 | Fixed `db953ffca` | Yes | **CLOSED** |
| C06-R-8 | Partly fixed `db953ffca` | Repairer deferral sound; Storage not | **PARTIAL → C06-R-19** |
| C06-R-9 | Fixed `b5b1a2de3` | Yes | **CLOSED** |
| C06-R-10 | Fixed `8384e28bb` | Yes | **CLOSED** |
| C06-R-11 | Deferred + handoff | Handoff recorded verbatim | **ACCEPTED (deferred)** |
| C06-R-12 | Documented `d2440dcae` | Yes | **CLOSED** |
| C06-R-13 | Fixed `0d3d76e70` | Yes | **CLOSED** |
| C06-R-14 | Fixed `d2440dcae` | Yes, with a new exposure | **CLOSED**, see C06-R-18 |
| C06-R-15 | Carried | Acknowledged | **ACCEPTED (carried)** |

### What was checked, not just read

- **C06-R-1 (closed).** The third constructor parameter is now
  `IUpdatePrincipalDefaultInspectionLocation? … = null` (`EvaSubmission.cshtml.cs:42`);
  the view wraps the default-location panel in `@if (Model.DefaultLocationAvailable)`;
  `OnPostUpdateLocationAsync` answers `NotFound()` first when the dependency is
  absent (`:174-183`) — a 404, never a faked save. The bridge proof really is a
  bare host: `EvaSubmissionPageRendersWithoutDefaultLocationFormWhenNoC06RegistrationsArePresent`
  uses a plain `IntakeWebApplicationFactory` with no `WithC06Adapters`, and
  `DependencyInjection.cs` at this head registers exactly one C06-adjacent line
  (`IInspectionAddressChoicesQueries`, `:346`) — `IUpdatePrincipalDefaultInspectionLocation`
  is genuinely unregistered, so the null path is exercised for real. It asserts
  200, the manual-EVA panel present, and both "Default inspection location" and
  `name="LocationOperationKey"` absent. Lane 3's `OrganizationAdministrationWebTests`
  failure has moved from line 107 (the GET) to line 135 (a POST) — independent
  confirmation the 500 is gone.
- **C06-R-3 (closed).** `EfOrganizationAdministration.cs:473-480`: the allocated
  count is hoisted above the mutation and `var before = ToSummary(entity, allocatedCaseCount);`
  is taken before the first assignment. `ToSummary` materialises a new
  `PrincipalAdministrationSummary` from the entity's current field values
  (`:790-812`), so `before` is a real prior-fact snapshot, not a live view.
  `AddHistory(..., before, result)` now matches both sibling commands. Reusing one
  count for both snapshots is correct — this command touches only `Default*` and
  `Version`.
- **C06-R-4 (closed).** Five Core tests cover the
  `Normalize(UpdatePrincipalDefaultInspectionLocationRequest)` overload (IBA clears
  label/address/postcode and the whole source triple; a physical address is
  trimmed; a physical choice without an address throws; a blank reason throws; an
  undefined `Kind` throws `ArgumentOutOfRangeException`), plus two command tests
  asserting the non-Administrator denial leaves `DefaultInspectionLocationUpdates`
  empty and that the store receives normalized values. The handoff's named output
  is asserted through the read model:
  `QdosPrincipalDefaultsToImageBasedAssessmentOnThePrincipalsIndex` slices the
  seeded QDOS `<tr>` out of the rendered Principals index and asserts it says
  "Image Based Assessment" — the right level, since that is exactly the
  null-columns ambiguity the prior finding named.
- **C06-R-5 (closed).** `InspectionAddressChoicesQueries.cs:157-173`: the prefix
  predicate is in the `Where`, `OrderByDescending(field => field.ConfirmedAtUtc)`
  precedes `Take(500)`, and the exact normalized comparison in `AddIfMatches` still
  runs afterwards. Union and dedupe rules intact and unchanged —
  `DistinctBy(choice => choice.Id)`, then exact-first, normalized name, normalized
  postcode, id, then `Take(MaximumResultLimit)` (`:212-222`). One new narrowing is
  C06-R-17.
- **C06-R-9 (closed).** The `nameQualifies` parameter is gone from `AddIfMatches`
  and all four call sites; the prefix test is unconditional (`:251-256`).
- **C06-R-10 (closed).** `InspectionLocationMatchPolicy.IsExactMatch` is the one
  owner (`OrganizationDirectory.cs:47-63`), called by both adapters, and the same
  predicate is now the first `OrderByDescending` key in SQL, before
  `Take(limit * 4)` (`EfOrganizationDirectory.cs:55-61`). The single inline
  repetition is in the SQL `ORDER BY`, documented, and safe in the direction that
  matters — the database's case-insensitive collation makes the SQL predicate a
  superset of the ordinal in-memory one, so no ordinal-exact row can be cut.
  Asserted end-to-end through `IInspectionLocationChoices` by
  `SearchRanksAnExactMatchBeforeAPrefixMatchAndCarriesTheSourceRecordIdentity`,
  which also pins item 5's `SourceRecordId`/`SourceVersion` for the directory
  fallback (`SourceRecordId ?? Id`) and the claimant identity.
- **C06-R-7 (closed).** `SearchWithNoDirectoryStillReturnsTheOtherSourcesAndInventsNoDirectoryRow`
  constructs `new InspectionAddressChoicesQueries(contextFactory)` with no directory
  and asserts a claimant row, a prior-principal row, and no `Directory` row.
- **C06-R-13 (closed).** `EditRefusesAStalePostedExpectedVersion` genuinely
  reproduces a stale write: it keeps the version the *first* GET rendered, advances
  the record through a real edit, re-GETs only for a fresh antiforgery token and
  operation key, posts the stale version, and asserts 200 with "changed after this
  page was loaded" plus a SQL check that the first edit's `Notes` and `Version = 1`
  survived.
- **C06-R-14 (closed).** `NewClaimSourceId` is minted alongside `OperationKey`,
  bound as a hidden field (`Index.cshtml:80`), passed to `SaveAsync` in place of
  `Guid.NewGuid()` (`Index.cshtml.cs:88`) and re-minted on redisplay. A replayed
  POST now carries the same `Id`, so the request hash matches and
  `FindReceiptAsync`/`ReadReplay` returns the original create. New exposure is
  C06-R-18.

### Deferrals — judged acceptable, and what each hands to whom

- **C06-R-6 (accepted).** The command, policy and store carry the full source
  triple correctly and a hardcoded `"manual"` is an honest claim, never a faked
  source link — a missing feature, not a defect, and no file for a suggestion
  picker is in this ticket's map. **Hands to:** a follow-up ticket owning the
  settings-page suggestion picker wired to `IInspectionLocationChoices` /
  `IOrganizationDirectoryQueries`. **Carry forward on acceptance:** the plan's
  expected output "reasoned repairer override … remains source-linked" is *not*
  demonstrable end-to-end in this slice and must not be signed off as delivered.
- **C06-R-11 (accepted).** `CaseContracts.cs` is B-owned and outside "### C06
  files"; the field is dead, not dangerous. The handoff is recorded verbatim on
  `scratch/c06-notes` and in the report's DI/handoff section. **Hands to:** B,
  after C06 lands — remove `EvaAutomaticSubmission` from `CreatePrincipalRequest`
  and `UpdatePrincipalEvaSubmissionRequest`.
- **C06-R-15 (accepted, carried).** Unchanged and honestly acknowledged; it is
  assumption 4, not a new defect. **Hands to:** a new ticket for an
  Administrator-maintained directory writer owning `NormalizedName`/
  `NormalizedPostcode` production values. **Consequence to carry:** the Directory
  source — including C06-R-10's ranking fix — contributes nothing in production
  until that writer exists, so it is proved by tests only.

## Open findings

### C06-R-16 — BLOCKER — C06-R-2 is only half fixed: the two OperationKey properties still implicitly-Required each other

`src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml.cs:66`
(`EvaOperationKey`) and `:89` (`LocationOperationKey`)

Wave 20 lane 3, both failures, both `Expected: Found, Actual: OK` — a 200 from
`return Page()` where a 302 was due:
`OrganizationAdministrationWebTests.AdministratorRoutesAreDiscoverableAndPostThroughCoreEfCallers:135`
(the `?handler=UpdateEva` POST) and
`OrganizationDirectoryWebTests.PrincipalSettingsPageSavesDefaultLocationAndManualEvaIndependently:89`
(the `?handler=UpdateLocation` POST).

**Root cause.** ASSUMPTION 9 on `scratch/c06-notes` fixed `EvaReason` and
`LocationReason` on a premise that is false for the other half of the pair:
"`EvaOperationKey`/`LocationOperationKey` were NOT affected — their defaults are a
freshly-generated non-empty GUID (`NewOperationKey()`), which passes the
implicit-required check even when unposted."

MVC does not validate the property's current value. In
`ParameterBinder.EnforceBindRequiredAndValidate`, when a top-level bound property
produced no binding result and its `ModelMetadata.IsRequired` is true, the
validator runs against `model: null` — the property initializer never enters the
picture. `EvaOperationKey` and `LocationOperationKey` are non-nullable `string`
`[BindProperty]` properties, so with `<Nullable>enable</Nullable>` and no
`SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` in `Program.cs`
(`:306-312` adds no such option) each carries an inferred `RequiredAttribute` and
`IsRequired = true`. Each form in `EvaSubmission.cshtml` posts only its own key
(`:32` renders `EvaOperationKey`, `:69` renders `LocationOperationKey`), so on
every POST the *other* key is unbound, validates as null, and adds a "field is
required" error. `ModelState.IsValid` is therefore false in both handlers (`:127`,
`:198`) before either handler's own checks matter, and both fall through to
`Page()`.

Symmetric, and exactly what lane 3 shows: `UpdateEva` fails on the absent
`LocationOperationKey`, `UpdateLocation` on the absent `EvaOperationKey`. It is
not a regression introduced by `6614b8de9`; `6614b8de9` fixed the GET 500
(C06-R-1) and thereby let the second test reach a POST that would always have
failed. Not DI, not the store, not `ExpectedVersion`.

**Ruled out, with reasons** (so round 3 does not re-litigate them):

- *The "form has expired" operation-key parse* (`:122`, `:191`). Each handler
  checks only its own key, and the tests post the exact `Guid.ToString("N")` value
  the page rendered, which `IsOperationKeyValid` accepts
  (`AdministrationPageModel.cs:5`).
- *`[StringLength]` on the nullable reasons.* Both permit null and the posted
  reasons are far under `MaximumReasonLength`.
- *An `ExpectedVersion` mismatch surfacing as the mutation-error branch, or the
  C06-R-3 change bumping the version before the compare.* In both failing tests
  the principal is freshly created (version 0) and the failing POST is its first
  mutation, so there is no drift. C06-R-3 moved a `COUNT` query and added a
  `before` snapshot; it did not touch
  `entity.Version = changed ? checked(entity.Version + 1) : entity.Version`.
- *Non-nullable value types.* `EvaManualSubmission` and
  `LocationIsImageBasedAssessment` are `bool` and are also unposted by the other
  form, but the inferred `RequiredAttribute` is added for non-nullable *reference*
  types only. The repo proves this in place:
  `AutomationAdministrationWebTests.StoppingAutomationDisablesTheClientRegistrationThroughTheReasonDialog:79-88`
  posts `?handler=SetEnabled` with only `TargetEnabled`, `OperationKey` and
  `Reason` — omitting the non-nullable `bool SendToAiEnabled` — and gets its 302.
  That page's design is the tell: its only two non-nullable reference-typed bound
  properties are `Reason` and `OperationKey`, and all three of its forms post both.

**Fix (pick one, and keep the keys per form).** Idempotency must stay per form —
the two keys must remain two properties so neither handler can consume the
other's key, which the current view already guarantees.

1. Declare both `string?` (keeping the `= NewOperationKey()` initializers so the
   GET still renders a fresh key), and widen
   `AdministrationPageModel.IsOperationKeyValid` to `string?` so a null key falls
   into the existing "form has expired" branch on its own handler. Smallest diff,
   consistent with how `EvaReason`/`LocationReason` were just fixed.
2. `ModelState.Remove(nameof(LocationOperationKey))` at the top of
   `OnPostUpdateEvaAsync` and `ModelState.Remove(nameof(EvaOperationKey))` at the
   top of `OnPostUpdateLocationAsync` — the repo's existing idiom
   (`Roles/Index.cshtml.cs:121`, `Access/Index.cshtml.cs:88`,
   `Accounts/Edit.cshtml.cs:70`).
3. The `Mailboxes.cshtml.cs` shape ASSUMPTION 9 already identified: one nullable
   complex `[BindProperty]` per form. Largest diff; the most durable.

Whichever is chosen, sweep *every* non-nullable reference-typed `[BindProperty]`
on this page model, not just the two named here — that is the general rule this
page has now broken twice.

### C06-R-17 — MINOR — the new prior-location SQL predicate is narrower than the normalized rule, not coarser

`src/Pegasus.Infrastructure/Persistence/InspectionAddressChoicesQueries.cs:147-163`

The C06-R-5 fix pushes `field.Value.StartsWith(rawPrefix)` into SQL, where
`rawPrefix` is `query.Prefix.Trim()`. The comment calls it "a coarse … prefix
predicate … AddIfMatches below still applies the exact normalized comparison as
the real filter". It is coarser only on case. On whitespace it is *narrower* than
the rule it pre-filters for: `NormalizeNamePrefix` collapses every interior
whitespace run to one space and trims (`OrganizationDirectory.cs:31-34`,
`:69-97`), so a stored `"12  High Street"` normalizes to `"12 HIGH STREET"` and
prefix-matches a query of `"12 High"` — but `"12  High Street".StartsWith("12 High")`
is false in SQL, so the row is filtered out before `AddIfMatches` ever sees it.
The same holds symmetrically for a query with irregular whitespace, and for a
stored value with leading whitespace or a tab. Free-text case addresses are
exactly where such values occur.

Fix: normalize the stored value in the predicate the same way (a computed or
persisted normalized column is the clean answer, and would also serve the
directory's `NormalizedName`), or widen the SQL predicate to the
whitespace-insensitive form, or state honestly in the comment that irregular
whitespace is not matched and record it as a known limit.

### C06-R-18 — MINOR — the client now supplies the new claim source's id, and a version-0 row can be overwritten by the create form

`src/Pegasus.Web/Pages/Administration/ClaimSources/Index.cshtml.cs:54, :88` and
`Index.cshtml:80`

C06-R-14's fix is correct for replay, but it moves the new row's identity into a
client-controlled hidden field, and `SaveAsync` is a single create-or-update
(assumption 1). `OnPostCreateAsync` always sends `ExpectedVersion: 0`. A claim
source created and never edited is still at `Version = 0`
(`EfClaimSourceAdministration.cs:94`), so a create POST carrying that row's id
passes `RequireCurrentVersion(0, 0)`, takes the *update* branch, and silently
overwrites the existing record while the page reports a create. No privilege
boundary is crossed — the page is Administrator-only and antiforgery-protected,
and an Administrator can already edit claim sources — so this is data integrity
and audit clarity, not escalation.

Fix: have `OnPostCreateAsync` reject a posted id that already exists (or pass a
create-only intent), or derive the id from the operation key so it is not
caller-chosen, or keep the hidden field and add a test that a create POST naming
an existing version-0 id is refused.

### C06-R-19 — MINOR — Storage-source coverage is still missing, and only the Repairer half of the deferral is justified

`tests/Pegasus.IntegrationTests/InspectionAddressSuggestionTests.cs`;
`src/Pegasus.Infrastructure/Persistence/InspectionAddressChoicesQueries.cs:134-146`

C06-R-8 asked for a repairer *and* a storage address in the union test. The
correction added the ordering/source-identity test and deferred "repairer" with a
sound reason — `EfCaseDataStore` never populates
`CaseDataProjection.Inspection.RepairerAddress`, so there is no path to seed it.
That reason does not extend to Storage: `EfCaseDataStore` does write
`CaseDataFieldNames.StorageLocation` (`:366`) and maps it back (`:450`, `:646`),
so `InspectionLocationSourceKind.Storage` is seedable and remains the one union
source with no coverage at all. Fix: seed a storage location in the union test and
assert its row, source kind and source triple; narrow the deferral note to
Repairer only.

### C06-R-20 — MINOR — the two failing tests still assert only a status code

`tests/Pegasus.IntegrationTests/OrganizationAdministrationWebTests.cs:135`,
`tests/Pegasus.IntegrationTests/OrganizationDirectoryWebTests.cs:89`

C06-R-2 asked for the redisplayed validation text to be asserted so the next
failure names its own cause. It was not done, and the consequence is exactly what
this round cost: two runs of a 56-second lane whose entire message was
`Expected: Found, Actual: OK`. Fix: on a non-redirect, read the response body and
include the `validation-summary` / `status-card--error` text in the assertion
message (or assert it directly), for both the EVA and the Location POST.

## Confirmed unchanged from round 1

Stop conditions still none tripped: no import path or spreadsheet seed assumption,
no merged role types, no second principal catalog, no external address provider or
new package, and automatic EVA is not reintroduced anywhere on C's surfaces. The
frozen seeded-principal assertions and the seeded-estate fixes are untouched by
these eight commits.

---

## C06 review attestation — round 3, superseding — head `dc24438e2`

- **verdict:** `needs-changes`
- **supersedes:** round 2 (needs-changes at `8384e28bb`)
- **head:** `dc24438e29f27cda288f8ffa657c31bc868af9b4`, worktree
  `C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c06`, branch
  `c06-directory`, tree clean
- **correction diff:** `git diff ab7108c0c..dc24438e2` — 4 commits
  (`9710ef998`, `f435148c4`, `64416c567`, `dc24438e2`)
- **independent:** true (reviewer is not the implementer)
- **ownership:** PASS with one recorded deviation (C06-R-23)
- **frozen signatures:** PASS — round 3 does not touch
  `OrganizationDirectory.cs`, `ClaimSourceAdministration.cs` or
  `InspectionAddressResolution.cs`
- **stop conditions:** none tripped
- **evidence binding:** Release binaries built 17:23:37, after the head commit
  at 17:23:20; lanes 2-6 ran `--no-build` against them
- **findings:** 2 blocker, 0 major, 3 minor open

### Lanes seen (wave 23)

| Lane | Exit | Result | Summary |
|---|---|---|---|
| 1-build | 0 | PASS | Build succeeded. 0 Warning(s), 0 Error(s) |
| 2-core | 0 | PASS | Failed 0, Passed 61, Total 61 |
| 3-integration | 1 | **FAIL** | Failed 2, Passed 36, Total 38 |
| 4-host | 0 | PASS | Failed 0, Passed 41, Total 41 |
| 5-browser | 0 | PASS | Failed 0, Passed 2, Total 2 |
| 6-architecture | 0 | PASS | Failed 0, Passed 100, Total 100 |

`pass` requires every lane PASS; lane 3 is red, so the verdict is
`needs-changes`. Both failures are new and were introduced by this round's own
commit `64416c567`. Round 2's two `Expected: Found, Actual: OK` failures are
gone, and lane 4 rose from 32 to 41 passing.

### Per-finding disposition (C06-R-16…R-20)

| Finding | Severity | Claimed | Verified | Verdict |
|---|---|---|---|---|
| C06-R-16 | Blocker | Fixed `9710ef998` | Yes; the sweep is complete | **CLOSED** |
| C06-R-17 | Minor | Fixed `64416c567` | Production fix sound and needed; its new test fails | **PARTIAL → C06-R-21** |
| C06-R-18 | Minor | Fixed `dc24438e2` | Yes | **CLOSED** |
| C06-R-19 | Minor | Fixed `64416c567` | Assertion right; the fixture breaks the test | **PARTIAL → C06-R-22** |
| C06-R-20 | Minor | Fixed `f435148c4` | Yes | **CLOSED**, see C06-R-25 |

What was checked rather than read:

- **R-16.** Both keys are `string?` keeping their `NewOperationKey()`
  initializers, and every remaining `[BindProperty]` on `EvaSubmissionModel` is
  a value type or nullable — the demanded sweep is complete. Each handler reads
  only its own key (`:127`/`:145` vs `:196`/`:218`) and each form renders only
  its own hidden field (`EvaSubmission.cshtml:34`, `:70`), so no handler can
  accept the other form's key. The `IsOperationKeyValid(string?)` widening
  changes no other page: the method body is byte-identical,
  `Guid.TryParseExact` already accepted null, and all 17 other call sites were
  enumerated — every one passes a non-nullable `string`, except
  `Accounts/Index:332` which already short-circuits on `IsNullOrWhiteSpace`.
- **R-18.** The id is now SHA-256 over the operation key's 16 GUID bytes plus a
  `"claim_source_id"u8` discriminator — the same construction as the existing
  `InspectionAddressChoicesQueries.DeterministicId`, deterministic and
  documented. Replay under the same key returns the receipt before the entity
  lookup, so no second row (proved by
  `CreateReplayWithTheSameOperationKeyNeverCreatesASecondRow`). A different key
  cannot reach an existing row's id without inverting SHA-256, and the id is no
  longer client-supplied at all. A reused key with an edited payload fails
  `SameHash` and raises `OperationConflict` rather than overwriting; no receipt
  purge or TTL exists that could reopen that window. `Guid.ParseExact` cannot
  throw — the derivation runs only inside `if (ModelState.IsValid)` after
  `IsOperationKeyValid`.
- **R-17 production half.** The predicate is genuinely evaluated in SQL (it is
  inside the server `.Where`; EF Core throws rather than client-evaluating, and
  the query ran against real SQL Server returning rows). It is also genuinely
  needed: `ISaveCase` collapses whitespace via `CaseDataOperations.Text`, but
  the intake-acceptance path does not —
  `Ext18InspectionAddressPolicy.Evaluate` takes `candidate.Value.Trim()` only
  and that value reaches `CaseDataFields.Value` verbatim through
  `CaseDataSnapshotFactory.UpsertConfirmed`. Irregular interior whitespace does
  reach production rows.

### New findings

**C06-R-21 — BLOCKER — the R-17 regression test seeds through the one path that
already collapses whitespace.**
`tests/Pegasus.IntegrationTests/InspectionAddressSuggestionTests.cs:84-108`.
Lane 3 failure 1. The collection came back holding one row whose `Address` is
`12 High Street, AB1 2CD` — one space — while the test seeded and asserts two.
`SaveEditableDataAsync` goes through `ISaveCase`, which runs
`CaseDataPolicy.Normalize` (`EfCaseDataStore.cs:161`) whose `Text(...)` helper
does `string.Join(' ', value.Split((char[]?)null, RemoveEmptyEntries))`
(`CaseDataOperations.cs:153, :256-267`), destroying the doubled space on write.
The test never exercises the branch it was written for. Not a flake, not the
predicate, not an EF translation failure. Fix: seed the `CaseDataFieldEntity`
directly through `IDbContextFactory` (`FieldName = InspectionAddress`,
`ValueKind = Confirmed`, the doubled-space `Value`, a `ConfirmedAtUtc`) — the
same technique the union test already uses for `OrganizationDirectoryEntryEntity`
— or drive the intake-acceptance path. Keep the assertion; only the fixture is
wrong.

**C06-R-22 — BLOCKER — seeding the Storage location wipes the claimant
address.**
`tests/Pegasus.IntegrationTests/InspectionAddressSuggestionTests.cs:37`,
`:354-358`. Lane 3 failure 2, at line 69 — the Claimant assertion, which passed
in every prior wave. The result holds PriorPrincipalLocation, Directory and
Storage rows and no Claimant row, and the Storage choice reports
`SourceVersion = 2`. `SaveStorageLocationAsync` posts a fresh
`CaseEditableData(StorageLocation: …)` whose every other property is null; the
helper never merges onto `current.Data`, and `EfCaseDataStore.SetConfirmed`
**removes** the existing confirmed field when the incoming value is null
(`:380-388`). The R-19 fix silently deleted the Claimant coverage beside it.
Fix: seed both fields in one `CaseEditableData`, or better, make
`SaveEditableDataAsync` merge onto `current.Data` with a `with` expression —
`SaveClaimantAddressAsync`, `SaveInspectionAddressAsync` and
`SaveStorageLocationAsync` are all partial and any two in sequence will keep
doing this. Keep both assertions.

**C06-R-23 — MINOR — a shared administration base class was edited outside
"### C06 files" without a recorded deviation.**
`src/Pegasus.Web/Pages/Administration/AdministrationPageModel.cs:5`
(`9710ef998`). The file is not in the C06 map and `AdministrationPageModel`
appears zero times in the ticket's files document, so no slice owns it — no
cross-slice conflict, and round 2 prescribed this exact edit as fix option 1,
so it is authorized in substance. But it is disclosed only as a clause in the
report's R-16 row, and recorded as a deviation nowhere. Fix: one line on
`scratch/c06-notes` recording the deviation and its authorization, plus the
file in the report's deviations list. No code change.

**C06-R-24 — MINOR — the SQL collapse is still narrower than
`NormalizeNamePrefix`, and the predicate is now non-sargable.**
`src/Pegasus.Infrastructure/Persistence/InspectionAddressChoicesQueries.cs:158-176`.
`NormalizeNamePrefix` collapses on `char.IsWhiteSpace` (U+00A0, U+000B, U+000C,
U+2000-200A included); the SQL chain replaces only `\t`, `\r`, `\n`, and SQL
`Trim()` is `LTRIM(RTRIM())`, which strips spaces only — so a stored address
carrying an NBSP, including a leading one, is still dropped before
`AddIfMatches` sees it. Four `Replace("  ", " ")` passes reduce a run of *n*
spaces to `ceil(n / 16)`, so runs of 17+ survive. Separately, seven `REPLACE`s
and an `LTRIM(RTRIM())` around `Value` make the predicate non-sargable — no
index seek is possible before the `ORDER BY … TOP 500`. Fix: normalize on write
(a persisted computed column would serve this predicate, the `ORDER BY` and the
directory's `NormalizedName` at once — an A-owned migration, so a handoff), or
state the two uncovered cases in the comment as a known limit. Do not extend the
`Replace` chain.

**C06-R-25 — MINOR — the R-20 helper is duplicated verbatim, and its message is
built on every passing assertion.**
`OrganizationAdministrationWebTests.cs:233-259` and
`OrganizationDirectoryWebTests.cs:232-258`. `DescribeValidationErrorsAsync` and
its two `[GeneratedRegex]` members are byte-identical in both files;
`IntakeWebTestSupport` is the shared file that should own them. And
`Assert.True(cond, $"…{await DescribeValidationErrorsAsync(response)}")`
evaluates the message eagerly, reading and scanning the body on every passing
call — use `if (status != Redirect) { Assert.Fail($"…"); }` instead.

### The three review questions

1. **Plan vs ticket:** nothing new. The three accepted deferrals (C06-R-6
   suggestion picker, C06-R-11 `CaseContracts.cs` handoff to B, C06-R-15
   directory writer) stand as recorded.
2. **Implementation vs plan:** no new gap; round 3 closes the last blocker
   against the plan's expected outputs. The two open blockers are test-fixture
   defects, but until C06-R-21 is fixed the "prefix matches survive irregular
   stored whitespace" behaviour is unproven.
3. **Simplification pass and honest dispositions:** mostly yes. The
   finding-to-commit table is accurate for R-16, R-18, R-19 and R-20, and the
   ASSUMPTION 9 correction on `scratch/c06-notes` is exemplary — it names the
   false premise and the exact MVC mechanism without renumbering to hide it.
   Two gaps: the R-17 row claims "Fixed … added a double-space input test" when
   that test fails and does not exercise the branch (written against the build
   gate before lane 3 ran — unverified, not dishonest, and must not be carried
   into acceptance), and the `AdministrationPageModel.cs` deviation is not
   recorded (C06-R-23). C06-R-25 shows the pass did not sweep the two new test
   helpers.

---
kind: review-attestation
verdict: pass
supersedes: C06 review attestation (round 3, superseding) — head dc24438e2
ticket: INTK-060
slice: C06 — current principal, organization and address directory
pr: "none — controller override: this slice head is reviewed in its worktree, not through a PR"
head_sha: "f1519a2f9a804018333dbca2ff5a5fd020fc9a98"
head: f1519a2f9
correction_diff: git diff dc24438e2..f1519a2f9 (2 commits: 99985b6af, f1519a2f9;
  1 file, 58 insertions, 6 deletions, tests only)
slice_diff: git diff 930440465..f1519a2f9 (27 first-parent C06 commits,
  30a5196c5 … f1519a2f9)
worktree: C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c06
branch: c06-directory
review_round: 4 (targeted re-review of C06-R-21…R-25 plus a whole-slice sanity pass)
independent: true
reviewer: "pegasus-reviewer (not the implementer)"
plan_hash: "62649b22a7e43d77"
ticket_updated: "2026-09-06T17:00:35.239Z"
board_sha: "c1949355149d977c70d7dea5df45690709a9ff1b"
expected_reviewers: []
threads_snapshot: []
ownership: PASS with one recorded deviation (C06-R-23, now disclosed)
frozen_signatures: PASS
stop_conditions: none tripped
evidence_binding: worktree HEAD f1519a2f9, tree clean (0 modified paths); head
  committed 2026-09-06T17:47:36+01:00, Release binaries written
  2026-09-06T17:49:29+01:00, wave 26 started 2026-09-06T17:57:19+01:00 and ran
  lanes 2-6 --no-build against those binaries
lanes_seen:
  - {lane: 1-build, exit: 0, result: PASS, summary: "Build succeeded. 0 Warning(s), 0 Error(s)"}
  - {lane: 2-core, exit: 0, result: PASS, summary: "Failed 0, Passed 61, Total 61"}
  - {lane: 3-integration, exit: 0, result: PASS, summary: "Failed 0, Passed 38, Total 38"}
  - {lane: 4-host, exit: 0, result: PASS, summary: "Failed 0, Passed 118, Total 118"}
  - {lane: 5-browser, exit: 0, result: PASS, summary: "Failed 0, Passed 2, Total 2"}
  - {lane: 6-architecture, exit: 0, result: PASS, summary: "Failed 0, Passed 100, Total 100"}
findings:
  - {id: C06-R-21, severity: blocker, disposition: fixed, summary: "the irregular-whitespace regression test seeded through ISaveCase, which collapses whitespace on write; now seeded at row level the way the intake-acceptance path leaves it (f1519a2f9)"}
  - {id: C06-R-22, severity: blocker, disposition: fixed, summary: "the second partial save deleted the just-confirmed claimant address; Storage and claimant are now seeded in one save and all four union assertions are intact (99985b6af)"}
  - {id: C06-R-23, severity: minor, disposition: fixed, summary: "the AdministrationPageModel.cs edit outside the C06 files map is now disclosed verbatim as a scope deviation on scratch/c06-notes and in the report"}
  - {id: C06-R-24, severity: minor, disposition: accepted-risk, summary: "the SQL pre-filter is still narrower than NormalizeNamePrefix (NBSP and other Unicode whitespace) and non-sargable", reason: "the preferred remedy is a persisted normalized column needing an A-owned migration, out of scope for a C-side correction round; the consequence is a suggestion that is not offered, never lost or mis-saved data; recorded on scratch/c06-notes as a tracked A handoff"}
  - {id: C06-R-25, severity: minor, disposition: accepted-risk, summary: "DescribeValidationErrorsAsync and its regexes are duplicated across two test files and the failure message is built on every passing assertion", reason: "test-only, follows the file's own InputTagRegex precedent, and moving it to IntakeWebTestSupport touches two further files outside this round's named scope; recorded on scratch/c06-notes for the next simplification pass"}
  - {id: C06-R-26, severity: note, disposition: accepted-risk, summary: "SaveClaimantAddressAsync and SaveInspectionAddressAsync remain partial-record helpers, so two of them called in sequence on one case would still delete the earlier confirmed field", reason: "no live instance remains, and the comment at InspectionAddressSuggestionTests.cs:36-41 warns the next author in the exact terms of the mechanism; the merge fix stays available if a third caller needs it"}
  - {id: C06-R-27, severity: note, disposition: accepted-risk, summary: "the R-21 seed writes UpsertConfirmed's fallback provenance triple, while a real accepted row usually inherits the underlying suggestion row's intake_evidence provenance", reason: "the prior-location query filters only on FieldName, ValueKind, CaseId, PrincipalId and Value and never reads provenance, so the fidelity gap cannot affect the behaviour under test"}
skill_sha256:
  - {file: "C:/Users/PGUSER/Documents/github/pegasus-worktrees/v1-intake-c06/.agents/skills/kanmer-review/SKILL.md", sha256: "5426f2e193a5aca413df78d2b8eb36f3de2903d00f3f574a409733d181e73e44"}
  - {file: "C:/Users/PGUSER/documents/github/pegasus/.agents/skills/kanmer-review/SKILL.md", sha256: "addf26c9981cefa755a9db3a1ee06383432230708641b076ee336d64a1096741"}
---

# C06 review attestation (round 4, superseding) — head f1519a2f9

Verdict is `pass`. Both round-3 blockers are genuinely fixed, and both
fixes are test-only: the two commits over `dc24438e2` touch one file,
`tests/Pegasus.IntegrationTests/InspectionAddressSuggestionTests.cs`
(58 insertions, 6 deletions). No production line moved this round, which is the
right shape — round 3's production fixes for R-17 and R-19 were already correct,
and this review re-confirmed that before accepting the fixture changes.

## C06-R-21 — FIXED (`f1519a2f9`)

Verified in four steps rather than taking the report's word:

1. **The premise is true.** `CaseDataOperations.Text` is
   `string.Join(' ', value.Split((char[]?)null, RemoveEmptyEntries))` — every
   whitespace run collapses on write, and `Normalize` routes `InspectionAddress`
   through it (`CaseDataOperations.cs:153`, `:256-267`). `ISaveCase` genuinely
   cannot produce the stored state this test needs.
2. **The seed is legal and cannot collide.** `CaseDataFields` has primary key
   `(CaseId, FieldName, ValueKind)` and check constraints on `ValueKind`,
   `ValueType`, `SourceKind`, `PolicyVersion > 0` and the confirmed-row
   `ConfirmedByActor`/`ConfirmedAtUtc` pair
   (`CaseDataModelConfiguration.cs:39-77`). The seeded row satisfies every one.
   `CloneCasesAsync` copies the case, the snapshot and the workflow but **no**
   `CaseDataFields` rows, so the prior case holds no confirmed
   `inspection_address` row for the `Add` to conflict with. Setting `CaseId`
   alone is correct: `CaseId` *is* the snapshot foreign key
   (`HasForeignKey(item => item.CaseId)`), so no navigation assignment is needed.
3. **It mirrors the intake-acceptance path.**
   `CaseDataSnapshotFactory.AddResolvedInspection` (`:295-364`) writes the
   resolved value through `UpsertConfirmed` with `SourceKind = case_acceptance`
   for `InspectionAddressResolutionState.Accepted`, `addressLabel =
   "accepted inspection address"`, `PolicyKey`/`PolicyVersion` from
   `Ext18InspectionAddressPolicy`, and `SourceIdentity =
   snapshot.OriginIntakeReceiptId.ToString("D")` — the same six values the helper
   writes — and `Ext18InspectionAddressPolicy.Evaluate` only `Trim()`s the
   candidate (`:47-56`), so irregular interior whitespace really does survive
   that path into the row.
4. **The assertion is the right one.** `AddIfMatches`
   (`InspectionAddressChoicesQueries.cs:245-282`) passes the raw `address`
   argument straight into both `Label` and `Address`; `NormalizeNamePrefix` is
   applied only inside the `StartsWith` test at `:266-269`. So
   `choice.Address == "12  High Street, AB1 2CD"` asserts the stored value
   verbatim, and the doubled space is what makes the test meaningful. The SQL
   pre-filter collapses `"12  High Street…"` to `"12 High Street…"` and compares
   it against the uppercased `namePrefix` `"12 HIGH"` under the database's
   case-insensitive collation; `AddIfMatches` then re-applies the exact rule —
   which is precisely the R-17 behaviour under proof.

The `<remarks>` block on the test and the `<summary>` on the helper both name
`Ext18InspectionAddressPolicy` and `CaseDataOperations.Text` as the reason
`ISaveCase` cannot be used, as round 3 asked.

## C06-R-22 — FIXED (`99985b6af`)

`EfCaseDataStore.SetConfirmed` (`:369-388`) removes the confirmed row whenever
the incoming value is `null`, so the root cause round 3 pinned is real. The fix
seeds `ClaimantAddress` and `StorageLocation` in one `SaveEditableDataAsync`
call (`InspectionAddressSuggestionTests.cs:42-45`), and the now-unused
`SaveStorageLocationAsync` helper is removed rather than left dead.

All four union assertions are intact and unmodified — Claimant (`:77-78`),
Storage (`:82-83`), PriorPrincipalLocation (`:84-85`), Directory (`:86-87`) —
plus the `<= 20` cap and the `DistinctBy(Id)` assertions (`:88-89`). Nothing was
weakened to make the lane pass. The prior case's inspection address is still
saved separately (`:46`), which is safe: it is a different case, so no
destructive second save on a single case remains anywhere in the file.

The implementer took the review's smaller option (seed both in one save) over
its stated "better fix" (make `SaveEditableDataAsync` merge onto `current.Data`)
and recorded that choice with its reason on `scratch/c06-notes`. Sound for a
correction round: the merge would change behaviour for every caller of all three
save helpers.

## C06-R-23 — CLOSED

The deviation is now disclosed in both required places. `scratch/c06-notes`
carries the verbatim line **"DEVIATION — `AdministrationPageModel.cs` was edited
outside the C06 files map in round 3 (commit `9710ef998`), authorized by the
round-2 review's C06-R-16 fix option 1, behaviour-neutral, not reverted"**, and
the report's `## Correction round 4` repeats it. No code change was asked for
and none was made.

## C06-R-24, C06-R-25 — open, dispositioned `accepted-risk`

Both deferrals are sound and correctly scoped:

- **R-24** (the SQL pre-filter is still narrower than `NormalizeNamePrefix` for
  NBSP and the other Unicode whitespace classes, and is non-sargable). The
  review's own preferred remedy is a persisted normalized column, which needs an
  A-owned migration — genuinely outside a C-side correction round. The
  consequence is bounded: a prior address stored with an NBSP is dropped from
  *suggestions*; nothing is lost, mis-saved or mis-attributed. It must travel to
  A as a named handoff rather than evaporate, which `scratch/c06-notes` records.
- **R-25** (duplicated `DescribeValidationErrorsAsync` and eagerly built failure
  messages) is test-only, follows the file's own `InputTagRegex` precedent, and
  touching two further test files was outside this round's named scope.

## C06-R-26 — NOTE (new, non-blocking, `accepted-risk`)

`SaveClaimantAddressAsync` and `SaveInspectionAddressAsync` remain partial-record
helpers, so a future fixture calling two of them in sequence against one case
will still silently delete the earlier confirmed field — the R-22 trap, left
armed for the next author. No live instance remains, and the comment at
`InspectionAddressSuggestionTests.cs:36-41` warns the next reader in the exact
terms of the mechanism, which is why this is a note and not a minor. The merge
fix stays available if a third caller ever needs it.

## C06-R-27 — NOTE (new, non-blocking, `accepted-risk`)

A fidelity nit in the R-21 seed. `UpsertConfirmed` prefers the provenance of a
matching underlying `fact`/`suggestion` row (`underlying?.SourceKind ??
fallbackSourceKind`, `CaseDataSnapshotFactory.cs:477-481`), and the accepted
path normally *has* one, because `AddExtractedValue` writes the suggested
inspection address first (`:244-258`). A real accepted row therefore usually
carries the extraction's `intake_evidence` provenance rather than the
`case_acceptance` / `"accepted inspection address"` / Ext18 fallback triple the
seed writes; the helper mirrors the less common branch. It changes nothing under
test — the prior-location query filters only on `FieldName`, `ValueKind`,
`CaseId`, `PrincipalId` and `Value` (`InspectionAddressChoicesQueries.cs:166-176`)
and never reads provenance — so no action is required.

## Whole-slice sanity pass — `git diff 930440465..f1519a2f9`

This is expected to be the final C06 head, so ownership and contract checks were
re-run over the whole slice rather than the round-4 delta.

**Ownership — PASS with the one recorded deviation.** The two-dot stat over that
range shows 73 files, but most arrive on the shared branch. The C06-authored set
is the 27 first-parent non-merge commits from `30a5196c5` to `f1519a2f9`, and it
touches exactly 27 files. Every one is inside "### C06 files" or is a new C06
test file (`C06AdapterRegistrations.cs`, `OrganizationDirectoryWebTests.cs`,
`OrganizationDirectoryPersistenceTests.cs`, `ClaimSourceAdministrationTests.cs`,
`InspectionAddressSuggestionTests.cs`, and the two new Core test files), except
`src/Pegasus.Web/Pages/Administration/AdministrationPageModel.cs` — the disclosed
C06-R-23 deviation.

Verified by explicit ancestry probe (`git merge-base --is-ancestor …
task/pegasus-v1-intake`) that `f81932aa0` (Triage/Details), `15518699c`
(Operations/Index, OperatorLabels), `d9c6e6ed2`, `d2b50f46e` and `2b6b5ed37` are
all shared-branch commits that entered through the four merges, not C06 edits.
Their files (`Pages/Triage/*`, `Pages/Operations/*`,
`Presentation/OperatorLabels.cs`, `MultiFormatGenuineCorpusWebTests.cs`, the
corpus and reference-data blobs) are correctly absent from the C06-authored set.

**No A-owned edit.** `Persistence/DependencyInjection.cs`,
`Persistence/Migrations/**`, `*ModelSnapshot*`, `V1FoundationEntities.cs`,
`V1FoundationModelConfiguration.cs`, `CaseDataEntities.cs`,
`CaseDataModelConfiguration.cs`, `Program.cs`, every `.csproj` and
`Pegasus.slnx` appear in no C06 commit. `.worktrees/kanmer` and `kanmer-board`
untouched; no package added.

**Frozen contracts — PASS.** Over the whole range,
`src/Pegasus.Core/Cases/OrganizationDirectory.cs` (+79) and
`src/Pegasus.Core/Cases/ClaimSourceAdministration.cs` (+132) are **purely
additive** — zero deleted lines in either — so `OrganizationDirectoryRecord`,
`OrganizationDirectoryQuery`, `IOrganizationDirectoryQueries`,
`ClaimSourceRecord`, `IClaimSourceAdministration` and `IClaimSourceQueries` are
byte-identical to `930440465`.
`src/Pegasus.Core/Address/InspectionAddressResolution.cs` is not modified at
all. The 20-row cap still lives once, in
`InspectionLocationMatchPolicy.MaximumResultLimit`, with `ClampLimit` the only
caller-facing door.

**`EvaAutomaticSubmission` not reintroduced.** No C06 surface reads or offers
it: `PlanPrincipalEvaSubmissionUpdate` forces it false and counts a persisted
`true` as a change (`OrganizationAdministration.cs:648-655`), the two
construction sites pass `false` (`:430`, `:482`), and the settings page states
the retirement in both the page model and the view. The remaining references are
the B-owned `Eva/*` files, the A-owned entity and migration column, and the two
request records on `CaseContracts.cs` that C06-R-11 already handed to B.

**The fifteen seeded principals test is present and intact.**
`FreshDatabaseSeedsExactlyTheFifteenFrozenPrincipalsOnce` in
`tests/Pegasus.IntegrationTests/OrganizationAdministrationPersistenceTests.cs`
still asserts `Assert.Equal(15, seeded.Count)` (`:468`), exactly one row per
frozen id with its exact code, and the no-`HDUK`-principal rule. Untouched by
rounds 3 and 4.

## The three review questions

1. **Did the plan miss anything the ticket implies?** Nothing new. The round-1
   answer stands, and the three accepted deferrals (C06-R-6 suggestion picker,
   C06-R-11 `CaseContracts.cs` handoff to B, C06-R-15 directory writer) are
   unchanged and still correctly recorded.
2. **Did the implementation miss anything in the plan?** No. With R-21 fixed,
   the "prefix matches survive irregular stored whitespace" behaviour R-17
   introduced is now actually proven rather than asserted against a value the
   write path had already collapsed, and the union's Claimant coverage is
   restored. Every plan output has a caller and a test.
3. **Did the simplification pass run with honest dispositions?** Yes this round.
   The report no longer claims a passing test that failed: the R-17 row is
   superseded by an explicit fixture-defect account, both blockers name their
   commit, and the two deferrals say plainly that they are deferred and why. The
   R-22 entry volunteers that it took the smaller of the two offered options and
   gives its reason. The one honest gap left is R-25 itself — the pass still has
   not swept the duplicated test helper — and it is now recorded as such rather
   than described as done.

## Confirmed unchanged from rounds 1-3

Frozen seeded-principal assertions, the seeded-estate fixes, the item 3-5
matching rules (two-character minimum, cap 20 with no caller override,
exact-before-prefix through the single `InspectionLocationMatchPolicy.IsExactMatch`
owner, `DistinctBy(Id)` dedupe, four local sources, `Active` filter, full source
triple), item 6's store behaviour including the `before` snapshot, item 7 on
every C surface, and item 8's Administrator-only, versioned, reasoned,
idempotent writes. No production file changed since `dc24438e2`.
