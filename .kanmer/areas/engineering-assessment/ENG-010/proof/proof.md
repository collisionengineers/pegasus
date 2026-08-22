# Proof — verified in production

**Shipped:** PR #505, commits `df6d3b66`, `e4ce8e3b` · **Deployed:** Release 17 (`71911734`), still live on Release 18 (`1f3be493`).

## Both defects proved fixed against real vehicles

Read from production `VehicleLookupObservations` on 2026-08-22, for the two cases that
arrived after the deploy:

```
Registration  Outcome  Make     Mileage  Unit   Method                  Ver  MotTestsJson
DF18FEJ       current  BMW      72312    Miles  latest-mot-observation  2    702 bytes
LG64JAU       current  RENAULT  128343   Miles  latest-mot-observation  2    1565 bytes
```

**Defect 1 — MOT tests were all being discarded.** `MotTestsJson` now holds 702 and 1565
bytes of parsed tests. Before this fix every vehicle recorded **zero** MOT observations and
no failure, because `DateOnly.TryParse` rejects the full instant DVSA actually sends. Two
vehicles, two populated histories, is that fixed.

**Defect 2 — kilometres passed through unconverted.** `MileageUnit` reads `Miles` on both,
and `MileageMethodVersion` is `2` — the version this ticket bumped. The derived value is
normalised at the boundary, as the operator directed.

## A cross-check worth recording

QDOS26010's report states 132,389 miles ([[INTK-028]] extracted it) while the MOT-derived
value is 128,343 miles. Consistent with each other, and the document fact correctly
outranks the external estimate rather than overwriting it — which is what the ticket
required of the evidence classification.

## Not claimed

The case-overview rendering of the derived value with its provenance label has not been
seen, because that needs an authenticated session. The value, its unit, its method and its
version are proved from the persisted observation.
