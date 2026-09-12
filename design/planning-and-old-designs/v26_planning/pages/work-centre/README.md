# Work Centre

- **Mockup route:** `pegasus_shell_v26.html#/` in [`../../current/`](../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Index.cshtml`
- **Dialogs:** [`dialogs/`](dialogs/README.md) · **States:** [`states/`](states/README.md) · **Panels:** [`panels/`](panels/README.md)

## Screenshots

- [s01-work-centre.png](../../current/v26-shots/s01-work-centre.png)
- [s29-work-centre-760.png](../../current/v26-shots/s29-work-centre-760.png)
- [s33-work-centre-partial.png](../../current/v26-shots/s33-work-centre-partial.png)

## How it works today (read from the live source, 13 September 2026)

Sources: `src/Pegasus.Web/Pages/Index.cshtml(.cs)`, `src/Pegasus.Core/Operations/OperationsSnapshot.cs`, `DashboardCounts.cs`, `src/Pegasus.Core/Tasks/CaseWorkScheduling.cs`, `src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs`, `docs/frd/frd-12-operator-experience.md` § Work Centre.

### The page

One snapshot query (`GetOperationsSnapshot`) taken when the page loads, stamped with its time. If that read fails the page shows an unavailable state rather than zeros. Three parts:

1. **Metric strip** — five counts, each a link to the exact Cases tab: Not ready, Review, Held, Unidentified, Blocked. Blocked links to the Unidentified tab where blocked intake rows sit uncounted with their own chip.
2. **Needs attention** — up to 50 rows, one chip each. The first row is selected unless the address names another.
3. **Today** — the selected row: kind and reference, title, chip, "Why this needs attention" notice, Source, Owner, Last recorded outcome, Due, then the single next permitted action and Copy reference.

### The seven kinds and where each comes from

| Kind | Core query | Row title | Reason shown | Action |
| --- | --- | --- | --- | --- |
| Case | Due work: chase schedule `Scheduled`, next chase at or before now, not archived | the missing-material reason | chase state | Open Case |
| Held decision | Cases in state Held | claimant | Held | Open Case |
| Review Case | Cases in state Review that are not ready for engineer assignment | vehicle | Case needs review | Review Case |
| Unassigned Engineer | Cases in Review with no engineer whose completeness satisfies policy | vehicle | Engineer assignment is required | Assign Engineer |
| Mail | every open Unidentified item | file name or subject | the Unidentified reason code | Review source |
| Triage | Triage records in Open or Awaiting information (no finding yet) | registration | triage state | Open Triage |
| External work | request operations of kind External work that can be retried | the external kind | failure reason | Open Operations |

Failed AI jobs are not a kind; they live on Operations.

### What the chip means

The chip is `NeedsAttentionPriority` with four values. It is computed from one date per row, never set by hand:

| Chip | Rule |
| --- | --- |
| Overdue | the row's due instant is at or before now |
| High | fixed for External work (a retryable failure) |
| Today | the due instant is after now but before the end of the London day (midnight) |
| Normal | no due instant, or due after today |

Which date is "due":
- Case: the next chase time; if none, the missing-material Due-by date at 00:00 UTC.
- Held decision, Review Case, Unassigned Engineer: the Case's next chase time.
- Mail and Triage: none, so always Normal.
- External work: none, always High.

Because the Case kind only lists schedules whose next chase is already at or before now, every Case row is Overdue by construction. Today and Normal can only appear on Held, Review and Unassigned rows.

### Where the chase time comes from

- First chase = the moment the Case entered Not ready plus the chase interval, same local time of day (London). Interval is a workflow configuration, default 7 days, 1 to 365.
- Each chase run moves it forward by the interval from the previous chase.
- Holding a Case pauses the schedule (held-at and remaining interval are kept); closing, archiving or replacing a Case stops it.
- The interval is calendar days, not working days.

### Order of the list

Priority (Overdue, High, Today, Normal), then due date, then received date, then reference. Duplicate kind and id pairs collapse to one. Cut at 50; the shell notifications menu shows the first 10 of the same rows.

### Things the FRD does not settle, for the new FRD

- Why a Review Case or Held decision carries a chase date at all: the chase belongs to missing material, yet Held and Review rows reuse it as their due date.
- Mail and Triage never age: an Unidentified item from three weeks ago is Normal forever.
- High is a kind, not an urgency: it means "external failure", and it sorts above Today.
- Calendar days versus working days for the interval, and midnight as the "today" boundary.
- The 50-row cut is silent: nothing says more exist.

