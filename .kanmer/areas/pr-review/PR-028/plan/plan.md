# Plan — PR-028

## Approach

After PR-026/027 land on the shared branch, derive the final inventory directly from Git and rewrite the MAIL-004 PIR with one row per path and exact evidence.

## Steps

1. Capture `git diff --name-only origin/dev...HEAD` after blocker fixes and count it.
2. Reconcile every path, including generated migration files, route/accessibility inventories, scripts, docs and test fixtures.
3. Replace broad verification prose with exact commands/results and explicit non-deployment/live-write qualifications.
4. Run diff/docs checks and four simplicity lenses; push the shared PR update.

## Governing docs

FRD-08 remains the behavior owner. This ticket changes Kanmer evidence only and does not alter product behavior.

## Verification

Compare PIR rows to the final Git path list one-for-one; rerun `git diff --check` and documentation checks.

## Risks

The inventory can become stale if another fix changes the branch; generate it last.

## Simplification pass — 2026-08-20

- Reuse: Git's final `origin/dev...HEAD` path list is the single inventory source.
- Simplification: one row per path; no duplicate evidence document in the repository.
- Efficiency: verification claims name only commands actually run.
- Altitude: product behavior remains in FRD/design; this ticket corrects Kanmer review evidence.
- Applied finding: the final count is 24, not the original 23, because PR-027 added the canonical runtime-role migration test file.
- No unapplied findings.
