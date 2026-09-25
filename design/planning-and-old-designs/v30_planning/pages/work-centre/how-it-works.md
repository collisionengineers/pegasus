# Work Centre: how it works

Read from the live source on 25 September 2026 (`origin/dev` `32dabfc59`).

## What the page does not show

- With Engineer and Query counts (the rail's Cases count includes them; the
  strip does not).
- Failed external work; that is Operations.
- Who else is looking at the same item.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [FRD-15 · Work Centre](../../../../../docs/frd/frd-15-work-centre-queues-and-search.md#work-centre) | The head ("Updated HH:MM", Create Case, Refresh), the five metrics, Needs attention kinds and groups, Office/Mine, the Today pane, New cases, AI jobs, self-refresh |
| [Design authority](../../../../../docs/design/README.md#component-map) | `metric-strip--5`, `pane-layout--2`, `row-button`, `fact-grid`, `chips`, `status`; the reflow table |
| [FRD-27](../../../../../docs/frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list) | The Draft ready action per job kind |

## Source by layer

| Layer | File | Owns |
| --- | --- | --- |
| Web | `Pages/_WorkCentreBody.cshtml` | Header, metric strip, the two panes, the assignment dialog, New cases, AI jobs |
| Web | `Pages/Index.cshtml.cs` | Scope, kinds, paging, selection, refresh replay fields, the assign handlers |
| Web | `Presentation/NeedsAttentionPresentation.cs` | `ChipOrder`, `RowTitle`, `RowDetail` ("Kind · reference · subject"), `OwnerLabel`, `Facts`, `ActionLabel` |
| Web | `Presentation/OperatorLabels.WorkCentre` | Every label: Office-wide work, Needs attention, Today, Selected work, Office, Mine, All kinds, the due and received texts, Updated HH:MM |
| Web | `wwwroot/css/work-centre.css` | `wc-switch`, `wc-filters`, sticky `wc-group` heads, `wc-due` tones, the Today facts grid, the divider |
| Web | `wwwroot/js/work-centre.js` | Refresh in place on focus and every five minutes; never while a dialog is open or a field has focus |
| Core | `Operations` (Needs attention query), `AiWork` | What the rows are |

## Behaviours

### Header

Eyebrow **Office-wide work**, h1 **Work Centre**. Actions: the refresh
outcome text, **Refresh** (`Shared/_RefreshButton`, replaying scope, kinds,
pages, selection and since) and **Create Case** (primary).

### Metrics

A `meta wc-freshness` line "Updated HH:MM" above a `metric-strip
metric-strip--5`: Not ready, Review, Held, Unidentified, Triages, each an
exact link to its Cases tab. `0` renders as `0`; a failed read renders the
whole attention section's `notice--warning`.

### Needs attention (left pane)

Head: **Needs attention**, "N items", the Office | Mine switch. A `chips
wc-filters` row of kind chips (Case, Held, Review, Unassigned, Unidentified,
Triage, AI draft) each with its count, and **All kinds** to clear. The
scrolling list groups rows under sticky **Overdue (n)**, **Due today (n)**
and **Later (n)** heads, each with its empty sentence. A row is a
`row-button` link: title, "Kind · reference · subject", and a right column
of due text (toned by group) and "Owner · Received N d ago". Pagination
"Page N of M · earliest due first".

### Today (right pane)

Head **Today** · "Selected work". Selected item: eyebrow "Kind · reference",
h3 title, a red or amber chip only when Overdue or Due today, a three-column
fact grid (kind-specific facts, Reference, Owner, Due, Received), and the
actions: the dark next action (Assign Engineer, Review Case, Open Triage,
Review source, the AI draft's action) and **Assign to me** where Core would
accept it. Nothing selected: "Select an item".

### Assign Engineer dialog

Facts (Registration, Claimant, Principal, Engineer), an Engineer select,
**Assign to me**, Cancel, **Assign** or **Reassign**.

### New cases

Panel head **New cases** · "Updated HH:MM" · "Last 7 days · N rows". Rows:
reference (and "· Changed by automation"), "registration · claimant ·
principal", an arrival chip and the time. A **Since you last looked**
divider. Pagination "Page N of M · newest first".

### AI jobs

Panel head **AI jobs** · "Updated HH:MM" · "N draft ready · N failed". A
table: Job (kind and instruction), Record, Started by, Created, State (chip;
Taken adds "Lease expires …"), and the action column (Draft ready action,
Complete job, a failed job's reason with Open Case).

## Things the FRD does not settle

- Where "Updated HH:MM" sits: FRD-15 puts it in the head with Create Case
  and Refresh; the page draws it above the metric strip.
- Whether the metric tiles carry a tone or a glyph.
- The Today pane's empty presentation.
