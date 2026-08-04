# Page 22 review — Access review

Screenshot: `access-review.png` · Source: `src/Pegasus.Web/Pages/Administration/Access/Index.cshtml`
Route: `/Administration/Access` · Reviewed against `docs/ui-work/ui-standards-and-review.md`.

## 1. Aesthetics

- The most restrained page in the Administration set: one table, no side panel, no lede. The
  uppercase load is still four devices deep (eyebrow, visible caption "Current staff access and
  the last recorded review decision", six uppercase headers), but the underlying structure is
  right.
- The per-row form (Reason input + red "Record reviewed" button stacked in the Actions cell)
  doubles the row height and puts a saturated primary button on every row. On the single-row
  screenshot it dominates the table; with a full staff list it would be a column of red.
- "Review state" chip next to "Last reviewed" column is stating overlapping facts in two
  columns — a recorded date *is* the review state in every case except "never reviewed".
- The Reason label renders inside the Actions cell in sentence case while every other label on
  the row's row-line is an uppercase header — the row reads like a form fragment pasted into a
  table, which is exactly what it is.

## 2. Practicality

- **The screenshot shows "0001-01-01 00:00:00Z" as a last-reviewed time, chipped "Recorded".**
  That is `DateTime.MinValue` rendered as a real date and presented as a completed review — a
  genuine display defect. A minimum/default timestamp must never render as a fact; the row
  should read as never reviewed.
- The column header is **"Last reviewed (UTC)"** and values render `ToString("u")`
  (`2026-07-14 09:42:00Z`). Everywhere else operator-facing times render in London
  (`_FreshnessBanner` converts to Europe/London); an access review recorded at 23:30 on the
  31st would appear under the wrong day for the person reading it. Should be London time,
  formatted like the rest of the app ("14 Jul 2026 09:42"), with the header just "Last
  reviewed".
- The chip copy **"Due — no review recorded"** narrates; "Due" alone states the fact, and the
  adjacent empty date cell already says no review exists.
- Reason ergonomics: a bare single-line input with no cue about what belongs in it. For a
  review action the natural content is "access still appropriate" or a change note — one shared
  hint under the table would cover it.
- There is nothing to tell the operator *when* a review falls due (no due date, no cadence, no
  sort). The chip claims "Due" but the page cannot show why or since when. If review cadence is
  policy, the date it became due is the single most useful missing fact.
- Empty state ("No staff accounts are available for review.") is fine.

## 3. Performance, design and good practice

- **All row forms share one `Model.OperationKey`** (`<input ... value="@Model.OperationKey">`
  in the loop) while the Roles page generates a fresh key per row. Submitting a review for one
  person then another from the same rendered page re-uses the same idempotency key for two
  different operations — depending on how the handler treats replays, the second action could
  be swallowed. At minimum the two sibling pages disagree on the pattern; the per-row key is
  the correct one.
- The `<time datetime="...">` element is used correctly with the ISO value — good; only the
  human-readable rendering is wrong (UTC sorting format).
- The "Record reviewed" button carries `asp-page-handler="Review"` on the button rather than
  the form — works, but means the form's default action is unbound if the button attribute is
  ever lost; sibling pages bind the handler on the form.
- No pagination: fine for staff-scale data, and consistent with page 19/21.
- Single query, no scripts; no performance concerns.
