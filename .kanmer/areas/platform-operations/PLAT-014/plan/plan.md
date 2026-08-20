# Plan — PLAT-014: Correct missing LocalDB detection in Offline lifecycle

## Approach

Correct the single shared Windows classifier in `Get-PegasusDatabaseState`: retain its existing state-line and non-zero-exit handling, then recognize only an explicit missing-instance diagnostic naming the requested LocalDB instance. Everything else remains `Unknown`. Cover that contract through the helper's existing `-Command` seam in one standalone PowerShell assertion script, and give it a dedicated always-run Windows CI job. This beats changing `Invoke-LocalDevelopment.ps1` because its callers already enforce the correct ownership policy; it beats attaching the test to a conditional .NET lane because that would expand the diff into CI change classification and run unrelated builds; and it beats a live-LocalDB CI test because deterministic parsing coverage must not create machine state.

## Governing docs

- **No linked PRD, FRD, or ADR requires modification.** This fix-profile ticket has no `refs`, introduces no product behavior or architectural boundary, and therefore needs no new governing document or ADR.
- **Meets `docs/runbook.md#offline-development-profile`:** restores the documented Windows per-run LocalDB lifecycle so Start can create a genuinely absent exact-run instance while Stop/Reset still refuse ambiguous ownership. Verification uses only the supported Doctor → Initialize → Start → Status → Smoke → Reset commands and no manual service composition.
- **Preserves repository safety and architecture rules:** `scripts/PegasusPlatform.ps1` remains the one database-state owner for the per-run lifecycle; `scripts/Invoke-LocalDevelopment.ps1` continues to consume its four-state contract without a second implementation or bypass. No application, cloud, vendor, production, or product-document change is authorized.

## Steps

1. **Narrowly correct the Windows state classifier.** In `scripts/PegasusPlatform.ps1`, reuse the captured command output, requested `InstanceName`, existing non-zero-exit result, and existing `State: Running|Stopped` parser. Add an instance-bound, line-anchored match for the inner LocalDB diagnostic `LocalDB instance "<requested name>" doesn't exist!`, allowing trailing whitespace. Escape the requested name before matching. Do not treat the wrapping `Printing of LocalDB instance "<name>" information failed...` line as `Missing`; that wrapper is used for other print failures. Preserve `Unknown` for unrelated, wrong-instance, contradictory (state line plus missing line), wrapper-only, or otherwise unrecognized zero-exit output; leave the Linux/Docker branch and lifecycle callers unchanged.
2. **Add focused deterministic contract coverage.** Create `scripts/Test-PegasusPlatform.ps1` using the repository's standalone assertion-script convention. Dot-source the existing helper and pass a test-only PowerShell command function through its existing `-Command` parameter; do not add a production abstraction or touch live LocalDB. Independently assert: the exact captured two-line zero-exit missing fixture (including the trailing space after `doesn't exist!`) → `Missing`; wrong-instance missing output with exit 0 → `Unknown`; wrapping `information failed` line without the inner missing diagnostic → `Unknown`; unrelated zero-exit output → `Unknown`; `Running` and `Stopped` state lines → their named states; contradictory state-plus-missing output → `Unknown`; and a non-zero response → the existing `Missing` result. The script must run on Windows; `Get-PegasusDatabaseEngineKind` would otherwise take the Docker branch.
3. **Give the regression test an honest automated caller.** Add a small `local-development-scripts` job to `.github/workflows/ci.yml` on `windows-latest`, with checkout and one explicit invocation of `./scripts/Test-PegasusPlatform.ps1`. Run it for every workflow invocation instead of adding new change flags or coupling a PowerShell parser contract to the .NET, documentation, infrastructure, or cloud lanes. Accept that editing `ci.yml` already trips the existing build and infrastructure flags.
4. **Run focused and repository verification.** Execute `pwsh ./scripts/Test-PegasusPlatform.ps1`, then the repository's canonical locked restore, Release build, and non-corpus test commands from `docs/runbook.md`. Confirm the CI workflow diff calls the focused test on Windows and that no product/deployment documentation is changed.
5. **Perform and record the required simplification pass before the PR.** Review the branch diff through reuse, simplification, efficiency, and altitude lenses. Remove behavior-preserving excess, and append a dated `Simplification pass` section to this plan recording each finding and disposition; do not use simplification to broaden behavior or scope.
6. **Verify the real owned Windows lifecycle from a clean committed checkout.** Record the pre-existing LocalDB instance names read-only, run Offline Doctor and Initialize, then Start one newly generated run. Capture its exact run id and successful Status and Smoke output. Reset that exact run id through the supported action, confirm its run directory and `PegasusDevelopment_<run-id>` instance are absent afterward, and confirm every pre-existing LocalDB instance remains present. Do not manually delete, rename, stop, or otherwise act on an unrelated instance. If a leftover Failed run directory exists from an earlier [[PLAT-005]] Start attempt, Reset that exact run id through the supported action after the classifier fix rather than deleting it by hand.
7. **Confirm exact cleanup and unrelated-instance preservation.** After Reset, prove the exact run directory and `PegasusDevelopment_<run-id>` instance are absent and every name from the pre-existing LocalDB inventory remains present. Do not infer cleanup from process exit alone.
8. **Prepare review evidence.** Keep the checklist current, write the post-implementation report with the focused-test/build/lifecycle outputs and exact run identity, and open the PR to `dev`.
9. **Produce merged-source proof and unblock the dependent task.** After independent review and merge, `proof.md` must cite or repeat the focused contract check and merged-source owned lifecycle evidence before [[PLAT-005]] resumes its screenshot work.

## Verification

Pre-merge verification and the post-implementation report will record:

- `pwsh ./scripts/Test-PegasusPlatform.ps1`
- `dotnet restore ./Pegasus.slnx --locked-mode`
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"`

These are the canonical commands from `docs/runbook.md#locked-restore-build-and-test`.

- `pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline`
- `pwsh ./scripts/Initialize-LocalDevelopment.ps1`
- `pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start`
- `pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Status`
- `pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Smoke -RunId <exact-run-id>`
- `pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Reset -RunId <exact-run-id>`
- Read-only before/after LocalDB instance inventories plus exact-run directory/instance absence after Reset

The focused assertions prove all classifier outcomes without state mutation, including the live two-line missing fixture. The owned lifecycle proves the real Windows caller can create and clean up only its exact run. On merged source, `proof.md` records the same evidence tier and explicitly states that this proves local tooling only—not application behavior, deployment, Azure, or vendor integration.

## Risks / open questions

- **An over-broad diagnostic match could weaken ownership protection.** Mitigation: bind the match to the escaped requested instance name and the inner `doesn't exist!` line with optional trailing whitespace; test wrong-instance, wrapper-only, and arbitrary zero-exit output as `Unknown`.
- **A line-anchored match that ignores trailing whitespace will miss the live LocalDB 2025 diagnostic.** Mitigation: the golden missing fixture is the captured two-line output, including the space after `doesn't exist!`.
- **PowerShell command fakes could accidentally depend on process-global exit state.** Mitigation: each test case explicitly sets output and `$LASTEXITCODE`, invokes the helper once, and asserts the returned state immediately.
- **A live lifecycle failure could leave owned local artifacts.** Mitigation: retain the exact generated run id, use only supported Status/Reset actions, and preserve diagnostics for a failed owned run; never broaden cleanup to unrelated instances.
- **CI placement could grow into conditional-lane plumbing.** Mitigation: use one dedicated always-run Windows job; do not edit `Get-CiChangeFlags.ps1` or its test.
- No operator or product decision remains.
