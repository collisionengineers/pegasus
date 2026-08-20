# Research — PR-023: make the LocalDB CI contract test exit successfully

## Question

Why does PR #471's new `local-development-scripts` check fail on GitHub Actions immediately after the test prints its success message, while the same focused test passes locally, and what is the smallest repair that preserves PLAT-014's classifier contract?

## Findings

- The failed [GitHub Actions job](https://github.com/collisionengineers/pegasus/actions/runs/32364388605/job/96410637569) completed its test step in under a second after checkout. Its only test output is `Pegasus platform LocalDB state classification passed.`, followed by Actions reporting exit code 1.
- `scripts/Test-PegasusPlatform.ps1` deliberately runs its final fixture with a non-zero exit code to assert that `Get-PegasusDatabaseState` returns `Missing`. The fixture writes that value to `$global:LASTEXITCODE`, so the successful script path leaves the process-global native-command status at 1.
- GitHub's PowerShell shell dot-sources the script; its final process status therefore reflects the remaining `$LASTEXITCODE` even though all assertions passed. The review independently confirmed `pwsh -NoProfile -File ./scripts/Test-PegasusPlatform.ps1` passes locally.
- `scripts/PegasusPlatform.ps1` and `.github/workflows/ci.yml` are not defective: the classifier and its new job are correctly scoped. The repair belongs only in the test's successful epilogue.
- PLAT-014's review found no other correctness, documentation, or scope issue. PR-023 is the only blocking review item and must land on the existing PLAT-014 PR branch before re-review.

## Implications

- Explicitly reset `$global:LASTEXITCODE` to 0 after the assertions, before the success output. Do not remove or weaken the final non-zero fixture: it is required to preserve the existing Missing-on-command-failure contract.
- Keep the CI job and production classifier unchanged. Run both direct and GitHub-style PowerShell invocations locally, then push the fix to the existing PLAT-014 branch and wait for its new CI run.
- No operator, product, architectural, or governing-document decision is required.

## Open questions

No open questions remain.
