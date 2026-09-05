# Review record — CASE-042 (PR https://github.com/collisionengineers/pegasus/pull/663)

Reviewed 2026-09-05 at head `92daafe2890dc73fef8a64a3bcd1c0a8a51ebbcf`
(branch `task/case-042-awaiting-instruction-queue`), which matches the head
named for review — the branch did not move. Detached review worktree at
`.worktrees/case-042-review`.

- Implementer: gpt-5.6-sol (medium).
- Independent read: gpt-5.6-terra, effort xhigh, read-only in the review
  worktree.
- Dispositions, verification and gating: Claude Opus (this record).

## Verdict

**REQUEST CHANGES.** One blocker: the success path of the ticket's single new
control ends on a 404. Both reviewers reached it independently, from the code,
and the shipped test does not catch it. Everything else in the change is sound
— the R-7 single-predicate split, the R-1 every-row-selectable link, the
no-N+1 read, the repurposed tests and the scoping are all confirmed correct.

## Findings and dispositions

| # | Severity | File:line | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:289` | A **successful** attach redirects to `?tab=awaiting&selected=<intakeId>` via the inherited `RedirectToSurface`, but the just-attached intake has left the awaiting list (`ListAsync(false)` excludes it once the manual association is active), so `OnGetAsync`'s `if (SelectedId is not null && selectedRow is null) return NotFound();` (`:341-343`) serves a 404 and the `TempData["Confirmation"]` success message is consumed unseen. The operator's one successful action ends on Not found. | **Fix — returned to the implementer.** Confirmed by reading the code, not the report; the branch's own `AwaitingAttachMovesTheImageIntakeToAnExistingCase` proves the row leaves the tab, which is exactly what makes the redirect 404. |
| 2 | blocker (test) | `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs:508-512` | The success test asserts only the 302 and then GETs a **different** URL (`/Cases?tab=awaiting`, no `selected=`), so it never exercises the redirect target and masks finding 1. Rule 19 — the test does not prove the claim it is written for. | **Fix — returned with finding 1.** The test must follow `response.Headers.Location`, assert 200, assert the confirmation renders, and assert the row is gone. |
| 3 | should-fix | `src/Pegasus.Web/Pages/Cases/Index.cshtml:36` | The new failure banner uses `class="alert alert--error"`. Neither class exists in `src/Pegasus.Web/wwwroot/css/site.css` (the only stylesheet `_Layout.cshtml:50` links), and the class appears nowhere else in the repository. The two pages this block was to copy render it as `class="validation-summary"` (`UploadStatus.cshtml:31`, styled at `site.css:304`). The refused-attach text renders as unstyled body copy, so R-9's "the failure must be visible" is only half met and a new one-off class was invented against "the existing convention wins". | **Fix — returned to the implementer.** One-word change inside an owned file. |
| 4 | should-fix | `src/Pegasus.Core/Operations/DashboardCounts.cs:34` | `AwaitingInstruction = 0` is an optional parameter where plan R-3 specified a required one (rule 6). | **Rejected, with reason — the deviation is correct and already recorded.** R-3 is self-contradictory: a required 5th positional parameter cannot coexist with the same plan's assertion that every existing `new(0,0,0,0)` initialiser keeps compiling. The three such call sites (`Pages/Index.cshtml.cs` — UIIMP-008's, `DashboardBoundaryTests.cs`, `QdosAllocationRecoveryTests.cs`) are all outside this ticket's owned paths, so the alternative crosses a lane boundary the plan forbids. The default is not a compatibility path: `EfDashboardQueries` always passes the value explicitly, and the placeholder initialisers legitimately mean zero. The executor documented it in the report's "Deviation recorded" section. |
| 5 | should-fix | `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:552-560`, `Index.cshtml:263-271` | Operator-facing literals (`Images`, `Received`, `Source`; `Case reference`, `Reason`, `Add to an existing case`) sit outside `OperatorLabels`. | **Rejected, with reason — this is the shipped convention on these exact surfaces.** `ImageRow` on `dev` already writes its fact keys as inline literals (`("State", …)`, `("Registered", …)`, `("Chase", …)`); the diff renames one and adds two in the identical shape. The form's field labels and button text copy `UploadGroupStatus.cshtml:66-85` verbatim in shape (`<label>Case</label>`, `<label>Reason</label>`, `Add to an existing case`). The *values* inside those facts do come from `OperatorLabels` (`ImageCustodyState`, `OfficeDate`, `SourceChannel`, `ImageChaseState`). No `OperatorLabels.cs` edit was needed and none was made. |
| 6 | nit | `plan/plan.md` "Simplification pass" finding 3 | The accepted risk calls the shared `ProjectAsync` image-count subquery "bounded"; `ListAsync` is unpaged, so the correlated count runs for every row in an unbounded result set. | **Accepted risk, wording to be corrected with the fix round.** It remains one correlated subquery, not an N+1 round trip, and the three extra callers are low-traffic; the disposition's substance holds, only "bounded" overstates it. |
| 7 | nit | `post-implementation-report.md` "PR" section | Records head `353f3da1b`; the reviewed head is `92daafe28` after the final `origin/dev` merge. | **Fix with the round — trivial.** Rule 17: the recorded SHA must be the one reviewed and merged. |
| 8 | nit | `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:24-25` | The class `<remarks>` still says the rail's Pre-Case work group is "(Triage)". | **Fix with the round — one word.** |
| 9 | nit | `src/Pegasus.Web/Pages/Cases/Index.cshtml:262-267` | The new `reference` and `reason` inputs carry no `maxlength`, where the sibling form uses 60 and 500 to match the stored columns. | **Deferred to the fix round as optional.** Server-side refusal already surfaces (once finding 3 is fixed); adding the two attributes matches the sibling form and costs nothing. |

