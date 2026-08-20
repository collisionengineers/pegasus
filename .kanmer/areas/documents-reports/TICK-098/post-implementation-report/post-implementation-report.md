# Post-implementation report — TICK-098

## Summary

Corrected the RPT-03 governing contract: a future Audit report reuses the approved Inspection physical report and carries only Audit provenance plus the existing immutable Audit reference. The current Audit renderer surface remains closed; this documentation-only change neither adds a caller nor claims activation.

## Changes

| File | Change | Why |
|---|---|---|
| `docs/capabilities.md` | Replaced the false conservative/maximised-specification and uplift RPT-03 entry with Inspection-output parity and Audit-reference provenance. | Aligns the capability registry with the operator correction. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Added the normative **Audit report parity** behaviour, fail-closed evidence rules, and explicit prohibition on a second presentation family or uplift. | Gives the future caller one Core-owned shared-report contract while preserving the existing closed activation boundary. |

## Governing docs

- **FRD-11:** now states that an Audit uses the equivalent Inspection report's approved contract, template, wording, layout, and renderer presentation, while missing or ambiguous Audit evidence fails closed.
- **ADR-0025:** unchanged. The work maintains its integration boundary: no new renderer, service, template family, or deployment unit was introduced.

No ADR was needed: this corrects required behaviour in its FRD and capability registry without choosing a new technical mechanism.

## Risks / follow-ups

RPT-03 remains **Later** and unavailable in the active renderer. An implementation ticket must have an accepted caller and activation evidence before it adds any Audit render path. This change deliberately does not alter the existing RPT-02 output or its closed readiness gate.

## Verification hand-off

On merged `main`:

1. Run `git diff --check HEAD~1 HEAD` — expected: no whitespace errors.
2. Confirm the RPT-03 row links to `#audit-report-parity` and says no dual-specification or uplift.
3. Confirm FRD-11's **Initial renderer activation** still lists Audit as unavailable, so this PR has not claimed a closed feature as delivered.
