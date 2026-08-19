# Research — Audit report parity

## Operator correction

The earlier RPT-03 premise was wrong. Audit reports are physically identical to Inspection reports. There is no Audit-only conservative/maximised comparison or uplift output.

## Verified reference rule

The existing case-identity authority already defines the only relevant internal distinction:

- every Audit consumes the normal principal/year Case/PO sequence;
- repairable derives `a.{Case/PO}`;
- total loss derives `ap.{Case/PO}`;
- missing, conflicting, or ambiguous Audit evidence fails closed;
- the reference is immutable after allocation.

## Implications

Reuse the approved Inspection report contract, template, wording, layout, and renderer. Audit workflow provenance and the applicable internal Audit reference remain typed Core data, but they do not create a different physical report. The stale RPT-03 capability/FRD wording must be reconciled by the implementation owner.
