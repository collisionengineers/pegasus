---
id: PLAT-046
type: ticket
title: >-
  The Worker serves before migrations complete, so a column-adding release
  throws until they land
status: backlog
area: platform-operations
assignee: ''
profile: fix
labels:
  - release
  - worker
  - migrations
  - production-incident
links:
  - TICK-077
refs:
  - docs/runbook.md
archived: false
created: '2026-08-28T03:25:47.384Z'
updated: '2026-08-28T03:25:47.384Z'
---

## What

The Worker begins running its timers against the production database before
the release's migrations have been applied, so any migration that adds a column
the new code reads throws on every tick until it lands.

## Why

Observed in production on 2026-08-28, alert *Pegasus production application
exceptions* (Sev1) on `pegasus-prod-logs-252ow37gij`:

```
Invalid column name 'EvaAutomaticSubmission'.
52 exceptions, 02:56:42Z to 02:58:40Z
13 failed runs of StagedArtifactReconciliationFunction
```

[[TICK-077]] added `Principals.EvaAutomaticSubmission` and a reconciliation
sweep that reads it. The sweep is on the existing 10-second reconciliation
timer, so between the Worker starting and the migration completing it threw
roughly every ten seconds. It recovered by itself the moment the migration
applied, and there has been nothing since.

Nothing was lost and no case was affected — the sweep is idempotent and simply
enqueued nothing during the window. The problem is that this is not specific to
EVA. **Any future release whose migration adds a column its own code reads will
do exactly this**, and each one costs a Sev1 page for a fault that is expected
and self-correcting.

Two things are wrong:

1. The Worker's timers start before migrations are known to be applied.
2. A predictable two-minute deployment window pages at Sev1, which trains
   people to ignore the alert that would matter.

## Approach

- Establish the actual ordering the release performs today: whether migrations
  run before, during, or alongside Worker startup, and whether readiness gates
  the timers. `GET /health/ready` also showed 47 failures in the same period,
  which suggests readiness knows about pending migrations but does not hold the
  timers back.
- Decide the fix: hold timer execution until migrations are confirmed applied,
  or sequence the release so the Worker is not serving until they are. Prefer
  whichever the existing readiness check already knows.
- Consider whether a schema-shaped `SqlException` during startup deserves its
  own handling rather than escaping as an unclassified fault.
- Re-check the alert rule so a bounded deployment window does not page at Sev1
  while a sustained fault still does.

## Verification

- [ ] A release adding a column its own code reads produces no exception
      storm.
- [ ] The alert still fires for a sustained exception rate.
- [ ] `docs/runbook.md` records the ordering guarantee.

## Notes

- Incident evidence: workspace `0e4342c1-73ea-48d8-8571-8bca88991b21`,
  `AppExceptions` between 02:56Z and 02:59Z on 2026-08-28.
- Deployed release was `09beefef` (EXT-04) plus `84132d01` (ENG-022).
- No operator action was needed; recorded here so the next release does not
  repeat it.
