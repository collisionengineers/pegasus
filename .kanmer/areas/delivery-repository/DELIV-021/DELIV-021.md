---
id: DELIV-021
type: ticket
title: 'Prove near-real-time intake latency, recovery, telemetry, and seven-day cost'
status: backlog
area: delivery-repository
order: 210
assignee: ''
profile: chore
labels: []
links:
  - INTK-041
  - INTK-042
  - INTK-043
  - MAIL-013
  - MAIL-035
refs:
  - docs/prd/pegasus-product.md
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
  - docs/adr/0033-warm-unified-work-queue-for-five-second-intake.md
archived: false
created: '2026-08-25T15:18:41.009Z'
updated: '2026-09-03T15:15:27.327Z'
---

## What

Prove the remaining production evidence for INT-33 now that the near-real-time
intake implementation and deployment baseline has shipped.

## Shipped baseline

- [[INTK-041]] established the near-real-time durable-intake contract.
- [[INTK-042]] deployed immediate post-commit intake publication and the
  one-minute interrupted-work recovery path in release 32.
- [[INTK-043]] deployed Microsoft Graph mailbox wakes, the unified warm Worker
  queue, and five-minute mailbox fallback recovery in release 33.
- Those releases prove deployment, routing, configuration, and technical
  health. They do not prove the required production latency distribution,
  recovery behaviour under loss, stage telemetry, or normalized idle cost.

## Remaining acceptance

- Exercise ordinary supported e-mail and manual-upload paths in production and
  measure from Pegasus durable receipt to the current case or custody outcome.
- Prove p95 at or below five seconds. Attribute Box and other provider delay
  separately rather than hiding it in Pegasus processing time.
- Prove the immediate wake and slower recovery paths without duplicate business
  effects, and show truthful Received or Processing state for large, retrying,
  or legitimately incomplete work.
- Record enough low-cardinality stage telemetry to support the latency and
  recovery claims without source content.
- Observe seven normalized days and prove idle Functions cost at or below
  GBP 0.50 per day with the accepted warm-queue architecture.
- Obtain explicit approval for every production write or cloud configuration
  change. If deployment occurs, refresh `docs/current-architecture.md` and
  `docs/operations.md` in the same task.

## Outcome

Implementation and deployment are complete through [[INTK-041]], [[INTK-042]],
and [[INTK-043]]. This ticket now owns only the remaining production latency,
recovery, telemetry, and seven-day normalized-cost proof.
