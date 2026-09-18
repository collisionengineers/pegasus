# v28 current

A temporary design review artifact created at operator request on
18 September 2026, under the [documentation index](../../../../docs/index.md)
carve-out. Not application code, not design authority, and not implementation
evidence — a screenshot or a rendered mockup state proves only that this
static file renders as shown, not that any behaviour is deployed or accepted.

**This round proposes nothing.** Every file below is a faithful, as-is
capture of the live application on `origin/dev` (`6c02a8608`, 18 September
2026): real labels (via `OperatorLabels`), real CSS (`site.css` plus each
page's own live stylesheet), real Lucide glyphs, synthetic fixture data.
Where the live UX itself is awkward or internally inconsistent, that is
captured as found — see `v28-notes.md` §6 (deliberate departures) and §7
(the sign-off list, which is expected to hold only genuine *capture*
ambiguities, never design opinions, for this round).

## How to open

Open any `pegasus_*_v28.html` file directly in a browser — no server, build
step or network access required. The "Mockup controls" strip at the bottom
left is demo control, not product UI: it drives every state via
`<select>`/checkbox controls, each mirrored as a query-string preset (see
`v28-notes.md` §4 for the frame numbers and `v28-build/pages/*.strip.html`
for the exact per-page keys).

## Files

| File | Live pages captured |
| --- | --- |
| `pegasus_work_centre_v28.html` | `Pages/Index.cshtml` (Work Centre) |
| `pegasus_cases_index_v28.html` | `Pages/Cases/Index.cshtml`, `Cases/Create.cshtml` |
| `pegasus_case_record_v28.html` | `Pages/Cases/Details.cshtml` + every `Shared/_Case*.cshtml` partial, `Cases/Eva/Send.cshtml`, and the `Cases/Vehicle.cshtml`/`Workflow.cshtml`/`Tasks.cshtml`/`Closure.cshtml`/`Custody.cshtml`/`Assessment/*` family |
| `pegasus_triage_unidentified_v28.html` | `Pages/Triage/Index.cshtml`, `Triage/Details.cshtml`, `Unidentified/Index.cshtml`, `Unidentified/Details.cshtml` |
| `pegasus_image_intake_v28.html` | `Pages/ImageIntake/Details.cshtml`, `PreCaseImages/Index.cshtml`, `Intake/Source.cshtml`, `Intake/Asset.cshtml`, `Intake/Image.cshtml` |
| `pegasus_mail_upload_v28.html` | `Pages/Mail/Index.cshtml`, `Mail/Message.cshtml`, `Mail/Compose.cshtml`, `Upload.cshtml`, `UploadStatus.cshtml`, `UploadGroupStatus.cshtml` |
| `pegasus_search_operations_v28.html` | `Pages/Search/Index.cshtml`, `Operations/Index.cshtml` |
| `pegasus_administration_v28.html` | `Pages/Administration/**` (13 sub-areas) |
| `pegasus_account_shell_v28.html` | `Pages/Account/*`, `Connect/Authorize.cshtml`, `Error.cshtml`, `StatusCode.cshtml` — the navless `_LayoutAuth` family |

`v28-build/` holds the source this round is built from (`build.py`,
`shell-chrome.html`, `navless-chrome.html`, `mock-engine.js`,
`pages/*.body.html` / `*.js` / `*.strip.html`, `smoke.mjs`, `shoot.mjs`,
`selfcheck-runner.mjs`) — not itself part of the mockup, kept so the round is
reproducible and reviewable.

## Evidence

- `v28-selfcheck.html`, driven by `v28-build/selfcheck-runner.mjs`, loads
  every file above through its query-string presets and asserts each loads,
  exposes `window.MOCK`, opens its shell dialogs, and raises zero console
  errors. Result recorded in `v28-notes.md` §7.
- `v28-shots/` holds the 1580×1000 / 1440×900 / 760×1000 screenshots cited
  in each page's README and in `v28-notes.md` §2's file table.
- Neither is application evidence — see the evidence-discipline paragraph
  above.

## Status

240/240 self-check assertions pass; 144 screenshots taken clean. See
`v28-notes.md` §7 for the full sign-off list (A-K, eleven genuine capture
ambiguities, zero design decisions) and its current state — nothing is
approved yet; this Stage 1 round awaits operator review.
