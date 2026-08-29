# Post-implementation report — PLAT-027

Branch `task/plat-027-staff-accounts-roles`, PR
[#619](https://github.com/collisionengineers/pegasus/pull/619) against `dev`.
`origin/dev` (`b92cb9a7`) was merged in first; it was a clean fast-forward
(0 ahead / 60 behind), no conflicts.

## Commits

| SHA | Slice |
| --- | --- |
| `774ff072` | `fix(identity): report a never-reviewed staff account as unreviewed` |
| `ed29e97d` | `feat(administration): label map for the staff accounts area` |
| `7c820cec` | `feat(administration): fold roles and access review into Staff accounts` |
| `a03e5e07` | `test(administration): pin the consolidated staff accounts area` |

## What shipped

`/Administration/Accounts` is the one "Staff accounts & roles" area the admin
rail already pointed at. Its accounts table carries **Username · Role ·
State · Last reviewed · Save · Account**, and below it the Create staff
account panel. Every drawn control posts to a handler on that page, and each
handler calls the Core use case that already owned the operation —
`ICreateStaffAccount`, `IAssignStaffRoles`, `IDisableStaffAccount`,
`IReviewStaffAccess`. No Core port, use case, policy, CSS class, JS behaviour,
partial or package was added.

`[Authorize(Policy = Administrator)]` is preserved, and because each mutation
still runs through its own use case, the three distinct Core rights
(`ManageStaffAccounts`, `AssignStaffRoles`, `ReviewStaffAccess`) are unchanged
by the fold. The permanent administration history is untouched: every
operation still writes its own `ActionHistory` row through the same adapter.

**Access review survived the fold intact** — both halves of it. The Review
action is the contract's `Account` column control; the readout is the added
`Last reviewed` column, which renders the recorded time or Core's
`ReviewIsOutstanding` as the amber `Due` chip. `ReviewIsOutstanding` is read
from Core, not re-derived in the Web layer.

## A real defect found and fixed on the way (report this loudly)

`src/Pegasus.Infrastructure/Persistence/EfStaffAccountQueries.cs` —
`ListAsync` resolved each account's last access review with
`GetValueOrDefault` over a `Dictionary<string, DateTimeOffset>`. For an
account with no recorded review that returns `default(DateTimeOffset)` =
`0001-01-01T00:00:00+00:00`, **not null**. Consequences, all live on `dev`
before this PR:

- `GetAccessReview` computes `ReviewIsOutstanding = IsEnabled &&
  LastAccessReviewAtUtc is null`, so it has been **permanently false** since
  the feature shipped. No account has ever been shown as due for review.
- The superseded `/Administration/Access` page printed
  `0001-01-01 00:00:00Z` in its "Last reviewed" column and the chip
  "Recorded" for every never-reviewed account.

Fixed with one cast — `item => (DateTimeOffset?)item.OccurredAtUtc` — in
commit `774ff072`. `GetAsync` (the single-account path) was already correct,
which is why the bug was invisible on `Accounts/Edit`.

**This is outside the declared lane.** It was fixed rather than deferred
because the file belongs to no in-flight lane, the change is one token, and
this ticket's own "Last reviewed" column depends on the flag being truthful
(decision D19, disposition 2). Flagging it for the orchestrator to confirm
the ownership call. The failing assertion that exposed it is now a
regression test.

## Superseded routes — for UIIMP-009 to delete (wave 5)

Nothing was deleted here. These are now unreachable from navigation and
carry no capability the consolidated area lacks:

| Route | Files |
| --- | --- |
| `/Administration/Roles` | `src/Pegasus.Web/Pages/Administration/Roles/Index.cshtml{,.cs}` |
| `/Administration/Access` | `src/Pegasus.Web/Pages/Administration/Access/Index.cshtml{,.cs}` |
| `/Administration/Accounts/Edit/{id:guid}` | `src/Pegasus.Web/Pages/Administration/Accounts/Edit.cshtml{,.cs}` |

Their `docs/design/test-ui/catalogue.json` entries and
`docs/design/test-ui/pages/administration-{access,roles,account-edit}--default.html`
snapshots go with them. `SupersededStaffAccessRoutesStillAnswerUntilTheRemovalTicket`
pins that they still answer today and that the new area links neither; that
test is UIIMP-009's to delete alongside the pages.

## Divergences from the drawn contract

Each is argued in the plan; repeated here so a reviewer sees them without
opening it.

| Drawn | Shipped | Reason |
| --- | --- | --- |
| Columns "Name, Username" | one `Username` column | `StaffAccountSummary` has no person-name field, and `ActorDisplayNames` resolves a staff actor to its username. Inventing a name is fabricated domain data. |
| — | added `Last reviewed` | The access-review readout has nowhere else to live. |
| `Role (inline select)`, single-valued | `<select multiple>` | `AssignStaffRolesRequest.Roles` is a set and `CaseEngineerEligibility` gates on `HasEngineerRole` specifically, so Administrator does **not** imply Engineer. A single-valued select would silently strip engineer eligibility from a multi-role account. |
| `Save (disabled until changed)` | Save always enabled | The enable-on-change behaviour belongs in `wwwroot/js/site.js`, out of this lane (TICK-223 in flight). A permanently disabled Save would be an inert control (D7). |
| role Save reason via `_ReasonDialog` | labelled Reason input in the row form | `_ReasonDialog` renders a self-contained `<form>` with no `id` and only server-known `DialogHiddenFields`, so it cannot carry the client-chosen select value. It **is** used for Disable and Review, whose payloads are fully server-known. |

## Defects reported outside this lane

1. `src/Pegasus.Web/Pages/Operations/Index.cshtml` (PLAT-023, merged) styles
   its row reason with `.row-confirm` and `details > summary.btn`. Both are
   inside `site.css`'s `/* ==== LEGACY (wave 5 deletes) ==== */` block (lines
   1980 and 1984), so those rows lose their styling when UIIMP-009 deletes it.
