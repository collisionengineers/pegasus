---
id: PLAT-046
type: ticket
title: Stop old Web and Worker before planned destructive migrations
status: verifying
area: platform-operations
order: 40
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-08-28T07:54:27.369Z'
  review: '2026-09-08T18:40:24.775Z'
  verifying: '2026-09-09T00:57:46.575Z'
taken_at: '2026-09-08T17:52:41.849Z'
branch: PLAT-046-destructive-migration-shutdown
worktree: .worktrees/plat-046
claim_expires_at: '2026-09-08T19:09:16.346Z'
claim_controller: codex-mcp-client
lease_id: f52130e7-d7ab-40ac-aa69-edb643bda825
lease_revision: 11
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\plat-046'
lease_phase: implementing
lease_heartbeat_at: '2026-09-08T18:39:16.346Z'
labels:
  - release
  - worker
  - migrations
  - production-incident
links:
  - TICK-077
refs:
  - docs/runbook.md
commits:
  - bbae334ca33c1f89617dfe458d8d7ac45dff24a0
  - 7e5aff7cf9c2bb69710ec962c4b5e18ded9fde08
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/711'
archived: false
created: '2026-08-28T03:25:47.384Z'
updated: '2026-09-09T00:57:46.575Z'
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

## Current operator-selected approach — 8 September 2026

Current release tooling already applies migrations/grants before new Web/Worker
packages. Complete the remaining old-runtime hazard through release procedure,
not a new per-tick runtime readiness service.

Identify destructive migrations during planning. Temporarily stop BOTH old Web
and Worker before the database changes, accepting a short outage. In the current
unreleased project no compatibility infrastructure is invented; after actual
release schedule the migration outside typical usage hours. Record a concrete
approved window then, not hardcoded hours now.

Use fresh exact-target inventory, persistent Worker Disabled settings plus whole
Function App stop/read-back, exact Web revision deactivation and zero-replica
read-back. Migrate/bootstrap/verify head, deploy new approved bytes with Worker
disabled, then explicitly activate and smoke. No old incompatible runtime restart
after destructive migration begins; recover forward only.

This replaces the historical plan's per-tick schema check and ADR0030's transient
old-runtime-error allowance for this condition. Reconcile an ADR, canonical release
skill, migration recipe, runbook and AGENTS. Centralize only the existing script
Worker Disabled-setting name list; no new control plane or Bicep activation flag.
The alert thresholds remain unchanged; source investigation found they correctly
reported the sustained storm. No alert suppression is part of this ticket.

## Acceptance

- [ ] Planning identifies destructive/non-additive changes and affected capability.
- [ ] Exact Worker stopped and old Web inactive/zero replicas are mandatory evidence
      before destructive SQL; failures/unknown state block the migration.
- [ ] Approved manifest migration/grants/head precede new Web/Worker activation;
      Worker disabled through new-package deployment, explicit safe re-enable.
- [ ] Short outage and post-release outside-usage scheduling are explicit; forward-only
      recovery and separately authorized live operations remain clear.
- [ ] Canonical Worker setting census retains missing/extra/duplicate/value failures.
- [ ] Local scoped script/document checks pass; no claim of a live migration test.
- [ ] No runtime schema polling, dependencies, alert weakening or historical rewrite.

## Original incident notes


- Incident evidence: workspace `0e4342c1-73ea-48d8-8571-8bca88991b21`,
  `AppExceptions` between 02:56Z and 02:59Z on 2026-08-28.
- Deployed release was `09beefef` (EXT-04) plus `84132d01` (ENG-022).
- No operator action was needed; recorded here so the next release does not
  repeat it.
