## Azure diagnosis, 2026-09-02 (read-only; nothing changed)

Workspace `pegasus-prod-logs-252ow37gij` (customer id `0e4342c1-73ea-48d8-8571-8bca88991b21`), Worker `pegasus-prod-worker-252ow37gij`, SQL `pegasus-prod-sql-252ow37gij/pegasus`.

### Estate state at 14:18Z

- `ApprovedInboxPollStates`: `LastCompletedAtUtc = DueAtUtc = 14:15:02Z`, `LastFailureCode` empty, no lease.
- `ApprovedMailboxSubscriptions`: `09018cc2…`, `Active`, expires 2026-09-06 11:15Z, last maintained 09:50Z.
- Functions: all 7 enabled; `ApprovedInboxPollSchedule = 0 */5 * * * *`, `IntakeStagedArtifactReconciliationSchedule = */10 * * * * *`, `PendingWorkRecoverySchedule = 0 * * * * *`.
- Queues `intake-work` / `intake-work-poison`: 0 pending (peek).
- `InboxRecoveryFunction`: 336/360 failures in the prior 30 h, all before 12:50Z (release 38); since then only the 13:15:00Z run failed (`TaskCanceledException`, HttpClient 100 s timeout on a Graph call, self-recovered).
- `InvalidDataException "Graph Inbox message omitted receivedDateTime."` last seen 12:45:04Z.

### Timeline (UTC) — from `AppRequests`, `AppTraces` (queue `InsertedOn`) and `AppDependencies`

| Time | Function | Graph calls | Result |
| --- | --- | --- | --- |
| 14:00:00 | InboxRecoveryFunction | delta | nothing |
| 14:00:36 | queue insert (wake) | | notification for EREF9 (received 14:00:36) |
| 14:00:37 | UnifiedWorkFunction 455 ms | delta 87 ms | nothing |
| 14:04:17 | queue insert (wake) | | notification for EREF8 (received 14:04:17) |
| 14:04:19 | UnifiedWorkFunction 127 ms | delta 83 ms | nothing |
| 14:05:00 | InboxRecoveryFunction | delta 76 ms | nothing |
| 14:05:13 | `POST /hooks/microsoft-graph/mail` 202 | | notification for EREF24 (received 14:05:12) |
| 14:05:15 | UnifiedWorkFunction | delta 75 ms + `messages/…Gc_ubQAA/$value` | EREF24 staged (`approved-inbox-notification`) |
| 14:10:00 | InboxRecoveryFunction | delta 244 ms + `/$value` ×3 (`Gc_ubQAA`, `Gc_uSgAA`, `Gc_qKgAA`) | EREF8 + EREF9 staged (`approved-inbox-poller`); EREF24 re-listed, deduplicated — no second receipt |

Receipts: `3f4f117e…` EREF9 received 14:00:36 / staged 14:10:03 → QDOS26035; `71bff04d…` EREF8 received 14:04:17 / staged 14:10:02 → U45 ([[INTK-056]]); `c45c65a8…` EREF24 received 14:05:12 / staged 14:05:15 → `case_type_unavailable`, manual review ([[INTK-057]]).

### Not the cause

- Webhook/subscription: healthy; only 2 `POST /hooks` rows in 30 h in `AppRequests`, but the queue `InsertedOn` stamps prove the wakes arrived (Web request telemetry is sampled).
- MAIL-033: the 09-01 08:40Z/08:45Z runs show one and two `/$value` fetches *before* the throw — the sparse item was an update entry on an older message, not a new one.
- `StagedArtifactReconciliationFunction` failed every 10 s 12:50:30Z–13:54:20Z (384× duplicate `ExternalWorkItems.OperationKey vehicle-lookup:auto:MT21ZFA` / FK `VehicleLookupRequests`), ending when the wipe deleted the rows. Unrelated; watch for recurrence after the next case.

### KQL used

```kusto
AppRequests | where TimeGenerated > datetime(2026-09-02T12:00:00Z)
| where OperationName in ('InboxRecoveryFunction','UnifiedWorkFunction') or Name contains 'hooks'
| project TimeGenerated, OperationName, Success, DurationMs, OperationId | order by TimeGenerated asc

AppDependencies | where TimeGenerated > datetime(2026-09-02T13:54:00Z)
| where Target has 'graph.microsoft.com' | project TimeGenerated, Data, DurationMs, ResultCode | order by TimeGenerated asc

AppTraces | where OperationId in ('c2d46809…','fca45f2c…') | project TimeGenerated, Message
```

Reference: learn.microsoft.com/graph/delta-query-overview#limitations ("Processing delays … Retry the @odata.deltaLink after some time").
