# Plan — PLAT-069 (2026-09-02, gpt-5.6-terra high; revised 2026-09-03 after plan review)

## Objective

At `897db953`, remove the Operations Service health table and add the
administrator-only D37 partial-data notice. Do not create or alter the
Administration page.

## Starting state

`/Operations` currently renders the Service health table when
`GetServiceHealth` is composed. `ServiceHealthSnapshot` has row states and
`ExternalWorkLimitReached`, but no D37 predicate. `PLAT-051`'s destination
`Pages/Administration/ServiceHealth/Index.cshtml` is absent (verified:
`ls src/Pegasus.Web/Pages/Administration/` lists Access, Accounts,
Automation, Configuration, MailCategories, Mailboxes, Organizations,
Principals, Roles, Shared only).

The Test UI capture already includes `OperationsWebTests`; its composed-health
fixture has a retryable external-work failure, producing a `Failed` health row.
The default test identity is Administrator, so a new Operations notice state is
capturable without changing a capture host.

## Planning assumptions

1. "Not current" means at least one `Partial` or `Failed` health row. The Core
   predicate deliberately excludes `Running`, `Configured`, and
   `ReviewRequired`, matching D37's mockup condition. It also excludes
   `ExternalWorkLimitReached`: that flag means the Operations projection is
   truncated, not that a health query is stale or failed. (Open question 1,
   resolved by the controller 2026-09-03.)

2. The Operations result-limit condition stays a separate notice, and its
   explanatory sentence is removed so both notices are label-only. The two
   notices must not read as the same line: the limit notice keeps the existing
   `Partial data` label (it is the pre-existing name for that condition), and
   the D37 health notice carries its own `Service health` label. (Open
   question 2, resolved by the controller 2026-09-03.)

3. PLAT-069 merges before PLAT-051. Because the Administration Service health
   endpoint does not exist, the notice ships **without an anchor at all** —
   absent, not dead, and not a disabled seam. This follows the established
   `_AdminNav.cshtml` rule (future areas are omitted, "never as dead links")
   and is a static omission, not a runtime endpoint probe. The link line is
   added by the PR that creates the destination (PLAT-051, wave 4), which
   adds one anchor to `Pages/Operations/Index.cshtml`; PLAT-069 is out of the
   lane by then, so there is no capacity conflict. (Open question 3, resolved
   by the controller 2026-09-03.)

   Evidence for "no provisional link":
   `tests/Pegasus.IntegrationTests/WorkCentreLabelTests.cs:50-66` (UIIMP-008)
   records that an `asp-page` naming an unknown page renders `href=""`
   silently — exactly the dead link the ticket forbids.

## Dependencies

- **PLAT-051:** owns
  `src/Pegasus.Web/Pages/Administration/ServiceHealth/**` and, when it lands,
  adds the one Service health anchor to the Operations notice and its
  live-link assertion. PLAT-069 does not edit that page or
  `Pages/Administration/Shared/_AdminNav.cshtml`.
- No migration is required; do not touch
  `src/Pegasus.Infrastructure/Persistence/Migrations/**`.
- Do not alter governing documents or current-state documents; D37 is already
  recorded by DELIV-041, and deployment-state refresh remains DELIV-030's
  responsibility. FRD-12's "links to Administration Service health" wording
  describes the end state of the epic and is satisfied when PLAT-051 lands.

## Shared-path lease

Before editing, take the capacity-one lease for exactly:

- `src/Pegasus.Web/Presentation/OperatorLabels.cs`
- `docs/design/test-ui/**` — only the new Operations state and its catalogue
  entry

Refresh the lane with `git merge --no-edit origin/dev`; do not edit any other
shared-lock path.

## Governing rules

D37 requires Administration-only Service health and no Operations table.
Notices contain labels and controls only: do not port "Some figures may be
behind." or the existing limit-warning explanation. Visible labels belong only
in `Presentation/OperatorLabels.cs`; preserve exact Core state labels. An
absent destination is absent: no provisional, empty or disabled link.

## Ordered steps

