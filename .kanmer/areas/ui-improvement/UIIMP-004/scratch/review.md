# Independent review — 2026-08-26

## Changes

PR #562 replaces the hand-authored Test UI inventory and pages with a JSON manifest, test-only ASP.NET response capture, response selection/normalization, generated HTML, and update/verify scripts. It removes three obsolete states and renames three reworked states, and updates design/runbook guidance.

## Comments

1. **Blocking — generated defaults do not represent their declared Razor branches.** `TestUiSnapshotTests.Generate` selects an unmarked default as any route response that does not match another state's marker (`TestUiSnapshotTests.cs:90-101`). That admits access-denied/error responses. The committed `inbox--default.html`, `upload-group-status--default.html`, and `upload-status--default.html` all render `<title>Access denied</title>` / `<h1>Access denied</h1>`, while `catalogue.json` declares populated Inbox and completed upload outcomes (manifest lines 482-501, 634-647, 672-691). `administration-mail-categories--default.html` is likewise Access Denied although its branch says the add-category form. Therefore the 57/57 generation count and byte comparison prove reproducibility of wrong selections, not current-branch parity.

2. **Blocking — the ticket's required post-JavaScript DOM and screenshot parity was not implemented.** The ticket body requires normalized DOM parity and standard live/offline screenshots for every visual state, and research records the user's post-JavaScript decision. The PR contains no Playwright/DOM/screenshot comparison in `TestUiSnapshotTests`; it captures raw middleware response HTML and performs string byte comparison only. The plan silently changes verification to server-response byte comparison, while `open-questions` still says preserve 60 states and the ticket body still lists 60/post-JavaScript/screenshots. This misses both the approved plan implication and the ticket acceptance criteria.

3. **Blocking — offline URLs remain unresolved and the validator explicitly ignores them.** `NormalizeAndRewrite` returns unmatched application URLs unchanged (`TestUiSnapshotTests.cs:134-146`). Generated pages retain root-relative sign-out actions, case export actions, retained-source links, and `/Received/{guid}/Image` image sources. For example, `vehicle-images-details--default.html:116-121` contains a root-relative evidence image and download target, which cannot load when the HTML is opened locally. `Test-UiCatalogue.ps1:131` now exempts every root-relative reference, so its “0 broken local references” result cannot substantiate local viewability.

4. **Blocking — volatile identifiers are not comprehensively normalized.** The declared normalization covers antiforgery, a small named hidden-value set, and cache fingerprints, but generated paths and image/source URLs retain request-specific GUIDs (for example `case-details--default.html:106` and `upload-group-status--needs-decision.html:127-145`). A retained-capture update/verify can pass while a genuinely independent recapture drifts; the post-implementation report does not identify whether verify used `-SkipCapture`.

5. **Non-blocking process issue — review was requested while UIIMP-004 is still in Implementing, not Review.** No merge should occur until fixes are committed, evidence is corrected, and the ticket enters Review.

## Disposition

- Comments 1-4: needs changes in UIIMP-004 before re-review.
- Comment 5: correct stage after implementation fixes.
- No files were edited and the PR was not merged.

## Verdict

**Needs changes.** The report does not match the actual generated output or the ticket's parity acceptance criteria. The simplification pass is structurally honest about reuse and isolation, but its “no further simplification” disposition does not compensate for the correctness gaps above.

# Independent re-review — commit 35292cff — 2026-08-26

## Re-check of prior blockers

1. **Resolved — default selection.** Explicit markers were added for the four affected defaults, and the committed pages now render Outlook categories, Inbox, completed grouped upload, and completed upload rather than Access Denied.

2. **Still blocking — Chromium parity is tautological and does not compare Live rendering with offline rendering.** `VerifyBrowserParityAsync` writes `file.Value` (the already normalized and URL-rewritten generated HTML) to `.test-ui-live.html` beside the committed file, after the preceding assertion established that `file.Value` equals the committed bytes. It then opens those two same-directory, byte-identical local files and compares them (`TestUiSnapshotTests.cs:81-98`). This necessarily gives both pages the same rewritten URLs and broken/missing assets. It is not a browser render of the live application response or live URL, so it cannot substantiate the report's “live/offline” post-JavaScript DOM and screenshot parity claim.

