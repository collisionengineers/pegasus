# v37 notes: Management Reports, three designs

Built on 9 October 2026 from `origin/dev` 970ef9f10. These notes cover the offline proposal only and are not application evidence.

## 1. What the operator has already settled

- **The round itself.** A mockup of Administration › Management Reports that also considers "the features and functionality behind this", with proposals for them, in three alternative designs (operator, 9 October 2026).
- **"A widget control" means a design switcher on the mockup strip.** The page gets no widget board and no customisation (operator's answer, 9 October 2026: "Design switcher in mockup").
- **The page stays Administrator-only**, as today (operator's answer, 9 October 2026: "Administrators only (as today)").
- **A dispute is a query.** "assume disputes and queries are the same - these must be rolled together both in our codebase, database, documentation, and the mockup design" (operator, 9 October 2026). Engineer activity and the Case list have no Disputes column in any design or on Today. The query counts already included disputes, so no total moves. The application, data and documentation change is [PR #1146](https://github.com/collisionengineers/pegasus/pull/1146).
- **Design A is chosen** (operator, 9 October 2026: "opt for a modified version of design A on the basis of these changes"). The modification is the dispute ruling above. B and C stay in the folder as the record; items I, Q and R close with them (section 6).

## 2. What changes, and where it shows

Shot numbers refer to `v37-shots/` (listed on the [page README](../pages/administration-reports/README.md)). Today's page is shot 04, drawn from the same fixtures. The running page, captured from the local visual host with no report data, is in `live-shots/`.

| # | Today | Proposal | Designs | Shots |
| --- | --- | --- | --- | --- |
| 1 | From, To and Person sit inside Engineer activity, labelled "Engineer activity filters", yet From and To also drive Reports by Principal, By month and Turnaround. Person silently filters Engineer activity only | One period bar (From, To, Apply) under the title governs every report. Person moves to Engineer activity's head, the one report it filters (item B) | A, B, C | 01–03, 16–19 |
| 2 | Download workbook sits in Engineer activity's filter row although it holds every report | Download workbook is the page action, beside the title (item B) | A, B, C | 01–03 |
| 3 | A period that ends before it starts shows zeros and "No engineer activity was recorded for this period." in Engineer activity, and Unavailable in the other reports. FRD-17 forbids the false zero | The error shows in the period bar and no report is drawn; the Case list, which has its own dates, stays (item B2) | A, B, C | 08–11 |
| 4 | Reports by Principal has 10 columns and By month 12: every measure three times, total · Inspection · Audit | One Work choice (All, Inspection, Audit) per table: 4 and 6 columns. The CSV and workbook keep every column (item D) | A, B, C | 01, 24, 25, 29 |
| 5 | One failed report blocks every CSV and the workbook | A failed report refuses only its own CSV; the workbook still needs every report. By month gets its own CSV (item L) | A, B, C | 12–15 |
| 6 | Explanatory notes on Engineer activity and the Case list | Not drawn (item C; a strip switch puts them back) | A, B, C | 01–03 |
| 7 | MI01–MI04 beside each heading | Not drawn (item M; strip switch) | A, B, C | 01–03 |
| 8 | Only Engineer activity sorts and has meters. A count's first click sorts smallest first. A sorted heading shows two arrows (finding 13) | Reports by Principal sorts and has meters too. A count's first click sorts largest first, with one arrow (item P) | A, B, C | 01, 24 |
| 9 | Person lists every enabled account of any role, and an inactive person with activity cannot be chosen | Person lists the people with activity in the period (item N; strip switch for live) | A, B, C | 17–19 |
| 10 | Four sections on one long page | **A**: the same four, tidied. **B**: five period tiles, then one report at a time from a Report choice or a tile (item Q). **C**: a Principal × month ledger for one Measure, with the chosen Principal's months and turnaround under it (item R) | per design | 01–03, 24–28, 33–36 |
| 11 | Turnaround mixes now (held) with the period (times) | With Queues on, the held figures move to Queues and Turnaround keeps the period's times (item E) | A, B, C | 05–07 |
| 12 | — | Proposals behind the page, each a strip switch: Queues (item E), Cases by stage (F), Previous period beside the totals (G), Period presets (H), Outcomes with Audit agreement (O). Month bars in C (I) | A, B, C | 05–07, 34–36 |

## 3. Live rules the mockup mirrors

- **Sections and words** are `Reports.cshtml` at 970ef9f10 with PR #1146's change (no Disputes column), word for word, including the two notes, the empty and Unavailable rows and the busy words (`OperatorLabels.Busy`). The Case list catalogue is `CaseListColumns.Build`, in order, with Core's "· Inspection" / "· Audit" titles.
- **Period** (`ReportsModel.LoadAsync`): To defaults to now and From to 31 days before; the inputs are minute-precision `datetime-local`. From after To raises "Choose a valid date range." in Engineer activity only; Reports by Principal, Turnaround and By month read as Unavailable.
- **Engineer activity** (`EngineerActivityReport`): reports credited to the recorded sender, Staff actors only; queries to the Case's assigned Engineer. Meters scale to the period's largest. Sort: none until chosen; then `SortDirectionFor` (asc, then desc on a second click).
- **Reports by Principal** (`PrincipalReportActivity`): every send counts, including Automation's, so the fixture's 110 against Engineer activity's 107 is the live difference (item J). Rows with nothing produced and nothing sent are hidden.
- **Turnaround**: held counts are now and ignore the period; rows show when held or when any average exists.
- **Downloads** (`site.js` `busyDownload`): Downloading…, then a tick and Downloaded. Live, any unavailable report makes every CSV and the workbook return 422, which shows "The file could not be downloaded. Try again." as a danger toast.
- **Case list**: "Use preset" ticks the preset's columns and names it; Save preset and Remove preset appear once one is loaded; no column ticked refuses with "Choose at least one column."; a duplicate name refuses with "Another preset already has that name.".

## 4. Frame rules

- **The frame** is the live shell from `_Layout.cshtml` and the Administration frame from `_AdminNav.cshtml` (220px nav, Management Reports current). Shot 04 and `live-shots/live-reports-1440.png` match. `capture-live-v37.py` diffs headings, columns, labels, buttons, tile labels, MI labels and notes, and found no difference (9 October 2026). That capture predates the disputes ruling, so it still shows the Disputes column that Today no longer draws.
- **Components** are site.css and admin.css only: `panel`, `metric-strip` (`--3`, `--5`), `metric-meta`, `table--compact`, `admin-report-measure`, `form-grid--auto`, `data-auto-submit` selects. The mockup's own CSS adds the head-select layout, C's selected row and the month bars, from site.css tokens.
- **Filters** are labelled selects, never pills (design README). B's tiles are `.metric` buttons with `aria-pressed`, as the Work Centre counts are.
- **Colour.** Navy for meters and bars, red for the chosen row's edge and the pressed tile, no green anywhere (nothing here is a confirmed completion), Unavailable never drawn as 0.
- **No explanatory copy.** No sentence is added. The strip's one line is mockup chrome.

## 5. Deliberate departures from live

- Nothing is posted. Apply, Use preset and the preset buttons change the page in place; a download only shows its busy and done states.
- A Period preset (item H) changes the choice but not the figures. Only C recomputes its figures from the chosen months.
- "Automation & AI" is drawn in the Administration nav; live shows it only when composed (the visual host does not).
- Fixtures are synthetic: six staff, the four Principal codes the integration tests use, and two presets. Live, the visual host has no report data, so `live-shots/` show the empty states.
- Preset confirmations show as a toast; live redirects and shows them as the page's confirmation.

## 6. Sign-off list

Every item is settled (9 October 2026); the decisions are in the table after the list. The first answer of each item was the proposal; "keep" means today's behaviour.

- **A.** Choose a design: A Tidied sections, B Overview first (recommended), or C Month ledger. *Confirm B, or choose another, or name the pieces to combine.* **Decided 9 October 2026: A**, "a modified version of design A on the basis of these changes", that is, A with no Disputes column. B was not taken.
- **B.** One period bar under the title for every report, Person on Engineer activity's head, Download workbook as the page action. This moves controls placed on the live page and changes FRD-17's description of the filter. *Confirm, or keep the filter inside Engineer activity.*
- **B2.** From after To shows "Choose a valid date range." in the period bar and draws no report (the Case list stays). *Confirm, or draw every report as Unavailable instead.* Either way the false zero goes, because FRD-17 already forbids it.
- **C.** The Engineer activity and Case list notes are not drawn. The design README allows no explanatory copy, but they are today's words and the Case list note is the only place N/A and blank are told apart. *Confirm, or keep either.* (Strip switch "Explanatory notes".)
- **D.** Inspection and Audit become one Work choice (All, Inspection, Audit) per table on the page; the CSV and workbook keep every column. New words: "Work", "All". It needs Fee notes produced split by work, which Core does not count today. FRD-17 L204–206 says the split columns are "on the page" and `AdministrationReportsWebTests` pins them. *Confirm, or keep the three columns.* (Strip switch "Inspection and Audit".)
- **E.** A Queues report (Now): Cases currently held, Triages and Unidentified, with the oldest of each, per Principal where a Principal applies. The held figures leave Turnaround. Triage figures are already read by `PrincipalReportActivity` and never shown; Unidentified is the Work Centre's count. New words: "Queues", "Now", "Oldest Triage since", "Oldest since". *Confirm, or leave out.* (Strip switch "Queues now".)
- **F.** A Cases by stage report (Now): open Cases per Principal in Not ready, Review, With Engineer, Held and Query, with a Total row. New words: "Cases by stage", "Total". *Confirm, or leave out.*
- **G.** "Previous period N" under each total: the same report for the same length of time just before. *Confirm, or leave out.*
- **H.** A Period choice before From and To: This month, Last month, This quarter, Last 12 months, Custom (in C: Last 6 months, This year, Last 12 months, Custom). *Confirm, or keep From and To only.*
- **I.** C's month bars: one navy bar per month under the ledger, the month total written beneath. The design README has no rule for a chart, and this would be the page's first beyond the meter. *Confirm, or draw the totals only.* (Strip switch "Month bars".) **Closed 9 October 2026:** C was not chosen.
- **J.** "Reports sent" counts three ways: Engineer activity counts Staff sends only (107 in the fixtures), Reports by Principal counts every send including Automation (110), and FRD-15 says the Work Centre's figure "agrees with MI-01" while counting every send. *Choose: keep both definitions; or count Automation sends in Engineer activity as their own row; or align the Work Centre and FRD-15 to say which they match.* Not drawn as a change.
- **K.** One way to write a turnaround: the page writes "6 days", the Engineer activity CSV `6.00:00:00`, the workbook `[h]:mm`; the Engineer activity CSV headings ("Recorded send actor", "Queries received for assigned Engineer", "Reports sent by recorded actor") differ from the page and workbook. *Confirm the page's words and headings everywhere, or keep.* Not drawn.
- **L.** A failed report refuses only its own CSV; the workbook still refuses unless every report read. By month gets its own Download CSV. *Confirm, or keep one failure blocking every download.*
- **M.** MI01–MI04 are not drawn beside the headings. *Confirm, or keep.* (Strip switch.)
- **N.** Person lists the people with activity in the period. *Confirm, or keep every enabled account.* (Strip switch.)
- **O.** An Outcomes report: reports produced in the period by outcome (Repairable, Total loss, Cash in lieu, Contract repair) and the Audits' Agrees / Differs with the original, per Principal. The words are Core's. *Confirm, or leave out.* (Strip switch.)
- **P.** Reports by Principal sorts by Principal, Reports produced, Reports sent and Agreed fees and has meters, as Engineer activity does; a count's first click sorts largest first. *Confirm, or keep today's order (smallest first) and Engineer activity alone sorted.*
- **Q.** Design B only: the five tiles (Reports produced, Reports sent, Agreed fees, Queries received, Cases currently held) and the report each opens; the Report choice. New word: "Report". *Confirm, or name other figures.* **Closed 9 October 2026:** B was not chosen.
- **R.** Design C only: whole London months replace From and To; the ledger shows one Measure (new word) with a Total column and row; choosing a Principal opens its months and turnaround; the By month table folds into the ledger and the chosen Principal. *Confirm, or name what to change.* **Closed 9 October 2026:** C was not chosen.
- **S.** This folder. *Keep it as the record of the round, or remove it in the Stage 2 pull request.*

**Decided 9 October 2026.** The operator: "b - yes", "b2 - agree", "c - agree", "d - yes", "L - yes", "i agree with all other findings and reccomemdations". The recommendations agreed to were the ones put to the operator that day:

| Item | Decision | In the mockup (Design A) |
| --- | --- | --- |
| A | Design A, with no Disputes column | Chosen on the comparison page |
| B | One period bar for every report; Person on Engineer activity; Download workbook in the page head | Drawn |
| B2 | From after To: the error in the period bar, no report drawn | Drawn (`state=invalid`) |
| C | Engineer activity's note goes; the Case list's N/A note stays | Default "Case list note only" |
| D | One Work choice per table; the CSV and workbook keep every column | Drawn |
| E | Queues report (now) | On by default |
| F | Cases by stage: left out (the Cases list already counts stages) | Off by default |
| G | Previous period under each total | On by default |
| H | Period presets | On by default |
| I | Closed with C | — |
| J | Automation's sends are their own Engineer activity row, so Reports sent agrees with Reports by Principal (110 in the fixtures) | Automation row drawn |
| K | The page's words and its "6 days" style in the CSV and workbook too | Not drawn (exports only) |
| L | A failed report refuses only its own CSV; By month gets its own CSV | Drawn |
| M | No MI01–MI04 labels | Drawn |
| N | Person lists the people with activity in the period | Drawn |
| O | Outcomes report with Audit agreement | On by default |
| P | Reports by Principal sorts and has meters; a count's first click sorts largest first, one arrow | Drawn |
| Q, R | Closed with B and C | — |
| S | Keep this folder as the record of the round | — |

**Not lettered: Stage 2 fixes unless the operator says otherwise.** Each is a defect against today's page or FRD-17, not a new rule:
- the false zero on an invalid period (finding 8; FRD-17 L204–208);
- the doubled sort arrow (finding 13: the page writes ↑ and site.css adds another), confirmed on the running page (`live-shots/live-reports-sorted-head-1440.png`);
- Apply forgets the sort, the CSV link ignores it, and choosing a Case list preset forgets the period and sort (finding 7);
- the unused `data-report-workbook` and `data-engineer-activity` hooks (finding 12);
- FRD-17 says "ten areas" at L7 and "twelve areas" at L300.

## 7. Self-check

9 October 2026, with every item decided: `python check-management-reports-v37.py` printed `RESULT {"fail": [], "okCount": 887}`, with no script or console error and no external request. It ran with `PEGASUS_CHROME` pointing at Playwright's Chromium 1234. What it checks:
- All four files render all 12 page states: one H1 "Management Reports", the Administration eyebrow, Management Reports current in the admin nav.
- Every visible word is a live label, a fixture value, or a proposal word listed under its lettered item. No banned word appears ("dispute" and "disputes" among them) and no green chip is drawn.
- A failed report shows Unavailable, never 0. An invalid period shows the error. Live keeps its false zero; the designs draw no report and no figure.
- With every proposal on, A and C draw Queues, Cases by stage and Outcomes, the held figures leave Turnaround, and Previous period sits under at least three totals.
- Nothing spills sideways at 1580, 1440 or 760 in any state.
- Coverage: each design shows all six Engineer activity columns, every Reports by Principal, By month and Turnaround fact, every period, Person and Case list control, and the five buttons. Design B is checked across all its reports.
- Flows:
  - no arrow before a sort; live's first click is smallest first, the designs' largest first, and only live draws its own arrow;
  - Person leaves one row; Person choices are 6 (proposal) or 7 (live);
  - Work Audit shows QDOS's 13; the columns switch restores ten columns;
  - the totals agree with their rows (112, £19,835.00); Reports sent is 110 in both reports with the Automation row, and 107 on Today;
  - B's tiles and Report choice open their report, and the tile is pressed;
  - C's Principal choice, Measure, six-month total (444) and bars switch;
  - downloads go busy, then done; live blocks every CSV on one failure, the designs only the failed report's;
  - the Monthly invoicing preset ticks its eight columns;
  - Apply with From after To shows the error;
  - the strip has four design links and ten switches, and none on Today.

50 screenshots were written. The record is `v37-shots/verification.json`. Neither the screenshots nor the self-check are application evidence.

## 8. Known limits

- The figures do not follow a Period preset or a Person beyond Engineer activity. Only C recomputes from its months.
- Cases by stage and Outcomes ignore Work and do not have an empty or Unavailable state of their own drawn, beyond Outcomes' empty row.
- The month bars have no text alternative of their own; the Total row beneath carries the figures.
- At 760 the head selects wrap under the heading, as live `panel-actions` do.
- B does not remember the chosen report across a period change; live Razor would carry `report=` in the period form.
- The live capture was taken from the visual host built from `dev` 37c4b96f5; `Reports.cshtml`, `admin.css` and the Administration nav are unchanged between that commit and 970ef9f10. It was taken before PR #1146, so `captured/` and `live-shots/` show a Disputes column; they are kept as the record of that day's page.

## 9. Not drawn: these need new recording

Listed so the operator can decide whether to raise them; none is proposed in this round.
- Invoice raised, invoice number, paid date and outstanding amount. Invoicing is deferred (FRD-24), so the page can show agreed fees, never money received.
- The actual inspection date. `inspection_date` is a free field whose planned-or-actual meaning is not recorded.
- A region or area for Engineers or Cases. The postcode is not held apart from the address.
- Principal groups (parent insurer or tier).
- Costs: AI usage, Glass's, Cazana, hosting, Engineer pay. There is no margin or cost per Case.
- Structured reasons for hold, cancellation, rejection, reopening and created in error. All are free text, so a "why" breakdown would not be reliable.
- Whether an AI proposal was accepted, edited or rejected as a job outcome.

## 10. Stage 2 plan (waiting for approval to start)

Design A with every item decided. Hand-over is [razor-html-mockup-conversion](../../../../.agents/skills/razor-html-mockup-conversion/SKILL.md). Builds on PR #1146 (no Disputes).
- `Pages/Administration/Reports.cshtml(.cs)`: the period bar with Period presets (B, H), the Person head select listing people with activity (N), the workbook page action, per-report CSV availability and a By month CSV (L), the period error in the bar with no report drawn (B2), the Work choice (`work=` / `workm=`, D), Reports by Principal sorting and meters (P), the Engineer activity note and MI labels removed (C, M), the Queues and Outcomes sections (E, O), and Previous period under each total (G). The four sections stay in today's order.
- Core: Fee notes produced by work in `MonthlyReportActivity` (D). Automation's sends as their own Engineer activity row (J). Queues from `PrincipalReportActivity`'s Triage figures plus the Unidentified count (E). Outcomes from the first confirmed report snapshots (O). A second run of the same reads for the previous period (G). The page's words and "6 days" durations in the CSVs and workbook (K).
- Defect fixes: the invalid-period false zero, the doubled sort arrow, Apply and presets forgetting the period and sort, the dead hooks, FRD-17's "ten" and "twelve areas".
- Docs: FRD-17 §Management Reports rewritten for the decisions; FRD-15 L59 (the Work Centre's Reports sent now agrees with MI-01, Automation row included); `capabilities.md` MI rows for Queues and Outcomes.
- Tests: `AdministrationReportsWebTests` (the split headers pinned on the page change under D), `AdministrationReportTablesTests` and `EngineerActivityReportTests` for K's headings and durations and J's row, and Core and persistence tests for every new read. CI runs them.
- This folder stays (S).
