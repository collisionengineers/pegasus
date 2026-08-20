# Independent review — 2026-08-20

Reviewer is independent of the implementation.

## Changes inspected

- `.github/workflows/ci.yml`: adds an always-run `windows-latest` `local-development-scripts` job that runs `./scripts/Test-PegasusPlatform.ps1`.
- `scripts/PegasusPlatform.ps1`: keeps non-zero command output as `Missing`; parses `State: Running|Stopped`; recognizes only a line-anchored, escaped requested-instance `LocalDB instance "<name>" doesn't exist!` diagnostic (with trailing whitespace) as `Missing`; treats contradictory state/missing content and other zero-exit output as `Unknown`.
- `scripts/Test-PegasusPlatform.ps1`: adds deterministic Windows coverage through the existing `-Command` seam for the exact missing fixture, wrong instance, wrapper-only output, unrecognized output, Running, Stopped, contradictory output, and non-zero output.

## Governing-doc and plan check

The fix profile has no linked PRD/FRD/ADR. The plan correctly treats the documented Offline lifecycle in `docs/runbook.md` as the behavioral constraint rather than changing product scope: one shared classifier remains the policy owner; lifecycle callers and their fail-closed ownership guard remain unchanged. The three-file diff matches the plan, files map, and report. Open questions are resolved/explicitly parked.

## Comments and disposition

1. **Blocking — CI is red.** The new `local-development-scripts` check failed in PR #471 run 32364388605, job 96410637569, at “LocalDB lifecycle classifier tests” immediately after checkout. The same test passed locally in `task/plat-014-localdb-detection`, so the runner-specific cause must be captured and fixed before merge. **Disposition:** filed [[PR-023]] blocking PLAT-014; no fix applied by the reviewer.
2. **Non-blocking — canonical non-corpus suite lacks a terminal result.** The author honestly records that the command exceeded 10- and 30-minute windows, rather than representing it as a pass. CI's pending unit/integration/browser lanes must be inspected again after the PR's red local-development check is repaired. **Disposition:** recorded; no separate ticket because the explicit CI failure already prevents merge and the report does not conceal the timeout.
3. **Non-blocking — no scope/architecture issue found.** The requested-instance match is escaped and line-anchored; test cases cover wrong-instance, wrapper-only, and contradictory responses; no LocalDB lifecycle caller, product code, cloud state, or governing document was changed. **Disposition:** accepted as implemented.

## Evidence checked

- Ticket, all present pipeline documents, open questions, gates, and linked dependency [[PLAT-005]].
- PR #471 title/body, one commit (`6cb9c59a761909a5e926452a2684af0438559cb9`), target `dev`, file list, and full patch.
- Local independent execution from the ticket worktree: `pwsh -NoProfile -File ./scripts/Test-PegasusPlatform.ps1` — passed.
- GitHub Actions run 32364388605: changes, documentation, reference-data, and infrastructure succeeded when checked; local-development-scripts failed; unit, SQL-integration shards, and browser were still in progress.

## Verdict

**Needs changes.** Do not merge PR #471 and do not move PLAT-014 to Verifying. [[PR-023]] must be resolved on the existing PR branch, then the PR needs a fresh independent review with all CI green.

## CI log detail — 2026-08-20

Exact failing log: https://github.com/collisionengineers/pegasus/actions/runs/32364388605/job/96410637569.

The job printed `Pegasus platform LocalDB state classification passed.` and then failed with `Process completed with exit code 1.` The final intentional non-zero fixture leaves global `$LASTEXITCODE` as `1`; Actions invokes the script with `pwsh -command ". '{0}'"`, so the harness reports failure even though every assertion passed. [[PR-023]] should explicitly reset/clear the successful script's final native exit state after testing the non-zero classifier path, then rerun/review.

# Re-review verdict — 2026-08-20

**Pass — independent reviewer.** This re-review includes the original three-file LocalDB classifier/CI diff and repair commit `4c7b459f02f24ce54f66b973eebfbf75596acb50`.

## Changes and governing-doc check

- `scripts/PegasusPlatform.ps1` recognizes only the escaped, requested-instance, line-anchored LocalDB missing diagnostic (including trailing whitespace); wrong-instance, wrapper-only, arbitrary, and contradictory zero-exit output remains `Unknown`. Existing state-line and non-zero handling, Linux/Docker behavior, and lifecycle ownership callers are unchanged.
- `scripts/Test-PegasusPlatform.ps1` covers the required classifier outcomes through the existing seam. The amended success epilogue resets `$global:LASTEXITCODE` only after all assertions, including the required intentional non-zero fixture, pass.
- `.github/workflows/ci.yml` adds the narrow always-run Windows caller. The amended diff/report remains within the plan, preserves the runbook's supported Offline ownership lifecycle, and introduces no product behavior or architectural scope. Both PLAT-014 and [[PR-023]] reports account honestly for the original red run and its correction.

## Comments and disposition

1. **Previous blocking CI failure — fixed in PR.** The first Windows job inherited the final test fixture's exit code 1 after printing success. The one-line epilogue preserves the fixture and makes the successful host exit 0. Independent GitHub-style local invocation passed.
2. **Previous non-blocking full-suite timeout — resolved by CI.** The complete PR workflow reached terminal success; no timeout was represented as a local pass.

## Evidence checked

- PR #471 target `dev`, full amended diff, both ticket plans/checklists/reports, questions, and gates.
- Local independent GitHub-style command: `pwsh -NoProfile -Command ". './scripts/Test-PegasusPlatform.ps1'"` — passed, exit 0.
- GitHub Actions run 32364977115: all 11 required checks succeeded (changes, documentation, local-development-scripts, reference-data, infrastructure, unit, three SQL integration shards, browser, and SQL integration coverage). PR is `CLEAN` and mergeable.

## Verdict

**Pass.** Merge PR #471 into `dev` only, then move PLAT-014 exactly one stage to Verifying. Do not run verification, write proof, close out, or promote to `main`.
