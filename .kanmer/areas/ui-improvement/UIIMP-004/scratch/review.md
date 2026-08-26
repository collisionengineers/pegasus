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
