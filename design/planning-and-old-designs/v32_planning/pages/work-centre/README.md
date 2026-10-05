# Work Centre

- Mockup route: `current/pegasus_work_centre_{a,b,c,d,e}_v32.html` (the live baseline is `pegasus_work_centre_live_v32.html`).
- Live source: `src/Pegasus.Web/Pages/Index.cshtml`, `Pages/_WorkCentreBody.cshtml`, `wwwroot/css/work-centre.css`, `wwwroot/js/work-centre.js`.
- [How it works today](how-it-works.md) · [How it should work](how-it-should-work.md)

## Dialogs

- Assign Engineer (`wc-assign-dialog`): the live dialog, opened from an Unassigned row's action; Engineer select, Assign to me, Cancel, Assign or Reassign.
- Mockup destination: demo only, shown when a link would leave the page.

## States

The fifteen presets in the mockup strip: default, Mine, Held + Review, Find BH17RZV, open row, QDOS26203 dismissed, nothing needs attention, nothing of mine, Needs attention unavailable, AI jobs unavailable, Activity unavailable, no work to show, Assign Engineer dialog, New cases, AI jobs.

## Panels

Header, metrics, Activity, Needs attention (toolbar, chips or Kind select, ledger, open row), New cases, AI jobs; design E's four lanes.

## Screenshots

`current/v32-work-centre-shots/NN-<design>-<state>-<width>.png` for eight states at 1580, 1440 and 760, `NN-<design>-dialog-assign-1440.png`, `full-<design>-1580.png`, and `00-live-default-<width>.png` for today's page. The list is in `verification.json`.

## Notes

See [v32-notes.md](../../current/v32-notes.md).
