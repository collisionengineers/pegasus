# Wireframe — Staff roles

```
+--------------------------------------------------------------------------------------------+
| COLLISION ENGINEERS  Pegasus   Dashboard Inbox Upload Queues Cases [Administration]  alex  |
+--------------------------------------------------------------------------------------------+
| Administration / Staff roles                                                               |
|                                                                                            |
| H1  Staff roles                                                                            |
|                                                                                            |
| /!\ Saving signs that person out everywhere, and the last enabled Administrator always     |
|     keeps the Administrator role.                                                          |
|                                                                                            |
| CURRENT ROLE ASSIGNMENTS                                                                   |
| +----------------------------------------------------------------------------------------+ |
| | USERNAME     STATUS     ROLES                              REASON           |          | |
| |----------------------------------------------------------------------------------------| |
| | jane.smith   [Enabled]  [x]Administrator* [ ]Eng [ ]User   [_________]  [Save roles]   | |
| |                          * checked+disabled "Last Administrator"                       | |
| | mark.taylor  [Enabled]  [ ]Administrator [x]Eng [ ]User    [_________]  [Save roles]   | |
| | sam.reeves   [Disab.]   [ ]Administrator [ ]Eng [x]User    [_________]  [Save roles]   | |
| +----------------------------------------------------------------------------------------+ |
|  Reasons are kept on the administration record.                                            |
+--------------------------------------------------------------------------------------------+
```

## Legend

| Symbol | Meaning |
|---|---|
| `/!\` | Attention card, amber trio — the single sentence of consequence copy on the page |
| `[Enabled]` / `[Disab.]` | Status chips (green / muted) |
| `[x]` / `[ ]` | Role checkboxes — checkbox state IS the current-roles display (text column removed) |
| `*` | Last enabled Administrator: checkbox checked + disabled, adjacent text "Last Administrator" |
| `[_________]` | Per-row Reason input, required, max 1000 chars |
| `[Save roles]` | Per-row primary action; per-row operation key retained |

Notes: per-row legend "Roles for {username}" is screen-reader-only; table caption
screen-reader-only; one uppercase section label; hint sentence appears once under the table,
not per row.