1. **Refresh the lane and acquire the two shared-path leases.**

   `git merge --no-edit origin/dev`. Re-check
   `src/Pegasus.Web/Pages/Administration/ServiceHealth/`: while it is absent
   the notice ships without an anchor (assumption 3). Record the two exact
   leases above.

   Files: none.

   Reuses: the EPIC lane-refresh convention `git merge --no-edit origin/dev`
   and the Administration rail's existing no-dead-link rule.

2. **Add the Core D37 notice predicate and focused boundary tests.**

   In `src/Pegasus.Core/Operations/ServiceHealth.cs`, add one
   `ServiceHealthPolicy` predicate over `ServiceHealthSnapshot` that returns
   true only for a `Partial` or `Failed` row. It must ignore
   `ExternalWorkLimitReached` and all other states.

   In `tests/Pegasus.Core.Tests/Operations/ServiceHealthTests.cs`, cover
   `Partial`, `Failed`, the excluded `Running`/`Configured`/`ReviewRequired`
   states, and the ignored limit flag.

   Reuses: `ServiceHealthPolicy` (ServiceHealth.cs:127),
   `ServiceHealthSnapshot`, `ServiceHealthState`, and the existing
   `Build(Sources)` fixture (`ServiceHealthTests.cs:232`).

3. **Replace Operations table markup with the two compliant notices.**

   In `src/Pegasus.Web/Presentation/OperatorLabels.cs`, add the Operations
   notice labels under one focused nested label class, following the
   `OperatorLabels.AiJobs` / `OperatorLabels.EvaHandoffs` shape: the health
   notice's `Service health` label and the limit notice's existing
   `Partial data` label, so neither stays a Razor literal.

   Retain `ServiceHealthAreaName`, `ServiceHealthServiceName`,
   `ServiceHealthStateName` and `ServiceHealthDependencyName`: removing the
   table leaves them without a caller until PLAT-051's Administration table,
   which is their named next caller in this epic. Do not delete them in the
   simplification pass; record the retention there.

   In `src/Pegasus.Web/Pages/Operations/Index.cshtml`, delete the entire
   Service health panel (lines 149–187) and its table. Keep the optional
   snapshot loading already supplied by `IndexModel`; **no `Index.cshtml.cs`
   change is made**. Render the health warning only when the snapshot is
   present, the Core predicate is true, and
   `User.IsInRole(Pegasus.Core.Identity.StaffRoleNames.Administrator)` is
   true (`_Layout.cshtml:12` spelling). It is a one-line label-only notice
   with no anchor while the destination is absent.

   Keep the separate `LimitReached` notice, deleting its explanatory sentence
   ("— Showing recent operational results; refresh for the latest activity.")
   and leaving the centralized `Partial data` label.

   Reuses: `.notice.notice--warning`, `#icon-alert-triangle`,
   `User.IsInRole`, `ServiceHealthPolicy`, `OperatorLabels`.

4. **Replace table-focused web assertions with D37 coverage.**

   In `tests/Pegasus.IntegrationTests/OperationsWebTests.cs`:

   - Adapt
     `ComposedServiceHealthRenamesInternalVocabularyAndRetriesThroughTheCanonicalCommand`
     to prove the table, its column headers and its internal vocabulary are
     absent, and that the Administrator sees the D37 label; keep its retry
     coverage on Attention required, which remains the canonical action
     surface.
   - Prove the negative role cases for **both** non-Administrator staff roles
     the acceptance condition names, as a `[Theory]` with
     `X-Test-Roles: Engineer` and `X-Test-Roles: User`
     (`IntakeWebTestSupport.cs:277-288`): neither sees the D37 notice.
   - Assert the Operations response contains no `href=""`, following
     `WorkCentreLabelTests.TheWorkCentreRendersNoEmptyLink`, so the dead-link
     class cannot regress while the destination is absent.
   - Make `RecordingOperationsStore`'s `LimitReached` configurable (it is
     hard-coded `false` at `OperationsWebTests.cs:649`, so no web test
     exercises the limit notice today) and add a combined-state test: with
     the limit flag set and a `Failed` health row, the page renders two
     separate one-line notices and neither carries the removed explanatory
     sentence.

   Reuses: `Configure(..., withServiceHealth: true)`,
   `RecordingOperationsStore`, `NoServiceHealthFacts`, `CreateClient`,
   `GetHtmlAsync`, and the `X-Test-Roles` convention.