3. **Partially resolved, still blocking for visual parity — root URLs no longer escape offline and validation checks them, but current evidence images are replaced with `#`.** Unmatched application URLs now become inert fragments and the validator no longer ignores root references. That is correct for non-executable form/download targets. It is not correct for visual image sources: `vehicle-images-details--default.html:121` and `upload-group-status--needs-decision.html:128,145` now contain `<img src=\"#\">`. Live Razor serves actual authorized evidence thumbnails at those locations. The tautological browser comparison uses the same `#` sources on both sides, so it cannot detect this visible loss. Absolute visual parity remains unproved and false for these states.

4. **Resolved — GUID normalization.** GUIDs are consistently mapped to per-page typed placeholders before URL rewriting, and no raw GUID remains in generated pages.

## Verification performed

- `Update-TestUiSnapshots.ps1 -Verify -SkipCapture`: pass, 1/1 snapshot test in 57 seconds.
- Searched all generated pages: no remaining root-relative references and no raw GUIDs.
- Inspected the four corrected default pages and the Chromium comparison implementation.

## Verdict

**Needs changes.** Prior findings 1 and 4 are resolved. Finding 2 remains, and finding 3 is syntactically resolved but still violates visual parity for evidence-image states. Compare browser output from the actual captured/live Razor page (with its image responses/assets available) against the offline snapshot, or materialize approved local image sources before comparison. Do not claim live/offline screenshot parity by comparing two identical rewritten local files.

# Final independent re-review — commit 44d16f46 — 2026-08-26

## Prior findings

1. **Resolved — state selection.** Explicit markers remain in place and the four formerly incorrect defaults render their declared current branches.
2. **Resolved — browser evidence.** The tautological two-local-file comparison was removed. Durable verification now keeps normalized Razor byte equality, opens every committed offline page in Chromium, captures a full-page screenshot, and requires every visible image to decode with positive natural width. The report also records the actual Chrome live/offline check and exact sign-in DOM/geometry comparison.
3. **Resolved — offline images and root URLs.** The middleware captures exact `image/*` response bytes; generation substitutes matching Razor image URLs with data URLs before GUID/route rewriting. The committed vehicle and grouped-upload evidence images contain captured PNG bytes rather than `#`, and catalogue validation reports zero broken local references.
4. **Resolved — GUID normalization.** Per-page deterministic GUID placeholders remain and no raw GUID is present in generated pages.

## Checks

- Inspected commit `44d16f46` and the response-capture/generator/browser-test changes.
- `scripts/Test-UiCatalogue.ps1`: pass — 52 routed sources, 57 prototypes, 0 broken local references.
- Confirmed embedded image data in vehicle detail and upload group pages; no visible `img src=\"#\"` remains.
- A local `Update-TestUiSnapshots.ps1 -Verify -SkipCapture` attempt could not build because an existing .NET/MSBuild process 75056 held `Pegasus.Core.dll`; this is a concurrent-worktree lock, not a failure of the test. The committed report supplies the clean verification evidence.

## Plan/report/simplification

The implementation now meets the corrected current 57-state plan and its Razor-origin/offline-viewability boundary. The report accurately records the review corrections and live/offline Chrome evidence. The simplification pass remains honest: the solution reuses the existing integration host, tests, assets and Playwright, and adds no production runtime path.

## Verdict

**Approve.** No remaining blocking finding in PR #562 at `44d16f46`. Merge remains subject to the repository's green-CI requirement; this reviewer did not merge.

# Independent scope re-review — commit f7c87173 — 2026-08-26

## Delta

The new commit changes the shared CI action from `actions/setup-dotnet` `10.0.x` to `10.0.302` and changes `global.json` roll-forward from `latestFeature` to `latestPatch`.

## Correctness

The two settings are mechanically compatible. Every .NET CI lane uses `.github/actions/dotnet-build/action.yml`; setup installs the 10.0.302 feature band and `latestPatch` prevents the resolver from choosing 10.0.400. It permits a later servicing patch in the 10.0.3xx feature band when one is installed.

