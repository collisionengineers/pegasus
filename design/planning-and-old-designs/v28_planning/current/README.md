# v28 current

A temporary design review artifact created at operator request on
18 September 2026, under the [documentation index](../../../../docs/index.md)
carve-out. Not application code, not design authority, and not implementation
evidence. A captured page proves what the application rendered for synthetic
fixture data on a local host, not that any behaviour is deployed or accepted.

The baseline is an as-is capture of `origin/dev` (`904903fd1`, 18 September
2026). The operator's proposals sit over it as a layer that can be switched
off; they are listed in `v28-notes.md` section 9 and recorded as they are made
in [`working-log.md`](working-log.md).

## How it is made

Nothing here is transcribed. `v28-build/capture.mjs` drives a locally running
Pegasus.Web, seeded with synthetic data, and saves the server's own HTML for
each state into `states/`. The live stylesheets, scripts, fonts and images are
copied into `assets/` and referenced relatively, so everything works offline
from the file system. The application's own JavaScript runs in every page:
dialogs, menus, the rail, the open-records strip, section navigation, the
Tabs layout and the damage plan behave as they do live.

The one addition to each page is `assets/mock/shim.js`. There is no server
behind the files, so the shim turns navigation to a live route into
navigation to the captured state for that route, and says so plainly when a
route or a post was not captured. It changes nothing on the page.

## How to open

Open any `pegasus_*_v28.html` file directly in a browser. Each is a frame:
the bar at the top is demo control, not product UI. It picks the area, the
state and a width (Fit, 1580, 1440, 760), switches between **Proposals** and
**Baseline**, shows the live route the state came from, and can open the state
page on its own. Indented entries under a state
are presets, such as a dialog opened or a section brought into view.

Links and GET forms inside a state move between captured states. A control
that posts to the server shows a short note naming the route instead.

## Files

| File | Area | States |
| --- | --- | --- |
| `pegasus_work_centre_v28.html` | Work Centre and the shell | 7 |
| `pegasus_cases_index_v28.html` | Cases and Create Case | 9 |
| `pegasus_case_record_v28.html` | Case record, edit session, new estimate, EVA send | 4, with 23 presets |
| `pegasus_triage_unidentified_v28.html` | Triage and Unidentified | 9 |
| `pegasus_image_intake_v28.html` | Image intake | 3 |
| `pegasus_mail_upload_v28.html` | Inbox, Message, Compose, Upload | 9 |
| `pegasus_search_operations_v28.html` | Search and Operations | 5 |
| `pegasus_administration_v28.html` | Administration | 16 |
| `pegasus_account_shell_v28.html` | Sign in, account, errors | 11 |

- `states/`: one offline page per captured state (73).
- `assets/`: the live CSS, JS, fonts and images as served, plus `mock/shim.js`,
  the generated `mock/routes.js`, and the proposals layer: `mock/proposals.js`
  and `.css` (P1 to P11), `mock/proposals-record.js` and `.css` (the Case record
  features, P12 to P36), `mock/proposals-record-2.js` and `.css` (the fifth
  pass, P37 to P51; P49 dropped), and the refined mark.
- `v28-build/`: the tooling, kept so the round is reproducible. `manifest.json`
  lists the states, `enrich.mjs` works the seeded Case through the live edit
  session, `capture.mjs` saves the pages, `frames.mjs` writes the family files,
  `shoot.mjs` takes the screenshots, `pagedocs.mjs` writes the page READMEs and
  `selfcheck.mjs` checks the result. `v28-notes.md` section 8 gives the
  commands.
- `v28-shots/`: every state and preset at 1580x1000, 1440x900 and 760x1000.
  `s..` files are the baseline; `p..` files show a proposal and are listed in
  `v28-build/proposal-shots.json` with what each one shows.

## Evidence

`v28-build/selfcheck.mjs` has two parts. Offline, every state page must load
with no console error, carry the banner and the shim, reference no
server-absolute asset and hold no fixture credential; every preset must find
its target; every family file must name only captured states. With the local
host running, every state that is addressable by URL alone is compared with
the running application: each element's tag and classes with its two
ancestors, and every heading, label, column header, term, option and button
text, must be equal in both directions. The result is in `v28-notes.md`
section 7.

Neither part is application evidence.

## Status

Stage 1, in collaboration. The capture's sign-off list is `v28-notes.md`
section 6; the proposals and what is open under them are section 9.
