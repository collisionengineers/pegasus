---
id: DELIV-033
type: ticket
title: >-
  Evaluate an execution-strategy connection retry for the LocalDB integration
  harness, if DELIV-031's timeout raise proves insufficient
status: backlog
area: delivery-repository
assignee: ''
profile: fix
labels:
  - ci
  - flaky
  - sql
groups:
  - EPIC-011
links:
  - DELIV-031
  - 'https://github.com/collisionengineers/pegasus/pull/612'
archived: true
created: '2026-08-29T08:33:03.130Z'
updated: '2026-09-03T14:14:43.202Z'
---

## What

[[DELIV-031]] preferred "a readiness gate + a bounded connection-retry policy
in the test host over raising timeouts" and shipped the timeout raise instead
(`ConnectTimeout` 15 -> 60 in `LocalDbTestDatabase.BuildConnectionString`).
Its recorded disposition rejected the readiness gate on evidence, and deferred
the connection-retry half here rather than silencing it.

## Why

The retry half of the ticket's preference was never evaluated on its merits.
Two candidates exist and neither was tried:

- EF Core's `UseSqlServer(..., o => o.EnableRetryOnFailure())` on the harness's
  compositions.
- `Microsoft.Data.SqlClient`'s configurable retry logic
  (`SqlConnection.RetryLogicProvider`), which unlike
  `ConnectRetryCount`/`ConnectRetryInterval` can be given custom error numbers.

## Trigger

Do not start this speculatively. Open it only if DELIV-031's own acceptance
fails — that is, if `sql-integration` still produces a
`Connection Timeout Expired` failure within ten consecutive runs after
DELIV-031 merges. If those ten runs are clean, close this as not needed.

## Approach

- Establish first whether the client-side connect-timeout signature (surfaced
  as error -2) is actually covered by the candidate's transient list. It is
  **not** covered by `ConnectRetryCount`/`ConnectRetryInterval`, which is why
  DELIV-031 removed them; do not assume EF Core's list is the same without
  checking it.
- Check the conflict between a retrying execution strategy and the harness's
  explicit transactions before adopting `EnableRetryOnFailure`.
- Note that a retry attached to the EF composition does not cover the raw
  `SqlConnection` opens on `MasterConnectionString()`, which is where the
  lifecycle DDL runs.
- Requires a full `Pegasus.IntegrationTests` run, not a focused filter, since
  a retrying execution strategy can break unrelated transactional tests.

## Verification

- [ ] Ten consecutive `sql-integration` runs without a connection-timeout
      failure.
- [ ] Full `Pegasus.IntegrationTests` suite green (no transaction/execution
      strategy regressions).

## Outcome

The trigger did not fire. In the first ten completed, non-cancelled `repository-check` workflows after PR #612 merged, no `sql-integration` failure contained the specified `Connection Timeout Expired` signature. Three SQL shards failed for unrelated assertion or route regressions, and the preserved local error 19 evidence is a different post-connect failure. The proposed execution-strategy retry is not required.
