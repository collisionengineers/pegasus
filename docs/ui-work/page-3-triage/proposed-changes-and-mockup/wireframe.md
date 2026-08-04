# Queues — wireframe

Pre-engineer-assignment case queues. 1280px+.

## Main state — Review tab active (one-click Confirm on each row)

```
+------------------------------------------------------------------------------------+
| COLLISION ENGINEERS | Pegasus   Dashboard Inbox Upload Queues* Cases Administration|
|                                                     alex · Change password · Sign out
+------------------------------------------------------------------------------------+
|                                                                                    |
|  Queues                                                                            |
|                                                                                    |
|  | Not ready 3 | Review 1 | Held 0 |          <- tabs with counts; Review active   |
|  ==============|=========|==========                                               |
|                                                                                    |
|  +------------------------------------------------------------------------------+ |
|  | CASE       REGISTRATION  CLAIMANT         PRINCIPAL    RECEIVED       ACTION  | |
|  |------------------------------------------------------------------------------| |
|  | Case 26003 GH78 JKL      Sample Claimant  Principal A  02 Aug 15:34 [Confirm] | |
|  +------------------------------------------------------------------------------+ |
|                                                                                    |
+------------------------------------------------------------------------------------+
```

## Not ready tab (waiting context in business words)

```
|  | Not ready 3 | Review 1 | Held 0 |                                               |
|  +------------------------------------------------------------------------------+ |
|  | CASE       REGISTRATION  CLAIMANT         PRINCIPAL    WAITING ON             | |
|  |------------------------------------------------------------------------------| |
|  | Case 26001 AB12 CDE      Sample Claimant  Principal A  Images missing         | |
|  | Case 26002 CD34 EFG      Sample Claimant  Principal A  Claim number missing   | |
|  | Case 26004 EF56 GHJ      Sample Claimant  Principal B  Images missing         | |
|  +------------------------------------------------------------------------------+ |
```

## Non-default state — Held tab, empty

```
|  | Not ready 3 | Review 1 | Held 0 |                                               |
|  +------------------------------------------------------------------------------+ |
|  |                            No cases are held.                                | |
|  +------------------------------------------------------------------------------+ |
```

## Legend

- `*` — active nav item.
- `| Tab N |` — tab with count; active tab underlined/filled. Counts match the Dashboard's
  Active cases tiles one-to-one.
- `[Confirm]` — one-click row action on Review rows only: confirms the case and passes it to
  engineer assignment.
- Empty states per tab: "No cases are waiting." / "No cases are ready to confirm." /
  "No cases are held." — never "No triage records match this view."
- The word "Triage" appears nowhere on this screen; it is reserved for the pre-case
  assessment type.
