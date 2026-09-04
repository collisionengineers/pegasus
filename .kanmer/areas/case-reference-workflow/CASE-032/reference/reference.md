# Review record — CASE-032 (PR https://github.com/collisionengineers/pegasus/pull/659)

**Head reviewed:** `ed0dc6ad2b00d1299b404f06596ee0ed499ec250`
(branch `task/case-032-queue-row-projections`; `git rev-parse HEAD` in the
detached review worktree equals the head named in the task — the branch did
not move).

**Reviewers:** built by gpt-5.6-sol; independent read by gpt-5.6-terra at
xhigh (`codex exec`, exit 0); dispositions, verification and gating by Claude
Opus (this record).

**Verdict: changes-required.** Two should-fix findings stand, both confirmed
independently against the code by the dispositioning agent. Not merged. The
ticket stays in Review.

## What is right

The diff (11 files, +231/−40) stays inside the paths the files document names,
adds no package, no query type, no page, no service and no migration. The three
columns it projects (`ImageIntakes.CustodyState` — `string?`,
`InstructionDrafts.ClaimNumber`, `InstructionDrafts.SuggestedPrincipalCode`)
already exist on `dev`, so the no-migration claim holds and no grant or
bootstrap-census change is missing. `Pegasus.Core` owns the new custody
vocabulary (`ImageCustodyState`); `ImageIntakeEntities.ImageCustodyStates`
remains the sole owner of the persisted strings, and all four writers of
`ImageIntakeEntity.CustodyState` (`EfImageIntakeStore.cs:197`,
`EfExternalWorkStore.cs:543,703`, `EfQueuedCustodyProcessor.cs:977,1007`) use
those constants, so `ParseCustodyState` covers every legitimately-written value
and fails closed by throwing on anything else — errors surface, no swallow. The
`InstructionDrafts` left join is cardinality-safe (`IntakeReceiptId` is the
draft's key, a nullable one-to-one), is one statement, and adds no per-row
lookup; `ListAsync` keeps its previous ordering and filtering, and
`GetByOriginReceiptAsync` keeps `SingleOrDefault` semantics. The
`Pages/Cases/Index.cshtml.cs` diff is confined to `ImageRow`, `TriageRow` and
their two quick-detail lists, leaving CASE-042's tabs/rail/loaders untouched.
The row *metas* correctly drop absent halves through the existing `Join`
helper. `TestUiSnapshotTests.cs`, `.github/workflows/ci.yml` and `scripts/*.ps1`
are untouched, and nothing under `docs/design/test-ui/` was committed —
consistent with the report's byte-identical claim.

## Findings and dispositions

| # | Severity | File:line | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | should-fix | `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:557,574,576` | The three new quick-detail pairs pass `string.Empty` when the source value is absent, and the renderer (`Pages/Cases/Index.cshtml:216,235`) emits **every** pair unconditionally. An absent custody, reference or provider therefore draws a labelled row with a blank value, not nothing. `Reference` and `Provider` are null whenever the Triage origin carries no instruction draft — the ticket's own open-questions document says manual classification leaves them null — so this is the common case, not an edge. It contradicts `docs/design/README.md` (absent renders nothing), the ticket's own acceptance condition ("no placeholder"), and this same file's established convention: `BlockedRow` (`:609-618`) adds the `E-mail` pair only `if (handle.Length > 0)`. Nothing else in the file renders a blank fact — `assignee` falls back to the explicit state word `"Unassigned"` (`:429`), which is correct and must stay. | **Fix.** Build the two `Facts` lists conditionally, adding each of `Custody`, `Reference` and `Provider` only when its source value is non-null, exactly as `BlockedRow` already does. Do not substitute a placeholder word and do not change the `Assigned to` fallback. |
| 2 | should-fix | `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs:254` | `Assert.Contains(DevelopmentOfflineIdentity.UserName, html)` is vacuous: `development-offline-administrator` is rendered by the authenticated shell on every page (present at `docs/design/test-ui/pages/queues--default.html:119`, a page with no Triage row at all). The assertion passes whether or not `TriageRow` renders its assignee, so the fourth half of `provider·assignee` is not proved. Plan step 7 and the ticket's own Verification list require all four halves asserted individually against seeded data. The other three assertions are sound — `TRIAGE-032`, `TR32AGE` and `QDOS` are each absent from the page for any other reason (the Principal select is offered only on Case scopes, `Index.cshtml.cs:130-152`, so the seeded `QDOS` principal cannot leak in on `?tab=triage`), as is `"Storing"` in the image test. | **Fix.** Assert the assignee inside the seeded Triage row rather than anywhere in the document — e.g. scope the assertion to the row markup containing `TRIAGE-032`, or assert the rendered meta `QDOS · <assignee display name>` as one contiguous fragment. Do not weaken or delete any existing assertion. |
| 3 | should-fix (reviewer) | `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:557,574,576` | The reviewer asked for the new `"Custody"`, `"Reference"` and `"Provider"` **field captions** to move into the CASE-032 `OperatorLabels` block. | **Rejected.** `OperatorLabels` owns operator *value* vocabulary — all 75 of its members map a domain value (a state, a kind, a reason, a role) to the operator's word for it; none is a field caption. Every quick-detail caption in this file is already a literal at its use site (`State`, `Registered`, `Chase`, `Registration`, `Assigned to`, `Opened`, `Kind`, `Handle`, `Received`, `Reason`, `File`). Moving three of thirteen captions into `OperatorLabels` would create a second, partial list of a concept that file does not own — the opposite of "one list per concept" — and break the existing convention, which CLAUDE.md's simplicity rails say wins absent a recorded reason. The *state labels* the ticket added (`Storing`/`Stored`/`Merged`/`Storage failed`) are correctly centralised at `OperatorLabels.cs:467-475` inside the mandated CASE-032 block. |

## Other checks

- **Report and checklist against the diff.** All three recorded deviations are
  true of the code: the third `TriageSummary` site
  (`ReconcileUnidentifiedDestinationsTests.cs:141-149`) is fixed with named
  arguments; the EF fix is present as the
  `Expression<Func<TriageEntity, bool>>` predicate applied to the Triage side
  before the join (`EfTriageStore.cs:470-505`), and the XML comment explains
  the constraint honestly; no snapshot artifact was committed. One immaterial
  drift: the report records 1219 Core tests, this head runs 1225 — the report's
  figures pre-date the branch's final `origin/dev` merge, not a false claim.
- **Simplification pass.** Honest. The rejected finding (the CASE-032
  delimiter comments) is correctly rejected — EPIC-012's build policy mandates
  that block. The applied shared-query finding introduced a real regression,
  which the executing agent caught through a genuine failing test
  (`NotReadyRailCountMatchesRowsAcrossBothOrigins`), diagnosed and fixed rather
  than suppressed; the fix is present and the test passes at this head.
- **Explanatory copy / absent vs disabled.** No field hints, how-it-works copy
  or empty-state panels are added. The only absent-value defect is finding 1.
- **Scope.** No file outside the owned paths is touched. No tooling file is
  touched. No new package.

## Commands run in the review worktree (`.worktrees/case-032-review`, detached at the reviewed head)

| Command | Exit | Result |
| --- | --- | --- |
| `git rev-parse HEAD` | 0 | `ed0dc6ad2b00d1299b404f06596ee0ed499ec250` — matches |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | — |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors |
| `dotnet test ./tests/Pegasus.Core.Tests/... --configuration Release --no-build` | 0 | 1225 passed, 0 failed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/... --configuration Release --no-build` | 0 | 100 passed, 0 failed |
| `dotnet test ./tests/Pegasus.IntegrationTests/... --filter "FullyQualifiedName~TriageQueuesWebTests"` | 0 | 9 passed, 0 failed |
| `codex exec -m gpt-5.6-terra -c model_reasoning_effort=xhigh` (independent read) | 0 | changes-required, 3 findings |

**Why this scope covers the change.** `git diff --name-only origin/dev...HEAD`
names exactly two Core contract files, two EF stores, three Web files and four
test files. The Release build proves every construction site the new record
members broke was found (the compiler, not a default, is the audit).
`Pegasus.Core.Tests` covers the two changed Core contracts and the three
updated helpers; `Pegasus.ArchitectureTests` proves the Core/Infrastructure/Web
dependency direction the new Core enum could have broken;
`TriageQueuesWebTests` is the only class this diff adds to or changes and is
the class that renders both changed row builders end to end against a real
database. No migration was added, so `Test-MigrationGrants.ps1` does not apply;
nothing under `docs/design/test-ui/` changed, so no snapshot artifact needed
re-inspection. The full integration and browser suites are GitHub CI's gate,
not this lane's — EPIC-012 §Build policy.

**CI was not gated on and the PR was not merged**, because findings 1 and 2 are
undispositioned fixes owned by the implementer.
