# Open questions — TICK-097

Resolved by the Collision Engineers operator on 2026-08-19.

- [x] Support exactly four RPT-02 assessment outcomes: **Total loss**, **Repairable**, **Cash in lieu**, and **Contract repair**. Contract repair is a distinct fourth variant with its approved capped agreed-repair wording; conflicting “three outcomes” prose in rendererref1 is stale.
- [x] Initially activate only the caller-backed **assessment and fee-note** families evidenced by `reference/rendererref1/`. Unsupported CollisionRenderer catalogue entries remain inactive.
- [x] Use the exact approved supplied assessment wording, named qualifications and three supplied signatures only when matched to the selected Engineer and followed by human approval before issue. Missing wording/qualification remains unavailable and fails closed; it is never invented.

## Parked (explicitly deferred)

- Audit rendering remains deferred to [[TICK-207]] until a representative Audit template is supplied/approved.
- If representative PDFs do not unambiguously prove whether the fee note is one page in the report artifact or a separately retained linked artifact, resolve packaging from the actual approved evidence before implementation; preserve distinct identity/hash/provenance either way.
