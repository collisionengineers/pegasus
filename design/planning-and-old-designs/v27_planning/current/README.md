# v27 mockups (current version)

A temporary design review artifact, created at explicit operator request per
[`docs/index.md`](../../../../docs/index.md)'s carve-out for such material. It is not
application code, not design authority, and not implementation evidence. **Remove
this folder once the v27 Stage 2 (the Razor implementation) lands, or once the design
here is formally accepted or rejected.**

## What this is

`pegasus_case_record_v27.html` is a self-contained, offline mockup of the Pegasus Case
record **exactly as it is on `origin/dev` today** (commit `5765a527a`, 16 September
2026). It is the baseline for the v27 round: the operator asked for "a mockup of the
existing Pegasus UI" before any change is proposed, so that every v27 proposal can be
read as a diff against it.

It is built from the live application, not redrawn:

- `site.css` and `case-workspace.css` are inlined verbatim, with the vendored Inter
  Variable font embedded, so the tokens, geometry and type are the live ones.
- The markup is a hand transcription of `Pages/Cases/Details.cshtml`, every
  `Shared/_Case*.cshtml` partial, `_CaseDialogs.cshtml`, `_Layout.cshtml`,
  `_ShellDialogs.cshtml` and `_ReasonDialog.cshtml`, keeping their classes, `data-*`
  hooks and label strings (`CaseWorkspaceLabels`, `OperatorLabels`).
- The damage diagram uses `DamagePlanGeometry` verbatim.
- The rules that decide what each state offers (edit lease, roles, lifecycle, Glass's
  session, report generation, custody, proposals) are restated one line each from
  `Details.cshtml` and `Details.Frame.cs`; `v27-notes.md` § Live rules lists them with
  their source symbols.

Open the HTML file directly in any browser — no server, build or network access needed.
The dark strip at the bottom left ("Mockup controls") is a demo control, not product UI:
it switches the Case's lifecycle state, edit lease, role, layout, rail, data, case type,
Glass's session, decisions proposal, report generation, AI draft, custody, lookup and EVA
policy, opens any dialog by name, jumps to a section and opens the image viewer. Every
strip state is also reachable by query string (see `v27-notes.md` § Query presets).

The baseline itself proposes nothing; where it departs from live it is a fixture or a
limit of the medium, listed in `v27-notes.md` § Known limits. The proposals ride on it as
strip variables: **Damage selector**, four click-anywhere ways to mark damage that
record the eight vehicle areas rather than panels (`v27-notes.md` § 11–12, sign-off
G–G7, `?clicker=pins|brush|area|arrow`); **Logo**, the refined mark in the rail (§ 13,
sign-off H, `?logo=refined|live`); and the **Proposals** row, the twenty-three reference-file
features (§ 15, sign-off I–I3, `?proposals=all|none` or
`?p=composed,cap,…`). The file opens with the proposals on; `?proposals=none` is the
baseline, and every baseline screenshot and self-check block is taken that way.

## Files

| File | Purpose |
| --- | --- |
| `pegasus_case_record_v27.html` | The baseline mockup of the live Case record. Built, not hand-edited: regenerate it with `v27-build/build.py`. |
| `v27-selfcheck.html` | Scripted check that drives every state × layout × section, the edit session, every dialog, the viewer, the damage clicker, the valuation calculator, the estimate tabs and the query presets; prints one `RESULT` line. See `v27-notes.md` § Self-check. |
| `v27-notes.md` | What the baseline mirrors and how it was verified, the frame numbers, the coverage audit, the deliberate departures, the lettered sign-off list, the self-check result and the known limits. |
| `discussion-log.md` | Chronological record of the request and each round of operator feedback. |
| `v27-shots/` | Headless-Chromium screenshots of every state (lifecycle, lease, roles, Glass's, report, custody, dialogs, viewer) at 1580×1000, plus 1440×900 and 760×1000 captures and one whole-page scroll. Listed on the [Case record page README](../pages/cases/case-record/README.md). |
| `v27-build/` | The build inputs and drivers: `case-record.src.html` (markup with placeholders), `case-record.js` (mock behaviour), `proposals.js` and `wording.js` (the § 15 proposals, spliced in by the build), `build.py` (inlines the live CSS, font, sprite and brand mark and writes the mockup), `shoot.mjs` and `shots.json` (screenshots over the DevTools protocol), `selfcheck.mjs` (runs the self-check and prints its result), `audit.py` (the coverage audit of live label strings). No npm packages; the Playwright Chromium build on this workstation is the browser. |

The operator's reference file for this round, `pegasus_case_dashboard_2026-09-15.html`,
sits one level up with its screenshots; the previous version of the Case record mockup
is [`../../v26_planning/current/pegasus_case_workspace_v26.html`](../../v26_planning/current/pegasus_case_workspace_v26.html).

## How to rebuild and re-verify

From the repository root, in PowerShell 7 or Git Bash:

```text
python design/planning-and-old-designs/v27_planning/current/v27-build/build.py
node   design/planning-and-old-designs/v27_planning/current/v27-build/selfcheck.mjs
node   design/planning-and-old-designs/v27_planning/current/v27-build/shoot.mjs design/planning-and-old-designs/v27_planning/current/v27-build/shots.json
python design/planning-and-old-designs/v27_planning/current/v27-build/audit.py
```

The build reads the live CSS, font, sprite and brand mark from the checkout, so it
should be run on `origin/dev` (the checkout was at `5765a527a`, equal to `origin/dev`,
when this baseline was built).

## Status

Items A–F in `v27-notes.md` § 7 ask the operator to confirm the baseline's fidelity and
fixtures, the scope of the shell and the placement of the round. Items G–G7 in § 11–12 ask
which of the four damage selector variants (if any) carries into Stage 2 and settle the
area wording; item H in § 13 is the refined mark; items I–I3 in § 15 are the twenty-three
reference proposals. Nothing is approved yet.
