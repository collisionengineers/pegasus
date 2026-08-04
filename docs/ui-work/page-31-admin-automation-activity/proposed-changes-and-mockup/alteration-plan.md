# Page 31 — Automation activity: alteration plan

Source: `src/Pegasus.Web/Pages/Administration/Automation/Activity.cshtml` (+ `.cshtml.cs`).
Review: `../review.md`. Standards: `../../ui-standards-and-review.md`.

## Review summary

A permanent record rendered as a database dump: eight columns, raw tool names and outcome
values in three of them, one column that holds the same value on every row, server-local
times labelled as if they were office times, a single filter that requires you to already
know the answer, and three lines of lede explaining the storage design. The redesign keeps
the one genuinely good idea — every row's Reference is a link that filters the record down
to that one operation — and rebuilds everything around it: plain column labels, an operator
label map for every value, Europe/London times, a designed empty state, and a filter bar
that starts from questions an administrator actually has.

## Changes

1. **Delete the lede.** Old: *"The consolidated permanent record of the Automation actor:
   every recorded action and every denied automation request. Each operation carries a
   correlation identifier, and filtering by it gives this record a stable address."* New:
   no lede (standards §4.1). H1 stays **"Automation activity"**; the table is the
   explanation.
2. **"Reference" everywhere, and never the banned term.** The filter label *"Correlation
   identifier"* → **"Reference"**; the column heading *"Correlation"* → **"Reference"**; the
   query parameter `correlationId` → `reference`; the empty-state suffix *" for this
   correlation identifier"* → *" for reference {value}"* (change 8). Standards §2.
3. **Six columns, plainly labelled.** Old: When / Record / Event / Subject / Outcome /
   Target / Correlation / Reason. New:

   | Old | New | Why |
   |---|---|---|
   | When | **When** | Kept; Europe/London (change 6), right-aligned tabular numerals |
   | Record | *(removed as a column)* | Folded into the event label and the Show filter |
   | Event | **What happened** | Labelled, never the raw value (change 4) |
   | Subject | *(removed)* | One registration exists; the value never varies (review lens 2) |
   | Outcome | **Outcome** | Chip with a labelled value (change 5) |
   | Target | **Related to** | Business reference, or "—" |
   | Correlation | **Reference** | Still the link that filters to one operation |
   | Reason | **Reason** | Labelled for denials; sanitised for failures (change 7) |

4. **An operator label map for every event value.** Old: `@record.EventKind`
   (`Activity.cshtml:59`) — the raw tool name for actions, the `SecurityEventType` value for
   denials. New: a page-model map in the shape of the existing `RecordTypeLabel`, throwing
   on an unmapped value so a new event is a build failure, not a raw string on screen:

   | Recorded value | Shown as |
   |---|---|
   | `pegasus_case_search` | Searched cases |
   | `pegasus_case_get` | Opened a case |
   | `pegasus_case_edit_begin` | Started a case edit |
   | `pegasus_case_edit_end` | Finished a case edit |
   | `pegasus_document_add` | Added a document |
   | `pegasus_document_download` | Downloaded a document |
   | `pegasus_document_export` | Exported a document |
   | *(the two receiving tools)* | Listed received items / Submitted an upload |
   | `automation_token_rejected` | Credentials rejected |
   | `automation_client_disabled` | Automation was turned off |
   | `automation_scope_denied` | Area not permitted |

5. **Label the outcomes and chip them.** Old: `@record.Outcome` (`Activity.cshtml:61`) —
   `Succeeded` / `Failed` / `Denied`. New chips, never colour-only:
   **Done** (green), **Failed** (charcoal), **Refused** (amber).
6. **Europe/London times.** Old: `record.OccurredAtUtc.ToLocalTime().ToString("dd MMM yyyy
   HH:mm:ss")` (`Activity.cshtml:57`) — the server's clock. New: converted through the same
   Europe/London zone the rest of the application uses (`src/Pegasus.Core/LondonCalendar.cs`),
   rendered **`04 Aug 2026 14:32`**. Seconds are dropped from the column and shown on hover
   via the cell's `title`; nobody scans a record by the second, and dropping them buys the
   column width back.
7. **Sanitise the Reason column.** Denials show the labelled reason, not the `automation_`
   snake_case code. Failures currently show `"{ExceptionTypeName}: {message}"` assembled in
   `AutomationMcpAuditor`; the column shows a plain sentence
   (**"The case could not be opened."**) and the technical detail stops being an operator
   surface. Where no reason exists the cell is **"—"**.
