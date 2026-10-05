# Work Centre: how it works today

Read from the live source on 5 October 2026 at `origin/dev` 81b571c36 (Release 84). This is the v30 design B, "Office ledger", as built on 25 September 2026 and extended by PR 1002 (Dismiss, 2 October 2026).

## What the page does not show

- **Dismiss on a Needs attention row.** It exists, but only inside the row's expanded detail (`_WorkCentreBody.cshtml` lines 312–319). Nothing on the closed row says the row can be dismissed. New cases show an always-visible icon-only X (lines 451–457); AI jobs show a text "Dismiss" button on each card (lines 557–561). Three lists, three treatments. This is what issue 1017 reports.
- **The next action** of a Needs attention row. It is inside the expanded detail too; the closed row names the task but offers no control.
- **Any activity figure.** New cases today, Sent to Engineer today / this week, Reports sent today / this week, E-mails received today and Needs sorting were tiles on this page until b952ff4a8 (28 August 2026, PR 610) replaced the page with the five queue counts. Their queries were deleted in 993f7b0ee (6 September 2026). No decision to drop them was recorded; the design documents of that round still describe them.
- **Up to five clocks.** The rail and the utility bar say "Current · HH:MM", the header says "Updated HH:MM", and the New cases and AI jobs section heads each say "Updated HH:MM" again.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [FRD-15](../../../../../docs/frd/frd-15-work-centre-queues-and-search.md) "Work Centre" | The metrics, the three sections and their tabs, the Needs attention kinds and due rules, Office / Mine, chips, Find, the ledger, the open row, New cases, AI jobs and Dismiss |
| [FRD-12](../../../../../docs/frd/frd-12-operator-experience.md) | The shell, the rail order (Work Centre first) and the utility bar's New case, omitted on this page only |
| [FRD-27](../../../../../docs/frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md) | The AI job list's per-kind actions (Review estimate, Open query, Review, Complete job) |
| [FRD-19](../../../../../docs/frd/frd-19-image-led-intake-and-pairing.md) | The Vehicle images paired kind |
| [Design authority](../../../../../docs/design/README.md) | Tokens, the metric strip, the ledger's reflow, voice and the closed necessary-copy list |
| [CONTEXT.md](../../../../../CONTEXT.md) | Reserved terms, including "First sent to Engineer", "New cases today" and the metric names |

## Source by layer

| Layer | File | Owns |
| --- | --- | --- |
| Web | `src/Pegasus.Web/Pages/Index.cshtml` | The route, `HidesNewCase`, the page CSS and script, the body partial |
| Web | `src/Pegasus.Web/Pages/Index.cshtml.cs` (`IndexModel`) | The three parallel reads, `OnGetRefreshAsync`, the `AssignEngineer`, `AssignToMe`, `AssignTriageToMe`, `CompleteAiJob` and `Dismiss` handlers, `PageUrl`, `VisibleTabs` |
| Web | `src/Pegasus.Web/Pages/_WorkCentreBody.cshtml` | Everything rendered, including the Refresh fragment |
| Web | `src/Pegasus.Web/wwwroot/css/work-centre.css`, `wwwroot/js/work-centre.js` | The `.wc-*` rules; remembered scope, in-place tabs, the five-minute and focus refresh |
| Web | `src/Pegasus.Web/Presentation/OperatorLabels.cs` (`WorkCentre`, `AiJobs`, `Busy`) | Every label |
| Web | `src/Pegasus.Web/Presentation/NeedsAttentionPresentation.cs` | Chip order, slugs, row title, subject, owner, facts, action label |
| Core | `src/Pegasus.Core/Operations/OperationsSnapshot.cs` | `GetOperationsSnapshot`, `NeedsAttentionPolicy` (`IsMine`, `CanTake`, `Matches`, `Priority`, `Order`), the 50-row page |
| Core | `src/Pegasus.Core/Operations/DashboardCounts.cs` | `NeedsAttentionKind`, `NeedsAttentionItem` (with `QualifiedAtUtc`), `WorkCentreMetrics` |
| Core | `src/Pegasus.Core/Operations/RecentCases.cs`, `WorkCentreAiJobs.cs`, `WorkCentreDismissals.cs` | The 7-day New cases window and the last-look stamp; the AI job list; `DismissWorkCentreItem` and `WorkCentreDismissalPolicy` |
| Infrastructure | `src/Pegasus.Infrastructure/Persistence/EfRecentCaseQueries.cs`, `EfWorkCentreDismissalStore.cs` | The New cases query (dismissals excluded in SQL), the one-row-per-record dismissal store |
| Infrastructure | `EvaFirstHandoffProxies`, `StaffMailSendOperations`, `CaseReportSentEvidence`, `IntakeReceipts`, `Cases.CreatedAtUtc` | The data the activity figures can be rebuilt from |

## Behaviours

### Header

Eyebrow "Office-wide work", h1 "Work Centre", "Updated HH:MM" beside the shared Refresh button, and Create Case as the page's one primary action. The utility bar omits New case on this page only (FRD-12).

### Metrics

Five one-line counts, Not ready, Review, Held, Unidentified, Triages, each a link to its Cases tab. They come from `OperationsSnapshot.Metrics`; a failed read renders no figure.

### One panel, three tabs

Needs attention, New cases and AI jobs, each with its count. A tab is omitted when its section is empty (Needs attention stays on Mine), kept with "—" when the section could not be read. The chosen tab travels in `?tab=`.

### Needs attention

Toolbar: h2, "N items", the Office / Mine switch (remembered per browser; every role opens on Office), Find. Eight kind chips with counts over the whole scope, multi-select, plus Clear filters. A five-column table (Next action, Record / detail, Owner, Due, Received) grouped Overdue (n), Due today (n), Later (n), empty groups not drawn, ordered by due instant, undated last. Choosing the task opens the detail row in place: kind, an Overdue or Due today chip, the title, six facts, then the next action, Assign to me where Core allows it, and Dismiss. Paged at 50.

### New cases

Every Case except a Triage Case created in the last 7 calendar days, plus a change the Automation actor made, newest first: reference, registration · claimant · principal, an arrival chip (Manual, E-mail, Principal API, Automation) and the time. "Since you last looked" divides what is new for this person. Each row ends in an icon-only Dismiss named "Dismiss {reference}".

### AI jobs

Queued, Taken (with "Lease expires HH:MM"), Draft ready and the Failed jobs of the same 7 days, excluding Market research, as cards: kind and state chip, the instruction, the record, "Started by", the time, the per-kind action, Complete job where the kind allows it, Open Case on a failed Case job, and a text Dismiss button.

### Dismiss

`WorkCentreDismissalPolicy`: a dismissal belongs to the record behind the row, applies for everyone, hides every row of that record that began at or before the dismissal in every tab, and the row returns when it re-qualifies. No undo, no notice, no dismissed list; the metrics do not change. Every Dismiss is a POST and a redirect, so focus returns to the top of the page.

### Refresh

The page refreshes itself every five minutes while visible and when the tab regains focus after 30 seconds away, never while a dialog is open or a field has focus. A failed section is retained and marked stale. A refresh does not mark New cases as seen.

## Things the FRD does not settle

- Where Dismiss sits on a Needs attention row, and that the three lists should draw it the same way.
- The activity figures: FRD-15 still carries a paragraph defining "New cases today" as "separate from Due today, Sent to Engineer and Reports sent", but no section says the page shows them, and the page does not.
- Whether one "Updated" clock is enough.
- "Lease expires" uses a word the design authority bans in operator copy.
