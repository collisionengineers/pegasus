# Page 11 — Triage record — wireframe

## Main state (open record, finding not yet recorded)

```
+------------------------------------------------------------------------------------+
| ● COLLISION ENGINEERS | Pegasus   Dashboard  Inbox  Upload  Queues  [Cases]  Admin |
+------------------------------------------------------------------------------------+
|                                                                                    |
|  Cases › Triage › AB12 CDE                                                         |
|  H1  AB12 CDE   [Open]   Assigned to alex          ( Complete )^  ( More v )       |
|                                                                                    |
|  +--------------------------------------------+  +------------------------------+  |
|  | FINDING                                    |  | ORIGIN                       |  |
|  |--------------------------------------------|  |------------------------------|  |
|  | Roadworthiness                             |  | E-mail received              |  |
|  |  (o) Roadworthy   ( ) Unroadworthy         |  | 4 Aug 2026 08:55             |  |
|  |                                            |  | Sample Insurer Ltd           |  |
|  | Assessment                                 |  | ( View e-mail )              |  |
|  |  (o) Repairable   ( ) Total loss           |  +------------------------------+  |
|  |                                            |                                    |
|  | Reason                                     |  +------------------------------+  |
|  | [________________________________________] |  | HISTORY                      |  |
|  | [________________________________________] |  |------------------------------|  |
|  |                                            |  | Opened                       |  |
|  | (( Record finding ))                       |  |  4 Aug 2026 08:56            |  |
|  +--------------------------------------------+  | Assigned to alex             |  |
|                                                  |  4 Aug 2026 09:00 · alex     |  |
|  +--------------------------------------------+  +------------------------------+  |
|  | REPLY                                      |                                    |
|  |--------------------------------------------|                                    |
|  | Replies to the finding e-mail appear here  |                                    |
|  | automatically. Link the reply that answers |                                    |
|  | this record.                               |                                    |
|  |                                            |                                    |
|  | principal-claims@sample.example            |                                    |
|  | Re: AB12 CDE assessment · 5 Aug 09:12      |                                    |
|  |                        ( Link this reply ) |                                    |
|  +--------------------------------------------+                                    |
+------------------------------------------------------------------------------------+

^ "Complete" is disabled until a finding is recorded and a reply is linked.
```

## Alternate state (styled not-found page — the state every /Triage/{id} URL shows today)

```
+------------------------------------------------------------------------------------+
| ● COLLISION ENGINEERS | Pegasus   Dashboard  Inbox  Upload  Queues  [Cases]  Admin |
+------------------------------------------------------------------------------------+
|                                                                                    |
|                                                                                    |
|                      +--------------------------------------+                      |
|                      |                                      |                      |
|                      |   H1  Record not found               |                      |
|                      |                                      |                      |
|                      |   This record does not exist or      |                      |
|                      |   you do not have access to it.      |                      |
|                      |                                      |                      |
|                      |   ( Go to Cases )                    |                      |
|                      |                                      |                      |
|                      +--------------------------------------+                      |
|                                                                                    |
+------------------------------------------------------------------------------------+
```

## Legend

| Symbol | Meaning |
|---|---|
| `[Cases]` | Active nav item — Triage-type work lives under Cases in the new IA |
| `[Open]` | State chip, amber (open/pending semantics) |
| `(o) / ( )` | Radio options (Roadworthy/Unroadworthy, Repairable/Total loss) |
| `(( Record finding ))` | Primary (red) commitment action |
| `( Complete )` | Header action, secondary until enabled by finding + linked reply |
| `( More v )` | Menu holding Cancel, Await information, Reopen (state-dependent) |
| `( Link this reply )` | Per-row secondary action; opens a confirm dialog with reason |
| History lines | Event label · time · actor name — no version integers, no GUIDs |
