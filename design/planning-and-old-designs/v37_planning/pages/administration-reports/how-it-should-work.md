# Management Reports: how it should work

Decided with the operator on 9 October 2026 (the brief, the two answers, the dispute ruling and the choice of A); the rest is open until Stage 1 sign-off. The lettered items are in [v37-notes.md](../../current/v37-notes.md).

## Rules the operator has already settled

- **D1.** The round produces three alternative designs, switched on the mockup strip; the page itself gets no widget board or customisation (operator, 9 October 2026).
- **D2.** The page stays Administrator-only (operator, 9 October 2026).
- **D3.** The proposals may extend the reports behind the page, not only its layout (operator, 9 October 2026).
- **D16.** A dispute is a query. No report counts disputes apart, and Engineer activity and the Case list have no Disputes column (operator, 9 October 2026; [PR #1146](https://github.com/collisionengineers/pegasus/pull/1146)).

## Proposed rules, shared by the three designs

- **D4.** One period bar under the title governs every report. Person sits on Engineer activity, the one report it filters. Download workbook is the page action. *Open: item B.*
- **D5.** A period that ends before it starts shows "Choose a valid date range." in the period bar and draws no report; the Case list keeps its own dates. *Open: item B2.*
- **D6.** Inspection and Audit are one Work choice per table on the page; the CSV and workbook keep every column. *Open: item D.*
- **D7.** A failed report refuses only its own CSV; By month has its own. *Open: item L.*
- **D8.** Reports by Principal sorts and has meters; a count's first click sorts largest first, with one arrow. *Open: item P.*
- **D9.** No explanatory note and no MI label is drawn. *Open: items C and M.*
- **D10.** Person lists the people with activity in the period. *Open: item N.*

## Proposed reports

- **D11.** Queues (now): held, Triage and Unidentified, with the oldest of each. *Open: item E.*
- **D12.** Cases by stage (now). *Open: item F.*
- **D13.** Outcomes and Audit agreement for the period. *Open: item O.*
- **D14.** Previous period under each total, and Period presets. *Open: items G and H.*

## Decided 9 October 2026

- **D15.** Design A, with no Disputes column (D16): the four sections in today's order, each tidied as A draws it. B's tiles and one-report view and C's month ledger and bars are not taken (operator: "opt for a modified version of design A on the basis of these changes"; items A, Q, R and I).

## Where this lands

FRD-17 §Management Reports is the owner. FRD-15 L59 and `capabilities.md` change only if items J, E, F or O are confirmed.
