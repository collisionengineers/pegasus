# Open decisions

This register contains unresolved product or technical choices only. Accepted
technical choices belong in [ADRs](adr/README.md), behavior in [FRDs](frd/README.md),
and product scope in the [PRD](prd/pegasus-product.md). The current operator task
and its linked PR/CI records own delivery work.

Add a question here only when it is genuinely unresolved, with its owner,
affected contract and the decision needed; remove it once its outcome has a
durable owner.

## Photo capture: a custom capture domain (deferred)

- **Owner**: the operator.
- **Affected contract**: [ADR-0090](adr/0090-public-photo-capture-surface.md)
  (same origin now), `PhotoCapture:PublicOrigin`
  ([configuration](engineering/configuration.md#photo-capture)) and the
  links [FRD-28](frd/frd-28-guided-photo-capture.md#photo-links) prints.
- **Decision needed**: whether photo links move to a Collision Engineers
  domain. Links carry the Pegasus hostname for now, and a custom domain stays
  a separate decision (operator, 9 November 2026, R779); EXT-19 stays
  retired, so a custom domain would take a new capability identity. Links
  already sent keep the hostname they were sent with until they expire.

## Mail classifier: how long its runs are kept

- **Owner**: the operator.
- **Affected contract**: [FRD-08](frd/frd-08-email-mailbox-and-background-processing.md)
  (the classifier's run record, R1318), the `ClassifierRuns` and
  `ClassifierRunItems` tables, and the live classifier's `runs` table that
  the hosted test instance reads (R1319).
- **Decision needed**: whether runs are deleted after a period, and which.
  The task proposed 90 days. Nothing deletes a log-like row today: the only
  age purge is the staff notifications' 30 days, an hourly step on the
  Worker's one-minute sweep, which a run purge would join, with a Worker
  delete grant on the two tables. Until this is decided, runs are kept; at
  one run every 15 minutes the database gains about 96 runs a day.
