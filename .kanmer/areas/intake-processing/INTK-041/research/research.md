# Research — near-real-time durable intake

## Question

What authoritative target state removes avoidable e-mail and manual-upload delay and stale UI states while preserving durable recovery, single ownership, and controlled Azure cost?

## Findings

1. **Both entry routes converge on one durable intake owner.** Manual upload stages a receipt in `Upload.cshtml.cs`; mailbox polling calls `PollApprovedInbox`, which calls `ReceiveIntake`. `ReceiveIntake` persists the original source and stable processing work. `ProcessQueuedIntake` owns source reading, identification, classification, extraction, association/allocation, case creation, and completion. This shared Core route must remain the single business implementation.
2. **Two timer hand-offs precede ordinary processing.** `InboxPollFunction` wakes mailbox receipt; `PendingWorkDispatchFunction` later finds pending work and publishes the stable identifier. `StagedArtifactReconciliationFunction` is a recovery/straggler route. Production schedules had been shortened to 15 seconds, 5 seconds, and 10 seconds respectively, but measured e-mail still took about 30 seconds end-to-end.
3. **The measured bottleneck is not later case policy.** One traced ordinary message spent 11.448 seconds before staging and 17.426 seconds from staging to post-reader processing, then about 1.3 seconds through classification and case creation. A separate 15 MB message completed in about 10.07 seconds. Stage spans are needed before changing the reader.
4. **Aggressive polling materially raised cost.** Read-only Azure cost evidence showed the Flex Consumption Function growing from roughly GBP 0.30–0.45/day before the tighter timers to GBP 1.39–1.65/day afterwards. Poll reduction bought little observed latency improvement.
5. **The current durable contract is sound but publication is delayed.** FRD-02 already requires original bytes, receipt, and processing dispatch to commit before acknowledgement; queue messages carry identifiers only; Worker is the sole processor; duplicate delivery must not duplicate outcomes. Immediate publication after that commit can remove the normal timer wait while a slow reconciliation timer retains recovery.
6. **ADR-0002 deliberately selected polling but left a measured exit.** Its mailbox and SQL-outbox sections select Worker polling and timer dispatch, while its alternatives section allows webhooks when measured latency makes polling unsuitable. The current evidence satisfies that condition, so a new ADR should partially supersede only that mechanism.
7. **Graph basic notifications fit the ownership boundary.** Microsoft Graph requires prompt validation/2xx responses, supports a shared clientState secret, limits Outlook subscriptions to under seven days, and provides lifecycle notifications plus delta recovery. The Web host can validate and enqueue a wake identifier without reading or processing mail; the Worker retains cursor/delta ownership.
8. **Truthful transient state is part of latency.** Existing FRD-02 exposes Received/Processing/Complete/Failed. Until original sender identity has been derived, the UI must show neutral Processing rather than the forwarding desk address. Large or retrying inputs remain visibly Processing instead of displaying a stale terminal projection.

## Implications

- Specify a two-stage trigger model: immediate best-effort publication after durable commit for ordinary work, plus slower reconciliation for loss recovery.
- Add a Graph wake-up ingress in Web, with no mailbox business processing in Web.
- Retain five-minute mailbox polling, one-minute pending dispatch recovery, and one-minute staged reconciliation as safety nets rather than primary scheduling.
- Require one approved-Inbox subscription persisted in existing SQL, six-hour maintenance, renewal within 48 hours, lifecycle recovery, and delta resync.
- Instrument stage boundaries and hold ordinary QDOS e-mail/manual-upload completion to p95 <=10 seconds.
- Keep scale-to-zero initially; an always-ready instance is a separately measured and approved operational change.
- Cap the normalized idle Function cost at GBP 0.50/day over seven days.

## Evidence status

Repository routing and current timer/caller ownership were checked directly. Runtime timings and costs are from the read-only production diagnostics gathered for this plan on 2026-08-25. Graph protocol constraints were checked against Microsoft Graph documentation. No cloud state was changed.
