# Wireframe — Manage staff account

```
+--------------------------------------------------------------------------------------------+
| COLLISION ENGINEERS  Pegasus   Dashboard Inbox Upload Queues Cases [Administration]  alex  |
+--------------------------------------------------------------------------------------------+
| Administration / Staff accounts / jane.smith                                               |
|                                                                                            |
| H1  Manage jane.smith                                                    [Enabled]         |
|                                                                                            |
| (i) status card - post-action confirmation, only when present                              |
|                                                                                            |
| ACCOUNT DETAIL                                |  ACCOUNT ACTION                            |
| +-------------------------------------------+ |  +--------------------------------------+ |
| | Password             Set                  | |  | Reason                               | |
| | Last access review   14 Jul 2026 09:42 -> | |  | [__________________________]         | |
| |                      (link to Access      | |  |  Kept on the administration record.  | |
| |                       review)             | |  |                                      | |
| +-------------------------------------------+ |  |  Disabling signs this person out     | |
|                                               |  |  everywhere and cannot be undone     | |
|                                               |  |  from this screen. The account stays | |
|                                               |  |  on the administration record.       | |
|                                               |  |                                      | |
|                                               |  | [ Disable account ]                  | |
|                                               |  +--------------------------------------+ |
|                                                                                            |
| DISABLED VARIANT of right panel:                                                           |
| +--------------------------------------+                                                   |
| | This account is disabled and stays   |                                                   |
| | on the administration record.        |                                                   |
| +--------------------------------------+                                                   |
+--------------------------------------------------------------------------------------------+
```

## Legend

| Symbol | Meaning |
|---|---|
| `[Enabled]` | Status chip in heading — the single statement of account status |
| `->` | "14 Jul 2026 09:42" links to the Access review page; London time, never raw UTC |
| `[ Disable account ]` | Primary red action; consequence sentence sits directly above it |
| `(i)` | Status card region, rendered only after an action |

Notes: duplicate "Status" detail row removed (heading chip owns it); "First password change"
becomes a "Password" fact (Set / Temporary chip); disabled variant replaces the form panel with
one sentence.
