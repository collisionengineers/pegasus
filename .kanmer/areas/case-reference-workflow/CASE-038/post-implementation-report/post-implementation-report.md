# Post-implementation report — CASE-038 (2026-09-04, Claude Opus 5)

Branch `task/case-038-case-workspace-frame`, worktree `.worktrees/case-038`,
base `origin/dev` = `ce027748aa5d00daea13a0359a5eb4a81aad912d`, head
`dd72edd6661d9822470d9bfad2f29be728705688`.
PR: https://github.com/collisionengineers/pegasus/pull/656

## Step 0 — the PLAT-070 prerequisite

PLAT-070 has merged (`60fc84dc0`, PR #649). The plan's literal gate — `git
grep -i "ReviewedByStaff\|RequireStaffImageReview\|staff-reviewed"` returns
nothing — is not literally empty on the branch, and that is expected: the
only remaining matches are historical EF `*.Designer.cs` migration snapshots
(immutable), PLAT-070's own `20260903153134_RemoveStaffReviewFlags` drop
migration, and `WorkflowConfigurationWebTests`' assertion that the flag is
*absent*. No live surface matches under `src/Pegasus.Web`, `src/Pegasus.Core`
or the persistence configuration. The gate's intent is met; CASE-038 neither
performs nor re-introduces that removal, and the Browser scenario asserts no
staff-review control renders on the frame (D44).

## Files changed (21)

Owned frame and vocabulary:

- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — the one ordered
  `Key`/`Label`/`Icon` `CaseSection` list (D30), the ribbon labels the frame
  renders, `DefaultSectionKey` and `AbsentValue`.
- `src/Pegasus.Web/Pages/Cases/Details.cshtml` — `case-sticky` block (ribbon
  with Engineer and the Sign-off slot, presence strip, action bar, edit bar,
  jump-nav) over `case-workspace` → `case-main` (eleven `section-<key>` hosts)
  + `case-context`. Open Assessment removed.
- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` — canonical `?section=`
  vocabulary, `SectionIsDeferred`, `OnGetSectionAsync`,
  `LoadIntakeGalleriesAsync`, `AssessmentIsReadOnly` replacing
  `CanOpenAssessment`.
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml` — horizontal
  `section-nav` of eleven `section-link` anchors with `aria-current`.
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml`,
  `_CaseEstimate.cshtml`, `_CaseSettlement.cshtml`, `_CaseReport.cshtml` —
  created as heading-only shells (ENG-034 contract item 6).
- `src/Pegasus.Web/wwwroot/css/site.css` — `case-sticky`, `section-nav`,
  `section-link`, `section-placeholder`, measured `--case-sticky-h` offsets,
  seven-item ribbon, 1360/760 reflow; `case-section-nav` and all its rules
  deleted.
- `src/Pegasus.Web/wwwroot/js/site.js` — the Case module (measure, lazy
  fragment mount, `?section=` jump, scroll-spy) plus the three root-scoped
  idempotent binders and the narrowed Ctrl+S target.

Declared exception (`Pages/Cases/Shared/*` lock, plan-settled):

- `src/Pegasus.Web/Pages/Cases/Shared/_CaseInspectionAddress.cshtml` — form id
  and `data-edit-save` only (lines 71–76), plus the now-false comment.

Proof:

- `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` — eleven ordered
  hosts and jump links, which sections the first response defers, the
  addressed section rendered and marked current, deleted keys not aliased, the
  fragment handler returning one body and refusing keys it does not serve, a
  held lease deferring nothing, one `case-edit-form`.
- `tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs` — a seeded
  Case-record scenario at 1580/1100/760 with a local minimal seed helper.
- Mechanical query-key retargets (plan Step 5) in `CaseVehicleWebTests.cs`,
  `CaseTasksWebTests.cs`, `CaseCustodyWebTests.cs`, `ImageIntakeWebTests.cs`,
  `ImageViewingWebTests.cs`, `Browser/OperatorJourneyTests.cs`.

Docs and snapshots:

- `docs/design/README.md` — `case-section-nav` struck from the vocabulary row
  and the 980px reflow row; no governing decision text changed.
- `docs/design/test-ui/catalogue.json` — the Details `default` branch text now
  names the frame it renders.
- `docs/design/test-ui/pages/case-details--default.html`,
  `case-details--conflict.html`, `docs/design/test-ui/index.html` —
  regenerated. `case-details--unavailable.html` is byte-identical.

## Commands (Windows, PowerShell 7)

| Command | Exit |
| --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 (0 warnings) |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"` | 0 — Core 1203, Architecture 100, Integration 1128 passed, 2 skipped |
| `dotnet test ./tests/Pegasus.IntegrationTests/... --filter "Category=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2` | 0 — 123 passed (run as the snapshot script's browser capture phase) |
| `./scripts/Update-TestUiSnapshots.ps1` | 0 — capture 123 + 308 passed, snapshot update passed |
| `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` | **1** — see the dependency below |
| `./scripts/Test-UiCatalogue.ps1` | not reached (the verify phase throws first) |
| Post-pass fast re-run: build / Core.Tests / ArchitectureTests | 0 / 0 (1203) / 0 (100) |

`./scripts/Test-MigrationGrants.ps1` was not run: this ticket has no
migration, as the plan requires.

## Blocking dependency — Test UI offline-render check (not this frame)

`Update-TestUiSnapshots.ps1 -Verify` fails with `Offline image failed to
load: pages/case-details--default.html`.

Cause, traced end to end: the `default` Case snapshot is captured with the
edit lease held, so — by design (plan finding 10) — the record renders every
section, including Files. The instruction-photograph gallery renders
`<img src="/Cases/{id}/Documents/{occ}/{ver}?inline=true">`, which is correct
in production. `TestUiSnapshotTests.NormalizeAndRewrite` rewrites every
application URL whose route has no *visual* catalogue entry to `#`; a
case-document download is classified `protocol`, so the committed snapshot
gets `<img src="#">`. `VerifyOfflineBrowserRenderAsync` then asserts every
`<img>` has a non-zero `naturalWidth`, which `#` cannot satisfy. Receipt
images escape this because they are inlined as `data:` URLs; case-document
images are not.

The tool's own rewrite creates the broken image, so the fix is in
`tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` — the Test UI tooling
lane (UIIMP-005/UIIMP-013; UIIMP-014 owns per-section Case states). Either
inline case-document images the way receipt images already are, or exclude
`#`-rewritten images from the offline-image assertion. CASE-038 does not own
that file and has not touched it (AGENTS.md rules 1 and 2, and the plan's
"stop and report if an unowned file is required" rule). This is the one red
check on PR #656 and needs an orchestrator routing decision.

## Deviations from the plan

1. **Lazy set narrowed (plan Step 2/3).** The plan deferred every section
   below the first three. Only Vehicle, Files and Notes are deferred: the
   other later sections are heading-only hosts or ENG-034's heading-only
   shells, so a fragment round trip would fetch a heading. `LazySectionViews`
   is that list; `OnGetSectionAsync` refuses every other key, and adding a
   body later is one entry. The plan's stronger guarantee still holds — a held
   lease defers nothing at all.
2. **Ctrl+S / Sign-off / icons.** As planned. The section descriptor keeps
   `Icon` and the jump-nav renders it, so the icon mapping has a caller rather
   than being dead data.
3. **`CaseTasksWebTests.InspectionAddressEditorPostsEveryEditableValueWithTheTypedAddressFirst`
   re-scoped.** The record now renders the Overview editor above the
   Inspection form and both post `inspectionAddress`, so the ordering
   assertion reads inside `case-inspection-address-form` rather than across
   the page. The same ordering is asserted; nothing was weakened. This is the
   same category as the plan's declared Step-5 mechanical exception in the
   same file.
4. **760px ribbon.** The generic 760 breakpoint stacks the ribbon to one
   column, which made the sticky block taller than the viewport and broke the
   scroll-spy outright. The record's ribbon keeps two columns at that width.
   This is one `.case-sticky`-scoped rule inside an owned file.
5. **No Assessment handler moved** — option B, as the plan resolved.

## Contracts handed on

- **ENG-034:** eleven stable hosts, the fragment URL
  `/Cases/{id}?handler=Section&section=<key>`, the four heading-only shells,
  and on `DetailsModel` the Case id, actor role, operation keys,
  `LeaseToken`, the lease handlers and `AssessmentIsReadOnly`. ENG-034 adds
  the nine Assessment handlers and `OnGetPreviewReportDraftAsync` in the PR
  that moves the forms.
- **CASE-039:** render into `#section-engineer-notes`; the host and heading
  exist.
- **CASE-029:** render into `#section-valuation`; the host and heading exist.
  The Vehicle body is re-hosted unchanged, not endorsed (D34 is CASE-029's).
- **CASE-040:** the Sign-off ribbon label and its absent-value slot exist;
  the EVA action is re-hosted unchanged (D36 is CASE-040's).
- **CASE-041:** the inspection form is `case-inspection-address-form`, posts
  to the Details `Save` handler, and is not the sticky bar's Save target.
- **UIIMP-005/013/014:** the offline-render dependency above.
