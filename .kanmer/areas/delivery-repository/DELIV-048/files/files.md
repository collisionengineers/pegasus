# DELIV-048 files

| Action | Path | Purpose |
| --- | --- | --- |
| Modify | scripts/PegasusPlatform.ps1 | Existing platform helper; one release bundle identity. |
| Modify | scripts/Build-ReleaseArtifacts.ps1 | Host-matching EF bundle; keep Linux deployed artifacts. |
| Modify | scripts/Test-AzureDeploymentPlan.ps1 | Host/pair validation, Linux-only mode check; keep safety gates. |
| Modify | scripts/Test-PegasusPlatform.ps1 | Cheap existing script acceptance, actual/mocked supported host cases. |
| Modify | tests/Pegasus.ArchitectureTests/WorkerActivationReleaseContractTests.cs | Validator fixture imports required platform file. |
| Modify | AGENTS.md | Portable release convention, outside managed section only. |
| Modify | docs/runbook.md | Native Windows/Linux release path and exact bundle selection. |
| Modify | docs/current-architecture.md | Repository tooling paragraph, no deployment claim. |
| Modify | docs/adr/0037-linux-authorised-release-workstation.md | Supersession metadata/status only. |
| Add | docs/adr/0039-windows-and-linux-release-workstations.md | User-approved workstation decision; no new deploy route. |
| Modify | docs/adr/README.md | Decision index. |
| Modify | .agents/skills/pegasus-release/SKILL.md | Canonical current release procedure. |
| Modify | .agents/skills/pegasus-release/references/database-migration.md | Manifest-named migration executable. |
| Modify | .codex/skills/pegasus-release/SKILL.md | Preserve canonical forwarding; name portable route. |

No snapshots, generated artifacts, schema, package locks, product code, CI
workflow, shared checkout, PLAT-028 or other active worktrees are modified.
