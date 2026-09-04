# Plan — PLAT-073: Provision Linux-native WSL toolchain

## Objective

Make the WSL host satisfy Pegasus Offline and Cloud static prerequisites from Linux-native storage and executables, reconcile the repository to Kanmer v0.4.1, and record only evidence-backed repair guidance.

## Starting state

origin/dev is c90f2b8915186efd5bf932cec573846ae75ff1fe and contains origin/main. Research is pinned at research/research.md@53d9fb4032408e93 and the file census at files/files.md@7dac72ac000858ca. Native PowerShell, Docker, GitHub CLI, Git and Python exist; the exact .NET and most Cloud tools do not. nvm contains Node 24 but resolves 26. Kanmer v0.4.1 runs outside the repo while get_status reports stale managed files.

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

Machine-private changes outside Git are /home/pguser/.config/pegasus/environment.sh, nvm aliases, /home/pguser/.local/bin, /home/pguser/.dotnet, /opt-installed tools and /etc/wsl.conf.

## Do not modify

Application code, tests/assertions, infra, product governing docs, operator notes, corpus, board branch contents directly, package locks, production/cloud state, and PLAT-074/UIIMP-016/DELIV-047 scope.

## Constraints

Reuse existing Doctor, platform and initialization owners. Use official signed repositories or pinned vendor release artifacts. No vendor authentication or cloud write. Preserve AGENTS content outside Kanmer managed delimiters. WSL restart remains an operator handoff.

## Ordered steps

1. Correct environment.sh and nvm default to Node 24; install exact .NET 10.0.302 and Cloud-profile tool versions; prove Linux-native paths and versions.
2. Run Initialize-LocalDevelopment.ps1 to restore locks and acquire pinned Chromium/SQL payload, then run Offline Doctor and canonical locked restore/build/test/browser checks sequentially.
3. Run kanmer-setup v0.4.1 in the task worktree, preserve the GUI board worktree and inspect the managed diff.
4. Run Cloud Doctor. Change scripts/PegasusPlatform.ps1 and docs/runbook.md only if an executed repair/version mismatch exists. Run platform/doc/Markdown regression checks and record the simplification pass.
5. Commit, push the task branch, open a PR to dev with Kanmer: PLAT-073, write the post-implementation report, and move to Review.

## Acceptance checks

- command -v for required executables returns no /mnt path and exact versions satisfy both Doctor profiles.
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
