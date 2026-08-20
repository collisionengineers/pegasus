# Proof — TICK-028 (OPS-09)

Type: command-log. Released in **release 14** (`d91fd7d7…`, PR #459); promoted to `main` (`39bb118a`). `deployment: n/a` — documentation of measured posture.

- Verification lane at the cut: runbook "Point-in-time restore commands" procedure (inventory → restore into a fresh `pegasus-restore-drill-<date>` DB, never overwriting `pegasus` → Entra-token verification → record/retain → approval-gated reclaim); operations Recovery records the measured posture — 7-day PITR window proven live by `earliestRestoreDate`, Geo backup redundancy, no LTR, ~10-min observed log-backup RPO vs the 15-min target, RTO explicitly unmeasured.
- The restore **drill** is honestly parked (open-questions "Parked (explicitly deferred)") awaiting the operator's exact-target approval for `pegasus-restore-drill-<date>` — the ticket claims documentation and measurement, not execution.
