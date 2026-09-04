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

---

# Review record — CASE-038 (PR https://github.com/collisionengineers/pegasus/pull/656) — re-review

Head reviewed: `edee9987f6675daf71db3407df958af9dc5a3db4`
(branch `task/case-038-case-workspace-frame`, review round 3 — the fresh
independent read after the round-2 fix). Reviewed in a detached checkout at
`.worktrees/case-038-review`, `git status --porcelain`-clean throughout;
nothing on the lane's branch was changed. Base at review time:
`origin/dev` = `c90f2b891` (dev moved during round 2 — CASE-032 #659 and
DELIV-046 #660 merged).

Reviewers: Claude Opus 5 (independent read, dispositions and gate) and
gpt-5.6-terra xhigh (independent cross-model read, run read-only in the same
detached checkout). Finding 1 below was reached independently by both.

The single new commit at this head is `edee9987f` "Fix CASE-007 dirty guard
for form= associated controls (CASE-038 review)", touching
`src/Pegasus.Web/wwwroot/js/site.js` (+16/−3) and
`tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs` (+84/−3).

## Verdict

**NOT MERGED — the code is sound; the PR is not in a mergeable state.**

No code blocker survives. The round-2 blocker is fixed correctly at its cause
and the whole diff re-reads clean. Merge is blocked by two things outside the
code and one honest-record fix:

1. **The PR is `CONFLICTING`.** `dev` advanced past the reviewed base, and
   `docs/design/test-ui/index.html` and
   `docs/design/test-ui/pages/case-details--default.html` conflict
   (`git merge-tree origin/dev origin/task/case-038-case-workspace-frame` →
   exit 1, two content conflicts; `catalogue.json` and `OperatorLabels.cs`
   auto-merge cleanly).
2. **There is no CI run for this head.** `gh run list --branch
   task/case-038-case-workspace-frame` lists `b5f5ccda9` (`success`,
   `33866768221`) as its newest; `edee9987f` was pushed at 17:23 and had
   produced no run 40 minutes later. The gate requires a green run at the
   reviewed head; there is nothing to gate on. Independently observed by both
   reviewers.
3. Finding 1 below — the new browser test does not prove the thing it is
   recorded as proving.

## Round-2 findings — closure evidence at this head

| Round-2 # | Status | Evidence |
| --- | --- | --- |
| 1 — the CASE-007 dirty guard missed a `form=`-associated control | **Closed at the cause.** | `site.js:578–590` replaces the per-form `input` listener with one delegated `document` listener that resolves the owning form from `event.target.form` (falling back to `closest('form')`), which *does* honour the `form=` association. The admitted set is unchanged — the same `form !== toggle && form.querySelector('input[name="editLeaseToken"]')` predicate the old `bind()` used. The submit-reset listener stays in the root-scoped idempotent `bind()`, so a lazily mounted section's forms still join the guard, and `window.pegasusDirtyEditForm` (Ctrl+S) is untouched. `grep -n "stopPropagation\|stopImmediatePropagation" src/Pegasus.Web/wwwroot/js/site.js` returns nothing, so no handler can stop an `input` event before it reaches `document`. |
| 2 — `Program.cs` and `test-ui/index.html` unnamed in the record | **Closed.** | `plan.md:670–682` "Expected files amendment (Claude, review round, 2026-09-04)" names both with their reason; `files.md:103–104` carries matching rows. |

## Findings and dispositions

| # | Sev | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | should-fix | `LayoutIntegrityTests.cs:216` — `InspectionAddressOutsideEditFormIsGuardedAndSaved` fills `#inspection-address` (outside the form) **and then** `#edit-reason`, which is inside `#case-edit-form` (`_CaseWorkflow.cshtml:195`, inside the form opened at line 161). Playwright's `FillAsync` dispatches an `input` event, so under the **old** per-form listener `#edit-reason` alone would have set `dirtyForm`, the confirmation would have appeared, and the `form=`-associated address would have been submitted and saved regardless. The test therefore passes identically against the bug it is recorded as proving, and it guards no regression of it. `plan.md:683–…` ("proving the confirmation dialog now appears") and the post-implementation report state otherwise. | **Confirmed, fix before merge — reached independently by both reviewers.** The *fix* is correct and I have verified it by inspection; what is unproved is the claim, and the record asserts the proof exists. **Fix inside owned files:** isolate the association — fill only `#inspection-address`, click Finish editing, assert the confirmation, click `[data-edit-finish-keep]`, then supply `reason` (and the mode) and save, asserting the persisted address as it already does. Set the required `reason` without dispatching `input` if the keep-editing step is not wanted. Then correct the proof sentence in `plan.md` and the report. |
| 2 | blocker (state, not code) | The PR is `CONFLICTING` against `origin/dev` (`c90f2b891`): `docs/design/test-ui/index.html` and `docs/design/test-ui/pages/case-details--default.html`. | **Confirmed, blocking the merge.** Merge `origin/dev` into the lane branch and regenerate the two Test UI artifacts under the capture lock (the conflict is in generated snapshot output, so it is resolved by regeneration, not by hand-editing the artifacts). |
| 3 | blocker (state, not code) | No Actions run exists for `edee9987f`. | **Confirmed, blocking the merge.** The gate is a green run at the reviewed head. This resolves itself with the push that carries the finding-1 and finding-2 work. |
| 4 | minor | `site.js:1533–1537` — when a placeholder is in the `failed` cooldown (`Date.now() - attempted < 5000`), `mount()` pushes the caller's callback onto `waiting` and returns; the failure path has already emptied `waiting`, so a jump-nav click landing inside that window neither scrolls nor reports. The callback is answered by the next successful retry. | **Accepted, no change.** The retry is automatic and bounded, the failure is visible on the placeholder and logged to the console, and no result is discarded — the callback is deferred, not dropped. Fixing it would add a timer for a five-second window after a fetch that has already failed once. |
| 5 | — | `AssessmentIsReadOnly` still has no reader in this diff. | **Accepted, unchanged from round 1.** Disclosed as simplification finding 9; ENG-034 (which this ticket blocks) is its stated reader, it adds no query, and at this head it is `?.IsReadOnly ?? true` — fails closed. |

## What was checked and found sound at this head

- **The dirty-guard fix itself.** Delegated listener, same admitted form set,
  `form` IDL property honoured, no `stopPropagation` anywhere in the file,
  submit reset and Ctrl+S paths unchanged. Verified by reading, not inferred
  from a green gate.
- **The committed snapshot artifacts are the real page.**
  `case-details--default.html` 41,720 bytes (CRLF working tree), begins
  `<!DOCTYPE html>`, exactly one `class="case-sticky"`, eleven section hosts
  (`damage`, `engineer-notes`, `estimate`, `files`, `inspection`, `notes`,
  `overview`, `report`, `settlement`, `valuation`, `vehicle`), zero `src="#"`.
  `case-details--conflict.html` 40,755 bytes with the same markers.
  `case-details--unavailable.html` 24,694 bytes, no sticky block — as on
  `origin/dev`. Opened directly; round-1 finding 1 stays closed.
- **The fragment handler** (`Details.cshtml.cs:274–320`) is actor-bound
  through the same authorized `IGetCase` load, normalizes the key against the
  one section list, serves only the closed `LazySectionViews` map, 404s every
  other key, restores the lease before choosing supplemental loads, and
  returns only the named body. Its `catch` excludes `OperationCanceledException`,
  logs, and returns 503 — no suppression.
- **`Program.cs`** adds a matching-only selector
  (`SuppressLinkGeneration = true`, `{handler:regex(^Section$)}`), so
  `/Cases/{id}` and every `?handler=` link generate exactly as before.
- **One list per concept:** `OperatorLabels.CaseWorkspace.Sections` is the only
  ordered section list; `NormalizeSection`, the nav, the eleven hosts and the
  headings all read it. No second copy in Razor, CSS or script.
- No migration, no Core or Infrastructure change, no new package, no
  Assessment POST handler, no `Pages/Cases/Assessment/**`,
  `AccessibilityTests.cs`, `scripts/`, `ci.yml` or `TestUiSnapshotTests.cs`
  edit. `git diff --name-only origin/dev...HEAD` stays inside the amended
  Expected files plus the declared exceptions.
- Every drawn control has a named handler; Sign-off remains a display slot
  (D31); the absent Open Assessment action is D30 and ENG-034's to restore.
- No explanatory copy introduced; no new label outside `OperatorLabels.cs`.
- No assertion weakened or deleted: the `CaseTasksWebTests` and mechanical
  retargets replace each moved claim with an equal or stronger one at the
  layer that can state it.
- The simplification pass (`plan.md:635–669`) is dated and honestly
  dispositioned; its unapplied findings (7, 9, 10) each carry a reason.

## Commands and exit codes (review checkout, Windows, bash)

| Command | Exit |
| --- | --- |
| `git worktree add --detach .worktrees/case-038-review origin/task/case-038-case-workspace-frame` | 0 (`HEAD` = `edee9987f6675daf71db3407df958af9dc5a3db4`) |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 |
| `dotnet test ./tests/Pegasus.Core.Tests/… --no-build` | 0 — 1219 passed, 0 failed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/… --no-build` | 0 — 100 passed, 0 failed |
| `dotnet test ./tests/Pegasus.IntegrationTests/… --filter "FullyQualifiedName~CaseDetailsWebTests\|FullyQualifiedName~CaseTasksWebTests"` | 0 — 68 passed, 0 failed (5m23s) |
| `git merge-tree --write-tree origin/dev origin/task/case-038-case-workspace-frame` | 1 — 2 content conflicts |
| `gh run list --branch task/case-038-case-workspace-frame --limit 3` | 0 — newest run is `33866768221` at `b5f5ccda9`; none at `edee9987f` |

That scope covers the change: the diff adds no Core or Infrastructure code, so
Core and Architecture are regression cover only; every changed type on the Web
side — `DetailsModel`, the Razor frame and the section partials — is exercised
by `CaseDetailsWebTests`, and `CaseTasksWebTests` covers the mechanical
retarget the layout change forced. The Browser and snapshot lanes were not
re-run locally; the committed artifacts were opened and inspected directly
rather than trusted through a gate, and the guard fix was read line by line
because finding 1 means no test currently sees it.

## What the implementer must change

1. Isolate the `form=` association in
   `InspectionAddressOutsideEditFormIsGuardedAndSaved` so the test fails
   against the pre-fix guard, and correct the proof sentences in `plan.md`
   and the post-implementation report (finding 1).
2. Merge `origin/dev` into the lane branch and regenerate
   `docs/design/test-ui/index.html` and
   `docs/design/test-ui/pages/case-details--default.html` under the capture
   lock to clear the conflict (finding 2).
3. Push, and let CI produce a green run at the new head (finding 3).

Nothing else is required; findings 4 and 5 need no change.

---

# Review record — CASE-038 (PR https://github.com/collisionengineers/pegasus/pull/656) — re-review

Head reviewed: `f3005ea667407ea5c9dcd4c298a9add200071855`
(branch `task/case-038-case-workspace-frame`, review round 4 — the fresh
independent read after the round-3 fix). Reviewed in a detached checkout at
`.worktrees/case-038-review`, `git status --porcelain`-clean throughout;
nothing on the lane's branch was changed. Base: `origin/dev` =
`c90f2b8915186efd5bf932cec573846ae75ff1fe`, which this head merges in
(`f3005ea66`), so the PR is no longer `CONFLICTING`.

Reviewers: Claude Opus 5 (independent read, dispositions and gate) and
gpt-5.6-terra xhigh (independent cross-model read, run read-only in the same
detached checkout). Finding 1 below was reached independently by both.

Two commits since the round-3 head: `fc5351e8b` "Test external form dirty
tracking" (`Browser/LayoutIntegrityTests.cs`, +10/−1) and the merge
`f3005ea66` "Merge origin/dev and regenerate Case Test UI", whose only
conflict resolution is the two generated Test UI artifacts.

## Verdict

**REQUEST CHANGES — not merged.** No code defect survives: the round-3
blocker is fixed at exactly the place it was raised, the whole diff re-reads
clean, and the cross-model read found no substantive code, authorization,
XSS, control-handler, label-ownership, Core-policy, snapshot or
merge-resolution defect. What remains is finding 1 — the ticket's
"Contracts handed on" section still hands the six tickets this one blocks a
fragment URL and a form id that do not exist at this head. The gate is also
not yet satisfiable: the exact-head Actions run `33901021975` was still
`in_progress` at review time.

## Round-3 findings — closure evidence at this head

| Round-3 # | Status | Evidence |
| --- | --- | --- |
| 1 — `InspectionAddressOutsideEditFormIsGuardedAndSaved` passed identically against the bug | **Closed at the cause.** | `LayoutIntegrityTests.cs:209–219` now fills **only** `#inspection-address`, then clicks Finish editing and asserts `#edit-finish-confirm` is visible with no `hidden` attribute, then dismisses via `[data-edit-finish-keep]` and asserts it closes — all before `#edit-reason` (the first in-DOM-tree control) is touched at line 231. Under the pre-fix per-form listener no in-tree control has been touched at that point, `dirtyForm` is `null`, the toggle's `if (allowed \|\| !dirtyForm) return;` releases the lease and the confirmation never appears: the assertion fails. The test now guards the regression it is recorded as guarding. The 302 and persisted-`Recorded value` assertions are kept unweakened. `plan.md:698–717` and the report's round-3 section both retract the earlier "proving the confirmation dialog now appears" sentence by name. |
| 2 — PR `CONFLICTING` against `origin/dev` | **Closed.** | `gh pr view 656` → `mergeable: MERGEABLE`. `origin/dev` `c90f2b891` is merged at `f3005ea66`; the two conflicting generated artifacts were resolved by regeneration under the capture lock, not by hand-editing. `git diff origin/dev...HEAD` touches 27 files, all inside the amended Expected files. |
| 3 — no Actions run for the reviewed head | **Partly closed.** | `gh run list` newest is `33901021975`, `headSha f3005ea66…` — the reviewed head — but `status: in_progress` at review time. The gate needs `conclusion: success`. |

## Findings and dispositions

| # | Sev | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | **fix before merge** | The post-implementation report's **"Contracts handed on"** section still states two things that are false at this head, and it is the interface document for the six tickets CASE-038 blocks. (a) *"**ENG-034:** … the fragment URL `/Cases/{id}?handler=Section&section=<key>`"* — `grep -rn 'handler=Section' src tests` returns **nothing**; the fragment moved to its own path `/Cases/{id}/Section?section=<key>` when round-1 finding 1 was fixed at its cause in `Program.cs`. (b) *"**CASE-041:** the inspection form is `case-inspection-address-form`, posts to the Details `Save` handler, and is not the sticky bar's Save target"* — that form was **deleted** by the round-1 finding-3 fix; the only occurrence left in the tree is `CaseTasksWebTests.cs:128`, `Assert.DoesNotContain("case-inspection-address-form", page)`. The Inspection section now contributes one control, `<input id="inspection-address" name="inspectionAddress" form="case-edit-form">`, to the single record form. Deviation 3 (report lines 131–137) is stale for the same reason: it describes "the Inspection form" and "both post `inspectionAddress`". | **Confirmed, fix before merge — record only, no code change. Reached independently by both reviewers.** This is the same class as round-1 finding 8 (the record contradicting the head), which was blocking, and here it is worse-directed: a lane reading the handed-on contract would build CASE-041 against a form id the diff explicitly asserts is absent, and ENG-034 against a URL that 404s. The existing "Record correction (2026-09-04, review finding 8)" section is the pattern — extend it, or supersede the three entries in place, naming the real fragment path and the `form=`-associated control. Also refresh the checklist's snapshot-evidence line, which still records `41,040 bytes` for `case-details--default.html` against the `64,427` this head commits. |
| 2 | minor | `OnGetSectionAsync` (`Details.cshtml.cs:274–320`) does not set `ManualChaseAttemptedAtUtc`, which `OnGetAsync:259` does. `_CaseHistory.cshtml:73` renders it into the manual-chase form's `attemptedAtUtc`, so a `notes` fragment fetched while the caller holds the lease would post `0001-01-01`. | **Accepted, no change — Core owns and refuses it.** `RecordManualCaseChase.cs:55` rejects `AttemptedAtUtc == default`, so the failure direction is a refused chase, not bad data. The path is also unreachable from the frame: `SectionIsDeferred` requires `LeaseToken is null`, and `_CaseHistory`'s `mayEdit` requires a lease, so the chase form never renders in a mounted fragment. |
| 3 | — | `catalogue.json`'s Details `default` branch text — rejected in round 2 as inaccurate-but-pre-existing about "the edit lease held … edit bar". | **Now correct, no action.** `origin/dev`'s UIIMP-015 (`b7fa4f70c`) tightened the `case-details--default` matcher to the Review state, and the regenerated artifact at this head carries `case-edit-form` (3), `edit-bar` (1), `data-edit-save` (1), `section-nav` (1) and `case-context` (1). Every clause of the branch text is now true of the artifact. |
| 4 | — | Round-3 findings 4 (jump-nav click inside the 5s failed-fetch cooldown) and 5 (`AssessmentIsReadOnly` has no reader). | **Accepted, unchanged.** No result is discarded — the callback is deferred to the automatic retry; `AssessmentIsReadOnly` is `?.IsReadOnly ?? true` (fails closed) and ENG-034 is its declared reader. |

## What was checked and found sound at this head

- **The committed snapshot artifacts are the real page**, opened directly, not
  trusted through a gate. `case-details--default.html`: 64,427 bytes as
  committed (65,484 in the CRLF working tree), begins `<!DOCTYPE html>`,
  exactly one `class="case-sticky"`, eleven section hosts (`damage`,
  `engineer-notes`, `estimate`, `files`, `inspection`, `notes`, `overview`,
  `report`, `settlement`, `valuation`, `vehicle`), zero `src="#"`.
  `case-details--conflict.html`: 40,091 bytes committed, same markers, three
  `data-lazy` placeholders (the no-lease state).
  `case-details--unavailable.html`: 24,390 bytes, no sticky block, as on
  `origin/dev`. `index.html`'s only change is the regenerated Details
  `default` sentence.
- **The dirty-guard fix**: one delegated `document` `input` listener resolving
  `event.target.form` (which honours `form=`), the same admitted form set
  (`form !== toggle && form.querySelector('input[name="editLeaseToken"]')`),
  the submit-reset still in the root-scoped idempotent `bind()` so a lazily
  mounted section's forms join the guard, `window.pegasusDirtyEditForm`
  (Ctrl+S) untouched, and no `stopPropagation` anywhere in the file.
- **One editor**: `_CaseDataHiddenFields.cshtml` is deleted, `_CaseWorkflow`'s
  hidden `inspectionAddress` is removed, and the two render conditions are
  byte-identical (`!string.IsNullOrWhiteSpace(leaseToken) && workflow.Archive
  is null && data is not null`), so the associated control is never orphaned
  and `inspectionAddress` has exactly one entry.
- **The fragment handler** is actor-bound through the same authorized
  `IGetCase` load, normalizes the key against the one section list, serves
  only the closed three-entry `LazySectionViews` map, 404s every other key,
  restores the lease before choosing supplemental loads, and returns only the
  named body. Its `catch` excludes `OperationCanceledException`, logs, and
  returns 503 — no suppression.
- **`Program.cs`**: matching-only selector, `SuppressLinkGeneration = true`,
  `{handler:regex(^Section$)}` — `/Cases/{id}` and every `?handler=` link
  generate exactly as before.
- **One list per concept**: `OperatorLabels.CaseWorkspace.Sections` is the only
  ordered section list; `NormalizeSection`, `_CaseWorkspaceNav`, the eleven
  hosts, the four shells' headings and the `?section=` vocabulary all read it.
  All eleven icon ids resolve. No second copy in Razor, CSS or script.
- **Scope**: `git diff --name-only origin/dev...HEAD` is 27 files, every one
  inside the plan's amended Expected files plus the declared
  `Pages/Cases/Shared/*` exception. No migration, no Core or Infrastructure
  change, no new package, no Assessment POST handler, no
  `Pages/Cases/Assessment/**`, `AccessibilityTests.cs`, `scripts/`, `ci.yml`
  or `TestUiSnapshotTests.cs` edit.
- **CSS**: `case-section-nav` is retired from every rule including the 980px,
  reduced-motion and forced-colors blocks; `case-workspace`, `case-context`
  and `case-main` keep live consumers. No dead selector left.
- Every drawn control has a named handler; Sign-off is a display slot (D31);
  the absent Open Assessment action is D30 and ENG-034's to restore; the four
  ENG-034 shells are heading-only, with no control, prose or placeholder.
- No explanatory copy introduced; no new operator label outside
  `Presentation/OperatorLabels.cs`; absent values render `AbsentValue`
  ("Not recorded"), never a blank.
- No assertion weakened or deleted. `CaseTasksWebTests`' rewrite replaces the
  in-form `inspectionAddress` ordering claim with a stronger pair — the old
  form is asserted absent and the `form=`-associated control asserted present
  exactly once over the whole page.
- The simplification pass (`plan.md:635–669`) is dated and honestly
  dispositioned; its unapplied findings (7, 9, 10) each carry a reason.

## Commands and exit codes (review checkout, Windows, bash)

| Command | Exit |
| --- | --- |
| `git worktree add --detach .worktrees/case-038-review origin/task/case-038-case-workspace-frame` | 0 (`HEAD` = `f3005ea667407ea5c9dcd4c298a9add200071855`) |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 — 0 warnings, 0 errors |
| `dotnet test ./tests/Pegasus.Core.Tests/… --no-build` | 0 — 1225 passed, 0 failed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/… --no-build` | 0 — 100 passed, 0 failed |
| `dotnet test ./tests/Pegasus.IntegrationTests/… --filter "FullyQualifiedName~CaseDetailsWebTests\|FullyQualifiedName~CaseTasksWebTests"` | 0 — 68 passed, 0 failed (5m32s) |
| `gh pr view 656 --json mergeable,mergeStateStatus` | 0 — `MERGEABLE` / `UNSTABLE` (run in flight) |
| `gh run list --branch task/case-038-case-workspace-frame --limit 3` | 0 — newest `33901021975`, `headSha f3005ea66…`, `in_progress` |

That scope covers the change: the diff adds no Core or Infrastructure code, so
Core and Architecture are regression cover only (and cover the merged
`origin/dev` Core changes this head absorbs); every changed type on the Web
side — `DetailsModel`, the Razor frame, the section partials, `Program.cs`'s
selector — is exercised by `CaseDetailsWebTests`, and `CaseTasksWebTests`
covers the one-editor claim the round-1 fix rests on. The Browser and snapshot
lanes were not re-run locally; the committed artifacts were opened and
inspected directly rather than trusted through a gate, and the guard fix and
its new browser assertion were read line by line.

## What the implementer must change

1. Correct the post-implementation report's "Contracts handed on" entries for
   **ENG-034** (the fragment URL is `/Cases/{id}/Section?section=<key>`) and
   **CASE-041** (there is no `case-inspection-address-form`; the Inspection
   section contributes `<input id="inspection-address" name="inspectionAddress"
   form="case-edit-form">` to the one record form), and supersede deviation 3,
   which describes the deleted Inspection form (finding 1). Refresh the
   checklist's `case-details--default.html` byte-size evidence to this head's
   `64,427`.
2. Let the exact-head Actions run `33901021975` finish green (round-3 finding
   3 / the gate). No push is needed for the gate itself; the finding-1 edits
   are board documents, not repository files, so they do not move the head.

Nothing else is required; findings 2, 3 and 4 need no change.

### Addendum — the exact-head CI run completed after the review was written

Run `33901021975` (`headSha f3005ea667407ea5c9dcd4c298a9add200071855`)
finished **`failure`**. One job failed: **`test-ui`** (`infrastructure` was
skipped as a consequence; every other job succeeded).

The failure is in the capture's browser phase and is **not an assertion
failure**:

```
Pegasus.IntegrationTests.Browser.LayoutIntegrityTests
  .InspectionAddressOutsideEditFormIsGuardedAndSaved [FAIL]
  Microsoft.Playwright.PlaywrightException :
    net::ERR_NO_BUFFER_SPACE at http://127.0.0.1:64767/Cases/efbff…
  Call log: - navigating to "…/Cases/efbff…", waiting until "networkidle"
  at BrowserTestSupport.GoToAsync(String relativePath) : BrowserTestSupport.cs:129
  at LayoutIntegrityTests.InspectionAddressOutsideEditFormIsGuardedAndSaved()
     : LayoutIntegrityTests.cs:202
Failed!  - Failed: 1, Passed: 123, Skipped: 0, Total: 124, Duration: 11m39s
Test UI phase 'Capture browser responses' failed with exit code 1.
```

`ERR_NO_BUFFER_SPACE` on the very first `GoToAsync` (line 202, before any
assertion in the scenario runs) is runner socket/buffer exhaustion under the
parallel browser capture, not a defect in the code or the test's claim. The
same test passed locally in this lane's own round-3 run
(`--filter FullyQualifiedName~LayoutIntegrityTests`, 70 passed, exit 0) and
123 of the 124 browser cases passed in this very run.

**Disposition: the gate is not met and the PR is not merged.** The reviewer's
rerun allowance in this workflow covers only a failed `changes` job, so
`test-ui` is not rerun here. This is a lane-external state finding, on the
same footing as round-3 findings 2 and 3:

3. Rerun the failed `test-ui` job on run `33901021975`
   (`gh run rerun 33901021975 --failed`) and require `conclusion: success` at
   `f3005ea66` before merge. If it fails again at the same place, the cause is
   browser-capture runner pressure and belongs to the Test UI cost lane, not
   to CASE-038 — raise it there rather than changing this ticket's code.

# Review record — CASE-038 (PR https://github.com/collisionengineers/pegasus/pull/656) — round 5, approved and merged

Head reviewed: `f3005ea667407ea5c9dcd4c298a9add200071855` — the same head as
round 4, unmoved (`gh run list` and `git rev-parse HEAD` both confirm it). The
round-4 items were board-document edits and a CI rerun, neither of which moves
the head, so no new commit exists and no re-verification of the diff's shape
was needed beyond a fresh independent read.

Reviewed in a detached checkout at `.worktrees/case-038-review`, created from
`origin/task/case-038-case-workspace-frame`, `git status --porcelain`-clean
throughout. Base: `origin/dev`.

Reviewers: Claude Opus 5 (independent read, dispositions, gate and merge) and
gpt-5.6-terra at xhigh (independent cross-model read, run read-only in the
same detached checkout, told to re-derive rather than restate rounds 1–4).

## Verdict

**APPROVE and merge.** No code defect exists at this head. The cross-model
read returned no BLOCKER and no code, authorization, encoding, handler,
Core-policy, migration, tooling-no-touch or merge-resolution defect; its five
findings are two record-accuracy items and three test-oracle-strength
observations, all dispositioned below with evidence. The two round-4 items are
closed: the report's handed-on contract is corrected in place, and the
exact-head Actions run finished green.

## Round-4 items — closure evidence at this head

| Round-4 # | Status | Evidence |
| --- | --- | --- |
| 1 — the report's "Contracts handed on" named a fragment URL and a form id that do not exist | **Closed.** | `post-implementation-report.md:511–539`, a new section "Record correction (2026-09-04, review round 3 — controller)" that states "They are superseded here; the earlier text stands only as history" and then supersedes all three entries by name: ENG-034's fragment URL is corrected to `/Cases/{id}/Section?section=<key>`; CASE-041's entry is corrected to "There is no `case-inspection-address-form` any more … the Inspection section contributes one control, `<input id="inspection-address" name="inspectionAddress" form="case-edit-form">`, to the single record form", with the instruction that CASE-041's controls must associate via `form=` with no form of their own; deviation 3 is retracted by name. `checklist.md` line 27 carries the refreshed snapshot evidence (`64,427` bytes), superseding the `41,040` figure by name. |
| 2 / the gate — the exact-head Actions run was `in_progress`, then `failure` on `test-ui` (`net::ERR_NO_BUFFER_SPACE`, a runner socket exhaustion, not an assertion) | **Closed.** | Run `33901021975`, `headSha f3005ea667407ea5c9dcd4c298a9add200071855`, `status: completed`, **`conclusion: success`**. Jobs: `test-ui`, `browser`, `unit`, `documentation`, `reference-data`, `sql-integration (1,2,3)`, `sql-integration-coverage`, `local-development-scripts`, `changes` all `success`; `infrastructure` `skipped` (the diff touches no `infra/**`). The rerun confirms the earlier failure was runner pressure, exactly as round 4 diagnosed; nothing in the code changed between the failure and the pass. |

## Findings and dispositions (round 5)

| # | Sev raised | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | SHOULD-FIX | `wwwroot/js/site.js:1585` — the lazy-fragment failure text `'This section could not be loaded.'` is an operator-visible literal in JS rather than in `Presentation/OperatorLabels.cs`. | **Rejected with reason — the existing convention wins.** `git show origin/dev:src/Pegasus.Web/wwwroot/js/site.js` already carries seven operator-visible literals of exactly this kind, set the same way: `'Refreshing'` (66), `'Copied'` (87), `'Choose files'` (218), `'Choose different files'` (250), `'No matching cases found'` (477), `'Loading quick preview…'` (792), `'Quick preview unavailable. Open the message for full detail.'` (822). This ticket adds an eighth in the established shape, not a new practice. Moving script-set transient strings into `OperatorLabels` is a repo-wide convention change touching a file this ticket does not own the policy for, and is a follow-up, not a CASE-038 defect. The string is one failure value, not explanatory copy, and the failure is not swallowed (the placeholder is marked `data-lazy-state="failed"` and the cause is logged to `console.error`). |
| 2 | MINOR | `Presentation/OperatorLabels.cs:1409` — the new members are contiguous but the block's comment (`// The identity ribbon the frame itself renders (D29, D31).`) does not name CASE-038, as the parallel-build policy asks. | **Accepted, no change.** The policy's stated purpose — "so concurrent additions merge cleanly" — is served by contiguity, and is demonstrated: this head merges `origin/dev` (`c90f2b891`) with no conflict in this file. Every added member carries a doc comment naming its governing decision (D30, D29/D31), which is the stronger provenance. Not worth a sixth round on a file three other lanes are waiting to append to. |
| 3 | SHOULD-FIX | `CaseDetailsWebTests.cs:170–171` — `CaseSectionKeys` (defined at `:1453` as `[.. OperatorLabels.CaseWorkspace.Sections.Select(s => s.Key)]`) is derived from the same production list the page renders, so reordering production changes expected and actual together; the D30 order is not independently pinned by that assertion. | **Accepted with reason — the claim the test makes is the claim it proves, and D30 is pinned elsewhere.** That assertion's documented claim is *one list per concept* — that the hosts and the jump-nav both read the single ordered list and neither keeps a second copy — and for that claim deriving from production is correct, not tautological. The D30 *vocabulary and order* are independently pinned three other ways at this head: `TheAddressedSectionIsRenderedAndMarkedCurrent` (`:189–199`) uses eleven test-local literal keys including the three deleted pre-redesign keys (`valuations`, `inspection-address`, `case-files`) and asserts each resolves to Overview rather than being aliased; `TheSectionFragmentRefusesKeysItDoesNotServe` (`:259–264`) pins the served/refused split by literal; and the committed snapshot `case-details--default.html` carries the eleven `id="section-<key>"` hosts as literal bytes under CI's `-Verify`. No assertion was weakened or deleted to reach this shape. |
| 4 | SHOULD-FIX | (a) `CaseDetailsWebTests.cs:248–251` — the fragment tests assert only the *absence* of frame chrome, so an empty or wrong body for `files`/`notes`/`vehicle` would pass. (b) `Browser/LayoutIntegrityTests.cs:155–158` — the evidence-viewer half of the mounted-control proof (`[data-evidence-item]:not([data-evidence-item-bound])` count `== 0`) is vacuous on a case seeded with no evidence images. | **Accepted with reason, and the record corrected here so no lane over-reads it.** (a) is covered end-to-end for the one fragment that matters: `LayoutIntegrityTests.cs:142` asserts `#section-files .panel` count `> 0` after the fragment mounts, at all three widths, so an empty Files fragment fails. `notes` and `vehicle` bodies are not content-asserted — a real but narrow gap in a body neither this ticket nor its blocked lanes author. (b) is confirmed: `SeedAcceptedCaseAsync` (`:282–292`) seeds an accepted case with no evidence image, so that one assertion is vacuous as written. The dialog half is **not** vacuous — it is filtered to `[data-dialog-open]` controls whose target `[data-dialog=…]` exists, and the Case record renders several. **Correction of the record:** checklist Step 4's phrase "assert a lazily mounted Files body opens its evidence viewer and dialogs" overstates what ships. What is proven at this head is: the mounted Files body arrives non-empty, and every dialog opener in the document (including the mounted body's) is bound by the root-scoped idempotent `bind(root)`. The evidence-viewer binding on a mounted body is *asserted but not exercised*. Reviewer's judgement: not a merge blocker — nothing is weakened or deleted, the binder is one shared root-scoped function whose dialog half is proven on the same mounted node, and the behaviour was read line by line. Any lane relying on evidence-viewer-after-mount should seed an evidence item; UIIMP-014, which owns the per-section Case snapshot states, is the natural home for that. |
| 5 | SHOULD-FIX | `post-implementation-report.md:21` — the heading "Files changed (21)" is wrong at this head (`git diff --name-only origin/dev...HEAD` is 27), its `_CaseInspectionAddress.cshtml` bullet still describes a renamed form that was deleted, and the inventory omits `Program.cs`, `_CaseDataHiddenFields.cshtml` (deleted), `_CaseWorkflow.cshtml`, the six retargeted test files and the `docs/design/**` artifacts. Codex also read `checklist.md:3` as claiming an unfiltered grep returns nothing. | **Partly rejected, partly accepted — no further round.** The `checklist.md:3` half is **rejected**: that line's own parenthetical already states the true, qualified result — "the only remaining matches are immutable EF migration snapshots, PLAT-070's own drop migration and an absence assertion — recorded in the report" — which I confirmed (`git grep -il` returns only `Persistence/Migrations/**` designer snapshots plus the absence assertion; no live surface under `src/Pegasus.Web` or `src/Pegasus.Core`). The inventory half is **confirmed and accepted**: it is stale. It is not blocking, because the two interface-bearing statements it contained were already superseded by name in the round-3 correction section under an explicit "the earlier text stands only as history" preamble, and because `files/files.md` carries `Program.cs` in its own "Review-round amendment". The residual is a count and an inventory in a section the document itself marks as history. **The authoritative file inventory for the six blocked lanes is recorded here instead**, from `git diff --name-only origin/dev...HEAD` at `f3005ea66` — 27 files: `docs/design/README.md`; `docs/design/test-ui/{catalogue.json,index.html}`; `docs/design/test-ui/pages/case-details--{conflict,default}.html`; `src/Pegasus.Web/Pages/Cases/Details.cshtml{,.cs}`; `src/Pegasus.Web/Pages/Cases/Shared/{_CaseDamage,_CaseEstimate,_CaseReport,_CaseSettlement}.cshtml` (created); `.../Shared/_CaseDataHiddenFields.cshtml` (**deleted**); `.../Shared/{_CaseInspectionAddress,_CaseWorkflow,_CaseWorkspaceNav}.cshtml`; `src/Pegasus.Web/Presentation/OperatorLabels.cs`; `src/Pegasus.Web/Program.cs`; `src/Pegasus.Web/wwwroot/{css/site.css,js/site.js}`; and the tests `Browser/LayoutIntegrityTests.cs`, `Browser/OperatorJourneyTests.cs`, `Case{CustodyWebTests,DetailsWebTests,TasksWebTests,VehicleWebTests}.cs`, `Image{IntakeWebTests,ViewingWebTests}.cs`. |

## What was checked and found sound at this head (my own read)

- **The head is the reviewed head.** `git rev-parse HEAD` in the fresh
  detached checkout = `f3005ea667407ea5c9dcd4c298a9add200071855`, and the
  branch has not moved since round 4.
- **The committed Test UI artifacts are the real pages**, opened directly by
  byte, not trusted through the gate. `case-details--default.html`: 64,427
  bytes as committed (`git cat-file -s`), begins `<!DOCTYPE html>`, exactly
  one `class="case-sticky"`, seventeen `id="section-…"` matches resolving to
  the eleven hosts (`damage`, `engineer-notes`, `estimate`, `files`,
  `inspection`, `notes`, `overview`, `report`, `settlement`, `valuation`,
  `vehicle`) plus six `-title` ids, zero `<img src="#">`.
  `case-details--conflict.html`: 40,091 bytes, same markers.
  `case-details--unavailable.html`: 24,390 bytes, no sticky block, unchanged
  from `origin/dev`. All three match the checklist's and the report's
  refreshed figures exactly.
- **Scope**: 27 files, every one inside `files/files.md` including its two
  plan-stage corrections, its Review-round amendment (`Program.cs`,
  `index.html`) and the declared `Pages/Cases/Shared/*` exception. No
  migration, so `Test-MigrationGrants.ps1` does not apply. No `Pegasus.Core`
  or `Pegasus.Infrastructure` change. No new package. No edit to
  `TestUiSnapshotTests.cs`, `.github/workflows/ci.yml` or any `scripts/*.ps1`
  (the TOOLING NO-TOUCH set), to `docs/operator-notes.md`, or to `corpus/`.
- **`Program.cs`** adds a matching-only selector with
  `SuppressLinkGeneration = true` and `{handler:regex(^Section$)}`, so
  `/Cases/{id}` and every `?handler=` link generate exactly as before, and a
  fragment response can never be mistaken for the page itself — the cause of
  round-1 finding 1, fixed at the cause rather than in the snapshot harness.
- **One list per concept**: `OperatorLabels.CaseWorkspace.Sections` is the one
  ordered eleven-entry `Key`/`Label`/`Icon` list; the page model's `?section=`
  vocabulary, `_CaseWorkspaceNav`, the eleven hosts and the four ENG-034 shell
  headings all read it. No second section list in Razor, CSS or script.
- **Labels**: every new operator-visible label is in
  `Presentation/OperatorLabels.cs` (the ribbon set, `SectionNav`,
  `DefaultSectionKey`, `AbsentValue = "Not recorded"`), with the one
  script-set failure string dispositioned as finding 1. No explanatory copy;
  absent values render `AbsentValue`, never a blank, and absent stays distinct
  from disabled.
- **Tests prove the claim, none weakened**: no assertion is deleted or
  relaxed anywhere in the diff. `CaseTasksWebTests`' rewrite replaces a
  weaker in-form ordering claim with a stronger pair (old form asserted
  absent, `form=`-associated control asserted present exactly once over the
  page). `InspectionAddressOutsideEditFormIsGuardedAndSaved` fills only
  `#inspection-address` before any in-DOM-tree control, then asserts the
  Finish-editing confirmation is visible — which fails under the pre-fix
  per-form listener, so it guards the regression it is recorded as guarding.
- The simplification pass (`plan.md:635–669`) is dated, its findings real, and
  each unapplied finding (7, 9, 10) carries a reason.

## Commands and exit codes (review checkout, Windows, bash)

| Command | Exit |
| --- | --- |
| `git fetch origin task/case-038-case-workspace-frame` | 0 |
| `git worktree add --detach .worktrees/case-038-review origin/task/case-038-case-workspace-frame` | 0 — `HEAD` = `f3005ea667407ea5c9dcd4c298a9add200071855` |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 — 0 warnings, 0 errors |
| `dotnet test ./tests/Pegasus.Core.Tests/… --no-build` | 0 — 1225 passed, 0 failed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/… --no-build` | 0 — 100 passed, 0 failed |
| `dotnet test ./tests/Pegasus.IntegrationTests/… --filter "FullyQualifiedName~CaseDetailsWebTests\|FullyQualifiedName~CaseTasksWebTests"` | 0 — 68 passed, 0 failed (5m46s) |
| `codex exec -m gpt-5.6-terra -c model_reasoning_effort=xhigh` (independent read) | 0 |
| `gh run view 33901021975 --json status,conclusion,headSha` | 0 — `completed` / **`success`** / `f3005ea667407ea5c9dcd4c298a9add200071855` |

Why that scope covers the change: the diff adds no `Pegasus.Core` or
`Pegasus.Infrastructure` code, so Core and Architecture are regression cover
only (and cover the `origin/dev` Core changes this head absorbs by merge);
every changed type on the Web side — `DetailsModel` and its
`OnGetSectionAsync`, the Razor frame, the section partials, `OperatorLabels`
and `Program.cs`'s selector — is exercised by `CaseDetailsWebTests`, and
`CaseTasksWebTests` covers the one-editor claim the round-1 fix rests on. The
Browser and snapshot lanes were not re-run locally because CI ran both green
on this exact head (`browser` and `test-ui` jobs, run `33901021975`); the
committed snapshot artifacts were nevertheless opened and measured directly
rather than trusted through that gate.

## Outcome

Approved. No blocker remains. Merged to `dev` with `gh pr merge 656 --merge`;
the merge commit SHA and the stage move to Verifying are recorded on the
ticket.
