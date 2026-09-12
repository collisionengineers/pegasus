# Work Centre

- **Mockup route:** `pegasus_shell_v26.html#/` in [`../../current/`](../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Index.cshtml`
- **Dialogs:** [`dialogs/`](dialogs/README.md) · **States:** [`states/`](states/README.md) · **Panels:** [`panels/`](panels/README.md)

## Screenshots

- [s01-work-centre.png](../../current/v26-shots/s01-work-centre.png)
- [s29-work-centre-760.png](../../current/v26-shots/s29-work-centre-760.png)
- [s33-work-centre-partial.png](../../current/v26-shots/s33-work-centre-partial.png)

## How it should work (draft FRD basis, 13 September 2026)

Decisions taken with the operator on 13 September. Each line is a rule the FRD can carry.

### D1. External work leaves the Work Centre

- Failed external work (custody, vehicle lookup, intake OCR) is never a Needs attention row. It lives on Operations only; the Operations rail badge carries the count.
- Where a failure blocks a person's work, the record says so at the point of use in operator words: Vehicle shows "Lookup failed", Files shows "Storage not ready". If that failure blocks Case readiness, the Case appears as a Case row with that reason, not as a failure row.
- The High priority is removed. It only ever meant "external failure".
- Open: whether Operations stays open to Engineers and Users or becomes Administrator-only.

### D2. The chip appears only when it changes what you do

- Overdue (red) with how late: "2 days overdue".
- Today (amber).
- No chip otherwise. Normal is the absence of a chip. The Due value is plain text on the row, relative when near: "Due Fri", "Due 24 Sep".
- With High gone, the list is ordered by due instant, earliest first, undated last, then by received, then by reference.

### D3. Every kind has a due instant

Each kind carries a target so it ages. The targets are **workflow settings** on the Administration › Configuration page (the same place as the chase interval), each with a default that is the standard:

| Kind | Due instant | Setting | Default |
| --- | --- | --- | --- |
| Case chase | next chase time | Chase interval (existing) | 7 days |
| Unidentified item | received + target | Unidentified target | 0 days (the day received) |
| Triage without finding | opened + target | Triage target | 1 day |
| Held decision | held + target | Held decision target | 7 days |
| Review Case | entered Review + target | Review target | 1 day |
| Unassigned Engineer | entered Review + target | Review target (shared) | 1 day |

Fixed rules, not settings:

- Targets and intervals are **calendar days**. Working days are not modelled.
- The day boundary is **midnight Europe/London**. "Today" means due before the next midnight; "Overdue" means due at or before now.
- A target of 0 days means due by midnight on the day it arrived.

Validation: 0 to 365 days for targets, 1 to 365 for the chase interval, as now.

### D4. The list is paged, never cut

- Needs attention is a paged list, page size 50, "Page 1 of N · earliest due first", Previous and Next, the same paging as the Cases list. Nothing is silently dropped.
- The metric strip counts everything regardless of paging.
- The shell notifications menu keeps showing the first 10 rows of page 1.

### Still open

- Operations audience (D1).
- Whether a Held decision should instead carry a review date chosen when the hold is placed, with the target as the fallback only.
- The exact copy for the relative due text.

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

### What "External work" means

External work is a job the Worker runs against a service outside Pegasus, queued durably with an attempt count. Six kinds exist (`src/Pegasus.Core/Custody/ExternalWorkProcessing.cs`):

| Kind | What it does | Outside service |
| --- | --- | --- |
| Create case custody | creates the Case's file storage when a Case is created | Box |
| Create audit reference custody | creates storage for an audit reference | Box |
| Create image case custody | creates storage for an image-initiated Case | Box |
| Merge image case custody | moves an image case's files into the Case it was merged with | Box |
| Vehicle lookup | fetches vehicle data for a registration | vehicle data provider |
| Intake OCR | reads text from a scanned received file | OCR provider |

A failure retries itself first: dependency-shaped failures back off at 1, 5, 15 minutes, 1 hour, 6 hours, six attempts in all. Only when the failure is terminal, or the attempts run out, does it become a request operation in state Failed with a retry available. That is the row the Work Centre shows as High, titled by the kind and carrying the attempt count, with Open Operations as the action; Operations is where Retry lives.

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

