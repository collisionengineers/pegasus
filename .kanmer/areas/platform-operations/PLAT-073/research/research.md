# Research — PLAT-073: Linux-native WSL toolchain

## Question

What remains necessary to make this WSL host satisfy Pegasus Offline and Cloud tooling checks using only Linux-native paths, while installing Kanmer outside the repository through its supported GUI/MCP shape?

## Findings

- The checkout is already on the WSL ext4 filesystem at /home/pguser/projects/pegasus; no source, tool or cache path needs /mnt/c (host inspection, 2026-09-04).
- /etc/wsl.conf now retains interop but sets appendWindowsPath=false and systemd=true. A WSL shutdown/restart is required before those boot-scoped settings and new docker-group membership are authoritative (host inspection).
- Native PowerShell 7.6.5, Docker Engine 29.8.0, GitHub CLI 2.88.0, Git, Python 3.14.4, libnss3-tools, Poppler and renderer fonts are installed. Docker server access succeeds in this session (version probes).
- .NET SDK 10.0.302, Azure CLI 2.88, azd 1.28.0, Bicep 0.45.15, Infisical 0.43.104, Box CLI 4.9.2, go-sqlcmd 1.10.0, Functions Core Tools 4.12.1 and ExchangeOnlineManagement 3.10.0 remain absent (command census versus origin/dev scripts/Invoke-Doctor.ps1).
- nvm contains Node 24.20.0, but the default currently resolves Node 26.8.1 because the shell helper passed --silent in the wrong position and the default alias is 26. Doctor requires major 24 and npm 11 (host inspection and Invoke-Doctor.ps1).
- origin/dev already contains platform-independent Doctor and local-development changes: Linux uses a pinned SQL Server container, go-sqlcmd, native paths and Playwright Chromium. This ticket should extend those existing conventions, not build parallel setup logic (origin/dev docs/runbook.md, scripts/Invoke-Doctor.ps1 and scripts/PegasusPlatform.ps1).
- Kanmer v0.4.1 is cloned outside the repo at /home/pguser/tools/kanmer and its build/headless smoke/GUI build pass. The source GUI created and synchronizes .worktrees/kanmer; agent MCP uses /home/pguser/.local/bin/kanmer-mcp-pegasus and an ignored project .codex/config.toml (host evidence).
- Kanmer get_status reports the repository managed AGENTS block and six skill files in each supported agent directory behind v0.4.1. Re-running kanmer-setup is the supported reconciliation path. The pinned Kanmer npm audit reports 16 locked dependency advisories (4 low, 6 moderate, 5 high, 1 critical); changing that upstream lock is outside Pegasus scope.
- The repo-root checkout is intentionally left dirty only by the GUI-managed .gitignore update and is 23 commits behind origin/dev. Ticket implementation must use a fresh branch/worktree from current origin/dev and must not absorb unrelated checkout state (git status and worktree census).
- EPIC-013 requires both Doctor profiles and canonical locked restore/build/test to pass, but explicitly makes cloud sign-in and cloud writes non-goals. Tool installation and static version checks do not authorize external operations (EPIC-013/context.md and docs/runbook.md).
- No declared Kanmer research sources exist for this area; external installation sources must be official vendor artifacts with pinned checksums or signed repositories where available (get_sources returned an empty set).

## Implications

Provision the missing exact versions on the Linux filesystem, correct the nvm activation/default, restore packages and the pinned database/browser payload, and trust the development certificate where supported. Reconcile Kanmer v0.4.1 using kanmer-setup from the task worktree. Repository changes should be limited to the managed Kanmer reconciliation plus any concrete Doctor/runbook repair correction discovered by executing both profiles; machine-private wrappers/config remain outside Git. Verification must record Linux-native command paths and no cloud login or write.

## Open questions

None. PLAT-074, UIIMP-016 and DELIV-047 own the database-image decision, accessibility evidence-policy change and release-route conversion respectively.
