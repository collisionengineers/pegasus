# Plan — PR-023: Make PLAT-014 local-development CI check pass

## Approach

Preserve the final non-zero fixture and the production classifier exactly as they are, then explicitly set the fixture's process-global native exit state to 0 only after every assertion has passed. This is smaller and safer than changing the fixture's test coverage, the classifier, or the workflow. The review ticket expressly repairs the existing PLAT-014 PR branch, so the one-line correction will amend that branch for independent re-review rather than create a second delivery PR.

## Governing docs

No PRD, FRD, or ADR applies or changes: this is a test-host exit-status correction for existing local tooling. It preserves the `docs/runbook.md#offline-development-profile` ownership contract already established by PLAT-014 and introduces no product behavior or architectural boundary.

## Steps

1. In `scripts/Test-PegasusPlatform.ps1`, add a successful-test epilogue that resets `$global:LASTEXITCODE` to 0 after the final intentional non-zero fixture has been asserted and before emitting the pass message.
2. Run the focused test both as a file and through the GitHub-style PowerShell command shape, checking each returns success; also run diff checks and the existing CI change-classification regression.
3. Commit and push the scoped correction to `task/plat-014-localdb-detection`, updating PR #471 and its author report/checklist with the repair evidence.
4. Wait for the re-run `local-development-scripts` GitHub Actions job and the complete PR check set to be green, then request a fresh independent review of PLAT-014. Do not merge or promote anything to `main`.

## Verification

- `pwsh -NoProfile -File ./scripts/Test-PegasusPlatform.ps1`
- `pwsh -NoProfile -Command ". './scripts/Test-PegasusPlatform.ps1'"`
- `pwsh ./scripts/Test-CiChangeFlags.ps1`
- `git diff --check`
- PR #471's `local-development-scripts` job and all required PR checks pass.

## Risks / open questions

- Resetting the status before the final assertion would hide a failed fixture. Mitigation: do it only after every `Assert-DatabaseState` call succeeds.
- A green local result does not prove the runner behavior. Mitigation: use the Actions-style command shape locally and require the re-run GitHub job to pass.
- No open questions remain.
