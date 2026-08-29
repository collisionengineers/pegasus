## 2026-08-29 — local evidence that LocalDB contention produces a *different* failure mode

While orchestrating the EPIC-011 closeout with four lanes building and testing
concurrently against one LocalDB instance, a focused integration run failed with:

```
Microsoft.Data.SqlClient.SqlException : A transport-level error has occurred when
receiving results from the server.
(provider: Session Provider, error: 19 - Physical connection is not usable)
Failed!  - Failed: 2, Passed: 4, Skipped: 0, Total: 6, Duration: 2 m 12 s
```

This is **not** [[DELIV-031]]'s symptom. DELIV-031 is a *connect*-phase timeout
("Connection Timeout Expired", pre-login/post-login), and its fix raised
`ConnectTimeout` from 15 s to 60 s. This failure happens **after** a connection is
established — the physical connection is torn down mid-result.

`ConnectTimeout` cannot help a connection that dies after it opens. The
`ConnectRetryCount` / `ConnectRetryInterval` properties, which
`IntakePersistenceIntegrationTests.BuildConnectionString` leaves at their
SqlClient defaults of 1 and 10 s, govern **idle-connection resiliency** and are
the knob aimed at exactly this class of break. That is this ticket's subject.

### What this does and does not prove

- It **does** show that DELIV-031's timeout raise is not sufficient for every
  LocalDB failure mode, which is this ticket's trigger condition.
- It **does not** prove the CI shards suffer this. The observed cause was local:
  four concurrent orchestration lanes driving one LocalDB, which CI does not do —
  CI shards across three runners, each with its own instance.

So this is **supporting** evidence, not the case for the change. Do not cite it as
a CI failure. If this ticket is worked, get CI evidence of the same error-19
signature before changing the harness, or record honestly that the only evidence
is local.

### Orchestration lesson, recorded so it is not relearned

Parallel lanes must not each run the integration suite against the shared LocalDB.
The authoritative integration gate is CI, which isolates per shard. Lanes should
run focused unit filters locally; the orchestrator runs the integration gates.
A local integration failure while several lanes are active is **inconclusive**
until re-run in isolation — and per AGENTS.md rule 20, INCONCLUSIVE is not PASS
and equally is not a defect report.
