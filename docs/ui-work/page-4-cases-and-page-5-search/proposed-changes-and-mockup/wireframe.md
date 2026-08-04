# Cases (merged with Search) — wireframe

Single page absorbing the old Search. Filter bar anchored at the top; results directly below.
1280px+.

## Main state — results with proper stage chips

```
+------------------------------------------------------------------------------------+
| COLLISION ENGINEERS | Pegasus   Dashboard Inbox Upload Queues Cases* Administration|
|                                                     alex · Change password · Sign out
+------------------------------------------------------------------------------------+
|                                                                                    |
|  Cases                                                                             |
|                                                                                    |
|  +------------------------------------------------------------------------------+ |
|  | [ Case, PO, registration, claimant... ] [Case stage v] [Principal v]         | |
|  |                                  [More filters v]  [ Search ]  Clear         | |
|  +------------------------------------------------------------------------------+ |
|     (More filters, when open: Received from/to · Instruction date · Engineer ·     |
|      Origin · Record type: Cases / Vehicle images)                                 |
|                                                                                    |
|  +------------------------------------------------------------------------------+ |
|  | CASE/PO   REG       CLAIMANT        CLAIM No  PRINCIPAL  STAGE      ENGINEER | |
|  |                                                          RECEIVED   ORIGIN   | |
|  |------------------------------------------------------------------------------| |
|  | Case 26001 AB12 CDE Sample Claimant CLM-0001  Principal A (Review)  Unassigned |
|  |                                                          01 Aug 14:02 E-mail | |
|  | Case 26002 CD34 EFG Sample Claimant CLM-0002  Principal A (Report   S. Engineer|
|  |                                                prep'n — navy) 31 Jul  E-mail | |
|  | Case 26003 GH78 JKL Sample Claimant CLM-0003  Principal B (Not ready) Unassigned|
|  |                                                          30 Jul     Upload  | |
|  | Case 25990 KL90 MNP Sample Claimant CLM-0004  Principal B (Report    S. Engineer|
|  |                                                complete — green) 28 Jul E-mail |
|  +------------------------------------------------------------------------------+ |
|                                                                                    |
|  Previous   Page 1   Next                                                          |
+------------------------------------------------------------------------------------+
```

## Non-default state — no matches

```
|  +------------------------------------------------------------------------------+ |
|  | [ AB99 XYZ                        ] [Case stage v] [Principal v]             | |
|  |                                  [More filters v]  [ Search ]  Clear         | |
|  +------------------------------------------------------------------------------+ |
|                                                                                    |
|  +------------------------------------------------------------------------------+ |
|  |                     No cases match these filters.                            | |
|  |                          [ Clear filters ]                                   | |
|  +------------------------------------------------------------------------------+ |
```

## Legend

- `*` — active nav item.
- `[ … ]` — text input (keyword box absorbs Case/PO, registration, claimant, claim number and
  free text for the common path).
- `[Case stage v]` — dropdown with human labels only: All stages / Not ready / Review / Held /
  Report preparation / Report complete / Rejected. Raw enum values never render.
- `[Principal v]` — dropdown: All principals / Principal A / Principal B / … (never free text).
- `[More filters v]` — disclosure; holds date range, instruction date, engineer, origin,
  record type. Closed by default.
- `(Stage)` — labelled stage chip: amber = Not ready / Held, navy = Review / Report
  preparation, green = Report complete, red = Rejected; text always present.
- Engineer column shows a display name or "Unassigned" — never a GUID.
- `/Search` retires; its URL redirects here with the query preserved.
