# ADR-0012 — Conservative MOT mileage estimation

**Status:** Accepted (2026-07-30).

When DVSA history must estimate Case mileage, Pegasus preserves raw observations; accepts only recognised mile/kilometre units; groups fail/retest episodes; excludes implausible or low-information intervals without deleting them; and treats a corroborated odometer drop as a new segment. It derives an estimate from a recency- and quality-weighted median of clean rates, using a versioned cohort prior only for sparse histories that pass its sample checks. Exact observations are returned on exact MOT dates, interpolation is limited to a compatible segment, forecasting is limited to a validated horizon, and calibrated intervals require eligible chronological holdouts. Otherwise Pegasus shows a wider, explicitly non-probabilistic range and never defaults it into the Case.

This deliberately favours a reviewable abstention or qualified range over a plausible but unsupported mileage value. It applies only after the separately accepted DVSA/DVLA route, input contract, and caller evidence activate vehicle enrichment; it neither selects a provider nor authorises an external call.

## Deferred capability impact

- **Deferred:** DVLA/DVSA provider selection, licence, contract, credentials,
  caller, and live activation remain open.
- **Preserved seam:** raw observations, normalized units, model/rule version,
  estimate/range, calibration evidence, and staff disposition remain distinct
  source-labelled identities.
- **Excluded:** this decision creates no provider adapter, scheduled lookup,
  cohort dataset, automatic external call, or unreviewed Case mutation.
- **Activation evidence:** representative chronological holdouts, contract and
  failure/recovery proof, a real caller, and operator acceptance are required.
- **Irreversible choice:** the estimate may be derived only by this conservative
  algorithm; unsafe evidence yields abstention or a qualified range rather than
  an invented mileage value.