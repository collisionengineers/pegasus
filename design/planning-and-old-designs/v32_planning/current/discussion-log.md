# v32 discussion log

Chronological. The operator's words are quoted; the rest records what was explored, built and raised. This log is not design authority.

## Round 1 — 5 October 2026

**Issue 1017** (collisionengineers, 5 October 2026 09:09Z): "Work Centre - All Items need dismiss option. Limited only to 'New Cases' at present - 'Needs Attention' does not show this. Redesign likely worth consideration as there are multiple tabs."

**The brief** (operator, same morning): "Complete resolution of issue 1017, ideally via full improvement/redesign of the Work Centre page. Create 5 separate designs for this. Additional issue: we lost the daily/weekly stats for sent to engineer/sent reports etc a while back. Incorporate issue resolution, full UI improvement, and the additional issue, as well as any other potential improvements/design ideas on the 5 mockups."

**What was explored.**

- The live page at `origin/dev` 81b571c36. Dismiss already exists on every tab (PR 1002, Release 81) but is drawn three ways; on Needs attention it is inside the expanded row only, which is why it reads as missing. `OnPostDismissAsync` has no kind filter.
- The history of the activity tiles: dropped in b952ff4a8 (28 August 2026, PR 610), queries deleted in 993f7b0ee (6 September 2026), no decision recorded. The data still exists (`EvaFirstHandoffProxies`, `StaffMailSendOperations`, `CaseReportSentEvidence`, `IntakeReceipts`, `Cases.CreatedAtUtc`), and Administration → Reports MI-01 reads a different source for Reports sent than the old tile did.
- Usability of the live page beyond the brief: the next action hidden behind a click, two navigation levels (tabs, chips, groups), five clocks, "Lease expires" against the banned-word list, chips against the "filters are dropdowns" rule.

**Two questions answered by the operator before building.**

1. Interim fix for 1017 while the round runs, or redesign only? "Redesign only."
2. Which figures? "Original set" (New cases today; Sent to Engineer today and this week; Reports sent today and this week; E-mails received today) and "Add Completed this week". Queries received and per-person figures were offered and not chosen.

**What was built.** Five designs (A Even ledger, B Morning brief, C Split desk, D One list, E Flow lanes) and the live baseline, all drawn from one synthetic office dated Wednesday 7 October 2026 09:41, on the live shell; the comparison page; the self-check and screenshot script; this folder. See [v32-notes.md](v32-notes.md).

**Items raised.** A to P in the notes.

## Round 2 — 5 October 2026, while the files were being checked

The operator opened design A as it was built and sent three remarks with screenshots:

1. "buttons on A shouldn't be stacked like this" (the AI jobs table's Open query and Complete job, one above the other in the Action column).
2. "this could use its own row as well" (the Draft ready chip squeezed under the job kind in the same cell). Read as: give the chip its own place; it has its own State column. If a separate row was meant, say so and it changes.
3. "this just looks garb its a bunch of loose black text and numbers" (the activity strip under the five counts).

**What changed.**

- The AI jobs table (A) gained a State column for the chip and a wider Action column; the actions sit on one line in every table.
- The activity strip, which is also the shared fallback placement for every design (item I), became a bordered panel headed "Activity" with a two-row table: the figures head the columns, Today and This week are the rows, with the same column rules as the ledger.
- Design E gained the kind chips above its lanes so a kind filter is visible there too, and its all-empty page keeps the four lane heads so the zero counts and figures still show.

**Items raised.** None new; item I now compares the panel table with each design's own placement.

## Round 3 — 5 October 2026

The operator, on the in-row action buttons of A and D: "the buttons all being different sizes looks very jarring".

**What changed.** Every in-row action button fills the Action column, so Open Case, Assign Engineer, Open Triage, Open query and Review Case share one width with the label aligned left. The AI jobs table's two-button cell is unchanged.

**Items raised.** None new.
