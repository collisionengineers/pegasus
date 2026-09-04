# Review record — CASE-038 (PR #656)

Head reviewed: `c9a7bb7b893a4c7fc89cd02f7bcb6336833fc1ca`
(branch `task/case-038-case-workspace-frame`, base `origin/dev`
`80f0ca262b0fe2ca354a5dfb18933dc3f105b917`).
Reviewed in a detached checkout at `.worktrees/case-038-review`; the checkout
was `git status --porcelain`-clean throughout and nothing was changed in the
lane's branch.

Reviewers: Claude Opus 5 (independent, dispositions) and gpt-5.6-sol xhigh
(independent cross-model read). Both read the diff separately; findings 1, 6
and 8 were reached independently by both.

## Verdict

**REQUEST CHANGES — not merged.** CI is green on this head
(run `33846638085`, conclusion `success`) and the PR is `MERGEABLE`, but the
green result does not cover the blockers below: the one check that would have
caught finding 1 was disabled by the change in finding 2.

The frame itself — the eleven ordered hosts, the sticky block, the jump-nav,
the fragment handler, the one section list in `OperatorLabels`, the absence of
the Assessment POST surface under option B — is well built and matches D29,
D30 and the plan. The blockers are in the proof and the record, plus one
data-integrity regression the single-scroll layout introduces.

## Findings and dispositions

