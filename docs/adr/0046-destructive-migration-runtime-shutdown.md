---
id: ADR-0046
status: accepted
date: 2026-09-08
supersedes: [ADR-0030]
superseded_by: []
related_capabilities: []
related_frd: []
tags: [deployment, schema, migrations, worker, outage]
---
# ADR-0046: Destructive migration runtime shutdown

- Status: Accepted
- Date: 2026-09-08
- Partially supersedes: ADR-0030's accepted old-runtime error window for a
  non-additive migration

## Context

The Worker can start its timers before a migration reaches a column its new code
reads. A bounded failure storm is not an acceptable deployment state simply
because it self-corrects when SQL completes. Pre-release Pegasus has no current
consumer or retained-data requirement that calls for compatibility infrastructure,
but a destructive migration still creates a rollback gap.

## Decision

Planning identifies non-additive operations, their affected capability, and the
forward-only recovery boundary. A destructive route has an explicitly approved
short outage: the old Worker is stopped and the exact old Web revision is inactive
with zero replicas before SQL changes. Unknown or failed read-back blocks the
migration.

The approved new Worker may be staged with all functions disabled while the old
schema remains intact, because the release route does not assume a stopped Flex
deployment state. That staging is not activation. After migration, runtime grants
and migration-head verification, the release provisions the approved new Web with
the Worker still disabled, then explicitly enables the same approved release and
proves active Web, running Worker, exact enabled census, and the full smoke.

Once destructive SQL begins, recovery is forward-only. Old Worker bytes and the
old Web revision must not resume against the changed or unknown schema. If the
new release cannot be activated, the operation is an unfinished outage and is
reported as such; it is not a successful deployment. After real release, an
approved migration window is scheduled outside typical usage, with the concrete
window recorded at that time rather than hardcoded here.

## Consequences

- ADR-0030 still permits the pre-cutover non-additive exemption, but no longer
  accepts old-runtime faults as the release window for that exemption.
- The release procedure owns exact commands and evidence; the runbook summarizes
  the operational guarantee rather than duplicating the recipe.
- No per-tick schema readiness mechanism, alert suppression, deployment feature
  flag, or compatibility fallback is introduced.

## Links

- [ADR-0030](0030-non-additive-schema-changes-before-cutover.md)
- [release procedure](../../.agents/skills/pegasus-release/SKILL.md)
- [repository runbook](../runbook.md)
