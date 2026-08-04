# Page 10 — Image reference detail — wireframe

## Main state (awaiting instruction, one case candidate)

```
+------------------------------------------------------------------------------------+
| ● COLLISION ENGINEERS | Pegasus   Dashboard  [Inbox]  Upload  Queues  Cases  Admin |
+------------------------------------------------------------------------------------+
|                                                                                    |
|  Inbox › Vehicle images › AB12CDE-01                                               |
|  H1  Image reference AB12CDE-01   [Awaiting instruction]                           |
|                                                                                    |
|  +----------------------------------+  +----------------------------------------+ |
|  | RECORD                           |  | ORIGIN                                 | |
|  |----------------------------------|  |----------------------------------------| |
|  | Registration      AB12 CDE       |  | From a manual upload                   | |
|  | Received          4 Aug 2026     |  | received 4 Aug 2026 14:01              | |
|  |                   14:02          |  |                                        | |
|  | Linked case       Not linked     |  | ( View original upload )               | |
|  +----------------------------------+  +----------------------------------------+ |
|                                                                                    |
|  +------------------------------------------------------------------------------+ |
|  | MATCHING OPEN CASES                                                          | |
|  |------------------------------------------------------------------------------| |
|  | Case 26001   Principal A   AB12 CDE      ( Open case )  (( Link to this case ))|
|  |------------------------------------------------------------------------------| |
|  | i Linking keeps this image reference permanently; it can be reversed          | |
|  |   before the report is sent.                                                  | |
|  +------------------------------------------------------------------------------+ |
|                                                                                    |
|  +------------------------------------------------------------------------------+ |
|  | REGISTRATION READINGS                                                        | |
|  |------------------------------------------------------------------------------| |
|  | AB12 CDE                    High confidence            4 Aug 2026            | |
|  | No registration readable    One image                  4 Aug 2026            | |
|  +------------------------------------------------------------------------------+ |
+------------------------------------------------------------------------------------+
```

## Alternate state (linked to a case)

```
+------------------------------------------------------------------------------------+
|  Inbox › Vehicle images › AB12CDE-01                                               |
|  H1  Image reference AB12CDE-01   [Linked to Case 26001]                           |
|                                                                                    |
|  +----------------------------------+  +----------------------------------------+ |
|  | RECORD                           |  | ORIGIN                                 | |
|  |----------------------------------|  |----------------------------------------| |
|  | Registration      AB12 CDE       |  | From a manual upload                   | |
|  | Received          4 Aug 2026     |  | received 4 Aug 2026 14:01              | |
|  |                   14:02          |  |                                        | |
|  | Linked case       Case 26001 ->  |  | ( View original upload )               | |
|  +----------------------------------+  +----------------------------------------+ |
|                                                                                    |
|  +------------------------------------------------------------------------------+ |
|  | LINK                                                                         | |
|  |------------------------------------------------------------------------------| |
|  | Linked to Case 26001 on 4 Aug 2026 by alex.        ( Unlink from this case ) | |
|  | i Unlinking needs a reason and is recorded; it is only possible before       | |
|  |   the report is sent.                                                        | |
|  +------------------------------------------------------------------------------+ |
|                                                                                    |
|  | REGISTRATION READINGS |  (unchanged)                                          |
+------------------------------------------------------------------------------------+
```

## Legend

| Symbol | Meaning |
|---|---|
| `[Inbox]` | Active nav item |
| `[Awaiting instruction]` | State chip, amber (pending) |
| `[Linked to Case 26001]` | State chip, neutral |
| `( Open case )` | Secondary button |
| `(( Link to this case ))` | Primary (red) button — the page's one commitment action; opens a confirm dialog that requires a reason |
| `i …` | Single inline consequence line, placed next to the control it concerns |
| `Case 26001 ->` | Link to the case detail page |
