# Page 7 — Request operations (`/Operations/Requests`) review

Reviewed from `operations-requests.png` and `src/Pegasus.Web/Pages/Operations/Requests.cshtml`
(labels in `Requests.cshtml.cs`). Under the new IA this becomes a Dashboard drill-down for
upload links and external work; the operator has stated Box File Requests are superseded.

## 1. Aesthetics

- Heading stack again: "OPERATIONS" / "Requests" / "Bounded Box, Pegasus upload-link and
  durable external-work outcomes." (`Requests.cshtml:19-21`). "Bounded" and "durable" are
  internal composition adjectives; no operator has ever asked for a "durable outcome".
- Default state is three stacked empty panels: "No Box file-request outcomes are
  recorded.", "No Pegasus upload-link outcomes are recorded.", "No durable external-work
  outcomes are recorded." (`Requests.cshtml:35,133,226`) — a page of three grey boxes.
- Item cards head themselves "Pegasus upload link · Active" (`Requests.cshtml:141`) then
  pile up an eight-row `<dl>` including "Limits version" and "Edit mode" — a settings dump,
  not a card. Buttons multiply beneath: up to five forms per card ("Renew edit mode",
  "Leave edit mode", revoke form, …).

## 2. Practicality

- **The revocation ceremony is the worst flow in the application.** To revoke a link the
  operator must first click "Enter edit mode to revoke" (`Requests.cshtml:79,172`) or
  "Recover edit mode to revoke" (`Requests.cshtml:90,183`), wait for a round-trip, then a
  *different* set of controls appears ("Renew edit mode", "Leave edit mode",
  "Reason for revocation", "Revoke Pegasus upload link", `Requests.cshtml:104,109,118,214`).
  If someone else holds the claim they get the shrug: "Revocation is unavailable until the
  current edit mode expires or is released." (`Requests.cshtml:95,188`). Concurrency
  plumbing is being operated by hand, by the user.
- "Edit mode" itself leaks as a data row: `LeaseLabel` renders "Edit mode available /
  active / unavailable" (`Requests.cshtml.cs:379-383`) as if it were a property of the
  request the operator should monitor.
- **Raw byte counts**: "Byte limit" shows `@item.AcceptedByteCount / @item.MaximumByteCount`
  (`Requests.cshtml:157`) — e.g. `2516582 / 26214400` — with fallback text
  "limit version unavailable". "Limits version" (`Requests.cshtml:158`) is a pure
  implementation integer shown to a human.
- Box file requests occupy the first, most prominent section while the business has
  already superseded them.
- Failure detail is the raw `FailureCode` token again (`Requests.cshtml:163`).
- The page is orphaned: reachable only through a Dashboard card that says "Unavailable".
- Truncation notice narrates internals: "Showing the latest @GetRequestOperations.MaximumItems
  request and external-work outcomes." (`Requests.cshtml:267`).

## 3. Performance / Design / Good practice

- The Box and Pegasus sections are ~90-line near-duplicates (`Requests.cshtml:31-127` vs
  `129-220`) differing in one handler name and one label — copy-paste markup that has
  already drifted (Box lacks the failure block Pegasus has).
- Kind filtering happens in the view (`Requests.cshtml:6-14` LINQ in the `@{}` block) —
  presentation logic that belongs in the page model.
- Five separate `<form>` posts per card each mint their own GUID operation key per render
  (`RequestsModel.NewOperationKey()`); the claim/renew/release trio makes the operator
  responsible for a state machine the server should walk in one round-trip.
- Hidden `expectedVersion` / `expectedCaseVersion` inputs are the right optimistic-
  concurrency guards — keep the guards, remove the visible ceremony.
- `<time datetime>` usage and `aria-labelledby` sectioning are correct, as on page 6;
  timestamps again use the machine `u` format.
- External work rows expose "Work kind" as the raw `@item.ExternalKind` value and
  "Attempts" as a bare integer (`Requests.cshtml:237-238`) — no label map exists on this
  path at all.
