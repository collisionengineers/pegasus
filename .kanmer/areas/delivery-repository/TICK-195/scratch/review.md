# Independent review — PR #384

## Changes

- `.github/workflows/ci.yml`: changes only the existing Windows documentation job, enabling full history and running the focused Markdown-placement regression and event-specific base/head validation before the existing link check.
- `scripts/Test-MarkdownPlacement.ps1`: adds an explicit-range Git validator for Markdown addition, copy, and rename destinations; permits only canonical PRD/FRD/ADR trees and the two registered workspace roots; aggregates invalid paths; fails closed on missing, all-zero, unavailable, malformed, or uncomparable evidence.
- `scripts/Test-TestMarkdownPlacement.ps1`: adds disposable-Git regression histories for allowed and forbidden additions, grandfathered modifications/deletions, rename/copy destinations, aggregate diagnostics, and invalid/all-zero bases. The final explicit `$global:LASTEXITCODE = 0` repairs the CI process exit after intentionally exercised Git failures.

## Comments

- No blocking comments.
- Non-blocking observation: the first full CI attempt hit a transient SQL deadlock in `QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate`. This is outside the PR's workflow/scripts-only diff. The failed-job rerun passed without a code change.

## Disposition

- No fixes or follow-up tickets required from this review.
- The transient SQL failure was rerun and is green; no TICK-195 scope was added.

## Verdict

PASS at exact head `fdd2aeba723717fb2391b64d7a810339123abc82`.

Checked the refreshed EPIC-001 context and every TICK-195 document, including open questions and execution scratch; report matches the three-file diff and the plan's governance boundary. Current `AGENTS.md`, `docs/index.md`, and the workspace register support the implemented policy. No `src/Pegasus.Web/**`, UI test, `docs/design/**`, `.stitch/**`, or `docs/temp-plans/**` path is changed. The workflow preserves TICK-200's classifier, infrastructure lane, shards, and build lanes.

Local evidence: focused regression suite passed in a fresh `pwsh` process with exit 0; real `origin/dev..HEAD` placement validation passed; documentation links passed for 219 files; `git diff --check` passed. GitHub evidence: all required checks passed at the reviewed head after the unrelated SQL deadlock rerun.
