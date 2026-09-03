# Open questions — ENG-035 (2026-09-02)

- [x] Derived `impact_severity` for two or more damaged zones, and which
  codes are canonical? Resolved 2026-09-03 by the controller: derived
  severity = the highest zone severity; Core's existing codes
  (`light_to_moderate` / `moderate_to_heavy`) are canonical and the mockup
  ids map onto them (one list per concept).
- [x] Is the mockup equity formula binding, and does excess contribute?
  Resolved 2026-09-03 by the controller: binding —
  equity = Engineer's value − (repair cost − betterment) − salvage value;
  excess is shown as its own field and is not part of equity (D41).

Amendment (operator, 2026-09-03, D45): a damage zone records zone, severity
and note only — there is no damage type field, no type label list and no
type column on the report. D39 is amended accordingly; drop `type` from the
`damage[]` record and the projection.

## Parked (explicitly deferred)

None.