5. **Add and generate the reachable Operations snapshot state.**

   Add an `operations--partial-data` state to
   `docs/design/test-ui/catalogue.json` under the existing `/Operations`
   entry, backed by `docs/design/test-ui/pages/operations--partial-data.html`.
   Add its `StateMatches` marker in
   `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs:19-53`; the marker
   must be a string only that state renders, so `operations--default` (which
   has no marker and takes the fallback branch) still matches its own capture.

   The capture is the rewritten composed-health test, whose default identity
   is Administrator and whose fixture yields a `Failed` row. Commit only
   Operations snapshot files; do not touch UIIMP-014's Case-record states.

   Reuses: the existing Operations catalogue entries (catalogue.json
   lines 531–549), `StateMatches`, `TestUiResponseCaptureMiddleware`
   (armed by `IntakeWebTestSupport.cs:174-176`), and
   `Update-TestUiSnapshots.ps1`.

## Acceptance conditions

- `/Operations` contains no Service health table, heading, columns, or rows.
- An Administrator sees one label-only partial-data health notice when a
  health row is `Partial` or `Failed`; it carries no anchor while
  `Pages/Administration/ServiceHealth` is absent, and the page renders no
  `href=""`.
- Engineer and User requests do not see the D37 health notice.
- `Running`, `Configured`, `ReviewRequired`, and the independent
  `ExternalWorkLimitReached` flag do not trigger the D37 predicate.
- The independent limit notice remains separate, is label-only, and contains
  no explanatory sentence; both notices render together when both conditions
  hold.
- The new Operations snapshot state is captured, committed, and verified.
- No Administration, migration, governing-document, shell, code-behind, or
  unrelated snapshot path changes.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

Do not run `./scripts/Test-MigrationGrants.ps1`; this ticket has no migration.

## Stop condition

Stop when the scoped PR targeting `dev` is open and PLAT-069 is in Review.

## Wrapper notes (Claude, 2026-09-03, read-only checks)

- Codex ran once, exit 0, in `.worktrees/research` at origin/dev
  `897db953`; the checkout was clean afterwards.
- VERIFIED — `tests/Pegasus.IntegrationTests/WorkCentreLabelTests.cs:50-66`
  (UIIMP-008): in this app an `asp-page` that names an unknown page renders
  `href=""`; it does not throw. This is why the notice ships anchorless.
- VERIFIED — `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs:277-288`:
  the test identity is `Administrator` unless `X-Test-Roles` (or
  `X-Test-Roleless`) is sent.
- VERIFIED — `src/Pegasus.Core/Operations/ServiceHealth.cs:271-285` and
  `OperationsWebTests.cs:646`: the composed fixture's retryable failed
  external-work item yields a `Failed` row, so the rewritten composed test
  renders the notice and is the capture that feeds the new snapshot state.
- VERIFIED — `.kanmer/data/board.yml`: profile `fix` gates leave-preparing
  on `files` + `plan` and enter-done on `proof`; the checklist is written
  for execution, not for a gate.
- The simplification pass (CLAUDE.md workflow step 4) is on the checklist;
  its dated "Simplification pass" heading is appended to this plan at
  execution time.

## Resolutions (2026-09-03)

- Controller: the notice shows for Partial or Failed only.
- Controller: one label-only notice line each; the limit warning's hint
  sentence is removed.
- Controller: PLAT-069 may merge before PLAT-051; the Administration link is
  absent until `Pages/Administration/ServiceHealth` exists.

## Plan review (2026-09-03, gpt-5.6-sol xhigh; dispositions Claude Opus)

Reviewer verdict: REQUEST CHANGES. All four findings are dispositioned below;
a fifth was raised by the dispositioning wrapper. The reviewer confirmed every
named reuse exists, that the file set is disjoint from the ENG-034, CASE-039,
CASE-040, CASE-041, CASE-029, CASE-042 and CASE-009 lanes, and that nothing in
the plan assumes a staff review flag (D44) or a damage type (D45); D46 is out
of scope.

