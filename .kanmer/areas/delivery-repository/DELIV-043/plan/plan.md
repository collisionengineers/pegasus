# Plan — DELIV-043: Cancel superseded repository-check runs and gate the heavy lanes behind the cheap invariant jobs

## Objective
One live `repository-check` run per pull request, and no heavy `.NET` lane starts while a cheap invariant job has already failed.

## Starting state
`.github/workflows/ci.yml` (`dev` at `c804056d`): no `concurrency`; `documentation`, `local-development-scripts`, `reference-data` have no dependants; heavy lanes `needs: changes` only; `sql-integration-coverage` is `always() && build`.

## Governing docs
`docs/engineering.md` "Branches and delivery": **Meets** (green still means every job succeeded or was path-skipped). No ADR.

## Required changes
1. Top-level `concurrency: { group: ${{ github.workflow }}-${{ github.event.pull_request.number || github.ref }}, cancel-in-progress: ${{ github.event_name == 'pull_request' }} }`.
2. `needs: [changes, documentation, local-development-scripts, reference-data]` on `infrastructure`, `unit`, `sql-integration`, `browser`, `test-ui`; their `if` unchanged.
3. `sql-integration-coverage`: `if: always() && needs.changes.outputs.build == 'true' && needs.sql-integration.result != 'skipped'`.

## Expected files
| Action | Path | Responsibility |
|---|---|---|
| Modify | `.github/workflows/ci.yml` | the three changes above; comments explaining each |

## Do not modify
Any script, lane command, timeout, runner, or the `changes` job's steps.

## Ordered steps
1. Edit `ci.yml`; parse it as YAML locally (node `yaml`) to catch indentation errors.
2. Commit, push `task/deliv-043-ci-concurrency-preflight`, PR to `dev`; the PR's own run exercises every lane.
3. Independent review; merge when green.

## Acceptance checks
- The PR run shows every heavy lane `success` (or path-skipped) after the three cheap jobs.
- After merge, a PR that fails `documentation` shows the heavy lanes `skipped` and `sql-integration-coverage` `skipped`.
- A second push to an open PR cancels the older run.

## Simplification pass
n/a — workflow-only; the diff is three additive edits.

## Stop condition
PR open and reviewed; merge into `dev` by the independent reviewer after green CI.
