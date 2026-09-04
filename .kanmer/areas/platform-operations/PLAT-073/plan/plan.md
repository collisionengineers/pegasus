# Plan — PLAT-073: Provision Linux-native WSL toolchain

## Objective

Make the WSL host satisfy Pegasus Offline and Cloud static prerequisites from Linux-native storage and executables, reconcile the repository to Kanmer v0.4.1, and record only evidence-backed repair guidance.

## Starting state

origin/dev is c90f2b8915186efd5bf932cec573846ae75ff1fe and contains origin/main. Evidence: `research/research.md`@`53d9fb4032408e93`, `files/files.md`@`7dac72ac000858ca`. Native PowerShell, Docker, GitHub CLI, Git and Python exist; the exact .NET and most Cloud tools do not. nvm contains Node 24 but resolves 26. Kanmer v0.4.1 runs outside the repo while get_status reports stale managed files.

## Governing docs

docs/runbook.md owns executable workstation requirements; AGENTS.md owns workflow; EPIC-013/context.md binds Linux storage, no Windows PATH, no cloud write and sequencing. scripts/Invoke-Doctor.ps1 is the executable prerequisite authority and scripts/PegasusPlatform.ps1 is the one repair-hint owner.

## Required changes

Correct host Node selection; install exact prerequisite versions; acquire repository-pinned packages, browser, certificate and SQL image through existing owners; reconcile Kanmer v0.4.1; correct only guidance disproved by execution.

## Expected files

