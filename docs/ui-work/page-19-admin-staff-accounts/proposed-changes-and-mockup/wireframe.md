# Wireframe — Staff accounts

```
+--------------------------------------------------------------------------------------------+
| COLLISION ENGINEERS  Pegasus   Dashboard Inbox Upload Queues Cases [Administration]  alex  |
+--------------------------------------------------------------------------------------------+
| Administration / Staff accounts                                                            |
|                                                                                            |
| H1  Staff accounts                                                                         |
|                                                                                            |
| (i) status card - post-action confirmation, success or failure variant, only when present  |
|                                                                                            |
| CURRENT ACCOUNTS                              |  CREATE STAFF ACCOUNT                      |
| +-------------------------------------------+ |  +--------------------------------------+ |
| | USERNAME    STATUS    ROLES     PASSWORD  | |  | Username                             | |
| |-------------------------------------------| |  | [__________________________]         | |
| | jane.smith  [Enabled] Admin.    Set  Mng> | |  | Temporary password                   | |
| | mark.taylor [Enabled] Engineer  {Temp} >  | |  | [__________________________]         | |
| | sam.reeves  [Disab.]  User      Set  Mng> | |  |  At least eight characters. The      | |
| +-------------------------------------------+ |  |  staff member must replace it at     | |
|                                               |  |  first sign-in.                      | |
|  EMPTY STATE (when no rows):                  |  | Reason                               | |
|  "No staff accounts yet. Create the first     |  | [__________________________]         | |
|   account with the form on the right."        |  |  Kept on the administration record.  | |
|                                               |  |                                      | |
|                                               |  | [ Create account ]                   | |
|                                               |  +--------------------------------------+ |
+--------------------------------------------------------------------------------------------+
```

## Legend

| Symbol | Meaning |
|---|---|
| `[Enabled]` | Status chip, green outline (hardened) / green tint (refreshed) |
| `[Disab.]` | Status chip "Disabled", muted grey |
| `{Temp}` | Password chip "Temporary", amber trio |
| `Set` | Plain text — password replaced by the user |
| `Mng>` | Row link "Manage" → staff account edit (page 20); column header is screen-reader-only |
| `[ Create account ]` | Primary action, red, the only saturated element |
| `(i)` | Status card region, rendered only after an action |

Notes: breadcrumb replaces eyebrow + back-link; table caption is screen-reader-only; exactly two
uppercase section labels on the page.
