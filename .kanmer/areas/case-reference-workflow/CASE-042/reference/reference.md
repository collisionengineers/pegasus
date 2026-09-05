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
