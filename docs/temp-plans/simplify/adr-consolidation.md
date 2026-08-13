# Condense ADRs into nine active technical decisions

## Source of truth

- ADRs own only current, cross-cutting technical architecture decisions.
- `docs/index.md` owns documentation structure and authority; `AGENTS.md` owns repository workflow; Requirements/Capabilities own product behaviour; Operations owns live state; Runbook owns executable procedures.
- Remove supersession chains and all obsolete ADR records. Git history remains the historical record.

| New ADR | Title | Consolidates |
|---|---|---|
| 0001 | Core Pegasus architecture and production delivery | 0002, cloud/release parts of 0004, 0007, technical parts of 0013, 0014, 0015 |
| 0002 | Independent workspace boundary | 0009 |
| 0003 | Intake extraction, custody and route architecture | 0001, 0003, 0005, 0006, 0008 |
| 0004 | Automation Actor and Send to AI boundary | staff-MCP parts of 0004, 0011, AI/MCP parts of 0013, 0021 |
| 0005 | Conservative MOT mileage estimation | 0012 |
| 0006 | Provider inspection mode | 0018 |
| 0007 | In-process VRM recognition | 0019 |
| 0008 | QDOS case-association predicates | 0020 |
| 0009 | Approved mailbox identity and activation | 0022, 0024 |

## Key changes

- Work from a fresh claimed task worktree, not the current dirty checkout; preserve all existing unrelated changes.
- Replace `docs/adr/` with the nine self-contained ADRs above plus a concise index. New ADRs contain no “supersedes” chains or references to removed ADR numbers.
- Move ADR-0013’s product rules into Requirements/Capabilities: image-intake status, readiness gates, cancellation, Box retry, dashboard, sequence limits, EVA image rules, and AI-05 allocation. Keep only technical boundaries in ADR-0001/0004.
- Remove ADR-0010 and ADR-0023. Make `docs/index.md` self-contained for documentation ownership and new-Markdown placement; retain task workflow solely in `AGENTS.md`.
- Remove ADR-0016. Retain the local evaluator boundary in existing Requirements, Capabilities, Design, and Operations pages; it remains a non-production, non-caller tool.
- Retarget every tracked reference—documentation links, `NOW.md`, changelog, Kanmer tickets, `.azure/deployment-plan.md`, code comments, tests, and release-script comments—to the new ADR identities. Preserve dates and underlying historical facts.
- Make ADR-0001 state the confirmed cloud decisions: local validation/test only, then explicitly approved production; Container Apps Web with one warm replica; local .NET SDK OCI build, manifest validation, ORAS upload, and digest-pinned deployment; no Docker daemon, ACR Build, remote build, or `azd up` release route.
- Remove `remoteBuild: true` from `azure.yaml`. Update `Test-AzureDeploymentPlan.ps1` to require `minReplicas: 1` and reject remote-build configuration.

## Verification

- Assert `docs/adr/` contains exactly `0001`–`0009` and `README.md`; deleted ADR paths do not remain.
- Run `./scripts/Test-DocumentationLinks.ps1`.
- Run `./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local`.
- Search all tracked files for removed ADR filenames, obsolete links, and supersession wording; allow only the new nine identifiers.
- Run `git diff --check`, then obtain independent review and green CI under the repository workflow.

## Assumptions

- The current Bicep/live intended Web scale is one warm replica, maximum one.
- The local SDK plus ORAS route is the only supported image-build/release route.
- This task changes documentation, release configuration, and static validation only; it performs no Azure operation and does not rewrite live-state evidence beyond retargeting ADR references.
