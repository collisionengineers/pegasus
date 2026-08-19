# Research — EXT-08 activation

## Question

What activates deterministic report generation as a real Pegasus capability?

## Findings

1. ADR-0025 already chooses integration into the application behind a Core-owned port once there is a real caller; source presence alone is not activation.
2. The operator has now supplied the real caller: when an assessment has all required accepted details, Pegasus invokes rendering and records a report reference. DOCS-001 owns that readiness-triggered workflow and durable identity/custody behavior.
3. Approved initial content is exactly the four rendererref1 assessment variants plus fee note. Unsupported workspace catalogue entries are inactive. Audit is deferred pending an approved template.
4. CASE-31/ENG-01/ENG-02 provide accepted structured source data, canonical repair specification, and Engineer-owned final decisions. Incomplete/unaccepted/ambiguous data fails closed.
5. SIMPLI-014 owns engine integration; DOCS-001 owns the real caller/reference; PLAT-007 owns Azure proof. EXT-08 is the capability-level acceptance envelope and should not duplicate those implementations.
6. Activation evidence requires deterministic mapping/versioning, idempotent generation, immutable artifact version/hash/provenance, failure/retry/recovery, staff-visible state, human approval before issue, representative visual parity, and deployed proof before claiming production.
7. Existing schedule labels say Later/post-alpha, but the operator's 2026-08-19 instruction explicitly activates this work now. Canonical `docs/capabilities.md` and FRD-11 must be updated in the implementation PR to reflect the new designation and exact behavior.

## Implications

- Treat EXT-08 as the integration acceptance ticket spanning SIMPLI-014 → DOCS-001 → PLAT-007, with capability docs and end-to-end proof.
- Do not create another renderer implementation or host.
- Generation is not approval, sending, or receipt.
- Production cloud writes remain approval-gated even though code/IaC/local validation is authorised.