Nothing was found against: owned-path scoping (all twelve changed files are in
the plan's Expected files, including the two `EfImageIntakeStore`/
`ImageIntakeContracts` fields taken under the plan's documented R-2 escape),
Core policy ownership, explanatory copy, a Create Case control (absent, not
inert — D50), new packages, or migrations (none — `Test-MigrationGrants.ps1`
not applicable).

## Independently verified claims

- **R-7 (one predicate for count and rows) — holds.** `EfDashboardQueries.cs:56-66`
  excludes an intake when an active manual association exists, or when no
  manual association exists and a `CaseIntakeLinks` row does. That is exactly
  `EfImageIntakeStore.CurrentCaseId` (`:978-983`) applied to the same two
  sources, and `IntakeManualAssociations` has `IntakeReceiptId` as its primary
  key (`PegasusDbContext.cs:593`, one-to-one at `:603-606`), so the projection's
  unordered `FirstOrDefault` cannot pick a different row than the count's
  `Any`. The linked-but-unmerged state is covered by
  `AwaitingCountExcludesReceiptLinkedBeforeMergeSynchronises`.
- **R-1 (every row selectable without script) — holds.** `Index.cshtml:131`
  routes `RowKind.Image` rows through `Model.Href(selected: row.Id)`; the
  full-record link stays the quick detail's Open button.
- **No N+1 — holds.** `LoadAwaitingAsync` (`:411-419`) contains no
  `ListImagesAsync` call; the count is a projection field.
- **Repurposed tests — none weakened.** All three kept their structural
  assertions and gained the split; the lifecycle-chip `Assert.Contains` became
  a `DoesNotContain` on the correct tab, as planned.
- **Snapshot artifacts — as reported.** `queues--default.html` 31,687 bytes and
  `queues--empty.html` 29,803 bytes as committed blobs (the working-tree
  figures are 461 and 426 bytes larger, exactly the CRLF checkout of 461 and
  426 lines); both begin `<!DOCTYPE html>`, both contain `Awaiting instruction`,
  neither contains `<img src="#">`. `queues--empty.html` still carries
  `class="muted">0 items` so its `StateMatches` expectation holds. No
  `catalogue.json`, `TestUiSnapshotTests.cs` or `queues--awaiting*` file was
  touched.

## Commands run in the review checkout (exit codes)

| Command | Exit | Result |
| --- | --- | --- |
| `git rev-parse HEAD` | 0 | `92daafe2890dc73fef8a64a3bcd1c0a8a51ebbcf` — matches the head under review |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors |
| `dotnet test ./tests/Pegasus.Core.Tests/... --configuration Release --no-build` | 0 | 1240 passed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/... --configuration Release --no-build` | 0 | 100 passed |
| `dotnet test ./tests/Pegasus.IntegrationTests/... --filter "FullyQualifiedName~TriageQueuesWebTests\|FullyQualifiedName~QdosAllocationRecoveryTests\|FullyQualifiedName~ImageIntakePersistenceTests"` | 0 | 39 passed |

That scope covers the change: `TriageQueuesWebTests` is the only class the diff
edits and owns every `/Cases` queue assertion including the new tab;
`QdosAllocationRecoveryTests` holds the `Assert.Equal(new(0,0,0,0), stages)`
the `CaseStageCounts` shape change could have broken;
`ImageIntakePersistenceTests` covers the `ProjectAsync` projection the two new
summary fields extend; `ArchitectureTests` enforces the Core/Infrastructure
dependency direction the new Core field and Infrastructure predicate sit
across. The full integration and browser suites are GitHub CI's gate on the
PR head, per EPIC-012 §Build policy — not duplicated here.
`scripts/Test-MigrationGrants.ps1` is not applicable: no migration.

Green local checks are not evidence against finding 1 — the shipped test walks
past the redirect target rather than following it.

## Not merged

The PR is **not merged** and CASE-042 stays in Review. Findings 1, 2 and 3 go
back to the implementer; 7 and 8 ride the same commit; 4 and 5 are rejected
with the reasons above and need no change; 6 is a wording correction in the
plan's simplification pass; 9 is optional. Re-review the new head after the
fix, then gate on CI for that head.

---

# Review record — CASE-042 (PR https://github.com/collisionengineers/pegasus/pull/663) — re-review

Re-review after the round-1 fix round.

- Head reviewed: `60c80769ffa045ba49b79a3c7115313cd67a0594` — matches the head
  named for review; the branch did not move. Detached worktree
  `.worktrees/case-042-review`, `git rev-parse HEAD` confirmed.
- Reviewers: gpt-5.6-terra (xhigh) independent read; Claude Opus dispositions,
  gate and merge.
- Round-1 head was `353f3da1b`. The only source commit since is `60c80769f`
  (three files) plus merge commit `92daafe28` bringing `origin/dev` in.

## Verdict

**REQUEST CHANGES** — one blocker remains open (finding 1), one nit rides with
it. The ticket stays in Review; not merged.

## Round-1 findings — closure at this head

| # | Round-1 finding | Closed? | Evidence |
| --- | --- | --- | --- |
| 1 | Successful attach redirected to `?tab=awaiting&selected=<intakeId>`, which 404'd because the attached row had left the queue | **Partly** | `Index.cshtml.cs:342-355` no longer 404s on `awaiting`; the redirect now resolves and the confirmation renders (proved by the strengthened test). But the fix drops the guard for *every* stale/bogus `selected` on that tab — see new finding 1. |
| 2 | The success test asserted only the 302 and GETed a different URL, masking finding 1 | **Yes** | `TriageQueuesWebTests.cs` `AwaitingAttachMovesTheImageIntakeToAnExistingCase` now follows `response.Headers.Location`, asserts `HttpStatusCode.OK`, asserts `"This was added to case {reference}."` and asserts the intake reference is gone. |
| 3 | Failure banner used `class="alert alert--error"`, which does not exist in `site.css` | **Yes** | `Index.cshtml:36` now emits `class="validation-summary"`; `site.css:304` styles `.validation-summary` with a `--danger` border and `--danger-bg` ground. |
| — | Stale `(Triage)` remark | **Yes** | `Index.cshtml.cs:25-26` now reads `Pre-Case work (Triage, Awaiting instruction)`. |
| — | `maxlength` on the attach inputs | **Yes** | `Index.cshtml:264` `maxlength="60"`, `:268` `maxlength="500"`, matching `UploadGroupStatus.cshtml`. |

## Findings at this head, with dispositions

| # | Severity | File:line | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:342-355` | The round-1 fix removes the `NotFound()` guard for the whole `awaiting` tab, not just for the post-attach redirect. Any unresolvable `?tab=awaiting&selected=<guid>` now silently renders **row 0's** quick detail instead of 404ing. That quick detail carries a *mutating* control — the "Add to an existing case" form (`Index.cshtml:257-273`), whose hidden `receiptId` is the substituted record's. The realistic path is not exotic: an operator follows a link or bookmark to image record A that a colleague attached in the meantime, and is shown record B's attach form with no signal that the record changed under them. Silent record substitution beneath a form that permanently associates evidence with a Case is exactly the wrong-record hazard the repository's product invariants exist to prevent. Other tabs are unchanged (verified). | **Fix.** Returned to the implementer. Keep the 404 guard for arbitrary stale selections and narrow the fallback to the post-attach case only — either redirect on success to `?tab=awaiting` with no `selected` (the base has already written `TempData["Confirmation"]`), or gate the fallback on the presence of the confirmation/error TempData. Add a test asserting that an arbitrary `selected` GUID on `?tab=awaiting` still returns 404. |
| 2 | blocker (raised by the independent reviewer) | `src/Pegasus.Core/Operations/DashboardCounts.cs:29-35` | `AwaitingInstruction = 0` is an optional compatibility default, where plan R-3 required a positional field with **no** default (rule 6). | **Rejected as a blocker, with reason.** Plan R-3 is self-contradictory: it demands both a required parameter placed before the optional `Complete` *and* that the four existing `new(0, 0, 0, 0)` initialisers — three of them in files the plan's *Do not modify* list forbids (`Pages/Index.cshtml.cs` — UIIMP-008, `DashboardBoundaryTests.cs`, `QdosAllocationRecoveryTests.cs`) — keep compiling untouched. Those cannot both hold in C#. The implementer took the only option that respects the lane boundary, and recorded the deviation explicitly in the post-implementation report ("Deviation recorded (packet contradiction)") and the PR description rather than silently. Production always passes the value (`EfDashboardQueries.cs:69-75`); the four defaulted call sites are placeholders whose correct value is `0`. Accepted as a recorded, honest deviation. The same reasoning covers the two defaulted `ImageIntakeSummary` parameters; `Pages/Search/Index.cshtml.cs:238` renders neither `ImageCount` nor `Source`, so no wrong value is displayed today (verified by grep). |
| 3 | nit | `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs:96-104` | The XML comment above `NotReadyAndAwaitingRailCountsMatchTheirRows` still describes Not ready as a combined formal/image queue ("the row query also lists awaiting-instruction Image Intakes"), which the test now asserts is false. | **Fix** — rides with finding 1. Rewrite the remark to describe the split-count contract the test now proves, keeping the INTK-013 citation. |

## Independent verification of the audit questions

Checked myself against the code, not taken from the reviewer:

- **Owned paths only.** `git diff --name-only origin/dev...HEAD` returns exactly
  the 12 files the plan's *Expected files* list names. No `OperatorLabels.cs`,
  `site.css`, `site.js`, `Pages/Shared/*`, `Pages/Cases/Shared/*`,
  `Pages/Index.*`, `catalogue.json`, `TestUiSnapshotTests.cs`, `scripts/*.ps1`
  or `.github/workflows/**` edit. No migration, so
  `Test-MigrationGrants.ps1` is not applicable.
- **No Create Case control** anywhere in `Index.cshtml` (D50); nothing drawn
  disabled or inert; no explanatory copy, field hint or empty-state panel added
  — the form is two labelled fields and a button.
- **Every drawn control has a named handler.** The form posts
  `asp-page-handler="Attach"`, resolved by the inherited
  `UploadConfirmationPageModel.OnPostAttachAsync`, which calls
  `IUploadCaseDecision.AttachAsync` — the leased `ILinkIntake` path — and
  surfaces `StaffAuthorizationException` as `Forbid()`. No third handler copy
  and no direct pairing-port call.
- **Count/row predicate parity (R-7).** `EfDashboardQueries.cs:56-68` counts
  `LifecycleState == AwaitingInstruction && !IntakeManualAssociations.Any(active
  on the origin receipt) && (any manual association exists || no
  CaseIntakeLinks row)`. `EfImageIntakeStore.ProjectAsync` derives
  `AssociatedCaseId` through `CurrentCaseId(:978-983)`: a manual association
  wins when present (null when `IsActive` is false), otherwise the first
  `CaseIntakeLinks` row. The two are equivalent, including the reversed
  (`IsActive == false`) association. `IntakeReceiptId` is the
  `IntakeManualAssociations` key, so the projection's unordered
  `FirstOrDefault()` cannot disagree with the count's `Any()`.
- **Chase fact retained** (`ImageRow`, R-8/FRD-02 INT-32); Received =
  `RegisteredAtUtc`; Source added; no Vehicle column; no lifecycle chip (the
  chip partial is now emitted only for a non-empty `row.Chip`).
- **No per-row `ListImagesAsync`**; the count comes from the one shared
  `ProjectAsync` subquery.
- **Tests not weakened.** The three Not-ready image tests are repurposed, not
  deleted: each keeps its structural assertions and gains the mirrored
  `DoesNotContain`/`Contains` pair on the other tab.
  `QdosAllocationRecoveryTests.cs:1272` `Assert.Equal(new(0, 0, 0, 0), stages)`
  is unedited and passes.
- **Simplification pass dispositions are honest**: two applied, one accepted as
  a named, reasoned risk (the shared `ProjectAsync` image-count subquery now
  costs three other callers), with the "bounded" wording corrected in this
  round rather than left standing.
- **Snapshot artifacts opened.** `queues--default.html` — 32,148 bytes on
  disk (31,687 with LF line endings, which is the figure the report quotes),
  begins `<!DOCTYPE html>`, contains the `Awaiting instruction` scope button,
  no `<img src="#">`. `queues--empty.html` — 30,229 bytes (29,803 LF), begins
  `<!DOCTYPE html>`, two `Awaiting instruction` occurrences, no `<img src="#">`.
  The report's byte figures are the LF counts; noted, not a finding. Neither
  `queues--*` file changed between `353f3da1b` and this head, as the report
  claims. The only `docs/design/test-ui/` change in that range came in with the
  `origin/dev` merge (`case-details--*`, CASE-041).
- **Report and checklist against the diff**: consistent, including the
  deviation section. The only stale line is checklist item 4's "required"
  wording for `AwaitingInstruction`, superseded by the report's recorded
  deviation.

## Commands and exit codes (review checkout `.worktrees/case-042-review`)

| Command | Exit | Result |
| --- | --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | restored |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors |
| `dotnet test ./tests/Pegasus.Core.Tests/... --configuration Release --no-build` | 0 | 1240 passed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/... --configuration Release --no-build` | 0 | 100 passed |
| `dotnet test ./tests/Pegasus.IntegrationTests/... --filter "FullyQualifiedName~TriageQueuesWebTests\|FullyQualifiedName~AccessibilityTests\|FullyQualifiedName~QdosAllocationRecoveryTests" -- xUnit.MaxParallelThreads=2` | 0 | 57 passed |

Why that scope covers the change: `git diff --name-only origin/dev...HEAD`
changes five source files across Core, Infrastructure and Web plus two test
files. Core and Architecture cover the `CaseStageCounts` and
`ImageIntakeSummary` contract changes and the dependency-direction invariants;
`TriageQueuesWebTests` is the only class exercising `/Cases` queue rows, counts
and the attach handler; `AccessibilityTests` renders `?tab=awaiting`; and
`QdosAllocationRecoveryTests` was added to the filter specifically to prove the
unedited `new(0, 0, 0, 0)` assertion still passes under the record change. No
migration was added, so `Test-MigrationGrants.ps1` is not applicable; the
`queues--*` captures were opened and their facts recorded above rather than
re-captured. The full suite is GitHub CI's gate, per EPIC-012 §Build policy.

## Gate

CI was **not** gated and the PR was **not** merged: finding 1 is an open
blocker. CASE-042 stays in Review pending its fix and a re-review at the new
head.

---

# Review record — CASE-042 (PR https://github.com/collisionengineers/pegasus/pull/663) — re-review

Round 3, after the round-2 fix round.

- Head reviewed: `44a5871bc1ca0d47d5aeaf00efbefda6752ad126` — matches the head
  named for review; the branch did not move. `git rev-parse HEAD` confirmed in
  a fresh detached worktree `.worktrees/case-042-review` (the leftover
  directory from round 2 held only two stray `bin/Release` DLLs and was
  removed before the worktree was created).
- Reviewers: gpt-5.6-terra (xhigh) independent read; Claude Opus dispositions
  and gate.
- Round-2 head was `60c80769f`. The only commit since is `44a5871bc`
  (`Index.cshtml.cs`, `TriageQueuesWebTests.cs`).

## Verdict

**REQUEST CHANGES — CI blocker.** The code review is clean: every round-2
finding is closed, no regression was introduced, and the independent
reviewer's one new blocker is rejected with reason below. The change does not
merge because **GitHub CI is red at this head** on an artifact this lane owns.

## Round-2 findings — closure at this head

| # | Round-2 finding | Closed? | Evidence |
| --- | --- | --- | --- |
| 1 | The round-1 fix exempted the whole `awaiting` tab from the stale-`selected` 404 guard, so an arbitrary `?tab=awaiting&selected=<guid>` silently rendered row 0's quick detail and its mutating attach form | **Yes** | `Index.cshtml.cs:342-348` now narrows the exemption to `isPostAttachRedirect` — `Queue == "awaiting"` **and** `TempData.ContainsKey("Confirmation") \|\| TempData.ContainsKey("UploadConfirmationError")`. Proved by the new `AwaitingNonexistentSelectionReturnsNotFound`, which asserts `HttpStatusCode.NotFound` for an arbitrary GUID with no prior attach. Every other tab's `NotFound()` is untouched. |
| 2 | Test remark above `NotReadyAndAwaitingRailCountsMatchTheirRows` still described Not ready as a combined formal/image queue | **Yes** | `TriageQueuesWebTests.cs:97-101` now states the split contract; the INTK-013 citation is kept. |

`TempData.ContainsKey` does not consume the entry (`ContainsKey` does not
touch `_initialKeys`, unlike `TryGetValue`/the indexer), so `_Layout.cshtml:165`
still renders and clears the confirmation. Proved empirically, not asserted:
`AwaitingAttachMovesTheImageIntakeToAnExistingCase` follows the redirect and
asserts `"This was added to case {reference}."` renders in the response body.

## Findings at this head, with dispositions

| # | Severity | Where | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | **blocker (CI)** | `docs/design/test-ui/pages/queues--empty.html` / capture procedure | CI run 33949716311 on this exact head is `failure`: eleven jobs green (`unit`, `browser`, all three `sql-integration` shards, `changes`, `documentation`, `reference-data`, `local-development-scripts`, `sql-integration-coverage`), `test-ui` red. At this head it died as `The action 'Capture and verify the Test UI snapshots' has timed out after 35 minutes` — the verify phase never ran. At the immediately preceding head `60c80769f` the same job reached verify and failed with **`Generated Test UI file is stale: pages/queues--empty.html`**. Nothing in `44a5871bc` touches a Razor page or a capture, so that drift is unresolved, not fixed. **Root cause:** `queues--empty` is filled by whichever captured `/Cases` response carries the marker `class="muted">0 items</span>` (`TestUiSnapshotTests.cs:48`), matched across the *whole* capture candidate pool. The lane regenerated with `-CaptureFilter "FullyQualifiedName~TriageQueuesWebTests\|FullyQualifiedName~CasesIndexWebTests\|FullyQualifiedName~TestUiFocusedRenderTests"`; CI captures the full suite and a different `/Cases` response wins the slot, so the committed file is not the one CI reproduces. `44a5871bc` also added `AwaitingNonexistentSelectionReturnsNotFound`, putting another `/Cases?tab=awaiting` response into the pool. | **Fix — returned to the implementer.** Re-capture `queues` at this head with a capture filter covering every class that produces a `/Cases` HTML response (or the unscoped capture), commit the regenerated `queues--empty.html`, and push. Do not change `TestUiSnapshotTests.cs`, `catalogue.json` or `scripts/*.ps1` — the artifact is wrong, not the gate. The local scoped verify passing is not evidence here: it verifies the file against the same narrow candidate pool that produced it. |
| 2 | blocker → **rejected** | `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:344` | The independent reviewer holds that gating the fallback on `TempData.ContainsKey("Confirmation")` is too generic, because `Confirmation` is also written by unrelated flows (password change), so an unrelated notice could suppress a legitimate 404 and show row 0's mutating attach form. | **Rejected as a blocker, with reason; residual risk accepted.** All three `Confirmation` writers were read: `Account/PasswordChange.cshtml.cs:147` redirects to `/Index`, `Administration/Accounts/Index.cshtml.cs:320` redirects to itself, `UploadGroupStatus.cshtml.cs:141` redirects to its own surface. Every one of those targets renders `_Layout`, which consumes the key on the immediately following request; none is `/Cases?tab=awaiting`. There is therefore **no single-session sequential path** to the described state. The only residual exposure is a cross-tab race — a second tab requesting a stale awaiting selection in the one-request window before the first tab's redirect target renders. Even then the substitution is not silent or dangerous: the quick detail renders the substituted row's own `reference·registration` as its `<h2>`, its attach form carries *that* row's `receiptId`, and an unrelated "Your password has been changed." banner is displayed beside it. No wrong-record write can occur without the operator submitting a form whose subject is named on screen. Accepted as a documented residual risk rather than a further round; the tighter form (a dedicated redirect marker keyed to the attached id) is a fair future cleanup, not a merge condition. |
| 3 | nit | `checklist/checklist.md` item 4 | It records that `CaseStageCounts` "gains a **required** `AwaitingInstruction`"; the shipped declaration is `int AwaitingInstruction = 0` (`DashboardCounts.cs:34`). | **Accepted — corrected here rather than by another round.** The deviation itself was rejected as a finding in round 2 and stands: plan R-3 is self-contradictory (a required 5th positional parameter cannot coexist with four surviving `new(0,0,0,0)` call sites, three of them in files this ticket may not touch). The report's "Deviation recorded (packet contradiction)" section states the truth; the checklist line is stale wording against an honest record, not a concealed deviation. |
| 4 | nit | `src/Pegasus.Web/Pages/Cases/Index.cshtml:257` | The attach form's hidden `id` is `@Model.SelectedId`, which is `null` in the post-attach fallback state, so a *second* attach started from that state posts `id=""` → `Guid.Empty` and redirects to `?tab=awaiting&selected=00000000-0000-0000-0000-000000000000`. That selection is also unresolvable, hits the same `isPostAttachRedirect` fallback and renders the confirmation, so behaviour is correct — the URL is merely ugly. `QuickDetail` carries no row id of its own, only `OriginReceiptId`. | **Accept risk.** Cosmetic; no incorrect record is acted on (`receiptId` is always the rendered row's). A future ticket can carry the row id on `QuickDetail`. |
| 5 | nit | `docs/design/test-ui/pages/queues--empty.html` | The `queues--empty` exemplar moved from the Not ready tab to the Awaiting tab, so it no longer renders the `filter-bar` (Principal / Missing selects). `queues--default` never carried it. Neither committed `/Cases` capture now covers the Not-ready filter bar. The catalogue branch ("the selected tab query succeeds and its result collection is empty") and the `StateMatches` marker both still hold, so this is coverage loss, not a stale state. | **Defer to [[UIIMP-014]]**, which owns `/Cases` snapshot states this wave and is already adding the `queues--awaiting` populated and empty states. Recorded here so that lane can restore filter-bar coverage when it lays out the `/Cases` state set. |

## Independently verified — no finding

Checked against the code at this head, not taken from the reviewer or the
report:

- **Owned paths only.** `git diff --name-only origin/dev...HEAD` returns
  exactly the twelve files the plan's *Expected files* list names. No
  `OperatorLabels.cs`, `site.css`, `site.js`, `Pages/Shared/*`,
  `Pages/Cases/Shared/*`, `Pages/Cases/Details.*`, `Pages/Index.*`,
  `catalogue.json`, `TestUiSnapshotTests.cs`, `scripts/**` or
  `.github/workflows/**` edit. No migration, so `Test-MigrationGrants.ps1` is
  not applicable and no grants/census diff is owed.
- **R-7 count/row predicate parity — holds, re-derived.**
  `EfDashboardQueries.cs:56-66` counts `LifecycleState == AwaitingInstruction`
  and no active `IntakeManualAssociations` row on the origin receipt, and
  (a manual association row exists, or no `CaseIntakeLinks` row exists).
  `ProjectAsync` derives `AssociatedCaseId` via `CurrentCaseId`
  (`EfImageIntakeStore.cs:978-983`): no manual row → the first
  `CaseIntakeLinks` row; manual row active → its `CaseId`; manual row inactive
  → `null`. Case-by-case the two agree, including the reversed-association and
  linked-but-unmerged states. `IntakeManualAssociations` is configured
  one-to-one on `IntakeReceiptId` (`PegasusDbContext.cs:603-606`), so the
  projection's unordered `FirstOrDefault()` cannot disagree with the count's
  `Any()`. `AwaitingCountExcludesReceiptLinkedBeforeMergeSynchronises` proves
  the interesting direction: with an active manual association inserted
  directly by SQL, the count and the rendered `row-button` count are both 0 —
  under the pre-split `MergedIntoCaseId is null` predicate the count would have
  been 1.
- **R-1 every row selectable without script — holds.** `Index.cshtml:131`
  routes only `RowKind.Image` rows through `Model.Href(selected: row.Id)`.
  `LoadAwaitingAsync` reads `ListAsync(false, …)`, ordered
  `OrderByDescending(CreatedAtUtc)` (`EfImageIntakeStore.cs:660`), so the
  first-registered intake in
  `AwaitingSecondRowSelectionShowsThatRowsQuickDetailWithoutScript` is
  genuinely the *second* row — the test is not passing on the `Rows[0]`
  fallback, and it additionally asserts the row markup emits `selected=`.
- **Every drawn control has a named handler; nothing inert.** The form posts
  `asp-page-handler="Attach"` to the inherited
  `UploadConfirmationPageModel.OnPostAttachAsync`, which calls
  `IUploadCaseDecision.AttachAsync` (the leased `ILinkIntake` path) and turns
  `StaffAuthorizationException` into `Forbid()`. `IndexModel` derives from that
  base and supplies only `RedirectToSurface` — no third handler copy, no direct
  pairing-port call. No Create Case control exists anywhere in `Index.cshtml`
  (D50). No disabled control, no explanatory copy, no field hint, no
  empty-state panel: the form is two labelled fields and one button.
- **Failure is visible.** `Index.cshtml:34-37` renders
  `TempData["UploadConfirmationError"]` as `class="validation-summary"`;
  `.validation-summary` is real and styled in `site.css`.
  `AwaitingAttachFailureIsVisibleAndLeavesTheRowInPlace` asserts both the
  message and that the row is still listed.
- **No labels-file violation.** The tab literal `new("awaiting", "Awaiting
  instruction", PreCaseGroup, "icon-image")` sits beside the shipped
  `new("triage", "Triage", …)` and `new("unidentified", "Unidentified", …)`,
  under the comment that allows a record kind's own settled name. The fact
  *values* still come from `OperatorLabels` (`ImageCustodyState`, `OfficeDate`,
  `SourceChannel`, `ImageChaseState`).
- **Chase fact retained** (R-8 / FRD-02 INT-32); Received = `RegisteredAtUtc`;
  Source added; no Vehicle; no lifecycle chip — `ImageRow` passes an empty
  `Chip` and `Index.cshtml:132-135` emits the chip partial only when non-empty.
- **No N+1.** `LoadAwaitingAsync` contains no `ListImagesAsync`; the count is
  one correlated subquery inside the shared `ProjectAsync`.
- **Tests prove the claim; none weakened.** The three Not-ready image tests are
  repurposed with mirrored `Contains`/`DoesNotContain` pairs on the two tabs;
  the lifecycle-chip assertion became a `DoesNotContain` on the awaiting tab as
  planned. `QdosAllocationRecoveryTests.cs:1272`
  `Assert.Equal(new(0, 0, 0, 0), stages)` is unedited and passes. Six new tests
  were added. `AwaitingAttachFailureIsVisibleAndLeavesTheRowInPlace` exercises
  the missing-reason refusal rather than the unknown-reference one; the plan
  wrote "unknown reference, **or** a missing reason", so this satisfies R-9.
- **Report and checklist against the diff:** consistent, including the frank
  "Deviation recorded (packet contradiction)" section and both prior fix
  rounds. Snapshot byte figures (31,687 / 29,803) match the committed blobs
  exactly; the larger working-tree sizes are the CRLF checkout. Both captures
  begin `<!DOCTYPE html>` (the report writes it lower-case), both contain the
  `Awaiting instruction` scope button, neither contains `<img src="#">`, and
  `queues--empty.html` still carries `class="muted">0 items` so its
  `StateMatches` expectation holds. The one stale line is checklist item 4
  (finding 3).
- **Simplification-pass dispositions are honest:** two applied, one accepted as
  a named risk (the shared `ProjectAsync` image-count subquery now costs three
  other callers), with the earlier "bounded" overstatement corrected in place
  rather than left standing.

## Commands and exit codes (review checkout `.worktrees/case-042-review`)

| Command | Exit | Result |
| --- | --- | --- |
| `git rev-parse HEAD` | 0 | `44a5871bc1ca0d47d5aeaf00efbefda6752ad126` — the head under review |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | restored |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors |
| `dotnet test ./tests/Pegasus.Core.Tests/... --configuration Release --no-build` | 0 | 1240 passed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/... --configuration Release --no-build` | 0 | 100 passed |
| `dotnet test ./tests/Pegasus.IntegrationTests/... --filter "FullyQualifiedName~TriageQueuesWebTests\|FullyQualifiedName~QdosAllocationRecoveryTests\|FullyQualifiedName~ImageIntakePersistenceTests\|FullyQualifiedName~AccessibilityTests" -- xUnit.MaxParallelThreads=2` | 0 | 66 passed |

Why that scope covers the change: the diff edits five source files across Core,
Infrastructure and Web plus two test files. `Pegasus.Core.Tests` and
`ArchitectureTests` cover the `CaseStageCounts` and `ImageIntakeSummary`
contract changes and the Core/Infrastructure dependency direction the new field
and predicate sit across; `TriageQueuesWebTests` is the only class exercising
`/Cases` queue rows, counts, selection and the attach handler;
`AccessibilityTests` renders `?tab=awaiting`; `QdosAllocationRecoveryTests`
holds the unedited `new(0, 0, 0, 0)` assertion the record change could have
broken; `ImageIntakePersistenceTests` covers the `ProjectAsync` projection the
two new summary fields extend. No migration, so `Test-MigrationGrants.ps1` is
not applicable. The two `queues--*` captures were opened and their facts
recorded above. The full suite is GitHub CI's gate per EPIC-012 §Build policy —
and it is that gate, not the local scope, that is red (finding 1).

## Gate

| Run | Head | Conclusion |
| --- | --- | --- |
| 33949716311 `repository-check` | `44a5871bc` (reviewed) | **failure** — `test-ui` timed out at 35 minutes; all eleven other jobs success |
| 33946291637 `repository-check` | `60c80769f` | failure — `test-ui`: `Generated Test UI file is stale: pages/queues--empty.html` |

**Not merged.** CASE-042 stays in Review. Finding 1 goes back to the
implementer; findings 3, 4 and 5 need no code change (3 corrected here, 4
accepted, 5 deferred to [[UIIMP-014]]); finding 2 is rejected with reason.
Re-review and gate at the new head once the regenerated `queues--empty.html`
is pushed and `test-ui` reaches its verify phase green.
