# Work Centre

- **Mockup route:** `pegasus_work_centre_v30.html` in [`../../current/`](../../current/README.md); `?state=default|mine|filtered|empty|unavailable|assign`, `?layer=baseline|proposal`, `?opt=metrics:toned`
- **Live source:** `src/Pegasus.Web/Pages/Index.cshtml`, `_WorkCentreBody.cshtml`, `Index.cshtml.cs`, `Presentation/NeedsAttentionPresentation.cs`, `wwwroot/css/work-centre.css`, `wwwroot/js/work-centre.js`
- **Dialogs:** [`dialogs/`](dialogs/README.md) · **States:** [`states/`](states/README.md) · **Panels:** [`panels/`](panels/README.md)

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

Not captured in this pass. `shoot.ps1` names them `work-centre-<state>-<width>.png`.

## Notes

The v29 round's [Work Centre planning folder](../../../v29_planning/pages/work-centre/README.md)
records the Triages metric and the Triage kind; both are live now and this
round starts from them.

## Three designs — 25 September 2026

Open the [comparison](../../current/pegasus_work_centre_designs_v30.html),
[A — Priority desk](../../current/pegasus_work_centre_a_v30.html),
[B — Office ledger](../../current/pegasus_work_centre_b_v30.html), or
[C — Due-date board](../../current/pegasus_work_centre_c_v30.html).
The [proposals](../../current/work-centre-design-proposals.md) describe the
functional changes and open decisions WA–WF. WG is settled: empty groups
and sections are hidden, as the operator requested.

The earlier “not captured” note above describes the prior walkthrough pass.
This pass has 108 state captures and three full-page captures. The
[self-check](../../current/v30-work-centre-selfcheck.html) passed 1,905 checks;
actual keyboard traversal, dialog containment, Escape/focus return and
section tabs passed too. No console errors or external requests occurred.
See the [record](../../current/v30-work-centre-shots/verification.json).

### Screenshot inventory

