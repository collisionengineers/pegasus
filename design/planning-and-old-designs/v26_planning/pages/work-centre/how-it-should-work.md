# Work Centre — how it should work

Draft FRD basis. Decisions taken with the operator on 13 September 2026; each line is a rule the FRD can carry. Where a decision changes another page, the rule lives in that page's folder and is only referenced here.

Decisions taken with the operator on 13 September. Each line is a rule the FRD can carry.

### D1. External work leaves the Work Centre

- Failed external work (custody, vehicle lookup, intake OCR) is never a Needs attention row. It lives on Operations only; the Operations rail badge carries the count. What Operations shows for it is in [`../operations/how-it-should-work.md`](../operations/how-it-should-work.md).
- Where a failure blocks a person's work, the record says so at the point of use in operator words: Vehicle shows "Lookup failed", Files shows "Storage not ready". If that failure blocks Case readiness, the Case appears as a Case row with that reason, not as a failure row.
- The High priority is removed. It only ever meant "external failure".
- Open: whether Operations stays open to Engineers and Users or becomes Administrator-only.

### D2. The chip appears only when it changes what you do

- Overdue (red) with how late: "2 days overdue".
- Today (amber).
- No chip otherwise. Normal is the absence of a chip. The Due value is plain text on the row, relative when near: "Due Fri", "Due 24 Sep".
- With High gone, the list is ordered by due instant, earliest first, undated last, then by received, then by reference.

### D3. Every kind has a due instant

Each kind carries a target so it ages. The targets are workflow settings on Administration › Configuration; the settings themselves, their defaults and validation are specified in [`../configuration/how-it-should-work.md`](../configuration/how-it-should-work.md). This page consumes them:

| Kind | Due instant |
| --- | --- |
| Case chase | next chase time |
| Unidentified item | received + Unidentified target |
| Triage without finding | opened + Triage target |
| Held decision | the hold's Review on date if given (D5), else held + Held decision target |
| Review Case | entered Review + Review target |
| Unassigned Engineer | entered Review + Review target |

Fixed rules, not settings: calendar days; midnight Europe/London as the day boundary; "Today" means due before the next midnight, "Overdue" means due at or before now.

### D4. The list is paged, never cut

- Needs attention is a paged list, page size 50, "Page 1 of N · earliest due first", Previous and Next, the same paging as the Cases list. Nothing is silently dropped.
- The metric strip counts everything regardless of paging.
- The shell notifications menu keeps showing the first 10 rows of page 1.

### D5. Hold carries an optional review date

Place on Hold gains an optional "Review on" date. When given it is the held Case's due instant here; otherwise the Held decision target applies from the moment the hold was placed. The dialog and record change are specified in [`../case-record/dialogs/hold-release/how-it-should-work.md`](../case-record/dialogs/hold-release/how-it-should-work.md).

### D6. Adopted improvements (13 September)

P1 to P6, P8 and P9 are adopted as decisions. P7 (Blocked) was not adopted on 13 September; it is superseded by D7 the same day.

- **P1. Group the list by due day** instead of a chip per row: headings "Overdue (3)", "Due today (5)", "Later (12)", each row keeping its relative due text. The D2 chips then only appear in the Today pane and the notifications menu.
- **P2. Office and Mine.** A two-way switch above the list: Office (everything, as now) or Mine (rows whose owner is me, plus unowned rows of kinds I can take). Engineers open on Mine, Administrators and Users on Office. Remembered per person.
- **P3. Kind filter** as chips across the top of the list: Case, Held, Review, Unassigned, Unidentified, Triage. Multi-select, counts on each.
- **P4. The next action does the action.** Assign Engineer opens the Case's assignment dialog on the spot (see [`../case-record/how-it-should-work.md`](../case-record/how-it-should-work.md)); Review Case opens the Case at its Review decision; Open Triage opens the Triage. "Open full record" and the action button are the same link today, so one of them goes.
- **P5. Freshness.** The snapshot is taken on load and never refreshed. Show "Updated 14:22", refresh when the tab regains focus and every five minutes, and a Refresh button. The mockup's rail already has the freshness indicator.
- **P6. Received age on the row**: "Received 3 d ago" beside the owner, so a long-waiting item reads as such even before it is overdue.
- **P7. Blocked opens Blocked** — superseded by D7.
- **P8. Take it.** An "Assign to me" action on Unassigned Engineer rows for Engineers, and on Triage rows without an assignee, straight from the Today pane. The Case side is in [`../case-record/how-it-should-work.md`](../case-record/how-it-should-work.md), the Triage side in [`../triage/how-it-should-work.md`](../triage/how-it-should-work.md).
- **P9. Empty states per group**, so "Nothing overdue" is visible good news rather than an absent heading.

### D7. The Blocked metric is removed

Blocked stops being an operator concept ([`../received-file/how-it-should-work.md`](../received-file/how-it-should-work.md)): refused material is a closed Unidentified item, unreadable material shows on its message, failed processing shows on Operations. The metric strip drops to four: Not ready, Review, Held, Unidentified.

The Mail kind (open Unidentified items) is unchanged by Unidentified widening: closed Unidentified items never appear here.

### Still open

- Operations audience (D1): deferred until the Operations page is planned, see [`../operations/how-it-should-work.md`](../operations/how-it-should-work.md).
- The exact copy for the relative due text.
