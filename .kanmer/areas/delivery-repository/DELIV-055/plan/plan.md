# Plan — DELIV-055: Restore the self-contained release migration host recipe

## Objective

Make the canonical database-migration reference self-contained for the current
Production Web host and link to it once from the existing AGENTS release
constraint, without changing release execution behaviour.

## Starting state

The reference names an absent `docs/runbook.md` heading. The current Production
host requires its nonblank key map in `Program.cs`; platform Bicep and the
existing administrator bootstrap provide the exact configuration/derivation
precedent. Evidence: `research/research.md`@`ea116398c24aaecb`,
`files/files.md`@`76bf978881dd09d8`; ticket revision
`rev1:9a19acdaf2b03bfe`.

## Governing docs

- `docs/adr/0007-direct-terminal-azure-deployment.md` — **Meets** the
  retained direct authorised-terminal and explicit immutable migration-boundary
  principle. The current release skill, not this ADR’s superseded environment
  clauses, supplies the operative migration-before-packages sequence.

## Required changes

- Replace the stale runbook-heading dependency in the existing migration
  reference with one PowerShell process-environment recipe.
- The recipe must derive SQL, Web identity, transport/custody account names and
  their service URIs from approved azd environment values; use the current
  platform’s Graph and Box public values; use `BOX_HOLDING_FOLDER_ID` and EVA
  public configuration from approved azd values.
- Use nonempty inert process-only placeholders only for Graph client state,
  Box client secret/config JSON, and EVA client credentials. Box JSON remains
  shape-valid. Do not retrieve or print actual secrets.
- Preserve `PreMigration`, manifest-bundle resolution, `--connection`-only
  invocation, database bootstrap, mismatch stop, and migration-before-packages.
  State explicitly that the reference does not settle PLAT-046 old-Web/Worker
  containment.
- Compact the existing AGENTS release bullet into a link to the canonical
  migration recipe; leave its meaning and all other instructions unchanged.

## Expected files

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Modify | `.agents/skills/pegasus-release/references/database-migration.md` | Canonical self-contained process-local migration-host recipe; no wrapper or secret material. |
| Modify | `AGENTS.md` | One compact link from the existing release instruction to the canonical recipe. |

## Do not modify

- `.agents/skills/pegasus-release/SKILL.md`
- `docs/runbook.md`
- `infra/**`
- `scripts/**`
- `src/**`
- `tests/**`
- The managed Kanmer block and the new-subagent section of `AGENTS.md`
- Any ticket other than DELIV-055, any retained worktree, and all cloud state.

## Constraints

- Documentation only: no build, test, verification script, browser/capture,
  migration, release, cloud operation, secret retrieval, or new dependency.
- Use repository-root-relative paths and existing scripts/configuration only;
  do not create a wrapper or hardcode a workstation-local path.
- Keep actual values confined to approved non-secret azd inputs or existing
  fixed public platform configuration. Placeholders are process-only and inert.
- This work is not authority to alter migration/package order or solve
  PLAT-046 containment.

## Ordered steps

### Step 1 — Repair the canonical migration reference
- Preconditions: current required Production key map and platform configuration
  have been read.
- Files: `.agents/skills/pegasus-release/references/database-migration.md`
- Change: replace the absent runbook cross-reference with the exact
  self-contained, manifest-bound process-environment recipe and explicit
  placeholder distinction.
- Preserved behaviour: `PreMigration`, database bootstrap, mismatch stop and
  migration-before-packages remain unchanged.
- Forbidden: secret values, wrapper scripts, cloud commands that mutate state,
  package-order changes, or a containment route.
- Negative cases: the final text must neither name the deleted runbook heading
  nor present a placeholder as an azd-configured value.
- Tests: none; this is a prose-only correction.
- Commands: static source inspection only; no verification script.
- Expected output: the reference names all required current Production keys and
  no obsolete runbook dependency.
- Done when: a semantic diff contains only the bounded reference repair.
- Deviation stop: any required key lacks a source mapping, or the recipe would
  need a secret value or a new execution helper.

### Step 2 — Link AGENTS to the one procedure owner
- Preconditions: Step 1 is complete and the reference path is unchanged.
- Files: `AGENTS.md`
- Change: add the canonical migration-recipe link to the existing
  Non-obvious constraints release bullet.
- Preserved behaviour: the bullet still directs authorised workstation and
  platform-matching artifact work to the release skill.
- Forbidden: managed-block edits, new-subagent edits, new instructions, or
  changes outside the one bullet.
- Negative cases: no duplicate release procedure and no path outside the
  repository.
- Tests: none; this is a prose-only correction.
- Commands: static diff inspection only.
- Expected output: one compact working relative link.
- Done when: only the permitted AGENTS bullet changed.
- Deviation stop: the change requires touching a DELIV-053-owned section.

### Step 3 — Perform semantic hand-off inspection
- Preconditions: Steps 1 and 2 are complete.
- Files: `.agents/skills/pegasus-release/references/database-migration.md`,
  `AGENTS.md`
- Change: none beyond correcting plan/checklist progress and the implementation
  report.
- Preserved behaviour: no release operation or verification command runs.
- Forbidden: tests, builds, scripts, browser/capture, commit, push, PR,
  cloud reads/writes, or edits outside Expected files.
- Negative cases: stop on stale key mapping, secret disclosure, altered
  migration order, or any PLAT-046 containment claim.
- Tests: independent documentation review and the sole verifier’s relevant
  serialized documentation check, deferred to parent coordination.
- Commands: `git diff --check` and targeted read-only diff/source inspection
  only.
- Expected output: clean whitespace and a bounded two-file diff; no claim that
  a documentation check has run.
- Done when: the post-implementation report records the omitted checks and the
  worktree is ready for parent-coordinated sole verification and independent
  review.
- Deviation stop: a check requires concurrent verifier ownership or any
  additional file.

## Acceptance checks

- Every current nonblank Production key in `Program.cs` is present, either
  as a configured/derived non-secret value or a clearly inert process-only
  placeholder.
- The reference has no dangling `Release artifacts and bootstrap` dependency,
  secret material, local path, wrapper or unsupported claim.
- The release skill’s migration-before-packages contract is unchanged, and the
  reference names PLAT-046 containment as unresolved/out of scope.
- AGENTS changes only the permitted compact link.
- Independent documentation review and the relevant serialized documentation
  check remain required; this task does not run them.

## Commands

- `git diff --check`
- Targeted read-only comparison against `src/Pegasus.Web/Program.cs`,
  `infra/modules/platform.bicep`, `infra/main.parameters.json`, and
  `scripts/Invoke-ProductionAdministratorBootstrap.ps1`.

## Failure and deviation rules

Stop and report any missing source mapping, unapproved secret exposure, need
for a wrapper or cloud action, order/containment change, protected-AGENTS
overlap, or request to run a sole-verifier check. Do not broaden the task.

## Stop condition

Leave DELIV-055 in Implementing with its exact worktree/branch and a complete
post-implementation report. Do not commit, push, open a PR, move to Review,
run verification, execute a release, or start another ticket.
