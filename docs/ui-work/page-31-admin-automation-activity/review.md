# Page 31 — Administration / Automation activity: review

Source: `src/Pegasus.Web/Pages/Administration/Automation/Activity.cshtml` and
`Activity.cshtml.cs`. Screenshot: `automation-activity.png`. Governing standards:
`../ui-standards-and-review.md` (§2 vocabulary, §4 presentation rules).

## 1. Aesthetics

- The lede — *"The consolidated permanent record of the Automation actor: every recorded
  action and every denied automation request. Each operation carries a correlation
  identifier, and filtering by it gives this record a stable address."* — is the purest
  example in the application of the UI narrating its own data model. "Consolidated" is a
  storage fact (two streams joined in `EfAutomationActivityStore`), "actor" is a C# type
  name, "correlation identifier" is banned outright (standards §2: show "Reference"), and
  the second sentence explains URL design to an administrator. Three lines of prose above a
  page that, in the captured state, contains nothing.
- The screenshot is the whole indictment: eyebrow, H1, three-line lede, a full-width empty
  input labelled **"Correlation identifier"**, a Filter button, and a panel containing the
  single sentence *"No Automation activity is recorded."* The filter field is roughly
  1400px wide for a value nobody can type from memory, and it sits **above** the thing it
  filters with no visual connection to it.
- The section label **"RECORDED ACTIVITY"** sits inside a panel on a page whose H1 is
  already "Automation activity" — the same words twice, one of them shouted, plus the
  `<caption>` *"Automation actions and denied automation requests, most recent first"*
  saying it a third time.
- Eight columns — When / Record / Event / Subject / Outcome / Target / Correlation /
  Reason — with no width discipline. Two of them ("Correlation", "Reason") hold long
  strings, one ("Subject") holds the same value on every row, and the numeric "When" column
  is set in a proportional face, so the timestamps do not align down the column.

## 2. Practicality

- **Raw machine values are the table's content.** `Activity.cshtml:59` renders
  `@record.EventKind` unchanged — for action rows that is the tool name, so an
  administrator reads **`pegasus_case_search`**, **`pegasus_case_edit_begin`**,
  **`pegasus_document_download`**; for denial rows it is the `SecurityEventType` value
  (`Token`, `Client`). `Activity.cshtml:61` renders `@record.Outcome` unchanged —
  `Succeeded`, `Failed`, `Denied`. Standards §4.3 bans exactly this, and the page already
  proves the fix works: `RecordTypeLabel` (`Activity.cshtml.cs:48-55`) hand-labels the
  record type, so someone wrote one map and stopped at one column.
- The **Reason** column for denial rows carries the reason code verbatim — snake_case
  strings prefixed `automation_` (`AutomationActivityConventions.SecurityEventReasonPrefix`)
  — and for failed action rows it carries `"{ExceptionTypeName}: {message}"`, assembled in
  `AutomationMcpAuditor` and truncated to 1000 characters. A .NET exception type name in an
  administration table is a defect, not a design choice.
- **The Subject column is dead weight.** There is exactly one Automation registration, so
  `record.SubjectId` is the same client identifier on every action row and on every denial
  row except an unauthenticated one (`"anonymous"`). A column that never varies is a column
  that should not exist; the one interesting case (an unauthenticated attempt) deserves a
  labelled event, not a repeated identifier.
- **Filtering by Reference is the only filter, and it is unusable as a starting point.** An
  administrator arrives asking "what has automation been doing?" or "why did that request
  fail?" — not holding a correlation value. There is no date range, no "show only refused
  requests", no filter by area. The one filter provided requires you to already have the
  answer; in practice you get it by clicking a value in the table, which works, but then the
  page offers no way back except a "Clear filter" link that only appears once filtered.
- **Times are wrong for the business.** `Activity.cshtml:57` uses
  `record.OccurredAtUtc.ToLocalTime()` — the *server's* local time, not Europe/London. Every
  other date surface in Pegasus goes through the London calendar
  (`src/Pegasus.Core/LondonCalendar.cs`, `_FreshnessBanner.cshtml:22`). On a UTC host this
  page silently labels UTC as if it were the office clock.
- **The empty state does not distinguish "nothing yet" from "nothing matched".** The copy
  is assembled by string concatenation — *"No Automation activity is recorded"* plus
  *" for this correlation identifier"* (`Activity.cshtml:34`) — so the filtered miss reads
  as a grammatical afterthought and neither variant offers a way out. This is the state in
  the screenshot, and it is the state every current deployment shows, because the feature
  gate that would produce any activity is off everywhere (see page 30).
- **Paging gives no position.** Two bare links, "Previous page" and "Next page", with no
  page number, no count, and no indication of how much record there is. `PageSize` is 50 and
  invisible.

## 3. Performance / Design / Good practice

- The query fetches `page * pageSize + 1` rows from **both** streams and joins them in
  memory (`EfAutomationActivityStore.ListAsync`). At page 1 that is 102 rows for 50
  displayed; at page 20 it is 2002 rows fetched, sorted and discarded to show 50. Offset
  paging over a union is the wrong shape for an append-only record — keyset paging on
  `(OccurredAtUtc, Id)` is the natural fix and would also let the UI say "older/newer"
  honestly.
- `HasMoreRecords` is derived correctly (fetch one extra), but no total is available, so the
  UI cannot show a count without a second query — worth deciding explicitly rather than
  leaving the operator with two unlabelled links.
- The correlation cell links back to the same page with the value as a query parameter —
  good, genuinely the best thing on the screen — but it renders the raw value as the link
  text, so the link is only clickable because it is also unreadable.
- An over-length filter value returns `NotFound()` (`Activity.cshtml.cs:33-36`): a raw
  browser 404 for a typo in a text box. Standards §4.6 requires a designed state; a
  validation message on the field is the correct response.
- Accessibility: the table has a `<caption>` and `scope="col"` headers (good), but the
  filter input's label is implicit-wrapped with no `id`/`for` pair, and the two paging links
  are indistinguishable to a screen reader user moving between pages because nothing
  announces which page they are on.
- `ActivityModel.RecordTypeLabel` throwing on an unknown enum value is the right pattern —
  a new record type becomes a build/test failure rather than a raw string on screen. It
  should be extended to event kinds and outcomes, not left as the only labelled column.