| Action | Path |
|---|---|
| Managed reconciliation | AGENTS.md |
| Managed reconciliation | .agents/skills/kanmer-*/** |
| Managed reconciliation | .grok/skills/kanmer-*/** |
| Managed reconciliation | .opencode/skills/kanmer-*/** |
| Managed reconciliation | .gitignore |
| Conditional correction | scripts/PegasusPlatform.ps1 |
| Conditional correction | docs/runbook.md |
| Inspect | scripts/Invoke-Doctor.ps1 |
| Inspect | scripts/Initialize-LocalDevelopment.ps1 |

Machine-private changes outside Git are /home/pguser/.config/pegasus/environment.sh, nvm aliases, /home/pguser/.local/bin, /home/pguser/.dotnet, /opt-installed tools and /etc/wsl.conf.

## Do not modify

Application code, tests/assertions, infra, product governing docs, operator notes, corpus, board branch contents directly, package locks, production/cloud state, and PLAT-074/UIIMP-016/DELIV-047 scope.

## Constraints

Reuse existing Doctor, platform and initialization owners. Use official signed repositories or pinned vendor release artifacts. No vendor authentication or cloud write. Preserve AGENTS content outside Kanmer managed delimiters. WSL restart remains an operator handoff.

## Ordered steps

### Step 1 — Provision exact host tools
- Preconditions: origin/dev is the recorded base and no cloud operation is active.
- Files: `scripts/Invoke-Doctor.ps1`
- Change: select Node 24 and install the exact Offline/Cloud prerequisite versions.
- Preserved behaviour: Windows interop remains enabled while Windows PATH import remains disabled.
- Forbidden: vendor login, cloud write, Windows-mounted tool or secret in arguments.
- Negative cases: reject a wrong version or any executable under /mnt.
- Tests: both Doctor profiles and native path/version census.
- Commands: pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline; pwsh ./scripts/Invoke-Doctor.ps1 -Profile Cloud.
- Expected output: exact-version checks pass or name only repository-owned payloads still awaiting initialization.
- Done when: every host-installed prerequisite resolves natively at the required version.
- Deviation stop: stop on login, secret prompt or unavailable pinned artifact.

### Step 2 — Initialize repository-owned payload
- Preconditions: Step 1 host checks pass.
- Files: `scripts/Initialize-LocalDevelopment.ps1`, `scripts/Invoke-Doctor.ps1`
- Change: run scripts/Initialize-LocalDevelopment.ps1, then canonical restore/build/test and Browser checks.
- Preserved behaviour: package locks and test assertions remain unchanged.
- Forbidden: manual alternate SQL/browser setup or concurrent heavy workloads.
- Negative cases: any non-zero command remains evidence and stops delivery.
- Tests: Offline Doctor, canonical non-Corpus suite and Category=Browser.
- Commands: pwsh ./scripts/Initialize-LocalDevelopment.ps1; dotnet restore ./Pegasus.slnx --locked-mode; dotnet build ./Pegasus.slnx --configuration Release --no-restore; dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"; dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Browser".
- Expected output: every command exits zero.
- Done when: Offline Doctor and all named build/test checks pass.
- Deviation stop: stop on a failed assertion or prerequisite belonging to a dependent ticket.

### Step 3 — Reconcile Kanmer and guidance
- Preconditions: Step 2 passes and the GUI board worktree is healthy.
- Files: `AGENTS.md`, `.agents/skills/kanmer-*/**`, `.grok/skills/kanmer-*/**`, `.opencode/skills/kanmer-*/**`, `.gitignore`, `scripts/PegasusPlatform.ps1`, `docs/runbook.md`
- Change: run kanmer-setup v0.4.1; change repair guidance only for an executed mismatch.
- Preserved behaviour: user-owned AGENTS content and board branch/worktree remain intact.
- Forbidden: direct board-branch mutation or speculative documentation.
- Negative cases: managed delimiter conflict or out-of-scope generated diff stops.
- Tests: Kanmer build/headless smoke, both Doctors, platform/documentation/Markdown checks.
- Commands: npm run build; npm run smoke:headless; pwsh ./scripts/Invoke-Doctor.ps1 -Profile Cloud; pwsh ./scripts/Test-DocumentationLinks.ps1; pwsh ./scripts/Test-MarkdownPlacement.ps1 -Base origin/dev -Head HEAD; git diff --check origin/dev..HEAD.
- Expected output: all commands exit zero and Kanmer reports no stale task-managed files.
- Done when: managed diff is bounded and exact checks pass.
- Deviation stop: stop on managed-block conflict, failing check or scope expansion.

### Step 4 — Deliver for independent review
- Preconditions: Steps 1–3 pass and simplification findings are dispositioned.
- Files: `AGENTS.md`, `.agents/skills/kanmer-*/**`, `.grok/skills/kanmer-*/**`, `.opencode/skills/kanmer-*/**`, `.gitignore`, `scripts/PegasusPlatform.ps1`, `docs/runbook.md`
- Change: commit, push, open the dev PR, report exact evidence and move to Review.
- Preserved behaviour: no shared branch is updated except through reviewed PR.
- Forbidden: self-review, self-merge or dependent-ticket work.
- Negative cases: dirty unexpected files or failed CI stop.
- Tests: git status, PR checks and exact diff review.
- Commands: git status --short; gh pr checks <pr>.
- Expected output: clean pushed branch and PR targeting dev.
- Done when: PLAT-073 is in Review with its PR recorded.
- Deviation stop: stop on remote base movement or CI failure.

## Acceptance checks

- `command -v pwsh dotnet node npm docker gh az azd bicep infisical box sqlcmd func` returns no /mnt path and exact versions satisfy both Doctor profiles.
- pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline exits 0.
- pwsh ./scripts/Invoke-Doctor.ps1 -Profile Cloud exits 0 without authentication.
- dotnet restore ./Pegasus.slnx --locked-mode, Release build and non-Corpus test commands exit 0.
- Category=Browser integration tests exit 0.
- Kanmer npm build and smoke:headless exit 0; get_status managed-artifact staleness is resolved for task contents.
- Repository platform, documentation link, Markdown placement and diff checks exit 0.

## Commands

Use the exact Doctor and canonical commands above, repository initialization, focused Browser test, npm run build/smoke:headless in /home/pguser/tools/kanmer, Test-PegasusPlatform.ps1 where applicable, Test-DocumentationLinks.ps1, Test-MarkdownPlacement.ps1 with explicit base/head, and git diff --check.

## Failure and deviation rules

Stop on cloud authentication/write, secret prompt, managed-block conflict, package-version drift, origin/dev movement before take, failed assertion, or scope belonging to a dependent ticket. Retain every failed command in the report.

## Stop condition

Stop with the PR open in Review. Do not self-review, merge, or start PLAT-074/UIIMP-016.

## Simplification pass

Pending implementation diff.
