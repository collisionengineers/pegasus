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
