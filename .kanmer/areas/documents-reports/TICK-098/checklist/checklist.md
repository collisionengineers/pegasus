# Checklist — TICK-098

- [x] Reconcile stale RPT-03 capability and FRD-11 wording with the operator's physical-output parity decision.
- [x] Specify reuse of the existing Inspection report Core contract and integrated renderer without a second template or presentation family.
- [x] Specify that the normal Case/PO and correct immutable `a.{Case/PO}` or `ap.{Case/PO}` internal Audit reference are bound through that shared contract.
- [x] Specify fail-closed behaviour for missing, conflicting, ambiguous, stale, or cross-case Audit reference evidence.
- [x] Specify that both Audit outcomes use the same physical report presentation as equivalent Inspection data when a future accepted caller activates RPT-03.
- [x] Prohibit dual-specification, monetary-uplift, and percentage-uplift fields, calculations, and output.

## Progress notes

2026-08-20 — Docs-only implementation. The active renderer is intentionally closed to Audit and RPT-03 remains Later; no code path, template, or feature-gate activation was added or claimed.
