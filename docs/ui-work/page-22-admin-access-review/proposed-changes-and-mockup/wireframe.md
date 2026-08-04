# Wireframe — Access review

```
+--------------------------------------------------------------------------------------------+
| COLLISION ENGINEERS  Pegasus   Dashboard Inbox Upload Queues Cases [Administration]  alex  |
+--------------------------------------------------------------------------------------------+
| Administration / Access review                                                             |
|                                                                                            |
| H1  Access review                                                                          |
|                                                                                            |
| (i) status card - post-action confirmation, only when present                              |
|                                                                                            |
| +----------------------------------------------------------------------------------------+ |
| | USERNAME    STATUS    ROLES     LAST REVIEWED      REVIEW   REASON                     | |
| |----------------------------------------------------------------------------------------| |
| | jane.smith  [Enabled] Admin.    14 Jul 2026 09:42  {Rev}    [________] [Record reviewed]| |
| | mark.taylor [Enabled] Engineer  Not yet reviewed   {Due}    [________] [Record reviewed]| |
| | sam.reeves  [Disab.]  User      2 Feb 2026 16:05   {Rev}    [________] [Record reviewed]| |
| +----------------------------------------------------------------------------------------+ |
|  Reasons are kept on the administration record.                                            |
+--------------------------------------------------------------------------------------------+
```

## Legend

| Symbol | Meaning |
|---|---|
| `[Enabled]` / `[Disab.]` | Status chips (green / muted) |
| `LAST REVIEWED` | London time, app-standard format "d MMM yyyy HH:mm" — never UTC sorting strings, never a minimum-value date |
| `Not yet reviewed` | Muted text when no genuine review instant exists (includes guarding `0001-01-01` defaults) |
| `{Rev}` | Chip "Reviewed", green |
| `{Due}` | Chip "Due", amber |
| `[________]` | Per-row Reason input, required, screen-reader label "Reason for {username}" |
| `[Record reviewed]` | Per-row primary action, compact; each row has its own operation key |
| `(i)` | Status card region, rendered only after an action |

Notes: caption screen-reader-only; chip column header shortens to "Review"; single-line row
form keeps rows compact.
