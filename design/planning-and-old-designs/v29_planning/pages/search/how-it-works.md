# Search — how it works today

Read from the live source on 23 September 2026 (origin/dev 446c3ce2f).

`/Search` is the advanced search: a filter form, a Vehicle images table when
image records match, and a Case results table with a Selected Case preview.
Paths are under `src/` unless stated.

## What this page does not show

- No Triage. The page queries Cases (`ISearchCases`) and vehicle-images
  records (`IImageIntakeQueries`) only; a `T-` reference finds nothing.
- No Unidentified items. A `U-` reference finds nothing.
- No Case type column and no Case type filter. The type shows only as the
  preview's eyebrow.
- No Due column. Due shows only in the preview, as the next chase date.
- No hold review date on the state chip. Results use `OperatorLabels.CaseStage`,
  not the Cases list's "Held · review on …".
- No Case creation here. **Create Case** goes to `/Upload`.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [FRD-15](../../../../../docs/frd/frd-15-work-centre-queues-and-search.md) | The filters (Case/PO or Image reference, Registration, Claimant, Claim/provider reference, Principal, State, Engineer, Received from and to, Origin); results as one table (Case/PO and Our ref, vehicle, claimant, principal, type, state, due); the selected-Case preview; vehicle-images records by Image reference or registration; an exact U-reference returns Unidentified items as their own result type. |
| [FRD-01](../../../../../docs/frd/frd-01-case-identity-and-lifecycle.md) | The `a.` value is an Audit Case's own Case/PO. |
| [ADR-0051](../../../../../docs/adr/0051-linked-audit-case-identity-and-custody.md) | Search treats the Audit Case as any other Case. |
| [FRD-16](../../../../../docs/frd/frd-16-case-record-workspace.md) | "Claim reference" is the provider's claim number; "Our ref" is the immutable Case reference. |
| [FRD-12](../../../../../docs/frd/frd-12-operator-experience.md) | `/Search` replaces the old Cases list; the command palette's Enter falls back to Search. |

## Source

| Layer | File | What it owns |
| --- | --- | --- |
| Web | `Pegasus.Web/Pages/Search/Index.cshtml` | The header, the filter form, the Vehicle images table, the Case results table and the preview pane. |
| Web | `Pegasus.Web/Pages/Search/Index.cshtml.cs` | The bound filters (drawn and undrawn), `OnGetAsync`, `LoadImageIntakeResultsAsync`, `ComposeRowsAsync` (`ResultRow`), paging links. |
| Web | `Pegasus.Web/Pages/Search/_CasePreview.cshtml` | The Selected Case preview. |
| Web | `Pegasus.Web/Pages/Shared/_ShellDialogs.cshtml`, `wwwroot/js/site.js` | The command palette's fallback to `/Search?query=`. |
| Core | `Pegasus.Core/Cases/CaseQueries.cs` | `ISearchCases`, `CaseSearchFilters`, `CaseSearchItem`. |
| Infrastructure | `Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` | `ApplySearchFilters`: how each filter matches. |

## Behaviours

### What it searches

`ApplySearchFilters` (`EfCaseQueryStore.cs`) applies each filter present:

| Filter (form label) | Parameter | Match |
| --- | --- | --- |
| Case or reference | `query` | Any of: `Reference` contains it; `AuditReference` contains it; the registration with spaces and hyphens removed contains its letters and digits; claimant contains it; claim number contains it; Principal equals it upper-cased; the state code contains it; the Engineer id equals it when it is a GUID; origin contains it |
| (not drawn) | `case` | `Reference` or `AuditReference` contains it |
| Registration | `registration` | Registration without spaces and hyphens equals it |
| Claimant | `claimant` | Contains |
| Claim reference | `claimNumber` | Contains |
| Principal (text, max 20) | `principal` | Equals |
| State | `state` | The stored state equals the chosen `CaseLifecycleState` |
| Engineer (More filters) | `engineerId` | Equals; the input takes a GUID |
| Received from, Received to (More filters) | `fromDate`, `toDate` | Europe/London day bounds |
| Origin (More filters) | `origin` | Equals |

Both `Reference` and `AuditReference` are matched. A Case made by Create
audit has both set to `a.{reference}`, so `a.QDOS…` finds it and so does the
original's number alone, which finds the original too.

The State select lists every `CaseLifecycleState` with its
`OperatorLabels.CaseStage` label, so "With Engineer" appears twice (Report
preparation and Post report) and the four closed dispositions appear as
"Closed · …".

Results are read 25 to a page. `kind=instructions` skips the vehicle-images
lookup; `kind=images` shows only vehicle-images records, and with no input
lists them all.

### Vehicle images

An exact Image reference in `case` or `query`, or a registration in
`registration` or `query`, finds vehicle-images records. They show in their
own table: Reference, Registration, Associated Case, Registered, State.

### Case results

The table's columns are **Case/PO** (the reference, linked to the Case, with
the claim number beneath), **Vehicle** (registration, with make and model
beneath), **Claimant**, **Principal**, **State** (a chip from
`OperatorLabels.CaseStage`) and **Editing** (who holds the edit lease).

An Audit Case's reference is its `a.` value, so it reads as one in the
Case/PO column. There is no other mark of Case type in the table.

### The Selected Case preview

`_CasePreview.cshtml` shows, for the selected row:

- an eyebrow with `OperatorLabels.CaseTypeName`: "Inspection", "Audit" or
  "Inspection and audit";
- the reference and registration as heading, claimant and Principal beneath,
  and the state chip;
- Accident circumstances;
- facts **Our ref**, **Engineer**, **Editing** (when held), **Due** and
  **Next action**;
- Outstanding requirements for a Not ready Case, and **Open Case**.

The **Our ref** fact shows the claim number (`ResultRow.ProviderReference`
is `item.ClaimNumber`). **Due** is the next chase date. **Next action** is
the first outstanding requirement's resolve text, else "Not recorded".

### Create Case

The header's **Create Case** links to `/Upload`. Its source comment says
`/Cases/Create` is receipt-bound and "returns NotFound without one"; the
Create page serves a manual form without a receipt (see
[Create case](../cases/create/how-it-works.md)).

## Things the FRD does not settle

- Whether Search should find Triage. FRD-15's Search section names Cases and
  vehicle-images records only.
- The Unidentified U-reference. FRD-15 says an exact U-reference search
  returns Unidentified items as their own result type; the page has no
  Unidentified query.
- The result columns. FRD-15 lists "type" and "due" in the table; the page
  has neither column.
- The preview's Our ref. FRD-16 keeps Our ref for the Case reference; the
  preview labels the claim number Our ref.
- Whether a search for an original's number should also return its Audit
  Case. The substring match returns both.
