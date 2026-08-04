# Inbox and Upload — wireframes

The old single page splits into two surfaces. Both wireframes at 1280px+.

## Screen 1 — Inbox (main state)

```
+------------------------------------------------------------------------------------+
| COLLISION ENGINEERS | Pegasus   Dashboard Inbox* Upload Queues Cases Administration|
|                                                     alex · Change password · Sign out
+------------------------------------------------------------------------------------+
|                                                                                    |
|  Inbox                                                                             |
|                                                                                    |
|  [All 9] [Ready to review 3] [Needs sorting 1] [Blocked 1] [Vehicle images 2]      |
|                                                                                    |
|  +------------------------------------------------------------------------------+ |
|  | SENDER              SUBJECT                          RECEIVED       STATE     | |
|  |------------------------------------------------------------------------------| |
|  | claims@principal-a  Instruction — AB12 CDE           04 Aug 09:12  (Ready to  | |
|  |                                                                     review)   | |
|  | Sample Sender       Query on Case 26002              04 Aug 08:41  (Needs     | |
|  |                                                                     sorting)  | |
|  | Manual upload       sample-instruction.pdf           03 Aug 16:20  (Ready to  | |
|  |                                                                     review)   | |
|  | photos@principal-b  Vehicle images — CD34 EFG        03 Aug 14:05  (Vehicle   | |
|  |                                                                     images)   | |
|  | unknown@sample      Undeliverable notice             03 Aug 11:57  (Blocked)  | |
|  +------------------------------------------------------------------------------+ |
|                                                                                    |
|  Previous   Page 1 of 2   Next                                                     |
+------------------------------------------------------------------------------------+
```

### Inbox — empty state

```
|  [All 0] [Ready to review 0] [Needs sorting 0] [Blocked 0] [Vehicle images 0]      |
|  +------------------------------------------------------------------------------+ |
|  |                        No e-mail matches this view.                          | |
|  +------------------------------------------------------------------------------+ |
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
|  |          E-mail, document, PDF or image — up to 10 MB                        | |
|  |                                                                              | |
|  +------------------------------------------------------------------------------+ |
|                                     [ Upload ]                                     |
+------------------------------------------------------------------------------------+
```

### Upload — outcome states (each replaces/joins the zone after submit)

```
Success:
|  +--[green rail]-----------------------------------------------------------------+|
|  | (ok) sample-instruction.pdf received — Ready to review          View item ->  ||
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
- `[Chip N]` — filter chip with count; the active chip is filled.
- `(Ready to review)` etc. — state chips (navy = Ready to review, amber = Needs sorting,
  red = Blocked, neutral = Vehicle images); always icon/text, never colour alone.
- `(ok) (!) (x)` — status-card icons for success / caution / failure, with a coloured left
  rail on the card.
- Sender/subject replace raw stored filenames; hex names never render. Sizes, when relevant,
  in MB to one decimal.