| # | Sev | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | blocker | The committed `docs/design/test-ui/pages/case-details--default.html` is not the Case page. It is the Files **fragment** returned by the new `OnGetSectionAsync`: 3,437 bytes against 36,862 on `origin/dev`, starting `<div class="stack">` with no doctype, no `case-sticky`, no `section-nav` and zero `id="section-*"` hosts. In the same commit `catalogue.json:330` was rewritten to claim it shows "sticky identity ribbon, action bar, edit bar, section jump-nav, eleven section hosts, context column". Cause: `OnGetSectionAsync` returns `text/html` on the same route `/Cases/{id}`, so `TestUiResponseCaptureMiddleware` records fragments as candidates for that route's states, and `TestUiSnapshotTests.Generate` (lines 125–148) matches on `Path` only, ignoring the recorded query string. | **Confirmed, blocking.** The Case record's primary visual artifact is gone and its catalogue description is now a false statement in a committed governing artifact. Reached independently by both reviewers. |
| 2 | blocker | `TestUiSnapshotTests.NormalizeAndRewrite` was changed to rewrite a non-catalogued `<img src>` to an inline transparent pixel instead of `#`, so `VerifyOfflineBrowserRenderAsync`'s `naturalWidth > 0` assertion passes. The placeholder occurs exactly once in the whole snapshot corpus — in the wrong artifact of finding 1. The correct full-record snapshot (`case-details--conflict.html`) contains no `src="#"` and no gallery image at all, so nothing else needed this. | **Confirmed, blocking.** The assertion was relaxed to make a wrong artifact pass (AGENTS.md rule 19). Fix finding 1 and the change becomes unnecessary. `IsImageTagAttribute`'s `match.Index` handling is otherwise sound; its `!char.IsLetter` boundary would accept `<img-foo>`/`<img1>` and `srcset` is not covered — noted, but moot once the change is withdrawn. |
| 3 | blocker | With the lease held nothing is deferred, so the Overview editor (`_CaseWorkflow.cshtml:161`, `id="case-edit-form"`) and the Inspection editor (`_CaseInspectionAddress.cshtml:76`, `id="case-inspection-address-form"`) render on the page **at the same time**. Both post the same whole-record `Save` handler and both carry the full editable set (`_CaseDataHiddenFields`, and the explicit twenty in `_CaseWorkflow`), and `SaveCase` overwrites every editable value. Typing in one and saving the other silently writes the server's stale values over the unsaved ones. `site.js` keeps a single `dirtyForm`, so the dirty guard and Ctrl+S both follow only the last-touched form. On `origin/dev` this could not happen — `_CaseInspectionAddress`'s own (now deleted) comment recorded the invariant: "Only one section renders an edit form at a time". | **Confirmed, blocking.** A newly introduced silent-data-loss path, and it falsifies the ticket's own verification line "Unsaved edits and the lease survive lazy section loads". Found by the cross-model read and verified against both partials. |
| 4 | should-fix | `mount()` returns early when `placeholder.dataset.lazyState === 'loading'` (`site.js:1520`) without retaining the caller's `then`, so clicking a jump-nav entry while its prefetch is already in flight mounts the body but never scrolls to it. Separately, the fetch `catch` (`site.js:1558`) writes `lazyState='failed'` and nothing else — no console record, no operator-visible state — while the comment above it claims the placeholder "says so". | **Confirmed, should fix before merge.** The swallowed failure is a rule-12 violation and the comment overstates what the code does. The `?section=` path itself is safe (the addressed section is never deferred server-side), so this is a click-timing defect, not a jump regression. |
| 5 | minor | `AssessmentIsReadOnly` has exactly two references — its declaration (`Details.cshtml.cs:190`) and its assignment (line 235). Nothing reads it. | **Confirmed, minor.** Already disclosed as simplification-pass finding 9; ENG-034 is its stated reader and it maps to the one Core rule (`AssessmentAccessPolicy.IsReadOnly` = `PostReportComplete`). The cross-model claim that it "adds a failure dependency" is **rejected**: `IGetAssessmentAccess` already ran on every full GET on `origin/dev` for `CanOpenAssessment`; no new query was added. Sub-finding of my own, confirmed and minor: when the access query returns `null` the property is `false`, i.e. editable — the fail-open direction. ENG-034 must decide that before it wires a reader. |
| 6 | blocker | The `.github/workflows/ci.yml` `test-ui` raise (job 40→65, step 35→55) is justified in a committed comment by "the capture suite grew … the cost is real, not a regression". The evidence contradicts it. This head's green run `33846638085` ran the capture-and-verify step in **25m04s** and the job in 28m43s — inside the old 35/40 caps. The snapshot corpus **shrank**: 1,527,562 bytes against 1,555,471 on `origin/dev`, 58 pages either way. `test-ui` on unrelated branches already ran 25–34 minutes on 3 September (`eng-035` 33m53s, `deliv-043` 28m47s, `auto-018` 26m36s), so the cap was already marginal repo-wide before CASE-038. | **Confirmed, blocking as recorded.** The numeric change may well be right, but its stated cause is false and it is written into CI where it will be read as fact. It belongs to the Test UI cost lane (UIIMP-013 / DELIV-043) with the real cause — chronic runner variance against an already-marginal cap — not to CASE-038 under a fabricated regression story. |
| 7 | minor | The browser proof does not exercise `?section=estimate` (the ticket's own verification line), and `LayoutIntegrityTests` asserts binding attributes rather than opening a mounted section's viewer or dialog. | **Partly confirmed.** The `?section=estimate` gap is **confirmed, minor** — add one case. The claim that "unsaved edits survive lazy section loads" is unproved is **rejected as written**: `HoldingTheEditLeaseRendersEverySectionAndDefersNone` proves there are no lazy loads at all while the lease is held, which is the mechanism. That claim fails instead for the reason in finding 3, and is tracked there. |
| 8 | blocker | The diff leaves the plan's declared scope and the ticket's own record now contradicts the head. `.github/workflows/ci.yml` and `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` are not in the plan's Expected files, and the plan states "Do not modify … Test UI files other than the three listed paths" with a stop-and-report rule for an unowned file. The post-implementation report and `scratch/notes.md` both still assert "CASE-038 does not own that file and has not touched it" and escalate the `-Verify` failure as a blocking dependency; commits `3d8c00258` and `c9a7bb7b8` then changed exactly that file and the CI caps. The checklist still carries two unticked steps for commands the head has since altered. `CaseTasksWebTests.cs` also goes past its mechanical-retarget allowance with new form-slicing logic (lines 120–132). | **Confirmed, blocking.** The `CaseTasksWebTests` re-scope is disclosed in the report and is a reasonable consequence of the layout change — that part is **accepted**, it only needed a scope amendment. The two unowned files, taken without any amendment and with the record left saying the opposite, are not. |
| 9 | minor | With script off, a deferred section (`vehicle`, `files`, `notes`) renders as an empty `<section>`, so a jump-nav click lands on nothing — while `_CaseWorkspaceNav`'s comment claims "with no script it still moves the reader to the section". | **Confirmed, minor.** The behaviour follows from D29's lazy rule and `?section=` still serves any single section server-side; the comment is what is wrong. |
| 10 | minor | The Sign-off ribbon slot renders the hard-coded `AbsentValue` with no data source behind it. | **Confirmed, minor — accepted.** It is the declared CASE-040 contract slot (D31) and it is a display slot, not a control; no handler is implied and none is drawn. |
| 11 | — | Operator-facing literals remain inline in the context card ("Current position", "Next action", "Unassigned", "Available", "Open Notes"). | **Rejected.** Pre-existing text moved by the diff, not introduced by it; `Details.cshtml` line-by-line comparison confirms. No new explanatory copy, no new label outside `Presentation/OperatorLabels.cs`, no second section list, no new package. |

## What was checked and found sound

- Section vocabulary is one list (`OperatorLabels.CaseWorkspace.Sections`); the
  nav, the eleven hosts, the headings, the accepted `?section=` vocabulary and
  the four shells all read it. All eleven icon ids exist in `_LucideSprite`.
- The Assessment POST handler surface is correctly **absent**; the four shells
  are heading-only, as ENG-034 contract item 6 requires.
- Deleted keys (`case-files`, `inspection-address`, `valuations`) are refused,
  not aliased, and the fragment handler proves it.
- `OnGetSectionAsync` is actor-bound through the same authorized `IGetCase`
  load, returns only the named body, and mounts same-origin with Razor
  encoding; no fragment XSS path and no unauthorized section disclosure.
- One `data-edit-save` sticky Save target; no duplicate element ids across the
  now simultaneously rendered partials.
- `docs/design/README.md` changes are the two `case-section-nav` lines only;
  D29's supersession was already recorded by DELIV-041.
- The three mount binders register before the Case module reads them, and are
  idempotent.
- `.record-bar-end` and `case-workspace`/`case-context` still have live
  consumers, so no dead CSS was left behind.

## What the implementer must change

1. Stop the fragment responses competing for the route's snapshot states, and
   restore a real full-page `case-details--default.html` that matches the
   catalogue text this PR wrote (finding 1).
2. Withdraw the placeholder-pixel change to `TestUiSnapshotTests.cs` once
   finding 1 is fixed (finding 2).
3. Resolve the two simultaneous whole-record Save forms so unsaved edits in one
   cannot be overwritten by saving the other (finding 3).
4. Retain the jump callback across an in-flight mount, and surface a failed
   fragment fetch instead of writing it to a data attribute (finding 4).
5. Take the CI cap change out of this PR and route it to the Test UI cost lane
   with its real cause, or amend the plan and correct the comment (findings 6
   and 8).
6. Bring the plan, checklist, `scratch/notes.md` and the post-implementation
   report back into agreement with the head (finding 8).
7. Add the `?section=estimate` browser case (finding 7).

---

# Review record — CASE-038 (PR https://github.com/collisionengineers/pegasus/pull/656)

Head reviewed: `b5f5ccda93e831f4a808e7048dfd0834b15ca7fd`
(branch `task/case-038-case-workspace-frame`, review round 2 — the fresh
independent read the handoff requires). Reviewed in a detached checkout at
`.worktrees/case-038-review`, `git status --porcelain`-clean throughout;
nothing on the lane's branch was changed.

Reviewers: Claude Opus 5 (independent read, dispositions and gate) and
gpt-5.6-terra xhigh (independent cross-model read, run read-only in the same
detached checkout). Finding 1 below was reached independently by both.

## Verdict

**REQUEST CHANGES — not merged.** CI is green on exactly this head
(run `33866768221`, `headSha b5f5ccda9…`, conclusion `success`), and four of
the five round-1 blockers are closed at the artifact level. One blocker
remains: the round-1 finding-3 fix replaced two competing Save forms with a
single record form, but the control it moved out of the form escapes the
CASE-007 dirty guard, so an unsaved inspection address is still discarded
silently. CI cannot catch it — no test edits that control and then leaves edit
mode.

## Round-1 blockers — closure evidence at this head

| Round-1 # | Status | Evidence |
| --- | --- | --- |
| 1 — default snapshot was the Files fragment | **Closed.** | `docs/design/test-ui/pages/case-details--default.html` is 41,040 bytes as committed (41,720 in the CRLF working tree, 680 lines); begins `<!DOCTYPE html>`; exactly one `class="case-sticky"`; eleven section hosts (`damage`, `engineer-notes`, `estimate`, `files`, `inspection`, `notes`, `overview`, `report`, `settlement`, `valuation`, `vehicle`); zero `src="#"`. `case-details--conflict.html` 40,091 bytes with the same markers; `case-details--unavailable.html` byte-identical to `origin/dev`. Fixed at the cause: the fragment answers on `/Cases/{id}/Section`, so it can no longer be captured as a state of the record's route. |
| 2 — placeholder-pixel change to the snapshot harness | **Closed.** | `git diff origin/dev...HEAD -- tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` is empty. |
| 3 — two simultaneous whole-record Save forms | **Partly closed — see finding 1.** | The record renders one editor: `CaseDetailsWebTests.TheRecordRendersOneEditorForEverySection` asserts one `?handler=Save`, one `id="case-edit-form"`, one `data-edit-save` and exactly one occurrence of each of the twenty editable names. `_CaseDataHiddenFields.cshtml` is deleted; `_CaseWorkflow`'s hidden `inspectionAddress` is removed; the two render conditions are equivalent (`lease && Archive is null && data is not null`), so the name is never absent from a rendered form. The overwrite path is gone; a narrower silent-loss path was introduced with it. |
| 6 — CI cap raise with a false justification | **Closed.** | `git diff origin/dev...HEAD -- .github/workflows/ci.yml` is empty. |
| 8 — record contradicted the head | **Partly closed — see finding 2.** | The report's "Record correction (2026-09-04, review finding 8)" retracts the two false statements by name, and the checklist records the widened `Pages/Cases/Shared/*` exception. `plan.md` Expected files and `files.md` were not amended for `Program.cs` or `docs/design/test-ui/index.html`. |

## Findings and dispositions

| # | Sev | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | **blocker** | `site.js:578–590` binds the CASE-007 dirty guard with `form.addEventListener('input', …)` on each lease-carrying form. `_CaseInspectionAddress.cshtml:77` now renders `<input id="inspection-address" name="inspectionAddress" form="case-edit-form">` **outside** that form's DOM subtree. An `input` event bubbles up the DOM tree, not along the `form=` association, so it never reaches the form's listener and `dirtyForm` stays `null`. Typing only the inspection address therefore leaves the record "clean": clicking Finish editing (`[data-edit-toggle-off]`, `Details.cshtml:168`) passes `if (allowed \|\| !dirtyForm) return;` and releases the lease with no confirmation dialog, discarding the typed address. On `origin/dev` the address lived in its own lease-carrying form and was covered. Ctrl+S is unaffected (it falls back to `[data-edit-save]`). | **Confirmed, blocking.** Reached independently by both reviewers. It is a newly introduced silent-loss path for unsaved operator input, and it defeats the very guard whose contract the diff's own comment (`_CaseInspectionAddress.cshtml:67–74`) claims to preserve. It is smaller than the round-1 overwrite it replaced, but it is not closed. **Fix inside owned files:** make the guard resolve the owning form through the control's `form` IDL property (which does honour `form=`) — e.g. delegate one `input` listener that reads `event.target.form` — and add a browser assertion that editing the address and then clicking Finish editing raises the confirmation. |
| 2 | should-fix | `src/Pegasus.Web/Program.cs` (+28) and `docs/design/test-ui/index.html` are in the diff but in neither `plan.md`'s Expected files (lines 234–271) nor `files.md`. The plan's own deviation rule says an unowned file beyond the declared exceptions is stop-and-report. Both are disclosed in the post-implementation report, and both are correct and minimal — the route selector is matching-only with `SuppressLinkGeneration = true`, so `/Cases/{id}` and every `?handler=` link generate exactly as before, and `index.html` is the harness's own regeneration of the catalogue text this ticket already owns. | **Confirmed, should fix before merge — record only, no code change.** Amend `plan.md` Expected files and `files.md` to name both paths with their reason. The changes themselves are **accepted**: `Program.cs` is the correct place to fix round-1 finding 1 at its cause, and taking it inside the page would not have worked. |
| 3 | minor | `site.js:1573` introduces the operator-facing literal `'This section could not be loaded.'` outside `OperatorLabels.cs`. | **Rejected, with the reason.** `OperatorLabels` is C# and unreachable from a static script file, and the existing convention already puts script-owned failure state inline — `site.js:834` carries the directly analogous "Quick preview unavailable…". It is a state message, not explanatory copy; it replaced a swallowed failure and closes round-1 finding 4. Introducing a Razor-rendered data attribute for one string would be the new convention the simplicity rails forbid without a recorded reason. |
| 4 | minor | No test proves the `form="case-edit-form"` association actually delivers `inspectionAddress` to `SaveCase`; the server-side POST tests post form data directly and so never exercise it. | **Confirmed, minor — fold into finding 1's browser test.** The mechanism is standard HTML5 and is exercised by the same browser the Browser suite drives, but the claim the fix rests on is unproved. The finding-1 browser scenario should assert the saved value, not only the confirmation. |
| 5 | minor | The new `OperatorLabels.CaseWorkspace` members (lines 1390–1433) are not wrapped in a comment block naming CASE-038, as the parallel-build policy asks. | **Accepted, no change.** They are one contiguous block at the end of an existing region, each member documented; nothing in the wave collides with them. |
| 6 | — | The record no longer draws Open Assessment (`Details.cshtml`, removed), so `/Cases/{id}/Assessment` has no UI entry point until ENG-034 lands its 301. | **Rejected as a defect — deliberate and recorded.** D30 states the record carries no Open Assessment action, and the plan's "Resolution (2026-09-03) — the Assessment handler surface moves with ENG-034" (option B) makes ENG-034, which this ticket blocks, the owner of both the move and the redirect. |
| 7 | — | `catalogue.json`'s Details `default` branch still says "with the edit lease held … edit bar", while the captured page contains no `case-edit-form`, no `data-edit-save` and no `edit-bar`. | **Rejected — pre-existing.** `git show origin/dev:…/case-details--default.html` also contains none of the three; the text predates this branch and this diff neither introduced nor worsened it. The claims this diff **added** (sticky identity ribbon, section jump-nav, eleven section hosts) are all true of the artifact. |

## What was checked and found sound

- Fragment handler (`Details.cshtml.cs:274–320`) is actor-bound through the
  same authorized `IGetCase` load, serves only the closed `LazySectionViews`
  map, refuses every other key with 404, restores the lease before choosing
  supplemental loads, and returns only the named body — proved by
  `TheSectionFragmentReturnsOnlyThatSectionBody` and
  `TheSectionFragmentRefusesKeysItDoesNotServe`. No fragment XSS or
  unauthorized-disclosure path.
- One list per concept: `OperatorLabels.CaseWorkspace.Sections` is the only
  ordered section list; the nav, the eleven hosts, the headings and the
  accepted `?section=` vocabulary all read it.
- `SectionIsDeferred` defers nothing while the lease is held, so a mounting
  body can never land under unsaved input
  (`HoldingTheEditLeaseRendersEverySectionAndDefersNone`).
- Round-1 findings 4, 5, 7 and 9 are closed: the mount keeps every caller's
  callback and answers them all on the one fetch; a failed fetch shows a
  visible state and logs to the console; `AssessmentIsReadOnly` is
  `?.IsReadOnly ?? true` (fails closed); `?section=estimate` is asserted at
  all three widths; the no-script nav comment now states what the code does.
- No migration, no Core or Infrastructure change, no new package, no
  Assessment POST handler, no `Pages/Cases/Assessment/**` edit, no
  `AccessibilityTests.cs` edit, no `scripts/` or `ci.yml` edit.
- No assertion was weakened or deleted: the `CaseTasksWebTests` rewrite drops
  `inspectionAddress` from the in-form field loop only because the control now
  sits outside the form, and replaces it with an exactly-one-occurrence
  assertion over the whole page.
- The simplification pass (`plan.md:635–…`) is dated, complete and honestly
  dispositioned; its one unapplied finding (10) carries a reason. It missed
  finding 1.

## Commands and exit codes (review checkout, Windows, PowerShell 7 / bash)

| Command | Exit |
| --- | --- |
| `git worktree add --detach .worktrees/case-038-review origin/task/case-038-case-workspace-frame` | 0 (`HEAD` = `b5f5ccda93e831f4a808e7048dfd0834b15ca7fd`) |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 — 0 warnings |
| `dotnet test ./tests/Pegasus.Core.Tests/… --no-build` | 0 — 1219 passed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/… --no-build` | 0 — 100 passed |
| `dotnet test ./tests/Pegasus.IntegrationTests/… --filter "FullyQualifiedName~CaseDetailsWebTests&Category!=Browser&Category!=Corpus"` | 0 — 68 passed |
| `gh run list --branch task/case-038-case-workspace-frame --limit 1` | 0 — run `33866768221`, `headSha b5f5ccda9…`, `completed`/`success` |

That scope covers the change: the diff adds no Core or Infrastructure code (so
Core and Architecture are regression cover only), and every changed type on the
Web side — `DetailsModel`, the Razor frame and the section partials — is
exercised by `CaseDetailsWebTests`, whose 68 cases include all five new frame
tests. The Browser and snapshot lanes were not re-run locally: CI ran them
green on this exact head, and the committed artifacts were inspected directly
rather than trusted through a gate. Finding 1 is invisible to all of them.

## What the implementer must change

1. Make the CASE-007 dirty guard see a control associated through `form=`
   (finding 1), and prove it with a browser assertion that edits the
   inspection address, clicks Finish editing, sees the confirmation, and saves
   the typed address (finding 4).
2. Amend `plan.md` Expected files and `files.md` to name
   `src/Pegasus.Web/Program.cs` and `docs/design/test-ui/index.html` with
   their reason (finding 2).

Nothing else is required; findings 3, 5, 6 and 7 need no change.
