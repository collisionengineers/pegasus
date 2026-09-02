---
id: MAIL-020
type: ticket
title: >-
  App Insights component daily cap (0.1 GB, 00:00Z reset) silences Web and
  Worker telemetry by mid-morning
status: done
area: mail-communications
order: 2450
assignee: claude-code
profile: fix
stageEntered:
  preparing: '2026-08-27T11:27:12.504Z'
  review: '2026-08-27T17:10:03.029Z'
  verifying: '2026-08-27T17:45:28.542Z'
  done: '2026-08-27T17:52:45.076Z'
labels:
  - observability
groups:
  - EPIC-010
links: []
commits:
  - 46a21f92
  - 14c6fd4155cf3dd63b33049b05caa370d5d9b94d
prs:
  - '#576'
archived: false
created: '2026-08-27T10:06:22.851Z'
updated: '2026-09-01T14:44:34.024Z'
---

## Problem

`Usage` for 2026-08-27 shows ~100 MB ingested between 00:00Z and ~05:30Z (3.7 + 20.6 + 20.7 + 20.7 + 20.8 + 14 MB per hour), then 0.1–0.5 MB/h for the rest of the morning. That matches the **component-level** `dataVolumeCap.cap = 0.1` GB with `resetTime = 0` (00:00 UTC) on `pegasus-prod-appi-252ow37gij` — distinct from the workspace cap (03:00Z reset, `RespectQuota`, whose `_LogOperation` therefore shows nothing). The Web's `AppRequests` end at 05:31Z; the Worker's last records trickled through to ~09:58Z. `AppDependencies` is 64.7 MB of the day's volume — the Worker's per-query SQL dependency telemetry.

Consequence today: the exception behind the failed EREF10 Audit allocation (INTK-044, 10:25Z) was never ingested and cannot be recovered; the MAIL-017 timer proof had to come from the database.

## Required outcome

Telemetry survives a full working day: drop `AppDependencies` volume (disable SQL dependency collection or sample it), keep exceptions/requests/traces unsampled, and either raise the component cap or align it with the workspace cap so one clear limit governs. `docs/operations.md` records the new shape. Interim operator note: a cap reset happens at 00:00Z, so a reproduction attempted after that will be captured.

## Outcome

Merged to `dev` via PR #576 (https://github.com/collisionengineers/pegasus/pull/576), merge commit `14c6fd4155cf3dd63b33049b05caa370d5d9b94d`, 2026-08-27. Proof PASS at that SHA.

Shipped in code: `infra/main.bicep` binds both the App Insights component cap (`pricingPlans`) and the workspace `dailyQuotaGb` to one `telemetryDailyCapGb = 0.5` variable; the Worker registers `SqlDependencyTelemetryFilter`, dropping only successful SQL dependency items; `docs/operations.md` records the shape.

**Not yet live.** The cap raise (0.1 → 0.5 GB on `pegasus-prod-appi-252ow37gij` and `pegasus-prod-logs-252ow37gij`) and the deployed Worker filter await the next release. Read-only checks at verification confirmed both live caps are still 0.1 GB. Raising them changes billing and requires explicit operator approval for those exact targets before that release; the release must also refresh `docs/current-architecture.md` and `docs/open-decisions.md` (review finding R3). No follow-up ticket was created by closeout.
