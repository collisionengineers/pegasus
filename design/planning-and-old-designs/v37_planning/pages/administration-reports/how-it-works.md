# Management Reports: how it works today

Read from the live source on 9 October 2026 at `origin/dev` 970ef9f10 (after Release 97). The page is Administration › Management Reports, `/Administration/Reports`, open to Administrators only (`StaffAccessRight.ViewOperationalReports`).

## What the page does not show

- **The period the other reports cover.** From and To sit in Engineer activity's form, labelled "Engineer activity filters", but also drive Reports by Principal, By month and Turnaround. Nothing on those three says so.
- **That Person filters one report.** Person narrows Engineer activity and its workbook sheet only; the other reports ignore it silently.
- **Triage figures.** `PrincipalReportActivity` reads each Principal's current Triage count and oldest Triage, and the page never draws them.
- **Why two "Reports sent" totals differ.** Engineer activity counts Staff sends only; Reports by Principal counts every send, Automation's included. Neither says which.
- **Which Turnaround figures are now.** Currently held and Oldest held since ignore the period; the three times follow it.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [FRD-17](../../../../../docs/frd/frd-17-administration-workspace.md) "Management Reports" (L186–250) | One period filter over MI-01 to MI-03, totals, CSVs, the workbook and its By month sheet, per-report fee counting, query types, Audit uplift, turnaround from receipt, sort and meters, the Inspection / Audit columns, unavailable never a false zero, and the MI-04 Case list and presets |
| [capabilities.md](../../../../../docs/capabilities.md) MI-01 to MI-04 | One line each, owned by FRD-17 |
| [FRD-15](../../../../../docs/frd/frd-15-work-centre-queues-and-search.md) L59 | The Work Centre's Reports sent "as the Engineer activity report MI-01 counts them … so the two agree" |
| [Design authority](../../../../../docs/design/README.md) | Tokens, metric strips, tables, filters as selects, no explanatory copy, the `bar-chart` icon |
| [CONTEXT.md](../../../../../CONTEXT.md) | Principal, Case/PO, Received date, Triage, Unidentified, Held, Audit, Inspection + Audit |

None of MI-01 to MI-04 traces to `reference/`. Greps there for management information, MI, KPI, turnaround, monthly, per engineer, invoice and statistics found nothing relevant (9 October 2026; binary workbooks were read through their shared strings). The authority is the operator decisions recorded in FRD-17, FRD-15 and the commits 9c8b6f92e, 01db68b93 and 8f7b492d1.

## Source by layer

| Layer | File | Owns |
| --- | --- | --- |
| Web | `src/Pegasus.Web/Pages/Administration/Reports.cshtml` | The four panels, their tiles, tables, empty and Unavailable rows, notes and downloads |
| Web | `src/Pegasus.Web/Pages/Administration/Reports.cshtml.cs` (`ReportsModel`) | `LoadAsync` (period defaults, the three reads, the invalid-range error), `Sorted`, `SortDirectionFor`, `SortArrow`, `AriaSort`, the `Csv`, `PrincipalCsv`, `TurnaroundCsv`, `Workbook`, `CaseListCsv`, `CaseListWorkbook` and preset handlers, `ReportsUnavailable` |
| Web | `src/Pegasus.Web/Pages/Administration/Shared/_AdminNav.cshtml`, `Shared/_PageHeader.cshtml` | The Administration frame and the header |
| Web | `src/Pegasus.Web/wwwroot/css/admin.css`, `site.css` | `admin-report-*` (note, filter, meter, sort), `metric-strip`, `th[aria-sort] a::after` |
| Web | `src/Pegasus.Web/wwwroot/js/site.js` | `busyDownload` (fetched downloads, refusal toast), the `data-sort-toggle` arrow flip |
| Core | `src/Pegasus.Core/Reports/EngineerActivityReport.cs` | MI-01 rows, the Staff-only sender rule, query crediting to the assigned Engineer |
| Core | `src/Pegasus.Core/Reports/V1ActivityReport.cs` (`PrincipalReportActivity`) | MI-02 and MI-03: produced, sent, agreed fees, the Inspection / Audit split, held, Triage, the three averages |
| Core | `src/Pegasus.Core/Reports/AdministrationReportWorkbook.cs` | `MonthlyReportActivity` and the four-sheet workbook |
| Core | `src/Pegasus.Core/Reports/CaseListColumns.cs`, `CaseListReport.cs` | The MI-04 catalogue, N/A and blank, presets |
| Infrastructure | `EfEngineerActivityQueries.cs`, `EfV1ActivityReportQueries.cs`, `EfMonthlyReportActivityQueries.cs` | The reads |

## Behaviours

- **Period.** To defaults to now and From to 31 days before, as London-local minute-precision `datetime-local`. The period is half-open and at most 366 days. From after To adds "Choose a valid date range." inside Engineer activity. That report keeps its default empty result (three zeros and "No engineer activity was recorded for this period."), while the others read as Unavailable.
- **Engineer activity.** Reports sent are credited to the recorded sender, Staff actors only. Queries received (with disputes and amendment requests) are credited to the Case's currently assigned Engineer. Received to sent is averaged from the instruction's receipt, or from Create audit for an Inspection + Audit Case's Audit. Meters scale to the period's largest.
- **Sort.** No arrow until a column is chosen. A second click on the same column reverses, so a count's first click sorts smallest first. The page writes the arrow in a span, and site.css adds another after the link.
- **Reports by Principal.** Produced counts confirmed reports; sent counts every send. Agreed fees are the fee frozen in each work's first confirmed report, if that report falls in the period. Rows with nothing produced and nothing sent are hidden. By month lists Principal × London month, newest first, and has no CSV of its own.
- **Turnaround.** A row shows if it has held Cases or any average.
- **Downloads.** A fetched download goes Downloading…, then Downloaded with a tick. If any of Engineer activity, Reports by Principal or By month failed, every CSV and the workbook return 422 and the toast says "The file could not be downloaded. Try again.".
- **Case list.** It has its own received dates (date-only, with All time and Include Triage Cases), six column groups, CSV and workbook by POST, and shared presets. A refusal is a 422 text body for a script, or the page with the notice without one. Choosing a preset is a separate GET that submits only `preset`.

## Findings

1. One period filter inside one report governs four.
2. Turnaround mixes now and the period; Triage figures are read and not shown.
3. "Reports sent" has three definitions: Engineer activity, Reports by Principal and the Work Centre.
4. Three received-to-sent measures, written three ways (page, CSV, workbook). The Engineer activity CSV headings differ from the page.
5. Explanatory notes on Engineer activity and the Case list.
6. 10- and 12-column tables built from the "· Inspection / · Audit" pattern.
7. Apply forgets the sort, the CSV ignores it, and choosing a preset forgets the period and sort.
8. An invalid period shows a false zero in Engineer activity, which FRD-17 forbids.
9. One failed report blocks every CSV and the workbook.
10. Downloads sit in three different places, and By month has none.
11. The Case list's filter vocabulary differs (date-only, All time). Person lists every role, and an inactive person with activity cannot be chosen.
12. `data-report-workbook` and `data-engineer-activity` have no consumer.
13. A sorted heading shows two arrows, confirmed on the running page (`current/live-shots/live-reports-sorted-head-1440.png`).

## Things the FRD does not settle

- The Person filter (FRD-17 never mentions it) and its choices.
- The Turnaround "Cases currently held" tile, and how Oldest held since is derived.
- Whether Automation sends belong in Engineer activity.
- The duration format in CSVs.
- The Case list's 366-day limit.
