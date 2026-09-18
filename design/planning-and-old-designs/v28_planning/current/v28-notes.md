# v28 notes

As-is capture, no proposed changes. Rebuilt on 18 September 2026 from
`origin/dev` at `904903fd1` (worktree `task/ui-baseline-rebuild`).

## 1. Why this round was rebuilt

The first build of v28 was hand-transcribed from the Razor source into nine
self-contained files with a bespoke mock engine. The operator rejected it as
not faithful. A review against the running application, recorded in the
discussion log, found:

| Fault in the first build | What the rebuild does instead |
| --- | --- |
| Built from `6c02a8608`; PR 791 then removed the Administration edit leases and rewrote every settings page, so Administration was stale | Captured from `904903fd1`, and the self-check compares against the running application, so staleness fails the check |
| Edit-only controls drawn in read mode across the Case record | Read mode and the edit session are separate captures of what the server rendered for each |
| Page wrappers dropped and state-toggle `div`s inserted, so page-scoped CSS and `.stack` gaps did not apply | Markup is the server's; nothing is inserted into it |
| Invented vocabulary: damage zone names, tyre and belt codes, a vehicle body dropdown, custody chip words, the report title | All values come from the application |
| A placeholder box for the damage plan | The live SVG, with zones recorded through the live control |
| Whole surfaces missing, including the open-records strip | The live scripts run, so the strip, dialogs, menus and section navigation work |
| Wrong formats and tones (`T-2601`, `U-1140`, spaced registrations, chip colours) | Real formats: `T-00001`, `U1`, `AB12CDE` |
| Incoherent fixtures (an "empty" state with non-zero counts) | One database; every count agrees with every list |
| A self-check that tested the mockup against itself | A parity check against the running application |

## 2. Files and what they capture

Nine family files frame 73 captured state pages and 35 presets over them.
[`README.md`](README.md) has the table; each page folder under
[`../pages/`](../pages/README.md) lists its states with their live routes and
screenshots. `v28-build/captured.json` is the generated list.

## 3. How a state is captured

1. `v28-build/enrich.mjs` works the seeded Case through the application's own
   edit session in a real browser: fields are set in the live controls and
   saved by the live Save button. Nothing is written to the database directly.
2. `v28-build/capture.mjs` opens each route in `manifest.json`, performs any
   listed steps (pressing Edit Case, following a link), and saves the
   response the server gives for the page the browser ended on. For the Case
   record, the sections that load as the reader approaches them are requested
   from the same endpoint `case-workspace.js` uses and placed where that
   script places them.
3. Two states are the response to a post (a failed sign-in, the signed-out
   page), so they are saved from the rendered document instead and marked
   `"dom": true` in `manifest.json`. States reached by a step, a post or a
   sign-in are excluded from the parity check because no URL alone
   reproduces them.
4. Stylesheets, scripts, fonts and images are downloaded as served and
   referenced relatively. Fingerprints are removed from file names.
5. `assets/mock/shim.js` is the only script that is not the application's.

Two local hosts supply the pages. Both are the synthetic fixture host that
reuses the integration-test composition, never production and never a real
mailbox or Box: one signs every request in as an Administrator and holds the
seeded records; the other uses real sign-in with one throwaway User-role
account. The host is not committed; it lives in the ignored `artifacts/`
folder of the primary checkout (`artifacts/ui-baseline-review/visual-host`).

## 4. Frame rules, as numbers

Not restated. The pages carry the live `site.css` and page stylesheets
unmodified, so every number is whatever the application ships. Screenshots
are taken at 1580x1000, 1440x900 and 760x1000.

## 5. Known limits

- **Coverage follows the fixture.** One Case (With Engineer), one Triage, three
  Unidentified items, one image intake, one unprocessed mail message. Queues
  with nothing in them show their real empty state. Each page folder has a
  "Not captured" list naming what the fixture cannot reach.
- **Freshness.** Partial, Stale and Unavailable cannot be forced on a healthy
  local host, so only Current is captured.
- **Posts.** A control that posts shows a note naming its route. Two
  transitions are wired because both ends were captured: Edit Case, and
  leaving the edit session.
- **Composition.** The local fixture does not compose automation or the
  connector, so Automation & AI shows its unavailable panel, the hub omits
  that card, and `/authorize` answers 404.
- **Fixture wording.** Names such as "Jane Example", "integration-user" and
  the 2031 dates are the fixture's, not invented for the mockup.
- **Folder, not single files.** A family file needs `states/` and `assets/`
  beside it. This is the price of not transcribing (item A below).

## 6. The sign-off list

This round proposes nothing, so nothing here is a design decision. Items A
and B are about the capture; C to H record what the capture found in the live
application, for the operator to confirm or to raise as work.

**A.** The mockup is a folder of captured pages framed by nine family files,
not nine self-contained files. **Confirm, or** ask for each state to be
inlined into a single file, which is possible but makes each about 1 MB.

**B.** Coverage is limited to what the synthetic fixture holds (section 5 and
every page's "Not captured" list). **Confirm, or** name the states worth
seeding next: a Case in Review, a Held Case, a processed mail message with a
linked Case, AI jobs, and a generated report are the largest gaps.

**C.** Saving the Case record with many changed fields at once is refused on
`904903fd1`: the history entry lists every changed field and overflows
`CaseHistory.Reason` ("String or binary data would be truncated"), and the
operator sees only "The case action was not applied". `enrich.mjs` saves three
fields at a time to avoid it. **Confirm** this should be raised as a defect.

**D.** `Account/AccessDenied.cshtml` renders inside the full shell, while the
error and status pages use the navless frame. **Confirm** this is intended.

**E.** The Work Centre prints "Updated HH:mm" twice, in the page header and
above the metric strip. **Confirm, or** raise it.

**F.** At 1440 wide the Cases table wraps the Due date onto four lines
("25 / Sep / 20 / 26"). **Confirm, or** raise it.

**G.** One Case record showed both "2 Sept 2026" (Incident date) and
"18 Sep 2026 10:05" (Received) on a local run. **Confirm, or** raise it.

**H.** Re-verified from the first build's list, each still true in source:
the Query state has no tone in `_StatusChip.cshtml` and renders neutral; the
Work Centre AI jobs table says "Lease expires HH:mm"; manual Create Case
offers no Audit type; no Administration page uses an `images/marks` mark;
the Administration hub's card icons differ from the nav's for Service health
and Reports. **Confirm** each is as intended, or raise it.

Items E, F, J and K of the first build's list were transcription
uncertainties. They no longer apply because nothing is transcribed.

_No item is settled yet._

## 7. Self-check result

Recorded in the discussion log with the date and the full `RESULT` line.

## 8. Reproducing the round

From `v28-build/`, with the two fixture hosts running and `HOST_INFO` and
`AUTH_INFO` pointing at the JSON files they write:

```
node enrich.mjs        # once per fresh fixture database
node release.mjs       # make sure no edit session is left open
node capture.mjs
node frames.mjs
node shoot.mjs
node pagedocs.mjs
LIVE=1 node selfcheck.mjs
```
