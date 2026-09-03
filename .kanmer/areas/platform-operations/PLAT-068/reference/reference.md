# Review record — PLAT-068 (PR https://github.com/collisionengineers/pegasus/pull/655)

- Reviewer family: Claude (Opus) wrapper + gpt-5.6-terra xhigh (the other
  family; the PR was built by Codex).
- Head SHA reviewed: `a1f5b947c85ceee6ceef14a0318eb4dcdd49ac19`
  (branch `task/plat-068-sign-off-account`), detached review checkout at
  `.worktrees/plat-068-review`.
- Date: 2026-09-03.
- **Verdict: REQUEST CHANGES — one blocker, from red CI.**

## Blocker

CI job `sql-integration (1)` (run 33806327632, job 100817759376) failed:

```
Pegasus.IntegrationTests.IntakePersistenceIntegrationTests.CommittedMigrationCreatesTheSqlServerSchema [FAIL]
Assert.Equal() Failure: Collections differ
Expected: [···, "20260829212237_GrantProviderSubmissionAcceptRecove"···]
Actual:   [···, "20260829212237_GrantProviderSubmissionAcceptRecove"···, "20260903135604_StaffAccountSignOff"]
                                                                          ↑ (pos 87)
Failed! - Failed: 1, Passed: 383, Total: 384
```

The PR adds migration `20260903135604_StaffAccountSignOff` but does not add it
to the committed-migration inventory asserted by
`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` (the
expected list ends at line 117,
`"20260829212237_GrantProviderSubmissionAcceptRecovery"`). Fix: append
`"20260903135604_StaffAccountSignOff"` to that list (comma after line 117).
This file is a migration-inventory assertion that any migration-adding ticket
must update; it belongs in this diff, not a separate ticket. It did not fail in
any local run because the test is LocalDB/SQL-Server-gated and was outside the
scoped filters run here.

## Cross-model review findings and dispositions

Independent review by `gpt-5.6-terra` (`model_reasoning_effort=xhigh`) over the
detached checkout; prompt and raw output in the session scratchpad
(`build/PLAT-068/review-prompt.md`, `review-out.md`). Its verdict was REQUEST
CHANGES with five findings. Every one was checked against the code by the
reviewing wrapper.

| # | Severity (as raised) | Finding | Disposition |
| --- | --- | --- | --- |
| 0 | blocker | (Wrapper, from CI) Migration inventory assertion not updated — see above. | **Send back for fix.** Red CI blocks the merge. |
| 1 | blocker | `StaffAccountAdministration.cs:641` — an existing default holder cannot be unflagged while retaining the designation: `Normalize` rejects `IsDefault && !IsSignOffEngineer`, and the dialog pre-checks `isDefault`, so clearing the flag needs the Default box unchecked too. | **Rejected with reason.** The plan's "retains the designation" clause describes state changed by *other* handlers (disable, role removal), which do not touch `IsDefaultSignOffEngineer` and are proven retained by `SignOffUpdatesAreReplaySafeAndTransferTheSingleDefault` (the disabled `second` account is still the sole `IsDefaultSignOffEngineer` row at the end of the test). D31 says only flagged accounts carry sign-off; refusing an unflagged default is the safer invariant, is surfaced through `DefaultRequiresEligible`, and needs no new rule. |
| 2 | blocker | `Index.cshtml:239` — no signature-removal control, so the "signature removed, designation retained" state is unreachable (`EfStaffAccountAdministration.cs:329` only writes a non-null signature). | **Rejected with reason — out of scope.** The ticket, plan Step 3 and checklist 3c specify exactly Upload/Replace; no removal control is in the brief. Scope is the brief; adding one would be a new capability. The unreachable lifecycle branch is a plan-prose artefact, not a defect in delivered behaviour. |
| 3 | blocker | `OperatorLabels.cs:1258` — a de-roled default holder renders `—`, and `SignOffState` re-implements the enabled half of eligibility in Web. | **Rejected (display) / accepted risk (rule).** The plan's own first exact state is "non-Engineer `—`", which the code follows literally; `Yes · not eligible` remains reachable (disabled default with qualifications on file). The `IsEnabled` short-circuit is behaviour-equivalent to `SignOffEngineerEligibility.IsEligible` under the branches that precede it (role, flag and signature are already established) and carries a comment saying so; the rule itself still lives once in Core. Accepted as a presentation short-circuit, not a second policy owner. |
| 4 | should-fix | `OperatorLabels.cs:1260` — the `—` placeholder is a literal, not a named constant. | **Rejected with reason.** The existing convention writes this placeholder as a bare literal directly in Razor (`Accounts/Index.cshtml:96`, `Administration/Automation/Activity.cshtml:57,59`); having it inside `OperatorLabels.cs` is already stricter than the codebase convention. It is a typographic placeholder, not an operator word. |
| 5 | should-fix | `IdentityUseCaseTests.cs:132` — the oversized-signature sample is zero-filled, so it would still be rejected if the size rule were deleted; `BeforeJson` after a signature replacement is not asserted. | **Accepted risk (size isolation) / rejected (snapshot).** `SignOffSignaturePolicy.Validate` evaluates `Length > MaximumBytes` before the magic-byte check, so the size path *is* exercised — it is simply not isolated, and the Web handler enforces the same bound at `Index.cshtml.cs:236`. Not worth a lane round-trip for an administrator-only path. The snapshot half is rejected: `SignOffSnapshot` serialises `HasSignOffSignature` (bool) and `SignOffSignatureDigest` only — the byte array is structurally absent from both snapshots, so no assertion can be weakened into leaking it. |

