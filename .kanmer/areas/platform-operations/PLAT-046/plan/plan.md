# Plan — PLAT-046

## What is actually wrong

Not the code, and not the alert. **The release procedure contradicts the
guarantee the runbook already states.**

`docs/runbook.md:1186-1191` says migrations "are applied before the new
packages are activated". `.claude/skills/pegasus-release/SKILL.md` runs them at
step 8, after provision deploys Web (step 6) and after the Worker `config-zip`
(step 7). Nothing in code, infra or CI enforces either order.

So the Worker started against the old schema, and its 10-second reconciliation
timer threw until `efbundle` ran. It recovered by itself. It will recur on any
release whose migration adds a column its own code reads.

## Steps

### 1. Reorder the release procedure — the fix

Move the migration step ahead of provision in
`.claude/skills/pegasus-release/SKILL.md`. Note that **step 6 deploys Web as a
side effect of `azd provision`**, so "before the deploy" means before step 6,
not between 6 and 7.

Renumber and re-check cross-references: grep the file for `step 8`, `§8`, and
`Migrations, only if there is one`. Check
`scripts/Test-AzureDeploymentPlan.ps1:4, 387-389` (`PreMigration` mode) for an
encoded assumption about ordering.

State in the step why the order matters, so it is not silently reverted.

### 2. Make the guarantee structural, not procedural

A procedure fix relies on the next operator following it. The ticket's title is
that the Worker *serves* before migrations complete, so close that too.

Add a schema-readiness check the Worker's timer functions consult per tick.
**Reuse `DatabaseReadinessHealthCheck`'s exact test** —
`GetPendingMigrationsAsync().Any()` (`src/Pegasus.Web/Health/DatabaseReadinessHealthCheck.cs:22`)
— rather than writing a second definition of "the schema is current". Lift it
to a shared Infrastructure check both hosts use.

When migrations are pending, the schema-dependent work is skipped and logged
**once per transition**, not once per tick, at Warning. It is not swallowed:
"schema not current, skipping" is a different statement from "the query
failed", and only the first is skippable. A `SqlException` from any other cause
must still surface.

Evaluated per tick, not at host build — PLAT-013 deliberately made the Worker
defer config parsing so an unresolved Key Vault reference cannot crash-loop it,
and a startup gate would undo that.

### 3. Record the ordering as enforced

Update `docs/runbook.md` so the sequence is stated as the procedure now
performs it, plus what the Worker does if it is ever violated anyway. Do not
rewrite `docs/operations.md`'s historical release records.

Check whether the rollback sequence (`docs/runbook.md:1168-1177`, Web → Worker
→ Database) has the mirror problem and needs the same treatment. It may not:
rolling *back* to older code against a newer schema is the additive-safe
direction.

### 4. The alert — probably nothing to do

`scratch/alert-rule.md` has the detail. The rule already requires ≥3 distinct
operations or ≥3 distinct minute buckets before firing; our storm cleared both
comfortably, so **the alert behaved correctly** and reported a real symptom.

Any threshold loose enough to hide 52 exceptions and 13 failed timer runs over
two minutes would equally hide a genuine two-minute outage. So no threshold
change.

If steps 1 and 2 land, there is no storm to suppress and this step is empty.
Only if a deployment window still pages should a deployment-scoped **alert
processing rule** be added — quiet for a *known* release, still paging for an
unknown fault at the same rate.

**That is a cloud write and needs the operator's explicit approval on the exact
target. Do not create or modify any alert rule without it.**

## Acceptance

- The release procedure applies migrations before any package is activated, and
  says why.
- A Worker tick against a not-yet-migrated database logs once and skips, rather
  than throwing on every tick.
- Any other `SqlException` still surfaces.
- The exception alert's thresholds are unchanged and still fire for a sustained
  fault.
- `docs/runbook.md` describes the ordering as the procedure performs it.

## Verification

- `dotnet build ./Pegasus.slnx --configuration Release`
- `dotnet test ./Pegasus.slnx --configuration Release --filter "Category!=Corpus"`
- A new integration test for the Worker-side check: an unmigrated LocalDB makes
  the schema-dependent sweep skip rather than throw. Model the fixture on
  `ReadinessEndpointTests.PendingSqlMigrationMakesReadinessUnavailable`
  (`LocalDbTestDatabase.CreateAsync(migrate: false)`), which proves the Web half
  and cannot be extended to cover the Worker.
- Re-read the alert rule after any change and confirm the thresholds are as
  recorded in `scratch/alert-rule.md`.

## Stop condition

PR open against `dev` with the ticket in Review. No alert-rule change without
explicit per-target approval. Do not merge.

## Simplification pass

To be run over the branch diff before the PR and recorded here.
