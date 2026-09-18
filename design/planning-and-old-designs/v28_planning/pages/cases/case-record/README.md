# Case record

- **Parent:** [Cases](../index/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Cases/Details.cshtml`, `src/Pegasus.Web/Pages/Cases/Details*.cs`, `src/Pegasus.Web/Pages/Cases/Shared/*.cshtml`, `src/Pegasus.Web/wwwroot/js/case-workspace.js`, `src/Pegasus.Web/wwwroot/css/case-workspace.css`
- [**How it works**](how-it-works.md)
- [States](states/README.md)
- [Panels (sections)](panels/README.md)
- [Dialogs](dialogs/README.md)
- [EVA handoff](eva-handoff/README.md)
- [Create audit](../create-audit/README.md)

The record is one Case worked through the application's own edit session by `v28-build/enrich.mjs`: overview notes, repairer and storage, vehicle details, tyres and belts, two recorded damage zones, one Glass's guide entered by hand, one hand-entered estimate, a repairable outcome and an Engineer's comment. Sections that load as the reader approaches them live are mounted in the saved page the same way `case-workspace.js` mounts them.

## Captured states

Each state is the running application's own HTML for the route shown, saved with the live CSS and JS. Nothing in it is transcribed.

| State | Live route | Open | Screenshots |
| --- | --- | --- | --- |
| Read | `/Cases/f3dbeb86-4ab8-4d5b-ade3-dea35c995b1f` | [frame](../../../current/pegasus_case_record_v28.html#case-record) · [page](../../../current/states/case-record.html) | [1580](../../../current/v28-shots/s17-case-record-1580.png) · [1440](../../../current/v28-shots/s17-case-record-1440.png) · [760](../../../current/v28-shots/s17-case-record-760.png) |
| Page-wide edit session | `/Cases/f3dbeb86-4ab8-4d5b-ade3-dea35c995b1f` | [frame](../../../current/pegasus_case_record_v28.html#case-record-editing) · [page](../../../current/states/case-record-editing.html) | [1580](../../../current/v28-shots/s18-case-record-editing-1580.png) · [1440](../../../current/v28-shots/s18-case-record-editing-1440.png) · [760](../../../current/v28-shots/s18-case-record-editing-760.png) |
| Edit session, new estimate being entered | `/Cases/f3dbeb86-4ab8-4d5b-ade3-dea35c995b1f?section=estimate&estimate=new` | [frame](../../../current/pegasus_case_record_v28.html#case-record-new-estimate) · [page](../../../current/states/case-record-new-estimate.html) | [1580](../../../current/v28-shots/s19-case-record-new-estimate-1580.png) · [1440](../../../current/v28-shots/s19-case-record-new-estimate-1440.png) · [760](../../../current/v28-shots/s19-case-record-new-estimate-760.png) |
| EVA send | `/Cases/f3dbeb86-4ab8-4d5b-ade3-dea35c995b1f/Eva/Send` | [frame](../../../current/pegasus_case_record_v28.html#case-record-eva-send) · [page](../../../current/states/case-record-eva-send.html) | [1580](../../../current/v28-shots/s20-case-record-eva-send-1580.png) · [1440](../../../current/v28-shots/s20-case-record-eva-send-1440.png) · [760](../../../current/v28-shots/s20-case-record-eva-send-760.png) |
| Tabs layout (on "Read") | `/Cases/f3dbeb86-4ab8-4d5b-ade3-dea35c995b1f` | [frame](../../../current/pegasus_case_record_v28.html#case-record?click=%5Bdata-case-layout%3D%22tabs%22%5D) | [1580](../../../current/v28-shots/s77-case-record-tabs-layout-1580.png) · [1440](../../../current/v28-shots/s77-case-record-tabs-layout-1440.png) · [760](../../../current/v28-shots/s77-case-record-tabs-layout-760.png) |

## Not captured

- Review, Not ready, Held, Query and closed Cases; the fixture Case is With Engineer.
- A colleague holding the edit session, Take over, and the refused-save panel ("Your change was not applied").
- An applied Engineer's Value, a generated report or fee note, prepared delivery, and Case documents. Report generation needs sign-off data the fixture lacks.
- An imported or Glass's-sourced estimate, Send to AI, and estimate comparison.
- The save-before-finishing dialog, which the live script raises only when the form is dirty. Edit a field in the edit-session state and press Cancel to see it.
