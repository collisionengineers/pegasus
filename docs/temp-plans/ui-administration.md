# UI Administration — task plan

Branch `task/ui-administration`. Pages 5-administration and 19–31.

## Defects closed

| Finding | Fix |
|---|---|
| Principals table prints a sequence-lineage GUID | Column removed — an internal identifier nobody can act on |
| "Create principal" renders an upload-arrow icon | A create action no longer wears an upload glyph |
| Access review shows UTC while the rest of the app is Europe/London | `OperatorLabels.OfficeTime` — the same instant now reads the same on every screen |
| Access review renders the `0001-01-01` sentinel as "Last reviewed" beside a `Recorded` chip | A default-valued timestamp is not a review; the row says "Not recorded" |
| Approved mailboxes join raw route-scope enum names | Humanised through the label map |
| Automation activity renders server-local time | Europe/London, like everything else |
| Automation activity prints raw `EventKind` / `Outcome` | Humanised |
| An over-length filter value returns `NotFound()` | A typo in a filter box is not a missing page; it is a validation message |
| Administration → Automation is permanently inert | The card is absent while the gate is off (rule 9), rather than leading to a page that can only say "not composed" |
| page-24 renders "Organization roles" twice and a caption repeating its own H1 | The duplicate legend and the repeated captions are visually hidden, kept for screen readers |
| Per-principal correlated `COUNT(*)` subquery | One grouped query for the page instead of up to 25 × 101 counts |
| Ledes and eyebrows across the admin screens | Removed |

## One judgment call

The Replace-principal lede carried a real **consequence** — what replacement
does and does not edit. Consequence guidance is exactly what the standards
keep, so it moved to sit with the control it concerns instead of being deleted
with the rest of the lede.

## Not done, and why

`AutomationMcpAuditor` composes the Reason column as
`"{ExceptionTypeName}: {message}"`, so the operator reads .NET type names. The
register is explicit that **a stable reason code is required writer-side
before the UI can label these** — labelling the exception string here would
invent a mapping from something that is not a stable code. It stays as
recorded, and the writer-side change is a Core task.

## Verification

- Core 441/441, architecture 73/73, integration 399 passed / 0 failed