| State | A · 1580 / 1440 / 760 | B · 1580 / 1440 / 760 | C · 1580 / 1440 / 760 |
| --- | --- | --- | --- |
| default | [1580](../../current/v30-work-centre-shots/01-a-default-1580.png) / [1440](../../current/v30-work-centre-shots/01-a-default-1440.png) / [760](../../current/v30-work-centre-shots/01-a-default-760.png) | [1580](../../current/v30-work-centre-shots/02-b-default-1580.png) / [1440](../../current/v30-work-centre-shots/02-b-default-1440.png) / [760](../../current/v30-work-centre-shots/02-b-default-760.png) | [1580](../../current/v30-work-centre-shots/03-c-default-1580.png) / [1440](../../current/v30-work-centre-shots/03-c-default-1440.png) / [760](../../current/v30-work-centre-shots/03-c-default-760.png) |
| mine | [1580](../../current/v30-work-centre-shots/04-a-mine-1580.png) / [1440](../../current/v30-work-centre-shots/04-a-mine-1440.png) / [760](../../current/v30-work-centre-shots/04-a-mine-760.png) | [1580](../../current/v30-work-centre-shots/05-b-mine-1580.png) / [1440](../../current/v30-work-centre-shots/05-b-mine-1440.png) / [760](../../current/v30-work-centre-shots/05-b-mine-760.png) | [1580](../../current/v30-work-centre-shots/06-c-mine-1580.png) / [1440](../../current/v30-work-centre-shots/06-c-mine-1440.png) / [760](../../current/v30-work-centre-shots/06-c-mine-760.png) |
| filtered | [1580](../../current/v30-work-centre-shots/07-a-filtered-1580.png) / [1440](../../current/v30-work-centre-shots/07-a-filtered-1440.png) / [760](../../current/v30-work-centre-shots/07-a-filtered-760.png) | [1580](../../current/v30-work-centre-shots/08-b-filtered-1580.png) / [1440](../../current/v30-work-centre-shots/08-b-filtered-1440.png) / [760](../../current/v30-work-centre-shots/08-b-filtered-760.png) | [1580](../../current/v30-work-centre-shots/09-c-filtered-1580.png) / [1440](../../current/v30-work-centre-shots/09-c-filtered-1440.png) / [760](../../current/v30-work-centre-shots/09-c-filtered-760.png) |
| empty | [1580](../../current/v30-work-centre-shots/10-a-empty-1580.png) / [1440](../../current/v30-work-centre-shots/10-a-empty-1440.png) / [760](../../current/v30-work-centre-shots/10-a-empty-760.png) | [1580](../../current/v30-work-centre-shots/11-b-empty-1580.png) / [1440](../../current/v30-work-centre-shots/11-b-empty-1440.png) / [760](../../current/v30-work-centre-shots/11-b-empty-760.png) | [1580](../../current/v30-work-centre-shots/12-c-empty-1580.png) / [1440](../../current/v30-work-centre-shots/12-c-empty-1440.png) / [760](../../current/v30-work-centre-shots/12-c-empty-760.png) |
| stale | [1580](../../current/v30-work-centre-shots/13-a-stale-1580.png) / [1440](../../current/v30-work-centre-shots/13-a-stale-1440.png) / [760](../../current/v30-work-centre-shots/13-a-stale-760.png) | [1580](../../current/v30-work-centre-shots/14-b-stale-1580.png) / [1440](../../current/v30-work-centre-shots/14-b-stale-1440.png) / [760](../../current/v30-work-centre-shots/14-b-stale-760.png) | [1580](../../current/v30-work-centre-shots/15-c-stale-1580.png) / [1440](../../current/v30-work-centre-shots/15-c-stale-1440.png) / [760](../../current/v30-work-centre-shots/15-c-stale-760.png) |
| partial | [1580](../../current/v30-work-centre-shots/16-a-partial-1580.png) / [1440](../../current/v30-work-centre-shots/16-a-partial-1440.png) / [760](../../current/v30-work-centre-shots/16-a-partial-760.png) | [1580](../../current/v30-work-centre-shots/17-b-partial-1580.png) / [1440](../../current/v30-work-centre-shots/17-b-partial-1440.png) / [760](../../current/v30-work-centre-shots/17-b-partial-760.png) | [1580](../../current/v30-work-centre-shots/18-c-partial-1580.png) / [1440](../../current/v30-work-centre-shots/18-c-partial-1440.png) / [760](../../current/v30-work-centre-shots/18-c-partial-760.png) |
| unavailable | [1580](../../current/v30-work-centre-shots/19-a-unavailable-1580.png) / [1440](../../current/v30-work-centre-shots/19-a-unavailable-1440.png) / [760](../../current/v30-work-centre-shots/19-a-unavailable-760.png) | [1580](../../current/v30-work-centre-shots/20-b-unavailable-1580.png) / [1440](../../current/v30-work-centre-shots/20-b-unavailable-1440.png) / [760](../../current/v30-work-centre-shots/20-b-unavailable-760.png) | [1580](../../current/v30-work-centre-shots/21-c-unavailable-1580.png) / [1440](../../current/v30-work-centre-shots/21-c-unavailable-1440.png) / [760](../../current/v30-work-centre-shots/21-c-unavailable-760.png) |
| assign | [1580](../../current/v30-work-centre-shots/22-a-assign-1580.png) / [1440](../../current/v30-work-centre-shots/22-a-assign-1440.png) / [760](../../current/v30-work-centre-shots/22-a-assign-760.png) | [1580](../../current/v30-work-centre-shots/23-b-assign-1580.png) / [1440](../../current/v30-work-centre-shots/23-b-assign-1440.png) / [760](../../current/v30-work-centre-shots/23-b-assign-760.png) | [1580](../../current/v30-work-centre-shots/24-c-assign-1580.png) / [1440](../../current/v30-work-centre-shots/24-c-assign-1440.png) / [760](../../current/v30-work-centre-shots/24-c-assign-760.png) |
| conflict | [1580](../../current/v30-work-centre-shots/25-a-conflict-1580.png) / [1440](../../current/v30-work-centre-shots/25-a-conflict-1440.png) / [760](../../current/v30-work-centre-shots/25-a-conflict-760.png) | [1580](../../current/v30-work-centre-shots/26-b-conflict-1580.png) / [1440](../../current/v30-work-centre-shots/26-b-conflict-1440.png) / [760](../../current/v30-work-centre-shots/26-b-conflict-760.png) | [1580](../../current/v30-work-centre-shots/27-c-conflict-1580.png) / [1440](../../current/v30-work-centre-shots/27-c-conflict-1440.png) / [760](../../current/v30-work-centre-shots/27-c-conflict-760.png) |
| new-cases | [1580](../../current/v30-work-centre-shots/28-a-new-cases-1580.png) / [1440](../../current/v30-work-centre-shots/28-a-new-cases-1440.png) / [760](../../current/v30-work-centre-shots/28-a-new-cases-760.png) | [1580](../../current/v30-work-centre-shots/29-b-new-cases-1580.png) / [1440](../../current/v30-work-centre-shots/29-b-new-cases-1440.png) / [760](../../current/v30-work-centre-shots/29-b-new-cases-760.png) | [1580](../../current/v30-work-centre-shots/30-c-new-cases-1580.png) / [1440](../../current/v30-work-centre-shots/30-c-new-cases-1440.png) / [760](../../current/v30-work-centre-shots/30-c-new-cases-760.png) |
| ai-jobs | [1580](../../current/v30-work-centre-shots/31-a-ai-jobs-1580.png) / [1440](../../current/v30-work-centre-shots/31-a-ai-jobs-1440.png) / [760](../../current/v30-work-centre-shots/31-a-ai-jobs-760.png) | [1580](../../current/v30-work-centre-shots/32-b-ai-jobs-1580.png) / [1440](../../current/v30-work-centre-shots/32-b-ai-jobs-1440.png) / [760](../../current/v30-work-centre-shots/32-b-ai-jobs-760.png) | [1580](../../current/v30-work-centre-shots/33-c-ai-jobs-1580.png) / [1440](../../current/v30-work-centre-shots/33-c-ai-jobs-1440.png) / [760](../../current/v30-work-centre-shots/33-c-ai-jobs-760.png) |
| quiet | [1580](../../current/v30-work-centre-shots/34-a-quiet-1580.png) / [1440](../../current/v30-work-centre-shots/34-a-quiet-1440.png) / [760](../../current/v30-work-centre-shots/34-a-quiet-760.png) | [1580](../../current/v30-work-centre-shots/35-b-quiet-1580.png) / [1440](../../current/v30-work-centre-shots/35-b-quiet-1440.png) / [760](../../current/v30-work-centre-shots/35-b-quiet-760.png) | [1580](../../current/v30-work-centre-shots/36-c-quiet-1580.png) / [1440](../../current/v30-work-centre-shots/36-c-quiet-1440.png) / [760](../../current/v30-work-centre-shots/36-c-quiet-760.png) |

Full-page views: [A](../../current/v30-work-centre-shots/full-a-1580.png),
[B](../../current/v30-work-centre-shots/full-b-1580.png),
[C](../../current/v30-work-centre-shots/full-c-1580.png).

These are offline mockup captures, not evidence of application execution.
