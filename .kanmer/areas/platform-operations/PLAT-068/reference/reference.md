# Review record — PLAT-068 (PR https://github.com/collisionengineers/pegasus/pull/655)

- Reviewer family: Claude (Opus) with cross-model reader gpt-5.6-terra xhigh —
  the other family from the implementer (Codex/gpt-5.6-sol).
- Head SHA reviewed: `a94fffd545d3f979e6d1a5bf9b82cbc9f013a894`
  (branch `task/plat-068-sign-off-account`).
- Review checkout: detached `.worktrees/plat-068-review` at that SHA.
- Verdict: **REQUEST CHANGES** — one merge blocker outside the code's quality
  (the branch no longer merges into `dev`) plus two should-fix findings.

## What was read

Ticket body, `research/`, `files/`, `plan/plan.md` (including both
"Simplification pass" sections and the "PR review" section),
`checklist/checklist.md`, `open-questions/open-questions.md`,
`post-implementation-report/post-implementation-report.md`, EPIC-012
`context.md` (D29–D50), and the full 21-file diff `origin/dev...HEAD`.

## Findings and dispositions

| # | Severity | Location | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs:117` and `src/Pegasus.Infrastructure/Persistence/Migrations/` | The PR is `mergeable: CONFLICTING` / `mergeStateStatus: DIRTY`. `git merge-tree` confirms one real content conflict: `dev` gained PLAT-070's `20260903153134_RemoveStaffReviewFlags` (#649), which edits the same committed-migration list line as this branch's `20260903135604_StaffAccountSignOff`. This branch's migration also now sorts **before** the new `dev` tail, which the plan's Step 2 explicitly forbids ("regenerate this one migration after the new tail; never a second migration"). The model snapshot and `OperatorLabels.cs` auto-merge cleanly; only the migration ordering and the list are at issue. | **Return to the implementer.** `git merge --no-edit origin/dev` in `.worktrees/plat-068`, regenerate the single `StaffAccountSignOff` migration after `20260903153134_RemoveStaffReviewFlags`, reconcile `PegasusDbContextModelSnapshot.cs`, and set the committed-migration list to the two entries in ID order. Re-run `./scripts/Test-MigrationGrants.ps1` and the delivery commands. Reviewer fixes are not permitted in this lane. |
| 2 | should-fix | `tests/Pegasus.Core.Tests/Identity/IdentityUseCaseTests.cs:132` | The oversized case is `new byte[SignOffSignaturePolicy.MaximumBytes + 1]` — all zeros, so it fails the PNG magic-byte check and would still be rejected with the 1 MiB limit deleted. The 1 MiB limit is therefore unproven, and checklist item 1d ("oversized signatures rejected") is not honestly discharged. Confirmed by reading `SignOffSignaturePolicy.Validate` (`StaffAccountAdministration.cs:481-498`), whose `||` chain short-circuits on the size test only for well-formed PNG prefixes. | **Fix (implementer).** Make the oversized fixture start with the PNG signature bytes so it exercises the size branch alone. One-line test change. |
| 3 | should-fix | `src/Pegasus.Core/Identity/StaffAccountAdministration.cs:506-520` | The `bool hasSignature` overload of `SignOffEngineerEligibility.IsEligible` has **no** caller other than the `byte[]` overload that delegates to it (`grep` over `src/` and `tests/`: the four production call sites and all five test assertions pass `byte[]`). The plan's simplification-pass rejection of collapsing the overloads justifies itself with "would drop the roles-collection overload the Core eligibility tests call directly" — the tests call the byte-array overload, so the stated reason is factually wrong. This is a disposition-honesty issue (conduct rule 22) as well as an abstraction with no second concrete caller. | **Fix or re-disposition (implementer).** Either delete the `bool` overload (its body folds into the `byte[]` one) or record an accurate rejection reason. Behaviour is unaffected either way. |
| 4 | nit — accepted | `docs/design/test-ui/pages/administration-accounts--default.html` | The regenerated snapshot's only account row is the non-Engineer offline administrator, so it shows the new column state `—` but not the Settings control or any sign-off state. The plan's Step 4 acceptance line ("the regenerated snapshot shows … the Settings control on Engineer rows only") is only half-visible. | **Accept risk.** The catalogue fixture is fixed and `catalogue.json` is correctly unchanged; the Settings-control presence/absence is proved directly by `StaffAccountsAndRolesWebTests` (`Assert.DoesNotContain`/`Assert.Contains` on `data-dialog-open="sign-off-{id}"`). No change requested. |
| 5 | nit — accepted | `checklist/checklist.md` | All 24 checklist items are still unticked although the report and the diff show them done. | **Accept.** Board hygiene only; does not gate the move. |

Reviewed and found sound (no finding raised): every drawn sign-off control has
a named production handler (`OnPostSignOffAsync`, registered use case, no inert
control and no inline script — `Assert.Empty(InlineScriptRegex().Matches(...))`
proves the page carries none); no explanatory copy was added; every new
operator word is a constant in `OperatorLabels.StaffAccounts`; the diff is
confined to the ticket's owned paths and touches no D44–D50 lane; the
eligibility rule exists exactly once, in Core, and is called by the EF
mutation and both seam queries (`OperatorLabels.SignOffState` derives display
state only); signature bytes never reach history, the page, a link or a served
route — only the digest and a `HasSignature` boolean; the migration is single,
additive, correctly typed, and its SQL Server filtered unique index matches the
entity configuration and the model snapshot; the default transfer clears the
previous holder inside the same serializable transaction and the digest is part
of the replay-conflict check.

## Commands run in the review checkout (head `a94fffd5`)

Scope rationale: the full solution suite is CI's job on this PR. Locally I ran
the whole `Pegasus.Core.Tests` and `Pegasus.ArchitectureTests` projects
(the changed Core type and its eight fake implementations live there and both
projects are fast), the two changed integration-test classes by filter, the
migration-grant script because a migration was added, and the Test UI
verification because `docs/design/test-ui/` changed.

| Command | Exit |
| --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | `RESTORE_EXIT=0` |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | `BUILD_EXIT=0` (0 warnings, 0 errors) |
| `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` | `CORE_EXIT=0` |
| `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` | `ARCH_EXIT=0` |
| `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~StaffAccountsAndRolesWebTests\|FullyQualifiedName~IntakePersistenceIntegrationTests"` | `INTEG_EXIT=0` — 15 passed, 0 failed |
| `./scripts/Test-MigrationGrants.ps1` | `GRANTS_EXIT=0` — 88 migration files checked |
| `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` | `SNAP_EXIT=1` — no retained capture in a fresh checkout (environmental, not a failure of the change) |
| `./scripts/Update-TestUiSnapshots.ps1 -Verify` (fresh capture) | `SNAP_VERIFY_EXIT=0` — capture 119 + 297 passed, snapshot verify 1 passed |

CI status at review time: `gh pr checks 655` reports **no checks on the
branch** — the merge is additionally blocked on green CI, which cannot run
until the conflict in finding 1 is resolved and the branch is pushed again.

## Cross-model reader

`codex exec -m gpt-5.6-terra -c model_reasoning_effort="xhigh"` over the review
checkout returned `Verdict: REQUEST CHANGES` with exactly two findings, both
reproduced and confirmed against the code here as findings 2 and 3. It raised
no correctness defect in the transaction, digest, null handling, limits,
projections, state ordering, disposal or cancellation flow, and confirmed the
two *applied* simplification fixes are genuinely present in the diff.

## Outcome

Not merged. Ticket remains in Review pending findings 1–3.
