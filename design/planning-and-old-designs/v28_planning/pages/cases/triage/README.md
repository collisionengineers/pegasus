# Triage

- **Parent:** [Cases](../index/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Triage/Details.cshtml`, `src/Pegasus.Web/Pages/Triage/Details.cshtml.cs`, `src/Pegasus.Web/Pages/Cases/Index.cshtml`, `src/Pegasus.Web/wwwroot/css/triage.css`
- [**How it works**](how-it-works.md)

`/Triage` has no list of its own; it answers with a permanent redirect to `/Cases`, and the list is the Triage tab there.

## Captured states

Each state is the running application's own HTML for the route shown, saved with the live CSS and JS. Nothing in it is transcribed.

| State | Live route | Open | Screenshots |
| --- | --- | --- | --- |
| Cases, Triage tab | `/Cases?tab=triage` | [frame](../../../current/pegasus_triage_unidentified_v28.html#cases-triage) · [page](../../../current/states/cases-triage.html) | [1580](../../../current/v28-shots/s21-cases-triage-1580.png) · [1440](../../../current/v28-shots/s21-cases-triage-1440.png) · [760](../../../current/v28-shots/s21-cases-triage-760.png) |
| Cases, Triage tab, row selected | `/Cases?tab=triage&selected=b1cae944-6c1e-40f3-a0ce-1b2c79d85f1b` | [frame](../../../current/pegasus_triage_unidentified_v28.html#cases-triage-selected) · [page](../../../current/states/cases-triage-selected.html) | [1580](../../../current/v28-shots/s22-cases-triage-selected-1580.png) · [1440](../../../current/v28-shots/s22-cases-triage-selected-1440.png) · [760](../../../current/v28-shots/s22-cases-triage-selected-760.png) |
| Triage record | `/Triage/b1cae944-6c1e-40f3-a0ce-1b2c79d85f1b` | [frame](../../../current/pegasus_triage_unidentified_v28.html#triage-record) · [page](../../../current/states/triage-record.html) | [1580](../../../current/v28-shots/s23-triage-record-1580.png) · [1440](../../../current/v28-shots/s23-triage-record-1440.png) · [760](../../../current/v28-shots/s23-triage-record-760.png) |
| Assign dialog (on "Triage record") | `/Triage/b1cae944-6c1e-40f3-a0ce-1b2c79d85f1b` | [frame](../../../current/pegasus_triage_unidentified_v28.html#triage-record?dialog=triage-assign-dialog) | [1580](../../../current/v28-shots/s100-triage-assign-dialog-1580.png) · [1440](../../../current/v28-shots/s100-triage-assign-dialog-1440.png) · [760](../../../current/v28-shots/s100-triage-assign-dialog-760.png) |
| Await information dialog (on "Triage record") | `/Triage/b1cae944-6c1e-40f3-a0ce-1b2c79d85f1b` | [frame](../../../current/pegasus_triage_unidentified_v28.html#triage-record?dialog=triage-await-dialog) | [1580](../../../current/v28-shots/s101-triage-await-dialog-1580.png) · [1440](../../../current/v28-shots/s101-triage-await-dialog-1440.png) · [760](../../../current/v28-shots/s101-triage-await-dialog-760.png) |
| Cancel Triage dialog (on "Triage record") | `/Triage/b1cae944-6c1e-40f3-a0ce-1b2c79d85f1b` | [frame](../../../current/pegasus_triage_unidentified_v28.html#triage-record?dialog=triage-cancel-dialog) | [1580](../../../current/v28-shots/s102-triage-cancel-dialog-1580.png) · [1440](../../../current/v28-shots/s102-triage-cancel-dialog-1440.png) · [760](../../../current/v28-shots/s102-triage-cancel-dialog-760.png) |
| Link Case dialog (on "Triage record") | `/Triage/b1cae944-6c1e-40f3-a0ce-1b2c79d85f1b` | [frame](../../../current/pegasus_triage_unidentified_v28.html#triage-record?dialog=triage-link-case-dialog) | [1580](../../../current/v28-shots/s103-triage-link-case-dialog-1580.png) · [1440](../../../current/v28-shots/s103-triage-link-case-dialog-1440.png) · [760](../../../current/v28-shots/s103-triage-link-case-dialog-760.png) |
| Principal dialog (on "Triage record") | `/Triage/b1cae944-6c1e-40f3-a0ce-1b2c79d85f1b` | [frame](../../../current/pegasus_triage_unidentified_v28.html#triage-record?dialog=triage-principal-dialog) | [1580](../../../current/v28-shots/s104-triage-principal-dialog-1580.png) · [1440](../../../current/v28-shots/s104-triage-principal-dialog-1440.png) · [760](../../../current/v28-shots/s104-triage-principal-dialog-760.png) |

## Not captured

- A Triage with a recorded finding, one awaiting information, one with a linked Case (Unlink case), and the chaser form with attachments.
- The edit session on a Triage, and a colleague holding it.
