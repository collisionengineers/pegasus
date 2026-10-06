# Case record: Valuation

- Mockup route: `current/pegasus_valuation_{a,b,c,d,e}_v33.html` (today's section is `pegasus_valuation_live_v33.html`).
- Live source: `src/Pegasus.Web/Pages/Cases/Shared/_CaseValuation.cshtml`, `_CaseValuationLines.cshtml`, `_CaseValuationOpening.cshtml`, `Pages/Cases/Details.Valuation.cs`, the `#section-valuation` block of `wwwroot/css/case-workspace.css`, and the valuation block of `wwwroot/js/case-workspace.js`.
- [How it works today](how-it-works.md) · [How it should work](how-it-should-work.md)

## Dialogs

None of its own. Report a problem on a source's notice opens the shared problem dialog, which this round does not change.

## States

The eleven presets in the mockup strip: editing with Glass's fetched and nothing recorded (the operator's screenshots), the same while reading, editing and reading with a calculation recorded, the Engineer's own figure typed, a calculation that cannot be worked out, a VAT-registered claimant, AI market research in progress, the Inspection view, and nothing recorded while editing and while reading.

## Panels

The three values; the sources (five guide sources and AI market research); the calculation (previous total loss, condition deduction, commercial VAT, value increases); the recorded calculation; On the report.

## Screenshots

`current/v33-valuation-shots/NN-<design>-<state>-<width>.png`: `00`–`17` for three states of today's section and of each design at 1580, 1440 and 760; `18`–`37` for four more states of each design at 1580. Each is the whole section. The count and the section heights are in `verification.json`.

`current/v33-conformance/<state>-<width>.png`: the implemented section in seven states at 1580, 1440 and 760, for comparison with design D's shots.

## Notes

See [v33-notes.md](../../../current/v33-notes.md).
