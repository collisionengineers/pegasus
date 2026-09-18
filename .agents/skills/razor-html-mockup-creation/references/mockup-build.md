# Building the mockup

Mechanics of the v26 round, generalised. Read before writing the first line of
HTML.

## One offline file per surface

- A self-contained HTML file: no server, build, network or font download. The
  Case record and the shell (every other rail destination, hash-routed) were two
  files that share tokens and hand the Case route to each other.
- Tokens, rail, utility bar, workspace tabs, Lucide glyphs and real geometry
  (the `DamageDiagramGeometry` SVG) are pulled from `origin/dev`, so the mockup
  is the live shell with the proposal inside it, not a new design system.
- Fixtures are synthetic (the v24 MA59BDY / QDOS26214 case). Images are
  generated scenes; there are no files behind a mockup.
- File names carry the version: `pegasus_case_workspace_vNN.html`,
  `pegasus_shell_vNN.html`, `vNN-selfcheck.html`, `vNN-shots/`. The previous
  version and the operator's reference file stay in the same `current/` folder.

## Mockup strip

A dark strip at the bottom left titled "Mockup controls", collapsible, and
stated in the README as demo control, not product UI.

| Row | Holds |
| --- | --- |
| Global | lifecycle state, role (Administrator, Engineer, User), data present or every list empty, freshness (Current, Partial, Stale, Unavailable), the page-level Unavailable notice, rail collapse |
| Per route | the states of the current page: Glass's session state, AI proposal, report state, lease, custody, lookup, Upload's Idle / Files chosen / Storing / Decided, Triage's states, and so on |
| Dialogs | open any dialog by name, plus the navless frame (sign-in) |
| Variables | each undecided design choice as a switch with its options and a Reset (v26: tab layout `place`, `label`, `icons`, `density`, `max`, `glyphs`, `close`, `wc`, `open`; damage clicker `plan`, `side`, `dial`) |

Every strip state is also reachable by query string (`state`, `edit=1`,
`section`, `glass`, `proposal=1`, `role`, `data`, `fresh`, `unavailable=1`,
`dialog`, `tabs=N`, `tabopt=key:value,...`, `viewer=N`) so a screenshot needs no
click, and the self-check can assert each preset.

## Persistence

Per-browser choices (rail collapse, collapsed sections, strip variables, the
working set of open records) live in `localStorage` keys namespaced by version
(`vNN.rail`, `vNN.tabopt`, `vNN.open`). Shared state is read by both files so a
record opened in the shell shows on the Case record's strip.

## Frame rules

State them as numbers in the notes and keep them:

- ribbon 56px, section row 40px, the sticky block measured at runtime, not assumed;
- 12-column content grid with 36px cells; aside 285px at 1441px and above,
  folded into a strip above the sections below that;
- 13.5px body text and 36px controls per the design authority;
- one primary plus one More menu per panel head, with any exception named;
- no control in a head that the section's state does not allow; availability
  stated once per section as a label, never a silently disabled control;
- read mode shows values, never empty inputs or unapplied options;
- read and edit share one geometry: value box and control box occupy the same
  cell, so entering edit never moves the page;
- no explanatory copy on the page; captions and labels only.

## Vocabulary

Labels, groupings and columns are exact strings from the live `.cshtml`
sources and `OperatorLabels`. When a page is assembled from prototype modules,
keep one recorded list of exact-string corrections applied by the build, each
asserted to match exactly once. CONTEXT.md overrides a live label that breaks
its rules.

## Self-check harness

`vNN-selfcheck.html` loads the mockup in a 1580×1000 iframe and drives it:

- a `check(name, cond)` collector, `window.error` capture, and a try/catch
  that records the exception;
- every lifecycle state × layout × section, the edit session, every dialog by
  name, every main flow, every strip variable, and every query-string preset;
- a `RESULT {"fail":[],"okCount":N}` line written after a short settle.

Skeleton: [assets/selfcheck-template.html](../assets/selfcheck-template.html).
Run it with file access allowed and read the RESULT line:

```powershell
& $chrome --headless=new --allow-file-access-from-files `
  --virtual-time-budget=30000 --dump-dom `
  design/planning-and-old-designs/vNN_planning/current/vNN-selfcheck.html |
  Select-String 'RESULT'
```

`$chrome` is the Playwright Chromium build. Zero fails and no console errors on
every rendered route is the bar; record the dated count in the notes. Every
lane that adds a page adds its checks.

## Screenshots

- Headless Chromium at 1580×1000, 1440×900 and 760×1000 (the design
  authority's capture rule), driven by the query-string presets.
- `NN-name.png` in `vNN-shots/`, `sNN-name.png` for shell routes, one
  whole-page scroll shot of the record.
- Each row of the notes' change table cites its shot numbers; each page README
  lists its shots.

## Known limits

The notes always end with what the mockup cannot show: font fallback, synthetic
images, a demo return that lands a fixed result, representative failure codes,
and any dialog still stubbed. A stub that opens an alert is a defect once the
live dialog exists.
