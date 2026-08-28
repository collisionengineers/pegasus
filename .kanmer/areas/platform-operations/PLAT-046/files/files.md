# Files — PLAT-046

## The finding that shapes this change

There is no code defect. **The release procedure contradicts the documented
guarantee**, and only the documentation is right.

`docs/runbook.md:1186-1191` states, as an established fact operators rely on:

> "…because migrations are applied before the new packages are activated…"

`docs/operations.md` records the same for past releases — release 9: "applied
with the immutable `efbundle.exe` **before the packages**"; release 8: "applied
explicitly **before activation**".

But `.claude/skills/pegasus-release/SKILL.md` numbers the steps:

| Step | Line | Action |
| --- | --- | --- |
| 6 Provision | 154 | `azd provision` — **this deploys Web** |
| 7 Deploy | 166 | Worker via `az functionapp deployment source config-zip` |
| **8 Migrations, only if there is one** | **185** | run `efbundle.exe` |

The Worker starts at step 7 against the old schema and the schema arrives at
step 8. Nothing in code, infra or CI enforces either order — the guarantee is
documentary and operator-enforced, and the written procedure violates it.

## Changed

| Path | Why | Risk |
| --- | --- | --- |
| `.claude/skills/pegasus-release/SKILL.md` | Move migrations before provision/deploy so the procedure matches the guarantee the runbook already states. This is the fix. | The whole change rests here. Renumbering steps risks stale cross-references elsewhere in the file — grep for "step 8" and "§8". |
| `src/Pegasus.Worker/Program.cs` or `WorkerDependencyInjection.cs` | A schema-readiness check the timer functions consult, so the guarantee stops being procedure-only. | The Worker deliberately defers config parsing to first use (PLAT-013) rather than aborting host build; a startup check must not reintroduce a crash-loop on an unresolved Key Vault reference. |
| `src/Pegasus.Worker/IntakeFunctions.cs` | The reconciliation timer skips schema-dependent work when the schema is not current, logging once rather than throwing every tick. | Must not silence a real fault. Skipping is only correct for "migrations pending", never for "query failed". |
| `docs/runbook.md` | State the ordering as an enforced sequence rather than an assumed one, and say what the Worker does if it is violated. | It is a protected authority file for operator meaning; this adds a mechanism note, it does not change a business statement. |

## Deliberately out of scope

- **Retuning the exception alert's thresholds.** See `scratch/alert-rule.md`:
  the rule is already built to ignore one-off noise, requiring ≥3 distinct
  operations or ≥3 distinct minute buckets. Our storm cleared both. Any
  threshold loose enough to hide it would hide a real two-minute outage.
- **Deployment-window alert suppression** stays a *conditional* follow-up: if
  the ordering fix removes the storm there is nothing to suppress. It is also a
  cloud write needing explicit per-target approval.
- **Making the Worker apply migrations.** It does not today and should not: a
  Functions host scaling to several instances would race, and `efbundle` as an
  immutable artifact is the accepted mechanism.
- **The non-additive migration hazard.** `docs/runbook.md:1186-1191` already
  documents it and ADR-0030 governs it. Migrate-first is safe for an additive
  migration (old code ignores a new column); neither order is safe for a
  destructive one, which is why that is its own accepted procedure.

## Context files — read these before changing anything

| Path | What it tells you |
| --- | --- |
| `.claude/skills/pegasus-release/SKILL.md` §6-§9 | The real sequence, and that step 6 deploys Web as a side effect of provisioning — so "move migrations before the deploy" means before **provision**, not between 6 and 7. |
| `docs/runbook.md:1186-1191` | The guarantee this change makes true, in the words operators already rely on. |
| `docs/runbook.md:1168-1177` | Rollback is Web → Worker → Database, the same shape and the same latent problem. Decide whether it needs the mirror fix. |
| `src/Pegasus.Web/Health/DatabaseReadinessHealthCheck.cs:16-25` | `GetPendingMigrationsAsync().Any()` → Unhealthy. **Web already knows exactly what the Worker needs to know.** Reuse this, do not write a second check. |
| `src/Pegasus.Web/Program.cs:1002-1013` | `/health/ready` is `.AllowAnonymous().ShortCircuit()`; `/health/live` always returns Healthy. Explains why Web's 47 readiness failures are correct behaviour, not a second fault. |
| `src/Pegasus.Worker/Program.cs` (51 lines) | `HostBuilder` → `ConfigureFunctionsWorkerDefaults` → `Run`. No startup task, no health check, no gate. Confirms the Worker has nowhere for this to live yet. |
| `src/Pegasus.Worker/IntakeFunctions.cs:149-155, 205-221` | The codebase's existing idiom for a timer declining one piece of work — an optional dependency that is null when uncomposed. It is a *static* gate decided at host build, so it cannot express "not yet"; a schema check needs to be evaluated per tick. |
| `infra/modules/platform.bicep:577` | `IntakeStagedArtifactReconciliationSchedule = '*/10 * * * * *'` — why 52 exceptions in two minutes. |
| `infra/modules/platform.bicep:46, 581-587` | `AzureWebJobs.<fn>.Disabled` is a deploy-time all-or-nothing activation switch tied to the azd env, unrelated to schema state. Not the gate to reuse. |
| `src/Pegasus.Worker/WorkerDependencyInjection.cs:62-65, 170-178` | PLAT-013: external config is parsed lazily *because* parsing at host build crash-looped the Worker on an unresolved Key Vault reference. Any startup check must not undo this. |
| `tests/Pegasus.IntegrationTests/ReadinessEndpointTests.cs:99-114` | Proves the Web behaviour and proves nothing about the Worker — the Worker is absent from it. A new test is needed, not an extended one. |

## Ripple

- No production caller changes. The sweep already tolerates enqueuing nothing.
- `scripts/Test-AzureDeploymentPlan.ps1` has a `PreMigration` mode
  (`:4, 387-389`) that may encode the current ordering — check before
  renumbering.
- `docs/operations.md` describes past releases in the old order; those are
  historical records and must not be rewritten.
