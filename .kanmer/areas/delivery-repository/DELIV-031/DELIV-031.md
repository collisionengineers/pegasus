---
id: DELIV-031
type: ticket
title: >-
  CI sql-integration shards intermittently fail with SqlException "Connection
  Timeout Expired" (pre-login/post-login) on the Windows runner
status: implementing
area: delivery-repository
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-28T21:31:21.667Z'
taken_at: '2026-08-28T21:30:36.955Z'
branch: task/deliv-031-sql-connect-timeout
worktree: ../pegasus-worktrees/deliv-031-sql-connect-timeout
labels:
  - ci
  - flaky
  - sql
groups:
  - EPIC-011
links:
  - DELIV-025
  - UIIMP-005
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-28T11:16:52.307Z'
updated: '2026-08-28T21:31:21.667Z'
---

## What

Four separate `repository-check` runs on 2026-08-28 (PRs #588 shard 3, #589 shard 2, #592 shard 2, and #581) failed one `sql-integration` shard with `Microsoft.Data.SqlClient.SqlException: Connection Timeout Expired … pre-login handshake` / `post-login phase` on unrelated tests (`OrganizationAdministrationWebTests`, `IntakePersistenceIntegrationTests.DraftReceiptPersistsReceiptHistoryContents`, `AutomationConnectorAuthorizationTests.UnregisteredRedirectUri…`, 202 tests in one shard). Re-running the failed job passes. The same tests pass locally on LocalDB.

## Why

Each flake costs a ~15-minute rerun and blocks merges during EPIC-011's high PR throughput; the new `test-ui` lane doubles the SQL load per run.

## Approach

- Inspect the runner's SQL Server startup/health in the shard job (service readiness wait, connection pool size, `MaxParallelThreads`, timeout settings in the test connection string).
- Prefer a readiness gate + a bounded connection-retry policy in the test host over raising timeouts; never mask real failures.
- Record the rerun counts in the PR checks as evidence.

## Verification

- [ ] Ten consecutive `sql-integration` runs without a connection-timeout failure.