Codex additionally confirmed, and the wrapper independently confirmed by
reading the diff: all 20 changed paths are owned by PLAT-068 (nothing under
`Pages/Shared/*`, `Pages/Cases/*`, `Core/Reports/*`, `Infrastructure/Reports/*`,
`site.js`, `site.css`, `docs/operator-notes.md` or `corpus/`); every drawn
control has a named handler (`OnPostSignOffAsync`, `data-dialog` /
`data-dialog-close` bound in the untouched `site.js`); the page contains no
inline script (asserted by `InlineScriptRegex` in the integration test); no
explanatory copy was added; nothing assumes D44 (review action), D45 (damage
type) or D46 (crop) behaviour; the migration correctly adds no `GRANT` (it
creates no table, and `Test-MigrationGrants.ps1` passes); and the plan's
Simplification pass dispositions match the code (the removed defensive
`ToArray()` at `EfStaffAccountAdministration.cs:330` and the simplified
`SignOffState` default branch are both present as described).

## Independent verification — commands and exit codes

Run in the detached review checkout at `a1f5b947`. The full solution filter was
not re-run locally; CI runs it sharded on the PR (and is what caught the
blocker). Scope rationale: the changed types are the Core sign-off contract
(Core.Tests), the layering of the new Core→Infrastructure→Web wiring
(ArchitectureTests), the EF store/query and the Accounts page handler
(`StaffAccountsAndRolesWebTests`), the new migration (`Test-MigrationGrants`),
and the regenerated routed-page snapshot (`Update-TestUiSnapshots -Verify`,
which captures fresh and ran 119 browser + 297 non-browser integration tests on
the way).

```
dotnet restore ./Pegasus.slnx --locked-mode                          RESTORE_EXIT=0
dotnet build ./Pegasus.slnx --configuration Release --no-restore     BUILD_EXIT=0   (0 warnings, 0 errors)
dotnet test ./tests/Pegasus.Core.Tests/... --no-build                CORETESTS_EXIT=0   (1188 passed)
dotnet test ./tests/Pegasus.ArchitectureTests/... --no-build         ARCHTESTS_EXIT=0   (100 passed)
dotnet test ./tests/Pegasus.IntegrationTests/... --no-build \
  --filter "FullyQualifiedName~StaffAccountsAndRoles&Category!=Corpus&Category!=Browser"
                                                                     INTTESTS_EXIT=0    (5 passed)
./scripts/Test-MigrationGrants.ps1                                   GRANTS_EXIT=0      (88 migrations checked)
./scripts/Update-TestUiSnapshots.ps1 -Verify                         SNAPVERIFY_EXIT=0  (fresh capture; 119 + 297 + 1 passed)
./scripts/Test-UiCatalogue.ps1                                       CATALOGUE_EXIT=0   (54 routed sources, 58 prototypes, 0 broken refs)
```

`git status --porcelain` in the review checkout is empty after every run.

Note on the post-implementation report: its recorded
`./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` exit 0 is not
reproducible in a fresh checkout — `-SkipCapture` throws "No retained Test UI
capture exists" when `artifacts/test-ui-capture` is absent. The verification was
redone here with a real capture (`-Verify`, no `-SkipCapture`) and passed.

## CI at the reviewed head

Run 33806327632, conclusion **failure**:
`changes`, `documentation`, `local-development-scripts`, `reference-data`,
`unit`, `browser`, `sql-integration (2)`, `sql-integration (3)`,
`sql-integration-coverage` — pass; `infrastructure` — skipped;
**`sql-integration (1)` — fail** (the blocker above).

Not merged. The ticket stays in Review pending the migration-inventory fix and
a green re-run.
