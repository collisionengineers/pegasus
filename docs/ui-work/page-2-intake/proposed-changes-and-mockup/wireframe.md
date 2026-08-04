# Wireframe — Inbox (pages 2 + 6 merged) and Upload

Inbox absorbs the former Email operations screen: one list of what arrived, with a direction
tab, a Failed filter, and retry in the row. Upload stays a separate surface. 1280px+.

## Screen 1 — Inbox, Received tab (main state)

```
+------------------------------------------------------------------------------------+
| COLLISION ENGINEERS | Pegasus   Dashboard Inbox* Upload Queues Cases Administration|
|                                                     alex · Change password · Sign out
+------------------------------------------------------------------------------------+
|                                                                                    |
|  Inbox                                                                             |
|                                                                                    |
|  | Received 9 | Sent 4 |                     <- direction tabs; Received active    |
|  =============|========                                                            |
|                                                                                    |
|  [All 9] [Needs sorting 1] [Blocked 1] [Vehicle images 2] [Failed 1]               |
|                                                                                    |
|  +------------------------------------------------------------------------------+ |
|  | SENDER              SUBJECT                    RECEIVED      MAILBOX   STATE  | |
|  |------------------------------------------------------------------------------| |
|  | claims@principal-a  Instruction - AB12 CDE     04 Aug 09:12  Instruc- (Case   | |
|  |                                                              tions    26001)  | |
|  | Sample Sender       Query on Case 26002        04 Aug 08:41  Instruc- (Needs  | |
|  |                                                              tions    sorting)| |
|  | claims@principal-b  Instruction - EF56 GHJ     04 Aug 08:02  Instruc- (Failed)| |
|  |   ~The last message from this mailbox                        tions    [Retry] | |
|  |    could not be processed.~                                                   | |
|  | Manual upload       sample-instruction.pdf     03 Aug 16:20  ~Mailbox (Case   | |
|  |                                                              not      26003)  | |
|  |                                                              recorded~        | |
|  | photos@principal-b  Vehicle images - CD34 EFG  03 Aug 14:05  Instruc- (Vehicle| |
|  |                                                              tions    images) | |
|  | unknown@sample      Undeliverable notice       03 Aug 11:57  Instruc- (Blocked| |
|  |                                                              tions            | |
|  +------------------------------------------------------------------------------+ |
|                                                                                    |
|  Previous   Page 1 of 2   Next                                                     |
+------------------------------------------------------------------------------------+
```

## Screen 1a — Retry, the two states (carried from page 6)

Row after first click on `[Retry]` — the inline confirm replaces the action cell:

```
|  | claims@principal-b  Instruction - EF56 GHJ     04 Aug 08:02  Instruc- (Failed)| |
|  |   ~The last message from this mailbox          Retry processing for this      | |
|  |    could not be processed.~                    item?  [[Retry]] [Cancel]      | |
```

Row after the post succeeds — and identically on replay:

```
|  | claims@principal-b  Instruction - EF56 GHJ     04 Aug 08:02  Instruc- (Failed)| |
|  |   ~The last message from this mailbox                        tions   (Retry   | |
|  |    could not be processed.~                                          scheduled)| |
```

## Screen 1b — Inbox, Sent tab

```
|  | Received 9 | Sent 4 |                                                          |
|                                                                                    |
|  +------------------------------------------------------------------------------+ |
|  | RECIPIENT           SUBJECT                    SENT         MAILBOX  WHERE    | |
|  |------------------------------------------------------------------------------| |
|  | provider@principal- Report - AB12 CDE          04 Aug 14:12 Instruc- <Open    | |
|  | a.example                                                   tions    case     | |
|  |                                                                      26001>   | |
|  | claims@principal-b  Information request        04 Aug 13:05 Desk      ~-~     | |
|  +------------------------------------------------------------------------------+ |
```

## Screen 1c — Inbox empty states

```
Filter All:      |  |                  No e-mail matches this view.                | |
Filter Failed:   |  |                Nothing has failed to arrive.                 | |
Sent tab:        |  |               Nothing has been sent recently.                | |
```

## Screen 2 — Upload (main state)

```
+------------------------------------------------------------------------------------+
| COLLISION ENGINEERS | Pegasus   Dashboard Inbox Upload* Queues Cases Administration|
|                                                     alex · Change password · Sign out
+------------------------------------------------------------------------------------+
|                                                                                    |
|  Upload                                                                            |
|                                                                                    |
|  +------------------------------------------------------------------------------+ |
|  |                                                                              | |
|  |                            [^ upload icon]                                   | |
|  |                    Drag a file here, or browse                               | |
|  |          E-mail, document, PDF or image - up to 10 MB                        | |
|  |                                                                              | |
|  +------------------------------------------------------------------------------+ |
|                                     [ Upload ]                                     |
+------------------------------------------------------------------------------------+
```

### Upload — outcome states (each replaces/joins the zone after submit)

```
Success, definitive:
|  +--[green rail]-----------------------------------------------------------------+|
|  | (ok) sample-instruction.pdf received - Case 26003 created       Open case ->  ||
|  +-------------------------------------------------------------------------------+|

Success, not definitive:
|  +--[amber rail]-----------------------------------------------------------------+|
|  | (!) sample-instruction.pdf received - Needs sorting             View item ->  ||
|  +-------------------------------------------------------------------------------+|

Duplicate:
|  +--[amber rail]-----------------------------------------------------------------+|
|  | (!) This file was already received on 3 Aug 2026.                             ||
|  |     No duplicate was created.                          View existing item ->  ||
|  +-------------------------------------------------------------------------------+|

Too large:
|  +--[red rail]-------------------------------------------------------------------+|
|  | (x) This file is 24.8 MB. Files must be 10 MB or smaller.                     ||
|  +-------------------------------------------------------------------------------+|

Failure:
|  +--[red rail]-------------------------------------------------------------------+|
|  | (x) The file could not be processed. Try again, or contact an                 ||
|  |     administrator if it keeps failing.                                        ||
|  +-------------------------------------------------------------------------------+|
```

## Legend

- `*` — active nav item.
- `| Tab N |` — direction tab with count; the active tab is underlined.
- `[Chip N]` — filter chip with count; the active chip is filled. Filters apply to the
  Received tab only — Sent has no business-state filters.
- `(Case 26001)` etc. — state chips: green = case created (linked), amber = Needs sorting /
  Needs text extraction / Pending, red = Blocked / Failed, neutral = Vehicle images. Always
  icon/text, never colour alone. There is no pending-draft chip: definitive intake creates the
  case at processing time (`../../defects-and-non-functional.md` §B4). There is no "Succeeded"
  chip either — a succeeded item is described by what it became.
- `[Retry]` — row action on Failed rows only; confirms inline before firing, then becomes a
  "Retry scheduled" chip. Replay renders identically.
- `~Mailbox not recorded~` — muted fallback, never styled as a real mailbox name.
- `(ok) (!) (x)` — status-card icons for success / caution / failure, with a coloured left rail.
- Sender/subject replace raw stored filenames; hex names never render. Sizes, when relevant, in
  MB to one decimal. Times are local `dd MMM HH:mm` with an ISO `<time datetime>` attribute.
