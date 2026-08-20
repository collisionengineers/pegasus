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
