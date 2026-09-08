# Files — PLAT-046: planned destructive migration shutdown

Current baseline dev9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c. This replaces the
obsolete .claude route/per-tick readiness map; the current .agents release route
already migrates before new packages.

## Changed
| Path | Why |
| --- | --- |
| .agents/skills/pegasus-release/SKILL.md | Planning classification, exact-target approved shutdown/read-back and disabled-first new deployment/activation. |
| .agents/skills/pegasus-release/references/database-migration.md | Require proven old-runtime shutdown before destructive SQL, preserve manifest/grants/head route, remove unresolved PLAT046 sentence. |
| docs/adr/0046-destructive-migration-runtime-shutdown.md | Operator-selected short-outage policy, partial supersession, post-release scheduling, recovery boundary. |
| docs/adr/0030-non-additive-schema-changes-before-cutover.md | Minimal metadata/status cross-reference replacing accepted transient old-runtime error window; historical body remains contextual. |
| docs/adr/README.md | ADR0046 catalogue and ADR0030 supersession linkage. |
| docs/runbook.md | Current migration/rollback guarantee agrees with shutdown and forward-only boundary, no duplicated command recipe. |
| AGENTS.md | Outside managed block: destructive migrations planned, old runtimes stopped, actual post-release window outside typical usage. |
| scripts/PegasusPlatform.ps1 | Canonical existing Worker Disabled names producer. |
| scripts/Test-AzureDeploymentPlan.ps1 | Consume canonical names without weakening exact Bicep/activation gates. |
| scripts/Invoke-ProductionSmoke.ps1 | Consume canonical names, retain full census and ActivationOnly distinction. |
| scripts/Test-PegasusPlatform.ps1 | Existing lightweight offline script contracts prove census wiring and relevant fail-closed behavior. |

## Context
docs/index.md, docs/engineering.md, infra/modules/platform.bicep,
infra/main.parameters.json, scripts/Update-TestUiSnapshots.ps1 are read-only.
The last is not relevant to verification and must not be run for this ticket.
Read scratch/alert-rule.md for preserved alert evidence. No source application,
database migration, Bicep, CI, package, operations history or Principal-doc changes.

## Ripple
Existing operational callers retain parameters and behavior. Full exact census
must still reject absent/extra/duplicate/malformed settings and wrong values.
No runtime schema polling, generic shutdown service, feature flag, new dependency,
new script entry point, or alert suppression. No live operation is authorized.
