# Open decisions

This register contains unresolved product or technical choices only. Accepted
technical choices belong in [ADRs](adr/README.md), behavior in [FRDs](frd/README.md),
and product scope in the [PRD](prd/pegasus-product.md). The current operator task
and its linked PR/CI records own delivery work.

Add a question here only when it is genuinely unresolved, with its owner,
affected contract and the decision needed; remove it once its outcome has a
durable owner.

## Cutover: the Case reference sequence is not seeded from the old system (#1601)

- **Owner**: the operator.
- **Affected contract**: [FRD-01](frd/frd-01-case-identity-and-lifecycle.md)
  (the reference format and the one sequence per Principal per year) and
  `CaseIdentityAllocator`, which starts a missing `CaseSequences` row at zero.
- **Decision needed**: whether and how each Principal's current-year sequence
  starts from the old system's last issued number. The old system issues the
  same `{code}{YY}{sequence}` format (R592), so a reference Pegasus allocates
  this year may repeat one the old system already issued. Production has
  carried live Cases since 2 November 2026 (R603);
  [#1601](https://github.com/collisionengineers/pegasus/issues/1601) stays
  parked under R606 ("Decide later") and this row records the live risk.

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
  was taken up on 30 October 2026 (R561: the Pre-Pegasus case mail category
  and the classifier agent's read-only old-system lookups). The date is
  2 November 2026 (R603).