| # | Severity | Step | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | 1, 3–5, acceptance | The plan body still required PLAT-051 to merge first and mandated a live `asp-page` link, contradicting the resolved open question 3 appended below it; step 1 would have paused execution immediately. | Fixed. Assumption 3, Dependencies, Governing rules, steps 1 and 3 and the acceptance conditions are rewritten: PLAT-069 ships the notice anchorless and PLAT-051 adds the link with its destination. The reviewer's suggested mechanism — render the anchor only when `Url.Page(...)` is non-empty — is rejected: a runtime endpoint probe is a disabled seam and a speculative conditional whose live branch no test in this PR can cover, where the repository's own convention (`_AdminNav.cshtml`: future areas omitted, "never as dead links") is a static omission. The `no href=""` assertion the reviewer asked for is adopted in step 4. |
| 2 | should-fix | 4 | Only the `User` role was tested although the acceptance condition names Engineer and User. | Fixed — step 4 specifies a `[Theory]` over `X-Test-Roles: Engineer` and `X-Test-Roles: User`. |
| 3 | should-fix | 4 | The resolved two-notice behaviour is untested: `RecordingOperationsStore` hard-codes `LimitReached: false` (`OperationsWebTests.cs:649`, confirmed by grep), so no web test exercises the limit notice or the removal of its sentence. | Fixed — step 4 makes the flag configurable and adds the combined-state test; the acceptance conditions name the both-conditions case. |
| 4 | nit | files inventory | `files.md` listed `Index.cshtml.cs` as "change (if needed)" while the plan says no code-behind change. | Fixed — `files.md` marks it unchanged, and step 3 plus the acceptance conditions state it explicitly. |
| 5 | should-fix (wrapper) | 3 | Deleting the table leaves `ServiceHealthAreaName`, `ServiceHealthServiceName`, `ServiceHealthStateName` and `ServiceHealthDependencyName` in `OperatorLabels.cs` with no caller anywhere in the solution (verified by grep). | Fixed as an explicit retention: step 3 keeps them for PLAT-051's Administration table — their named next caller in this epic — and tells the simplification pass to record the retention rather than delete them. |

## Simplification pass (2026-09-04)

Reviewer: gpt-5.6-sol low, over `git diff origin/dev` in the ticket
worktree, restricted to this ticket's owned paths and told not to flag the
deliberate retention of the four `ServiceHealth*Name` label helpers.

| # | Finding | Disposition |
| --- | --- | --- |
| 1 | `src/Pegasus.Web/Presentation/OperatorLabels.cs:1057-1062` and `src/Pegasus.Web/Pages/Operations/Index.cshtml:46,56` — `OperationsNotices` adds a ticket-specific type and two constants for one-use, static notice headings that are not shared or transformed; suggested inlining `Partial data` and `Service health` directly in the two Razor notices and removing the block. | Rejected. "Visible labels belong only in `Presentation/OperatorLabels.cs`" is this ticket's own Governing rule, and CLAUDE.md states "labels only in `src/Pegasus.Web/Presentation/OperatorLabels.cs`" as a repository-wide, one-list-per-concept rule (not a style preference); EPIC-011 context.md repeats it. Inlining the two label strings as Razor literals would put a second label source back into the view layer for exactly the malady the rule exists to prevent, even though each string currently has one call site. `OperationsNotices` follows the existing `AiJobs` / `EvaHandoffs` nested-class shape the plan named as the reuse target. No change made. |

The deliberate retention of `ServiceHealthAreaName`, `ServiceHealthServiceName`,
`ServiceHealthStateName`, and `ServiceHealthDependencyName` in
`OperatorLabels.cs` (no current caller after this ticket, named next caller
is PLAT-051's Administration Service health table) stands as recorded in
step 3 above; the reviewer was instructed not to flag it and did not.

Also corrected during implementation/self-verification (see
post-implementation report for full command list): the Codex-run full
snapshot capture (this repo's `Update-TestUiSnapshots.ps1` has no `-Scope`
flag yet) touched every catalogue page as an LF/CRLF line-ending stat, with
no actual content diff outside the three Operations pages and
`docs/design/test-ui/index.html`; all unrelated pages were reverted with
`git checkout --` before commit so the diff stays scoped to this ticket's
owned paths.
