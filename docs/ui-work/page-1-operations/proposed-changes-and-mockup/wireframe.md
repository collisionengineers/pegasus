# Dashboard — wireframe

Proposed layout at 1280px+. Every metric tile is a link to the exact filtered list behind it.

## Main state (Engineer account signed in)

```
+------------------------------------------------------------------------------------+
| COLLISION ENGINEERS | Pegasus   Dashboard* Inbox Upload Queues Cases Administration|
|                                                     alex · Change password · Sign out
+------------------------------------------------------------------------------------+
|                                                                                    |
|  Dashboard                                        Updated 4 Aug 2026 10:43  (R)    |
|                                                                                    |
|  ACTIVE CASES                                                                      |
|  +----------------+  +----------------+  +----------------+                        |
|  | Not ready      |  | Review         |  | Held           |                        |
|  | 3              |  | 1              |  | 0              |                        |
|  +----------------+  +----------------+  +----------------+                        |
|                                                                                    |
|  E-MAIL ACTIVITY                                                                   |
|  +----------------+  +--------------------+  +----------------+                    |
|  | Received today |  | Queries outstanding|  | Needs sorting  |                    |
|  | 5              |  | 2                  |  | 1              |                    |
|  +----------------+  +--------------------+  +----------------+                    |
|                                                                                    |
|  TODAY AND THIS WEEK                                                               |
|  +--------------+ +--------------+ +--------------+ +------------+ +------------+  |
|  | New cases    | | Sent to      | | Sent to      | | Reports    | | Reports    |  |
|  | today        | | Engineer     | | Engineer     | | sent today | | sent this  |  |
|  |              | | today        | | this week    | |            | | week       |  |
|  | 2            | | 1            | | 4            | | 0          | | 3          |  |
|  +--------------+ +--------------+ +--------------+ +------------+ +------------+  |
|                                                                                    |
|  TO DO                                   <- Engineer accounts only; absent for     |
|  +------------------------------------+     other roles (not disabled/greyed)      |
|  | [Report] Case 26001 · AB12 CDE ·   |                                            |
|  |          Principal A    Due 5 Aug  |                                            |
|  | [Report] Case 26004 · EF56 GHJ ·   |                                            |
|  |          Principal B    Due 7 Aug  |                                            |
|  | [Query]  Case 26002 · Sample       |                                            |
|  |          Claimant   Rcvd 4 Aug     |                                            |
|  +------------------------------------+                                            |
+------------------------------------------------------------------------------------+
```

## Non-default state — a real query fails at runtime

Only the affected tile changes; the rest of the page stays live. No "Unavailable"
placeholder pills anywhere: a tile exists only if its query exists, and `0` renders as `0`.

```
|  ACTIVE CASES                                                                      |
|  +----------------+  +---------------------+  +----------------+                   |
|  | Not ready      |  | Review              |  | Held           |                   |
|  | 3              |  | [!] Failed          |  | 0              |                   |
|  |                |  | last good 10:43     |  |                |                   |
|  +----------------+  +---------------------+  +----------------+                   |
```

## Legend

- `*` — active nav item (red underline).
- `(R)` — compact refresh icon-button beside the "Updated …" timestamp (corner element,
  no full-width banner, no "Current" badge).
- `UPPERCASE` — section label (one per section, the only uppercase text on the page).
- `[Report]` / `[Query]` — small type chips on To do rows.
- `[!] Failed` — designed failure chip (red) with last-good timestamp; only for genuine
  runtime failure of a real query.
- Boxes in metric strips are whole-tile links to the exact filtered list behind the count
  (Active cases → Queues tabs; E-mail activity → Inbox filters).