8. **Two designed empty states**, replacing the concatenated *"No Automation activity is
   recorded"* + *" for this correlation identifier"* (`Activity.cshtml:34`):
   - Nothing recorded: **"No automation activity has been recorded yet."**
   - Filtered miss: **"No activity matches reference OP-4821-K."** with a
     **"Show all activity"** action beneath it.
9. **A filter bar that starts from a question.** Old: one full-width Reference box and a
   Filter button. New: a single-line bar directly above the table —
   **Show** (*All activity* / *Actions only* / *Refused requests*) · **Reference** (narrow
   text box) · **Filter** · **Clear**, with "Clear" present only when a filter is applied.
   The Reference box is sized for the value it holds, not for the viewport.
10. **Paging states position.** Old: two bare links. New: **"Showing 1–50"** on the left of
    the pager, **Previous** and **Next** on the right, disabled-and-absent rather than
    disabled-and-visible at the ends of the record (standards §4.9).
11. **Validate the Reference field instead of 404ing.** Old: an over-length value returns
    `NotFound()` (`Activity.cshtml.cs:33-36`) — a raw browser 404 for a typo. New: an inline
    field message, **"That reference is too long."**, with the table unchanged behind it.
12. **One heading stack.** Eyebrow and "Back to Automation" link → breadcrumb
    **"Administration / Automation / Automation activity"**. The section label *"RECORDED
    ACTIVITY"* and the `<caption>` *"Automation actions and denied automation requests, most
    recent first"* are both dropped; a **"Newest first"** note sits in the pager row where
    sort order belongs.

## Dependencies

Backend work — plan only; nothing here is implemented by this document.

- **Event and outcome label maps** (changes 4, 5) in `ActivityModel`, extending the existing
  `RecordTypeLabel` pattern, with a test asserting every shipped tool name and every
  `SecurityEventOutcome` value is mapped.
- **Reason sanitisation** (change 7) needs the failure path in `AutomationMcpAuditor` to
  record a stable reason code alongside — or instead of — the exception string, so the UI
  has something to label. Today the only thing stored is the .NET message. This is the one
  change here that touches a writer, not a reader.
- **Europe/London conversion** (change 6): reuse the existing zone lookup rather than adding
  a third copy of `TimeZoneInfo.FindSystemTimeZoneById("Europe/London")`; the repository
  already has it in `LondonCalendar`, `CaseWorkScheduling` and two store classes, and the
  freshness banner's missing-timezone-data fallback is the behaviour to copy.
- **"Show" filter** (change 9) needs `ListAutomationActivityRequest` to carry a record-type
  filter; `EfAutomationActivityStore` already queries the two streams separately, so
  restricting to one is a narrowing, not a new query.
- **Position in the pager** (change 10): "Showing 1–50" is derivable from page and page size
  today. A total ("of 214") needs a count query over both streams — see open questions.
- **Keyset paging** (review lens 3): offset paging fetches `page * pageSize + 1` rows from
  both streams per request. Switching to keyset on `(OccurredAtUtc, Id)` is the correct fix
  and is compatible with the Previous/Next pager this plan draws.
- Renaming the query parameter `correlationId` → `reference` (change 2) changes existing
  bookmarks; the old name should be accepted as a fallback for one release.

## Open questions

- **Should the pager show a total?** "Showing 1–50 of 214" is better than "Showing 1–50",
  but costs a count query across two streams on every page view. Mockups show the cheap
  version; flagged for decision.
- **Is a date range worth adding now?** The review's practicality lens says an administrator
  arrives asking "what happened yesterday?", which no current filter answers. Adding it
  needs a store change. Mockups omit it and keep the filter bar to one line.
- **What replaces a failure's exception text (change 7) until the writer records a reason
  code?** Options: show nothing (cell reads "—") or show a single generic sentence. Mockups
  show a plain sentence; the honest interim may be "—".
- **Should "Related to" link to the case?** The value is a case reference for most action
  rows. Linking it makes the record navigable; it also means an administration record links
  into case data the administrator may not otherwise open. Operator decision; mockups render
  it as plain text.
- Does the Reference column need to stay visible at all once the value is unreadable, or
  should the whole row be the link to its own filtered view? Mockups keep the column,
  because copying a reference into a support conversation is a real task.
