# Files — PLAT-073

## Where the change lands

| Path | Why |
|---|---|
| AGENTS.md | kanmer-setup refreshes the managed v0.4.1 operating block; preserve the user-owned repository instructions outside it. |
| .agents/skills/kanmer-* | Reconcile the bundled agent skills reported stale by get_status. |
| .grok/skills/kanmer-* | Keep the repository-installed Kanmer skill projections consistent. |
| .opencode/skills/kanmer-* | Keep the repository-installed Kanmer skill projections consistent. |
| scripts/PegasusPlatform.ps1 | Only if executed Doctor output proves a Linux repair hint is inaccurate; reuse the existing centralized repair-hint map. |
| docs/runbook.md | Only if the executable, evidence-backed Linux provisioning procedure is not already represented accurately. |
| .gitignore | Retain the GUI-generated Kanmer ignore reconciliation if kanmer-setup confirms it; do not hand-edit unrelated rules. |

## Context files

| Path | What it tells the implementer |
|---|---|
| docs/runbook.md | Exact Offline/Cloud versions, Linux/Windows capability differences, initialization, and the no-authorization boundary. |
| scripts/Invoke-Doctor.ps1 | Executable version checks and which Linux prerequisites are mandatory versus advisory. |
| scripts/PegasusPlatform.ps1 | One centralized platform resolver, SQL-container contract and repair hints; a parallel installer is forbidden. |
| scripts/Initialize-LocalDevelopment.ps1 | Existing owner for package, browser and pinned SQL image acquisition. |
| global.json | Exact .NET SDK 10.0.302 contract. |
| package.json and package-lock.json | Exact Azurite dependency and npm restoration contract. |
| EPIC-013/context.md | WSL filesystem, no Windows PATH, Kanmer source-GUI/MCP, no cloud-write and sequencing constraints. |
| docs/engineering.md | Restored dev/main ancestry and ordinary task-branch delivery rules. |

## Ripple effects

Both Doctor profiles, canonical locked restore/build/test, Playwright/browser installation, Docker SQL image presence, GitHub authentication, and supported board synchronization are the acceptance surface. Managed Kanmer files can produce a broad mechanical diff and require explicit scope review. WSL restart is an unavoidable operator handoff because boot configuration and Unix group membership cannot be proven fully inside the current session.

## Out of scope

Azure SQL container qualification, accessibility-policy replacement, production release conversion, CI redesign, email-eval tooling, cloud authentication, cloud writes, production data and changes to Kanmer upstream dependency locks.
