# Plan — TICK-098: shared Audit/Inspection report output

## Governing decision

Audit reports are physically identical to Inspection reports. The internal process differs and Audits use the existing immutable `a.{Case/PO}` or `ap.{Case/PO}` reference, but neither difference authorizes a separate template, comparison pair, uplift, wording, layout, or renderer family.

## Steps

1. Correct stale RPT-03 capability and FRD-11 wording so the normative behavior states physical Inspection-report parity and the existing Audit reference rule.
2. Reuse the existing Core-owned Inspection report input/readiness/render contract and integrated Infrastructure renderer.
3. Carry the accepted Case/PO, applicable Audit reference, workflow provenance, and immutable report identity/version through the shared contract.
4. Fail closed for missing, conflicting, ambiguous, stale, or cross-case Audit reference evidence.
5. Prove repairable `a.` and total-loss `ap.` journeys, identical physical presentation against equivalent Inspection data, deterministic retries, correction lineage, and absence of uplift/dual-spec fields.

## Reuse and simplicity

No second template, report model, calculation owner, or Audit-only renderer is permitted. Existing case identity owns Audit reference derivation; existing report policy owns output.

## Verification

Run focused Core identity/report tests, renderer parity tests through real Chromium, integration tests for both Audit outcomes, and the repository's locked restore/build/test profile.

## Execution scope update — 2026-08-20

The active renderer surface is explicitly closed to Audit and no accepted Audit caller exists. Per FRD-11 and the feature-gate safety rail, this ticket records the corrected future contract only; it does not add an unreachable Audit code path or claim activation. The implemented scope is therefore the governing capability/FRD correction, with no code or renderer change.

## Simplification pass — 2026-08-20

n/a — docs-only. The review confirmed the existing renderer boundary remains the sole presentation owner; no abstraction, template, model, or feature-gate change is introduced.
