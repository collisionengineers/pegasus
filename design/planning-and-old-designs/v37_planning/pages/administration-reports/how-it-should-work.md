# Management Reports: how it should work

Decided with the operator on 9 October 2026: the brief, the two answers, the dispute ruling, the choice of Design A and every lettered item in [v37-notes.md](../../current/v37-notes.md).

## Decided 9 October 2026

### The round

- **D1.** The round produces three alternative designs, switched on the mockup strip; the page itself gets no widget board or customisation.
- **D2.** The page stays Administrator-only.
- **D3.** The proposals may extend the reports behind the page, not only its layout.
- **D15.** Design A: the four sections in today's order, each tidied as A draws it. B's tiles and C's month ledger are not taken (items A, I, Q, R).
- **D16.** A dispute is a query. No report counts disputes apart, and Engineer activity and the Case list have no Disputes column ([PR #1146](https://github.com/collisionengineers/pegasus/pull/1146)).

### Shared rules

- **D4.** One period bar under the title governs every report, with a Period choice (This month, Last month, This quarter, Last 12 months, Custom) before From and To. Person sits on Engineer activity, the one report it filters. Download workbook is the page action (items B, H).
- **D5.** A period that ends before it starts shows "Choose a valid date range." in the period bar and draws no report; the Case list keeps its own dates (item B2).
- **D6.** Inspection and Audit are one Work choice per table on the page; the CSV and workbook keep every column (item D).
- **D7.** A failed report refuses only its own CSV; the workbook still needs every report. By month has its own CSV (item L).
- **D8.** Reports by Principal sorts and has meters; a count's first click sorts largest first, with one arrow (item P).
- **D9.** Engineer activity's note and the MI labels are not drawn; the Case list keeps its N/A note (items C, M).
- **D10.** Person lists the people with activity in the period (item N).
- **D17.** Automation's report sends are their own Engineer activity row, so Reports sent agrees with Reports by Principal and the Work Centre (item J).
- **D18.** The CSVs and workbook use the page's headings and its way of writing a duration (item K).

### Reports

- **D11.** Queues (now): held, Triage and Unidentified, with the oldest of each; the held figures leave Turnaround (item E).
- **D12.** Cases by stage is left out (item F).
- **D13.** Outcomes and Audit agreement for the period (item O).
- **D14.** Previous period under each total (item G).

## Where this lands

FRD-17 §Management Reports is the owner and is rewritten in Stage 2; FRD-15 L59 and the `capabilities.md` MI rows follow (items J, E, O).
