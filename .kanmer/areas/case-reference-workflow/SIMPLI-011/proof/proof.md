# Proof — SIMPLI-011: decompose the Case Details workspace by capability

Verified on merged `dev` at `b763157a` (Merge #395, https://github.com/collisionengineers/pegasus/pull/395, merged 2026-08-17 15:48 UTC), checked out detached in the ticket's own worktree (`../pegasus-worktrees/simpli-011-case-details`, never `.worktrees/kanmer`). Log: `verify-011-dev.log` (session scratchpad).

## Commands and results (merged `dev`, 2026-08-17 16:49–16:53 BST)

| Command | Result |
| --- | --- |
| `dotnet restore --locked-mode` · `dotnet build --configuration Release` | 0 warnings, 0 errors |
| `dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build` | 580 / 580 passed |
| `dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build` | 94 / 94 passed (incl. `WebCustodialPagesHaveNoDormantTransportPath` on `CustodyModel`/`DetailsModel`) |
| `dotnet test tests/Pegasus.IntegrationTests … --filter "CaseDetailsWebTests|CaseReportApprovalWebTests|QdosCustodialWebTests|CaseCreateWebTests|CasesIndexWebTests|Browser"` | 77 / 77 passed — the six capability-page tests (`WorkflowPageBinds…`, `TasksPageBinds…`, `CustodyPageBinds…`, `VehiclePageBinds…`, `EvaDownloadPage…`, `ClosurePageBinds…`), the workspace edit-mode test (`WorkspaceRenewsAndLeavesEditMode…`), the retargeted workspace/approval tests, the Export page (`QdosCustodialWebTests`), and the Playwright operator journey through the split workspace |
| `Select-String asp-page-handler src/Pegasus.Web/Pages/Cases/Shared/*.cshtml` | 34 (35 before − the EVA download form, which posts to `/Cases/{id}/Eva/Download` without a handler); the six workspace forms remain ambient |

## CI (PR #395, run 32041587054, `ec0c2220`)

Attempt 3 fully green: unit, sql-integration (1)(2)(3), sql-integration-coverage, browser, documentation, reference-data, changes (infrastructure skipped by path filter). Attempts 1–2 failed only on GitHub-side `actions/setup-dotnet` download 429/502/503s and one LocalDB teardown flake in the pre-existing `CaseWorkflowPersistenceTests.LeaseReleaseAndExpiryDiscardReplayCredentialBeforeReplacement` (Core/Infrastructure untouched by this ticket; green on rerun).

## Acceptance

- **The visible workspace remains intact** — `Details.cshtml` unchanged; the two partials changed only in form targets (`asp-page`/`asp-route-id`), the download form's page, and the operation-key factory's owner name; the reviewer's normalised comparison found 33/34 handlers byte-identical (the 34th renamed to `OnPostAsync` with a download-specific log message); the Browser operator journey passes on merged `dev`.
- **Extracted operations are covered by behavioural tests** — every handler on `Workflow`, `Tasks`, `Custody`, `Vehicle`, `Closure` and `Eva/Download` is posted through the host from a leased workspace and its recorded command asserted; the two base refusal paths and the workspace's own renew/leave are covered too.
- **`DetailsModel` loads and displays** — `OnGetAsync` + ClaimLease/RenewLease/ReleaseLease + Save/ConfirmCompleteness; 10 constructor dependencies (was ~35); 1938 → ~630 lines.

## Review

Independent reviewer (fresh agent, read-only): **PASS**; comments C1–C8 all non-blocking and dispositioned in `scratch-review` (C1/C3/C6 fixed in `ec0c2220`; C2/C4/C5 fixed in the ticket documents; C7 bookkeeping; C8 CI rerun).
