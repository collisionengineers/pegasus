# Wireframe — Organizations

```
+--------------------------------------------------------------------------------------------+
| COLLISION ENGINEERS  Pegasus   Dashboard Inbox Upload Queues Cases [Administration]  alex  |
+--------------------------------------------------------------------------------------------+
| Administration / Organizations                                                             |
|                                                                                            |
| H1  Organizations                                                                          |
|                                                                                            |
| (i) status card - post-action confirmation, only when present                              |
|                                                                                            |
| CURRENT ORGANIZATIONS                          |  CREATE ORGANIZATION                      |
| +--------------------------------------------+ |  +-------------------------------------+ |
| | NAME          ROLES        ACTIVE  ACTIONS | |  | Organization name                   | |
| |               PRINCIPALS                   | |  | [_________________________]         | |
| |--------------------------------------------| |  |                                     | |
| | Organisation  Work         3       Manage  | |  | [ ] Work Provider                   | |
| | One           Provider             roles · | |  | [ ] Instruction Intermediary        | |
| |                                    Create  | |  |  Roles are independent - a Work     | |
| |                                    princ.  | |  |  Provider owns principals; an       | |
| | Organisation  Instruction  0       Manage  | |  |  Instruction Intermediary passes    | |
| | Two           Intermediary         roles   | |  |  work through without becoming      | |
| +--------------------------------------------+ |  |  the principal.                     | |
|                                                |  |                                     | |
|  (pager only when >1 page:  < Previous         |  | [ Create organization ]             | |
|   Page 2   Next > )                            |  +-------------------------------------+ |
+--------------------------------------------------------------------------------------------+
```

## Legend

| Symbol | Meaning |
|---|---|
| `ACTIVE PRINCIPALS` | Count of active principals; when the load is capped a muted "Showing first 20" follows the count |
| `Manage roles` | Row link → organization edit (page 24) |
| `Create princ.` | "Create principal" link, Work Provider organizations only |
| `[ ]` | Role checkboxes; help sentence sits directly beneath (the deleted lede, compressed) |
| `[ Create organization ]` | Primary red action |
| `(i)` | Status card region, rendered only after an action |

Notes: no Version column (concurrency integer is internal); no lede; caption
screen-reader-only; pager hidden on a single page; empty state "No organizations yet. Create
the first one with the form on the right."
