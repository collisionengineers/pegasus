# v28 notes

A faithful capture of `origin/dev` at `904903fd1` (18 September 2026, worktree
`task/ui-baseline-rebuild`), with the operator's proposals as a switchable
layer over it. The capture is sections 1 to 8; the proposals are section 9.
Changes are recorded as they are made in [`working-log.md`](working-log.md).

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

- **Coverage follows the fixture.** One Case (With Engineer, two estimates,
  four images of which two are in the report), one Triage, three
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

Items A and B are about the capture. C to H recorded what the capture found
in the live application; the operator ruled on them on 18 September.

**A.** The mockup is a folder of captured pages framed by nine family files,
not nine self-contained files. **Confirm, or** ask for each state to be
inlined into a single file, which is possible but makes each about 1 MB.

**B.** Coverage is limited to what the synthetic fixture holds (section 5 and
every page's "Not captured" list). **Confirm, or** name the states worth
seeding next: a Case in Review, a Held Case, a processed mail message with a
linked Case, AI jobs, and a generated report are the largest gaps.

**C to H.** *Settled 18 September: all six are valid issues to address, not
all of them in a mockup.* What was done with each is P6 in section 9. The
findings themselves: C, saving many Case fields at once is refused because the
history reason overflows `CaseHistory.Reason`; D, Access denied renders in the
full shell while the error family is navless; E, the Work Centre prints
"Updated HH:mm" twice; F, the Cases table wraps the Due date onto four lines
at 1440; G, one record showed "2 Sept 2026" and "18 Sep 2026"; H, Query has no
chip tone, the AI jobs table says "Lease expires", manual Create Case offers no
Audit type, no Administration page uses an `images/marks` mark, and the hub's
icons differ from the nav's.

Items E, F, J and K of the first build's list were transcription
uncertainties. They no longer apply because nothing is transcribed.

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

## 9. Proposals

From 18 September the round is a collaboration on top of the capture. The
captured pages are never edited; every change is in the layer:
`assets/mock/proposals.js` and `.css` (P1 to P11),
`assets/mock/proposals-record.js` and `.css` (the Case record, P12 to P36), and
`assets/mock/proposals-record-2.js` and `.css` (the fifth pass, P37 to P51 and the
corrections; P49 was dropped). The family files default to **Proposals** and
switch to **Baseline**; on a state page `?proposals=off` is the baseline and
`?skip=P4` turns one proposal off. [`working-log.md`](working-log.md) has the
request, the choices and the Stage 2 consequence of each.

| Id | Proposal | Shots | Status |
| --- | --- | --- | --- |
| P1 | The refined mark from `v27_planning/logo` replaces the lockup in the rail and on the navless frame | p01, p02 | asked for 18 September |
| P2 | Status colour by meaning: green succeeded, red did not, amber waiting, navy in hand, neutral settled | p03, p09, p12 | asked for 18 September; the label table is a proposal |
| P3 | "Provider" never appears: "Cancelled", "Principal" | p07 | asked for 18 September |
| P4 | The lifecycle strip on Overview is removed | p03, p04 | asked for 18 September |
| P5 | Damage by area: v27 variant C with the eight areas, with Reset | p05, p06 | chosen 18 September |
| P6 | Baseline issues D to H, where a mockup can show them | p01, p08, p10, p11 | asked for 18 September |
| P7 | The Lifecycle actions container on Overview is removed; Return to Review is already in the Actions menu | p04 | asked for 18 September |
| P8 | Cazana drawn like the other guide sources; Get valuation a real button at the bottom centre of each card; no Basis radio, a card is chosen by clicking it | p13, p14, p33 | asked for 18 September; extended the same day |
| P9 | "Estimate PDF" becomes "Print Estimate" and sits with Compare under More; Compare greyed out with one estimate | p15, p16 | asked for 18 September |
| P10 | The "Use estimate · …" lock chip is removed and Use estimate is simply available | p18 | asked for 18 September; revised the same day |
| P11 | Upload received reworked: files first, the decision beside them, Discard folded away last | p12, p17 | asked for 18 September |
| P12 to P30 | The nineteen v27 features, rebuilt on the live record: composed sentences, CAP, salvage slider, reason bank, Delete all and Undo, regional uplift, provenance chips, richer Compare, Supplementary, address book, Attach, re-send naming, Report and Fee tabs, ribbon badges, nine-section map, click to include, Queries, decisions as radio groups, Report wording | p20 to p26 | asked for 18 September; table in the working log |
| P31 to P35 | Repair Spec: the Estimate section renamed; notes, days and name removed with rename on the tab; one labour rate control; Target % of value slider; Contract repair agreed | p22, p27, p28 | asked for 18 September; P34 and P35 cross FRD-11 |
| P36 | The live Glass's slot and session states, drawn from the partial because the fixture has no Glass's login | p18, p29 to p32 | live behaviour, not a proposal |
| P6-I | A panel head's "not yet" line no longer takes empty-state padding, so the Vehicle head is the height of every other | p34 | live defect found 18 September |
| P34 (corrected) | The price floor reads 65 %, the reference file's figure; Apply and Remove scaling freeze a version and write a System note | p35 | corrected 20 September |
| P30 (extended) | Wording blocks drag by a grip as well as moving by the buttons; a computed Repair reserve cell sits beside the typed field on Decisions | p40, p44 | asked for 20 September |
| P29 (extended) | When the outcome is not Total loss a "Salvage: not applicable" line says why the rows are gone, and the change is logged | p44 | asked for 20 September |
| P22 (corrected) | Attach offers Repair Spec, the document Print Estimate opens, in place of "Breakdown"; each attachment carries a tooltip | p40 | corrected 20 September |
| P37 | Off-pattern cells read amber with a tooltip and the roll-up carries their amount as specialist; nothing is cleared | p35 | asked for 20 September; Core already computes the anomalies |
| P38 | Placements: claimant VAT status on Claim, the report content switches on Valuation, unrelated damage on Vehicle, Sign-off Engineer beside an Assigned engineer cell on Case details | p42, p45 | asked for 20 September |
| P39 | Sign-off Engineer follows the assigned Engineer when that Engineer is an eligible sign-off account | — | asked for 20 September; the fixture has no eligible account, so it cannot be shown |
| P40 | Generate report (and Produce PDF) stamp today's date into an empty Report date | p40 | asked for 20 September |
| P41 | Working images: rotate, Full page, remove with Undo, drag to reorder mirrored to the Report strip, Add images by picker or drop, Full page and Remove on the viewer | p41 | asked for 20 September |
| P42 | Produce PDF: a preview of the report as it will print with Report, Repair Spec and Images documents; Print Estimate opens the Repair Spec one | p38, p39 | asked for 20 September |
| P43 | Versions history: every import, scale, clear, restore and send freezes the outgoing draft with how it came about; Sent on report; Compare with current; Restore; an origin line under the tabs | p36, p37 | asked for 20 September |
| P44 | Every act the layer performs writes a System note to the Notes timeline | — | asked for 20 September |
| P45 | Sub-cards fold with the live collapse toggle and are remembered like the sections | p42, p43 | asked for 20 September |
| P47 | A page-wide stale banner under the ribbon while the report is stale | p46 | asked for 20 September |
| P48 | Materials per line: a Material £ column; the estimate figure becomes the column total | p35 | asked for 20 September; a Core change |
| P49 | Product type select | — | **dropped 20 September**: Case type stays Inspection or Audit, fixed at creation (FRD-01, ADR-0051); every Audit is `a.`, AP is gone; Commercial and Diminution routes stay deferred |
| P51 | Original report: a section for an Audit Case naming the assessor (the firms Core's third-party report profiles know), the report date, its roadworthiness and its repairable status; shown on an Audit Case (standalone, or linked by Create audit) | p48, p49 | asked for 20 September |
| P50 | One place for an image: its report role and order sit on its tile on the Images tab with the P41 tools and the count; the Report section's "Images in report" strip and preparation cards are hidden. The Files section keeps its live name, since it also holds Documents and Correspondence | p41, p47 | asked for 20 September |

### Open under the proposals

Settled 18 September: P2 (Active and Enabled green, the two reds), P3 (one
"Update Request" category), P5 (LH / RH), P8 (an error that says to contact an
administrator), P9 (Compare greyed out; Print opens the preview).

- **P5.** Reset returns to what the record held on opening. Confirm, or say it
  should clear the vehicle. v27's G1 is still open: is the disc stored with its
  derived areas, or only the areas.
- **P6.** Issue C is noted for later planning. "Lease expires HH:mm" on the AI
  jobs table was read as "keep the live wording"; confirm. The nine panel marks
  under `images/marks` are unused; confirm they go from `design/README.md`.
- **P8 and P3.** "Error. Contact an administrator." and "Update Request" are new
  copy in the operator's words; confirm the exact wording.
- **P31.** Whether "New estimate", "Print Estimate" and Settlement's "From current
  estimate" follow the rename to Repair Spec.
- **P34 and P35.** They put target-% scaling and the agreed contract sum on the
  record, which FRD-11 reserves to Core and to Send to AI. Confirm the rule
  changes, and the floors (£50 an hour, 50 % of price).
- **P25.** The badges crowd the ribbon at 1440. Badges, or untruncated facts.
- **P26.** Damage and Valuation lose their own heads inside Vehicle. Confirm.
- **P30.** Crosses FRD-11's fixed-template rule. Confirm the rule changes.
- **P21 to P23.** Drawn on a delivery form the fixture cannot reach. Seeding a
  generated report would let them be checked against a running page.
- **P36.** The Glass's slot is drawn from the live partial, not captured, so it
  sits outside the parity check. Enabling a Glass's login on the fixture
  account would let the real thing be captured.
- **P29.** Corrected 19 September to radio groups, with the unset state offered
  as "Not recorded". Confirm that wording for the empty option.
- **P32.** Estimate notes, Repair days and Estimate name are hidden, not
  deleted, so nothing is lost on save. Confirm the three fields go from the
  record altogether, or stay somewhere.
- **P48.** Materials move from the estimate to each line. Confirm the Core change:
  a `Materials` amount per line, the estimate's paint materials becoming the sum.
- **P49.** Settled 20 September: dropped. The instruction types are Inspection,
  Inspection + Audit (Create audit makes the linked Audit Case) and standalone
  Audit, as FRD-01 has them; no product select, no AP, Commercial and
  Diminution deferred. P51 keys off the Case being an Audit.
- **P43.** Which acts freeze a version in Stage 2, and whether "Sent on report"
  reads from the report's recorded estimate dependency.
- **P51.** Stage 2 has a source for every cell: Core's third-party report
  extraction records the issuer, report date, roadworthiness and outcome
  (`ThirdPartyReportContracts.cs`), and FRD-09's standalone Audit states an
  `originalReportVerdict`. Confirm the cells fill from the extracted original
  report when one is filed, with the chip saying so, and stay hand-entered
  otherwise. Confirm the firm list is the profile list plus Other.
- **P50.** The section is "Files" again because it holds Documents and
  Correspondence too; say if another name is wanted (the live button says
  "Add evidence"). The Report section keeps no image surface at all; confirm
  the report's image order is then the tile order plus each Supporting order.
- **P41.** Whether a removed image is Not used (the live role) or leaves the
  Case; the mockup hides it and offers Undo. Whether Full page is a report role
  or a flag on the Supporting image.
- **P39.** The fixture has no eligible sign-off account, so the rule is wired
  but not shown. Flagging an account on the fixture would let it be captured.
- **Settled 20 September (finalisation).** P5: Reset returns the areas to the
  values held when the edit opened; Core stores the eight areas only. P32:
  Estimate notes and Repair days are deleted; the name stays, edited on the
  tab. P41: Remove sets the role to Not used and the file stays on the Case;
  Full page is a flag on an included image. P31: everything says Repair Spec.
  P48: materials move to the line (additive migration). P30, P34 and P35:
  Engineer-owned on the record; FRD-11 is rewritten in Stage 2. P51: cells
  fill from the extracted original report when one is filed, hand-entered
  otherwise; the firm list is the profile list plus Other.
- **Copy sweep, 20 September.** Read against `docs/design/README.md` ("No
  explanatory copy", banned words, one fact one home): the P41 two-per-page
  note and the P37 tooltip sentence are gone (the cell is named Off-pattern);
  the P29 line is a label and a value ("Salvage · Not applicable"); the P44
  history rows state the act without a consequence clause; the P30 wording
  chips no longer use the banned word "composed" ("tracks fields", "from
  Estimate", "edited"); P38 no longer repeats the assigned Engineer on Case
  details, since the ribbon owns that fact. Still to decide by the operator:
  the live AI jobs wording "Lease expires HH:mm" uses a banned word.
- **After PR 792 and PR 793 (18 September, merged in the finalisation).** The
  captured `case-record-estimate-import*` states and P36's Complete import
  predate PR 793: an estimate upload now imports in one act, so P43 freezes v1
  at import and the origin line reads "Imported … from …". Sign-off
  eligibility (P38, P39) is enabled + flagged + signature after PR 792, with
  no role test; `access-denied-user-role.html` is superseded by it.
- **Declined 20 September.** Take over and ask to release, inline padlocks,
  an assigned-engineer select, Engineer's value in two places, the
  average-mileage shortcut, import overwrite after a confirm, WhatsApp as a
  channel, implicit reopen after send, and the autosave edit model are not
  wanted; they stay in the reference file only.
- **Already live.** The section link following the reader as the page scrolls
  and the sections' fold state remembered per browser are live behaviour, so
  neither is a proposal.

### Carried over from v27

v27 never went to Stage 2 and none of its proposals is live. The nineteen the
operator listed on 18 September are now P12 to P30, with P1 (the mark) and P5
(damage by area). v27's `offpattern`, `place`, `signoff` and `reportdate`
switches are P37 to P40 since 20 September. Of its needs-a-decision-first list
nothing is built: the product type select was drawn as P49 and dropped the
same day on the operator's ruling.

## 10. Stage 2 progress

Recorded 20 September 2026 as the slices were built; each is one PR from
`origin/dev`, with its FRD updated in the same change.

| Slice | Proposals | Branch / PR | State |
| --- | --- | --- | --- |
| A · Shell and global | P1, P2, P3, P6 D–I, P8 | `task/v28-shell`, PR 797 | built, 124 web tests green, CI running |
| B1 · Case details | P4, P7, P12, P25, P45 (P47 was already live) | `task/v28-case-details`, PR 798 | built, 147 web tests green |
| B2 · Section map | P26 (Case details, Claim, Decisions; Damage and Valuation nested under Vehicle), P38 (VAT registered on Claim, report switches on Valuation, Sign-off Engineer on Case details; unrelated damage stays in the nested Damage panel), P51 (Original report section on an Audit Case, hand-entered; the extraction fill is a follow-up) | `task/v28-case-details`, PR 798 | built, 121 web tests green, CI running |
| C · Vehicle, Damage, Valuation | P5, P8 cards, P13, P24 | — | not started |
| D · Repair Spec | P9, P10, P16–P20, P31–P37, P43, P44, P48 | — | not started |
| E · Decisions | P14, P15, P29, P30 reserve, P35 | — | not started |
| F · Report, images, delivery | P21–P24, P27, P30, P39–P42, P50 | — | not started |
| G · Upload and queries | P11, P28 | — | not started |

Beside the slices, the same day delivered the operator's three additions:
release notes (PR 794), problem reports (PR 795, stacked on 794) and the
Administration reports (PR 796). PR 793 (estimate import) carries PR 792's
gate removal and is green.
