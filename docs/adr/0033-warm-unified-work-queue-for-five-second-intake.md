---
id: ADR-0033
status: accepted
date: 2026-08-26
supersedes: [ADR-0032]
superseded_by: []
related_capabilities: [INT-33]
related_frd: [FRD-02, FRD-08]
tags: [intake, queues, latency, functions]
---
# ADR-0033: Warm unified work queue for five-second intake

- Status: Accepted; supersedes ADR-0032
- Date: 2026-08-26

## Context

Live evidence showed immediate publication removes the SQL recovery wait, but a
queue-triggered Flex Consumption Worker still waits for cold start. Separate
intake and external/custody queues multiply that cold path. A timer being warm
does not keep an independently scaled queue function warm.

## Decision

Use one typed `intake-work` queue and one `UnifiedWorkFunction`. Its message
states whether the durable identifier is intake or external work; it dispatches
only to the existing owning Core processor. Intake and custody keep their
separate durable records, claims, recovery, and business policy.

Keep one 2 GiB always-ready instance for that queue function. The ordinary
target is five-second p95 from Pegasus durable receipt through the current
case/custody outcome for supported work. Box confirmation remains a measured
best-effort final segment: provider delay is attributed, never hidden or
misreported as Pegasus processing delay.

One-minute recovery and mailbox polling remain recovery mechanisms. They do not
share the normal critical path and are not a warm-capacity substitute.

## Consequences

- The normal intake and custody hand-offs avoid a separate scale-to-zero wake.
- There is one queue format and poison route; pre-release bare-GUID messages and
  the retired external queue are removed rather than retained as a dual path.
- One always-ready 2 GiB instance adds a fixed Flex Consumption baseline cost;
  Azure cost evidence determines whether that size remains justified.
- Durable processing records low-cardinality stage timing so p95 can be split
  between queue claim, source work, allocation, and custody/provider work.
- This is source and deployment-template intent only. A deployment and real
  intake measurements are still required to prove the target.

## Options considered

- **Keep separate cold queues:** rejected because measured delay is dominated by
  queue-function delivery, not queue polling.
- **Warm every Worker function:** rejected because only the critical queue path
  needs low latency; warming timers would add cost without removing the delay.
- **Process intake synchronously in Web or a Graph callback:** rejected because
  it would hold external requests over untrusted parsing and duplicate the
  durable Worker boundary.

## Links

- [FRD-02](../frd/frd-02-intake-and-source-identity.md)
- [FRD-08](../frd/frd-08-email-mailbox-and-background-processing.md)
- [ADR-0032](0032-near-real-time-durable-intake-triggering.md)
