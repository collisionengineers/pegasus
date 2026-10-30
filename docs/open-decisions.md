# Open decisions

This register contains unresolved product or technical choices only. Accepted
technical choices belong in [ADRs](adr/README.md), behavior in [FRDs](frd/README.md),
and product scope in the [PRD](prd/pegasus-product.md). The current operator task
and its linked PR/CI records own delivery work.

Add a question here only when it is genuinely unresolved, with its owner,
affected contract and the decision needed; remove it once its outcome has a
durable owner.

## Cutover date and the definition of v1

- **Owner**: the operator.
- **Affected contract**: [ADR-0030](adr/0030-non-additive-schema-changes-before-cutover.md)
  (non-additive schema changes are permitted only before cutover),
  [ADR-0046](adr/0046-destructive-migration-runtime-shutdown.md) (the
  destructive-migration shutdown sequence), the disposable-test-data basis of
  the intake wipe, and the proof round and UAT pack that close release
  readiness.
- **Decision needed**: the date, or the condition, at which production stops
  being a test estate and carries real Cases, and what the application must
  do by then to be v1. Until it is made, production data is disposable and
  releases verify changes; after it, ADR-0002's expand-and-contract rule and
  ADR-0046's full containment apply without exemption.

## Cutover: a re-sent or forwarded old-system instruction creates a Case (#1600)

- **Owner**: the operator.
- **Affected contract**: [FRD-01](frd/frd-01-case-identity-and-lifecycle.md)
  (new correspondence about old work is handled under the current process),
  [FRD-08](frd/frd-08-email-mailbox-and-background-processing.md) (a forwarded
  old instruction is never promoted by guesswork), and the automatic allocation
  in `AllocateIntake.AttemptAutomaticAsync`, which creates a Case for a
  mailbox receipt classified New instruction with no Case match.
- **Decision needed**: how a re-sent or forwarded instruction for a Case still
  run in the old system is handled at cutover, so it does not become a
  duplicate Case under a new reference. The operator's ruling of 28 October
  2026 (R512) makes this the one cutover item that is decided and built before
  the v1 date; [#1336](https://github.com/collisionengineers/pegasus/issues/1336)
  was taken up on 30 October 2026 (R550: the Pre-Pegasus case mail category
  and the classifier agent's read-only old-system lookups), and
  [#1601](https://github.com/collisionengineers/pegasus/issues/1601) stays
  parked under R391 until the date is set.
