# Plan — PLAT-073: Provision Linux-native WSL toolchain

## Objective

Make the WSL host satisfy Pegasus Offline and Cloud static prerequisites from Linux-native storage and executables, reconcile the repository to Kanmer v0.4.1, and record only evidence-backed repair guidance.

## Preconditions and reuse

DELIV-046 is Done and origin/main is contained in origin/dev. Reuse scripts/Invoke-Doctor.ps1 as the executable census, scripts/PegasusPlatform.ps1 as the single repair-hint owner, scripts/Initialize-LocalDevelopment.ps1 for repository packages/browser/SQL acquisition, and the Kanmer v0.4.1 setup skill for managed-file reconciliation. No parallel installer or platform list is added.

## Step 1 — Pin the host shell and install exact prerequisites

- Correct /home/pguser/.config/pegasus/environment.sh and the nvm default so Node 24 is selected before Node tools run.
- Install .NET SDK 10.0.302 under /home/pguser/.dotnet and exact Cloud-profile tools from official signed repositories or release artifacts: Azure CLI 2.88, azd 1.28.0, Bicep 0.45.15, Infisical 0.43.104, Box CLI 4.9.2, go-sqlcmd 1.10.0, Functions Core Tools 4.12.1, and ExchangeOnlineManagement 3.10.0.
- Keep all tools/caches on the Linux filesystem. Do not authenticate to vendors or execute cloud writes.
- Verify each resolved executable path avoids /mnt and each version matches Doctor.

## Step 2 — Initialize the pinned Offline payload

- From the task worktree, run the repository-owned initialization path so npm lock restoration, .NET locks, package-pinned Chromium, HTTPS certificate and the pinned SQL image are acquired by their existing owner.
- Run Offline Doctor, canonical locked restore/build/test, and the Browser lane. Do not overlap SQL-container and browser-heavy work where avoidable.
- Record any failed attempt verbatim; correct host prerequisites without weakening checks.

## Step 3 — Reconcile Kanmer v0.4.1

- Run kanmer-setup against the task worktree using the already-cloned /home/pguser/tools/kanmer v0.4.1 source.
- Preserve the GUI-owned board branch/worktree and user-authored AGENTS.md content outside the managed block.
- Review the mechanical managed-file diff; retain only the supported setup output. Verify get_status no longer reports stale managed artifacts after the task version reaches dev.

## Step 4 — Align executable guidance only where proved

- Run both Doctor profiles. If a failing repair hint does not install the version Doctor itself requires, update the centralized hint in scripts/PegasusPlatform.ps1 and the matching runbook text; otherwise make no speculative documentation change.
- Run platform, documentation-link, Markdown-placement and relevant script tests.
- Run a documentation-only or code simplification pass as dictated by the final diff and record dispositions here.

## Step 5 — Deliver

- Commit small logical slices, push the recorded task branch, open a PR to dev with Kanmer: PLAT-073, and stop in Review for an independent agent.
- Required evidence: Offline Doctor PASS, Cloud Doctor PASS without login, canonical restore/build/non-Corpus test PASS, browser PASS, native path/version census PASS, Kanmer build/headless smoke PASS, and a clean task worktree.

## Deviations and stop conditions

Stop on a required cloud login/write, secret prompt, managed-block conflict, unexpected package-version drift, changed origin/dev before taking the ticket, or failure that indicates PLAT-074/UIIMP-016/DELIV-047 scope. A WSL shutdown/restart remains an explicit final host handoff; current-session evidence is not presented as boot-level proof.

## Simplification pass

Pending implementation diff.
