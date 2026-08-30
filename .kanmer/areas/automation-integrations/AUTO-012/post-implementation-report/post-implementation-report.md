# Post-implementation report — AUTO-012

Merged as PR **#635** into `dev` at `b9dcfec9`.

## What the accept path does now

`ReconcileProviderSubmissions` runs as a sixth bounded sweep on the already-live
`StagedArtifactReconciliationFunction` timer. Its candidate query inner-joins
the staged receipt, so it sees only submissions whose intake **was** retained
and whose accept record is still incomplete — the staged-receipt back-reference
missing, or no `Accepted` history row. It writes the back-reference (a no-op
when already set, never overwritten) and appends the missing `Accepted` row.

The condition is deliberately "no `Accepted` row", not "no history", so the
ticket's second symptom is repaired: a replay that wrote only `Replayed` still
gets its `Accepted`. The sweep writes `Accepted`, never `Replayed`.

`GET` therefore stops answering `Received` for a retained submission, bounded by
a ten-second timer rather than by whether the provider happens to retry — which
was the ticket's central complaint.

## Verification, as run

| Claim | Evidence |
| --- | --- |
| Starvation is gone | `AcceptRecoveryIsNotStarvedByOlderBareReservations` on real SQL — 60 day-older bare reservations plus one repairable submission. Pre-fix: `Expected: 1, Actual: 50` candidates, 0 repaired. Post-fix: 1 candidate, 1 repaired |
| A second `Accepted` row is refused | `ASecondAcceptedRowForOneSubmissionIsRefused` — the database refuses it on the primary key (`HasKey(item => item.Id)`, SQL 2627), not a time window |
| The repaired row is honest | `OccurredAtUtc == ReceivedAtUtc`, `NotEqual(Now, …)`, and `accepted.OccurredAtUtc < replayed.OccurredAtUtc` |
| A failure names its cause | `AcceptRecoveryCountsARecoverableFailureAndContinuesTheBatch` asserts the exact string, so a missing grant prints `SqlException: The UPDATE permission was denied…` instead of a healthy-looking zero |
| The collation the joins need | Pinned in `CommittedMigrationCreatesTheSqlServerSchema`; production verified live as `SQL_Latin1_General_CP1_CI_AS` |
| Nothing else broke | Core 1178, Architecture 100, Integration green; all eleven CI jobs green on #635 |

## Things worth carrying forward

**`IActionHistoryWriter` was never composed in the Worker at all.** The lane
found this because two `WorkerCompositionTests` failed, and fixed it — real
wiring, and the one change outside the Provider API's own files.

**The bootstrap census is an equality gate in both directions.** Omitting the
new Worker `UPDATE` grant from it would not have quietly under-granted; it would
have failed the bootstrap *after* the promotion and *after* migrations applied.
CI caught it, which is what that gate is for.

**Two review rounds, six defects, all found in code that was green.** The first
round's build passed, its own tests passed, and every CI job passed — and it
contained a sweep that would silently stop repairing anything after one storage
outage, plus a path that wrote duplicate rows into permanent history.

## Deferred, with reasons

- The Unidentified sweep's swallowed failures still log no cause — the same
  diagnostic gap fixed here, on the sibling reconciler. Filed as [[INTK-053]].
- The `ActionHistory` uppercase/lowercase join shape is **pre-existing**
  (`91033e48`), so normalising case there is wider than this ticket owns. The
  collation assumption is pinned instead.
- `AcceptHistoryGracePeriod` is kept, but it is now understood to protect only
  the first-attempt window; it cannot protect a retried bare reservation,
  because `ReceivedAtUtc` is never refreshed on retry. The idempotent id is what
  actually prevents the duplicate.

## Not activated

This ships no activation. `Features:ProviderApi` is enabled by [[TICK-058]]'s
own change, and **no credential is issued in release 37** — so the route this
repairs has nothing to repair yet. That is deliberate: the fix lands before the
first caller, not after.
