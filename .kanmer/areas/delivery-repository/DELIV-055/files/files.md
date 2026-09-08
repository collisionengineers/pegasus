# Files — DELIV-055

## Where the change lands

| Path | Why |
|---|---|
| `.agents/skills/pegasus-release/references/database-migration.md` | Replace the deleted-runbook dependency with the single self-contained, process-local migration-host recipe. Preserve PreMigration, manifest-bound bundle resolution, bootstrap and migration-before-packages ordering. |
| `AGENTS.md` | Add one compact link from the existing release-workstation/artifact constraint to the canonical migration recipe; do not alter the managed Kanmer block or any other instruction. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `.agents/skills/pegasus-release/SKILL.md` | The release owner, platform/manifest contract, explicit approval boundary, and invariant that migrations finish before packages. |
| `src/Pegasus.Web/Program.cs` | The exact nonblank Production key set, URI validation, and deferred Box/EVA factories that distinguish configured values from inert placeholders. |
| `infra/modules/platform.bicep` | The current deployed Web configuration names, public fixed endpoints and secret-backed values; it is the map, not a migration wrapper. |
| `infra/main.parameters.json` | The azd input names for Box holding and EVA public/secret configuration. |
| `scripts/Invoke-ProductionAdministratorBootstrap.ps1` | Existing process-local azd map and connection/storage derivations to follow without creating a second execution route. |
| `docs/adr/0007-direct-terminal-azure-deployment.md` | Governing direct-terminal and explicit migration-boundary decision; newer environment/platform clauses are superseded as recorded there. |
| `AGENTS.md` | The permitted single non-obvious-constraint link location and prohibition on unrelated managed-block edits. |
| `PLAT-046` Kanmer research/plan | The older containment investigation; its unresolved old-Web/Worker question is not DELIV-055 scope. |

## Ripple effects

- The release reference is used only during an approved manifest-bearing migration.
- No application caller, artifact, schema, Bicep, script, test, cloud target,
  secret, Kanmer ticket other than DELIV-055, or retained worktree changes.
- Documentation review must check that the prior absent-heading reference is
  gone, required keys match current `Program.cs`, and no deployment ordering
  has changed.

## Out of scope

- Executing a migration, bootstrap, provision, Worker deployment, cloud read or
  write, or release.
- New scripts, environment wrappers, secret retrieval, configuration values,
  runtime/schema/permission changes, and test/build/verification execution.
- PLAT-046 Web/Worker containment, maintenance/quiescence, alert handling, or
  any alternative package/migration ordering.
- DELIV-053-managed instructions, the Kanmer managed block, and v1
  Triage/Query/Audit implementation.
