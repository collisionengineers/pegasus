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
|  | Not ready 3 | Review 1 | Held 0 | Triage 0 | <- tabs with counts; Review active  |
|  ==============|=========|=========|==========                                     |
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
|  | Not ready 3 | Review 1 | Held 0 | Triage 0 |                                    |
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
|  | Not ready 3 | Review 1 | Held 0 | Triage 0 |                                    |
|  +------------------------------------------------------------------------------+ |
|  |                            No cases are held.                                | |
|  +------------------------------------------------------------------------------+ |
```

## Triage tab — a different entity (pre-case; no case reference leads the row)

```
|  | Not ready 3 | Review 1 | Held 0 | Triage 2 |                                    |
|                                                                                    |
|  [Open 1] [Awaiting information 1] [Finding recorded 0]   <- sub-state chips;      |
|                                                              Completed/Cancelled   |
|                                                              reachable, not shown  |
|  +------------------------------------------------------------------------------+ |
|  | REGISTRATION  CLAIMANT        PRINCIPAL    STATE        WAITING ON    ASSIGNEE| |
|  |------------------------------------------------------------------------------| |
|  | AB12 CDE      Sample Claimant Principal A  Open         2 days        Alex    | |
|  | CD34 EFG      Sample Claimant Principal B  Awaiting     5 days        Unassig-| |
|  |                                            information                 ned    | |
|  +------------------------------------------------------------------------------+ |
```

### Triage tab, empty (the shipped reality until defect B2 is fixed)

```
|  | Not ready 3 | Review 1 | Held 0 | Triage 0 |                                    |
|  +------------------------------------------------------------------------------+ |
|  |                         No triage work is open.                              | |
|  +------------------------------------------------------------------------------+ |
```

## Legend

- `*` — active nav item.
- `| Tab N |` — tab with count; active tab underlined/filled. Counts match the Dashboard's
  Active cases tiles one-to-one.
- `[Confirm]` — one-click row action on Review rows only: confirms the case and passes it to
  engineer assignment.
- Empty states per tab: "No cases are waiting." / "No cases are ready to confirm." /
  "No cases are held." / "No triage work is open." — never "No triage records match this view."
- `[Open 1]` etc. — sub-state chips, shown only inside the Triage tab. The three case tabs have
  no sub-states; their tab *is* the stage.
- "Triage" names one tab and nothing else — not the screen, the nav item, the title, or the
  route. It carries its reserved meaning there: a pre-case staff workflow for a recorded matter
  requiring a finding, which is a different entity from a Case stage. That distinction is why
  it gets its own tab and its own row shape rather than a fourth stage chip.
