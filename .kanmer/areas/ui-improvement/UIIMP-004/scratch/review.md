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
