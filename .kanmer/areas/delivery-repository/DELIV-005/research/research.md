# Research — DELIV-005: Remove Markdown-placement CI gate

## Question

What exact CI behaviour must be removed to stop the current release from
rejecting an asset README, while retaining the unrelated documentation checks?

## Findings

- `.github/workflows/ci.yml` has a `documentation` job with three relevant
  steps: Markdown-placement regression tests, the Markdown-placement gate, and
  documentation-link validation.
- The gate runs `scripts/Test-MarkdownPlacement.ps1` using the pull request's
  base and head SHAs. It rejects new Markdown outside a fixed set of roots.
- Release PR #400 fails this gate solely because
  `src/Pegasus.Web/wwwroot/images/marks/README.md` is outside that set.
  Reproduction: `./scripts/Test-MarkdownPlacement.ps1 -Base origin/main -Head origin/dev`.
- The user directed removal of “that CI.” The narrowly matching unit is the
  `Markdown placement` workflow step—not the regression test or
  documentation-link check. Retaining those steps avoids an unrelated
  reduction in test coverage.

## Implication

Remove only the `Markdown placement` step and its PR/push SHA environment
configuration from `.github/workflows/ci.yml`. Do not change the placement
script or its regression tests, and do not move or rename the asset README.

## Verified premises

- Workflow step layout: `git show origin/dev:.github/workflows/ci.yml`.
- Failure and path: GitHub Actions run 32121669690 / job 95663245511 and local
  reproduction against `origin/main..origin/dev`.
- Existing gate implementation: `git show origin/dev:scripts/Test-MarkdownPlacement.ps1`.