2. `Pages/Shared/_ReasonDialog.cshtml` hard-codes `maxlength="500"`, but
   `StaffAccountAdministrationPolicy.MaximumReasonLength` and the
   `ActionHistory.Reason` column are both 1000. Harmless (a narrowing, never
   an overflow), but the Disable/Review dialogs on this page accept 500 while
   the row Save accepts 1000. A `ViewData["DialogFormId"]` on that partial
   would also have removed divergence 5 above. `Pages/Shared/**` is out of
   this lane and several lanes render the partial.
3. `Pages/Administration/Index.cshtml` still renders an area-card landing,
   where `docs/design/README.md` §Routes says bare `/Administration` opens
   `accounts`. UIIMP-009's territory.
4. `ViewData["AdminAutomationComposed"]` is probed independently by every
   administration page. `AdministrationPageModel` is its natural home, but
   four admin lanes are in flight on that folder; whoever lands last should
   hoist it.
5. `docs/design/test-ui/catalogue.json` describes the accounts empty state as
   `"Model.Accounts.Count is zero."`; the model property is now `Rows`. Only
   the free-text `branch` field is stale — `scripts/Test-UiCatalogue.ps1`
   checks it is non-empty, not that it is accurate, so CI is unaffected. Left
   for the snapshot-regenerating merge rather than edited in-lane.

## Verification — real, observed numbers

Windows 11 + PowerShell 7, in the lane worktree, after the `origin/dev`
merge.

| Command | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release` | **Build succeeded. 0 Warning(s), 0 Error(s)** |
| `dotnet test ./tests/Pegasus.IntegrationTests/... --filter 'FullyQualifiedName~StaffAccountsAndRolesWebTests'` | **Failed: 0, Passed: 4, Skipped: 0, Total: 4** |
| `dotnet test ./tests/Pegasus.IntegrationTests/... --filter 'FullyQualifiedName~TestUiFocusedRenderTests\|FullyQualifiedName~AdministrationSearchAccountWebTests\|FullyQualifiedName~OrganizationAdministrationWebTests'` | **Failed: 0, Passed: 11, Skipped: 0, Total: 11** |
| `dotnet test ./tests/Pegasus.Core.Tests/... --filter 'FullyQualifiedName~Identity\|FullyQualifiedName~ActorDisplayNames'` | **Failed: 0, Passed: 74, Skipped: 0, Total: 74** |
| `dotnet test ./tests/Pegasus.ArchitectureTests/...` (no filter) | **Failed: 0, Passed: 100, Skipped: 0, Total: 100** |

Two intermediate failures are worth recording rather than hiding: the first
run of `StaffAccountsAndRolesWebTests` failed 2 of 4 on
`Assert.Null(...LastAccessReviewAtUtc)`, which is how the
`EfStaffAccountQueries` defect above was found; a later run failed 1 of 4
because Razor encodes the round-trip stamp's `+` as `&#x2B;`, fixed by
decoding the attribute in the test. **No assertion was weakened, skipped,
deleted or inverted at any point.** The one pre-existing assertion that
changed — the empty-state string in `TestUiFocusedRenderTests` — was
retargeted from `"No staff accounts are available."` (a sentence that
explained application initialization) to the exact new markup
`"<h2>No staff accounts</h2>"`; it is an exact-markup assertion where the old
one was a prose substring.