## Blocking findings

1. **The supplied local evidence does not validate the exact clean-CI SDK being requested.** The passing local shard used 10.0.303, while the shared action explicitly installs 10.0.302. On a clean runner the effective SDK can therefore be 10.0.302, not the validated 10.0.303. Either pin/install the actually validated 10.0.303, validate 10.0.302, or let the current GitHub run prove the clean-runner effective SDK and shard before merge.

2. **The causal evidence is insufficient for a repository-wide toolchain policy change.** The failed GitHub run shows one MailWorkspace HTML substring assertion after 310 other shard tests passed. A passing run under 10.0.303 establishes compatibility with that SDK but does not establish that 10.0.400 caused the failure rather than test state/timing/order. No comparison under 10.0.400 or identified SDK behavioral change is recorded.

3. **The change is outside UIIMP-004's planned files and implementation scope.** The ticket's files/plan cover Test UI capture, snapshots, scripts and UI documentation; they do not authorize a repository-wide SDK/CI policy change. Repository review rules say unplanned extras belong in their own ticket. This should be a separately tracked CI/toolchain fix (and then merged/rebased into the UI branch if needed), rather than being smuggled into the UI parity PR.

## Verdict

**Block pending scope/evidence correction.** There is no intrinsic `setup-dotnet`/`global.json` incompatibility, but PR #562 should not carry this unplanned repository-wide change on the evidence currently supplied. If the active GitHub run proves 10.0.302 and the owning ticket is explicitly expanded by the operator, re-review can clear the technical evidence point; otherwise move the pin to its own ticket/PR.

# Independent re-review — commit f840d48a — 2026-08-26

## Code and test

**Technically approved.** The SDK experiment is fully reverted: relative to `origin/dev`, neither `global.json` nor the shared build action has a net change.

The Mail candidate-link fix is correct and narrow:

- only the candidate anchor at `Message.cshtml:472-481` changes;
- `AssociationCandidateUrl` passes the same message id, mailbox, folder, page, search, queue, section, case query and target case id values previously supplied through Tag Helpers;
- `QueryHelpers.AddQueryString` is an existing repository convention (`Cases/Index.cshtml.cs`) and correctly omits null values and encodes supplied values;
- invariant page-number formatting and D-format GUIDs are explicit;
- the test now extracts the exact anchor containing the expected `targetCaseId`, HTML-decodes its href, and checks the two route values GitHub demonstrated were at risk. It no longer passes because those strings occur elsewhere in the page.

No unnecessary abstraction or compatibility path was added.

## Remaining blocking documentation issue

The operator authorized expanding UIIMP-004 to resolve the GitHub failure, but the ticket plan does **not yet record the implemented final scope**. Its “CI-resolution scope expansion” still limits work to SDK determinism and explicitly says “Do not change the unrelated MailWorkspace behavior”; that hypothesis was disproven and reverted. The actual narrow MailWorkspace URL fix is described only in the post-implementation report.

Repository workflow says the plan owns whole-task scope and review must compare implementation against it. Update the plan expansion to record: SDK hypothesis disproven/reverted; GitHub isolated the candidate-anchor URL omission; authorized scope is the one-link `QueryHelpers` correction and exact-anchor regression test; acceptance is focused/local plus fresh GitHub shard. Then the diff and plan will agree.

## Verdict

**Block only on plan correction and required green CI.** The implementation itself is approved and has no remaining correctness, simplicity, or net toolchain concern.

# Plan correction confirmation — 2026-08-26

The corrected plan now matches commit `f840d48a`: it records the disproven and fully reverted SDK hypothesis, the GitHub/Linux candidate-anchor mailbox omission, operator authorization for the one-link `QueryHelpers.AddQueryString` correction, the exact-anchor regression assertion, and focused plus fresh GitHub shard-1 acceptance.

**Formal plan blocker cleared.** The implementation remains independently approved. Run 33004368148 is active; merge remains gated only by the required green CI result.
