# Automation activity — wireframe

Proposed layout at 1280px+. Six columns, a one-line filter bar attached to the table it
filters, labelled values throughout, and a pager that states position.

## Main state — all activity

```
+---------------------------------------------------------------------------------------------+
| COLLISION ENGINEERS | Pegasus      Dashboard Inbox Upload Queues Cases Administration*       |
|                                                          alex · Change password · Sign out   |
+---------------------------------------------------------------------------------------------+
|  Administration / Automation / Automation activity                                           |
|                                                                                              |
|  Automation activity                                                                         |
|                                                                                              |
|  Show [ All activity  v ]   Reference [ ............ ]  [ Filter ]                           |
|  +----------------------------------------------------------------------------------------+ |
|  | WHEN             | WHAT HAPPENED        | RELATED TO   | OUTCOME  | REFERENCE | REASON   | |
|  |------------------+----------------------+--------------+----------+-----------+----------| |
|  | 04 Aug 2026 14:32| Added a document     | Case 26001   | [Done]   | OP-4821-K | —        | |
|  | 04 Aug 2026 14:31| Started a case edit  | Case 26001   | [Done]   | OP-4821-J | —        | |
|  | 04 Aug 2026 14:28| Searched cases       | —            | [Done]   | OP-4820-B | —        | |
|  | 04 Aug 2026 13:55| Area not permitted   | —            | [Refused]| T-9F2C    | Documents| |
|  |                  |                      |              |          |           | are not  | |
|  |                  |                      |              |          |           | permitted| |
|  | 04 Aug 2026 13:54| Opened a case        | Case 26001   | [Failed] | OP-4802-A | The case | |
|  |                  |                      |              |          |           | could not| |
|  |                  |                      |              |          |           | be opened| |
|  | 04 Aug 2026 11:02| Credentials rejected | —            | [Refused]| T-9E10    | Sign-in  | |
|  |                  |                      |              |          |           | details  | |
|  |                  |                      |              |          |           | not valid| |
|  | 03 Aug 2026 17:40| Downloaded a document| AB12 CDE     | [Done]   | OP-4790-C | —        | |
|  | 03 Aug 2026 17:12| Automation turned off| —            | [Refused]| T-9C88    | Automation| |
|  |                  |                      |              |          |           | was off  | |
|  +----------------------------------------------------------------------------------------+ |
|   Showing 1–50 · Newest first                                    [ Previous ]  [ Next ]      |
+---------------------------------------------------------------------------------------------+
```

## Alternate state — filtered to one reference, no match

```
|  Automation activity                                                                         |
|                                                                                              |
|  Show [ All activity  v ]   Reference [ OP-4821-K ]  [ Filter ]  Clear                       |
|  +----------------------------------------------------------------------------------------+ |
|  |                                                                                        | |
|  |   No activity matches reference OP-4821-K.                                             | |
|  |                                                                                        | |
|  |   [ Show all activity ]                                                                | |
|  |                                                                                        | |
|  +----------------------------------------------------------------------------------------+ |
```

## Alternate state — nothing recorded yet

```
|  +----------------------------------------------------------------------------------------+ |
|  |                                                                                        | |
|  |   No automation activity has been recorded yet.                                        | |
|  |                                                                                        | |
|  +----------------------------------------------------------------------------------------+ |
|  (no pager)                                                                                  |
```

## Alternate state — reference too long

```
|  Show [ All activity  v ]   Reference [ xxxxxxxxxxxxxx ]  [ Filter ]                         |
|                             (!) That reference is too long.                                  |
|  (table renders unchanged behind the message — no 404)                                       |
```

## Legend

- `*` — active nav item (red underline).
- Breadcrumb `Administration / Automation / Automation activity` replaces the eyebrow and
  the "Back to Automation" link.
- `WHEN` — Europe/London, `dd MMM yyyy HH:mm`, tabular numerals, seconds on hover only.
- `WHAT HAPPENED` — operator label; raw tool names and security-event type values never
  render. Denial rows read as denials without needing a separate Record column.
- `RELATED TO` — business reference (Case number, registration) or `—`; never an internal
  identifier.
- `[Done]` — green chip; `[Failed]` — charcoal chip; `[Refused]` — amber chip. Label always
  present, never colour-only.
- `REFERENCE` — the value, linked; clicking it filters the record to that one operation.
  This is the existing behaviour and the best thing on the current screen.
- `REASON` — labelled denial reason or plain failure sentence; wraps to two lines rather
  than truncating. Never a reason code, never an exception type name.
- `Show` — record-type filter: All activity / Actions only / Refused requests.
- `Clear` — present only when a filter is applied.
- Pager — position on the left, `Previous`/`Next` on the right; each is absent, not
  disabled-and-visible, at the ends of the record.
- `(!)` — inline field validation, amber; replaces the current raw 404 on an over-length
  value.
