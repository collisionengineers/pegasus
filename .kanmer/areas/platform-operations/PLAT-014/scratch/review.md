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