## Not run, and why

- **Browser category** and `scripts/Update-TestUiSnapshots.ps1` — this lane
  does not run them; snapshots are regenerated once per merge on the merging
  branch. `Browser/AccessibilityTests` already lists `/Administration/Accounts`
  among its routes and will exercise the new markup on the merge run.
- The **full suite** — the lane brief restricts this to focused filters.

## Evidence tier, stated honestly

Green build plus focused integration tests that drive all four handlers over
a real LocalDB through the real EF adapters. **Not** browser-verified: in
particular the HTML5 `form="…"` association that binds the Role select in one
`<td>` to the row form in another is standard markup and renders correctly,
but no executed test in this lane submits it through a browser. The merge
run's Browser suite is the next tier.

## Stop condition

PR open, not merged. Ticket walked to `review`. No `proof`, no `done`.

## Adversarial verifier remediation — 2026-08-29

This section supersedes the earlier capability account, file map, defect list,
verification table, checklist description, and Browser statement where they
conflict with it.

### What changed

- `Accounts/Index.cshtml` restores the existing Test UI empty-state match
  token as a heading, renders both password-change states, repopulates the
  targeted row's rejected role reason, and passes Core's reason limit to both
  staff reason dialogs.
- `Accounts/Index.cshtml.cs` retains the role post's staff id and exact
  reason for a rejected render.
- `OperatorLabels.StaffAccounts` appends
  `PasswordChangeComplete`; no existing member moved.
- `_ReasonDialog.cshtml` now accepts a caller-supplied reason maximum while
  preserving 500 as the schema-backed default. No task branch owned this
  shared file when checked, so this is the permitted small disposition-2
  correction.
- `StaffAccountsAndRolesWebTests.cs` proves the two dialog bounds, the
  completed password-change readout, rejected-role reason retention, and an
  accepted 1000-character access-review reason.
- `TestUiFocusedRenderTests.cs` pins
  `<h2>No staff accounts are available.</h2>`.

`TestUiSnapshotTests.cs` was not changed because PLAT-052 and UIIMP-005 both
change it. Its existing matcher now matches the lane-owned empty response
again, which also excludes that response from the default-state selection.
The application-initialization explanation remains deleted.

### Corrected capability account

The folded page preserves the 1000-character access-review reason, rejected
role reasons, and both first-password-change states. The earlier report's
description of the 500/1000 difference as a harmless external nit was wrong:
it was a capability narrowing and is fixed here.

The `EfStaffAccountQueries.cs` correction in `774ff072` now also has its
required dated disposition in the plan. It remains an intentional disposition-
2 shared-file fix, not an undisclosed lane expansion.

### Verification observed in this remediation

| Command | Exit | Result |
| --- | --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release` | 0 | Build succeeded; 0 warnings, 0 errors |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "FullyQualifiedName~StaffAccountsAndRolesWebTests"` | 0 | Failed 0, Passed 4, Skipped 0, Total 4 |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "FullyQualifiedName~TestUiFocusedRenderTests"` | 0 | Failed 0, Passed 3, Skipped 0, Total 3 |

The first PLAT-027 test run after the code edit failed 1/4 because a new test
assumed Razor would render adjacent input attributes without whitespace. The
assertion was corrected to select the targeted input and compare its decoded
`value`. An immediate `--no-build` rerun repeated the old 1/4 failure
because that test edit had not been compiled. After rebuilding, the unchanged
filter passed 4/4. No production assertion was weakened, skipped, deleted, or
inverted.

The full snapshot generator was not run: it owns a broad Browser/capture loop
and generated files outside this lane. The exact empty response is covered by
the 3/3 focused render class, and source inspection confirms its heading equals
the unchanged snapshot matcher token.

### Honest state

The checklist is **25/28**, not fully ticked. The three unticked items remain
the orchestrator-owned Browser/snapshot run, UIIMP-009 route deletion, and
post-merge proof/Done.

CI's Browser job was green on prior head `a03e5e07`; that was an underclaim
in the earlier report. Browser was not run locally here, and the new
remediation head is not claimed as browser-verified until its own CI runs.

### Remediation commits

| SHA | Slice |
| --- | --- |
| `611d8324` | `fix(administration): preserve folded staff workflows (PLAT-027)` |
| `bb5df64e` | `test(administration): cover verifier regressions (PLAT-027)` |
