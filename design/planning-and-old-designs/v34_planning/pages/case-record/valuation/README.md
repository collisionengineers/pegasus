# Case record: Valuation

- Mockup route: `current/pegasus_case_valuation_v34.html`, with `?state=` and `?opt=` presets.
- Live source: `src/Pegasus.Web/Pages/Cases/Shared/_CaseValuation.cshtml`, `_CaseValuationCalculation.cshtml`, `_CaseValuationOpening.cshtml`, `Pages/Cases/Details.Valuation.cs`, the `#section-valuation` block of `wwwroot/css/case-workspace.css`, and the valuation block of `wwwroot/js/case-workspace.js`.
- [How it works today](how-it-works.md) · [How it should work](how-it-should-work.md)
- [Dialogs](dialogs/README.md) · [States](states/README.md) · [Panels](panels/README.md)

## Screenshots

In `current/v34-shots/`. Each is the whole section unless noted.

| Shot | State | Widths |
| --- | --- | --- |
| `00-page-fetched` | The whole Case page around the section, editing | 1580 |
| `01-fetched` | Editing: Glass's fetched, the other guides typed, CAP chosen, nothing recorded | 1580, 1440, 760 |
| `02-fetched-read` | Reading the same: no card chosen, because nothing is recorded | 1580, 1440, 760 |
| `03-recorded` | Editing: a calculation recorded on CAP (Tow bar, −10 %, £200 deduction), earlier research | 1580, 1440, 760 |
| `04-recorded-read` | Reading the same | 1580, 1440, 760 |
| `05-own` | Editing: the Engineer's own figure typed | 1580 |
| `06-refused` | Editing: a calculation that cannot be worked out | 1580 |
| `07-claimant-vat` | Editing: the claimant is VAT registered | 1580 |
| `08-pending` | Editing: AI market research in progress | 1580 |
| `09-inspection` | Editing in the Inspection view | 1580 |
| `10-empty`, `11-empty-read` | Nothing recorded, editing and reading | 1580 |
| `12-fetched-word-basis` | Item B's other choice: "Basis" | 1580 |
| `13-fetched-manual-sentence` | Item C's other choice: the standing sentence | 1580 |

## Notes

See [v34-notes.md](../../../current/v34-notes.md).
