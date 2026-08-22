# Proof — verified in production, with numbers

**Shipped:** PR #505, commit `1a86f5db` · **Deployed:** Release 17, re-provisioned on Release 18 (`1f3be493`).

## The deployed configuration

Read back from the Function App after provisioning:

| Setting | Before | Now |
| --- | --- | --- |
| `PendingWorkDispatchSchedule` | `*/15 * * * * *` | `*/5 * * * * *` |
| `IntakeStagedArtifactReconciliationSchedule` | `30 * * * * *` | `*/10 * * * * *` |
| `ApprovedInboxPollSchedule` | `45 * * * * *` | `*/15 * * * * *` |

All nine `AzureWebJobs.<function>.Disabled` settings read `false`, so the worker is live.

`extensions.queues.maxPollingInterval` ships inside `worker.zip` and was deployed via
`config-zip` on both releases.

## Measured end to end

Two real instructions, received-to-case-visible, from production timestamps:

| Case | Received | Case created | Elapsed |
| --- | --- | --- | ---: |
| QDOS26009 | 22:59:57 | 23:00:16 | **19 s** |
| QDOS26010 | 02:00:34 | 02:01:04 | **30 s** |

Against the operator's reported **30–60 s and longer**. Numbers, not adjectives, as the
ticket required.

## Stated honestly rather than claimed as a win

**This is better, not "seconds".** The remaining cost is the inbox poll interval — up to 15 s
before the mail is even noticed — plus cold start, which the operator explicitly chose to
accept by declining an always-ready instance. The 19 s case is close to the floor those two
allow; the 30 s case suggests a cold start was paid.

What this ticket removed was the queue idle back-off: `maxPollingInterval` was unset, so
each of two hops backed off to the 60 s default. That cost is gone. Going below ~15 s
requires a decision the operator has already declined, and pretending otherwise would
misrepresent the result.

## Cost

About +20 no-op executions a minute (~29,000/day) on Flex Consumption. Immaterial against
the £75 budget alert, which remains the backstop.
