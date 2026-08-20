## Independent review — PR #447 (orchestrator, 2026-08-20)

Verdict: **pass**, merge on green CI.

- The group is now the registration unit: one ImageIntake per submission group via nullable `SubmissionGroupId` + filtered unique index + FK (no new table → existing grants cover it), group-scoped operation key `image-intake-register:group:{id}`, and a durable primary (lowest-ordinal image-only member) that every racing sibling computes identically — replays converge on the one row.
- Member receipts move to the registered decision inside the registration transaction; the replay/straggler path probes without touching receipts; race losers defer via the existing durable-work retry, never the instruction fallback (INTK-011's contract preserved and strengthened).
- Association authority stays with the group routing decision (fail-closed): registration never associates a case unless the decision was AssociateExistingCase.
- Promptness half delivered as a deliberate, commented bicep change: PendingWorkDispatchSchedule 60s→15s (dispatch latency was the ~21 s dead time). Release-14 provision preview will show this app-setting diff — EXPECTED, record it at deploy time.
- FRD-02 updated in the same PR. Concurrency tests added (GroupedImageIntakeConcurrencyTests) plus persistence coverage of the unique index.
