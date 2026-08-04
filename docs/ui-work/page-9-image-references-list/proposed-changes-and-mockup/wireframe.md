# Page 9 — Vehicle images list — wireframe

## Main state (rows present, "All" filter)

```
+------------------------------------------------------------------------------------+
| ● COLLISION ENGINEERS | Pegasus   Dashboard  [Inbox]  Upload  Queues  Cases  Admin |
|                                                     alex · Change password · Sign out|
+------------------------------------------------------------------------------------+
|                                                                                    |
|  Inbox › Vehicle images                                                            |
|  H1  Vehicle images                                                                |
|                                                                                    |
|  [ Registration or image reference____________________ ]  (Search)                 |
|                                                                                    |
|  (All 3)*  (Awaiting instruction 2)  (Linked to a case 1)                          |
|                                                                                    |
|  +------------------------------------------------------------------------------+ |
|  | IMAGE REFERENCE   REGISTRATION    RECEIVED (newest first)   STATE            | |
|  |------------------------------------------------------------------------------| |
|  | AB12CDE-01        AB12 CDE        4 Aug 2026                [Awaiting instr.]| |
|  | AB12CDE-02        AB12 CDE        4 Aug 2026                [Awaiting instr.]| |
|  | CD34EFG-01        CD34 EFG        3 Aug 2026                [Linked: Case    | |
|  |                                                              26001]          | |
|  +------------------------------------------------------------------------------+ |
|                                                                                    |
+------------------------------------------------------------------------------------+
```

## Alternate state (search returns nothing)

```
+------------------------------------------------------------------------------------+
| ● COLLISION ENGINEERS | Pegasus   Dashboard  [Inbox]  Upload  Queues  Cases  Admin |
+------------------------------------------------------------------------------------+
|                                                                                    |
|  Inbox › Vehicle images                                                            |
|  H1  Vehicle images                                                                |
|                                                                                    |
|  [ ZZ99 ZZZ_____________________________________ ]  (Search)                       |
|                                                                                    |
|  (All 3)*  (Awaiting instruction 2)  (Linked to a case 1)                          |
|                                                                                    |
|  +------------------------------------------------------------------------------+ |
|  |                                                                              | |
|  |            No vehicle images match this search.                              | |
|  |            ( Clear search )                                                  | |
|  |                                                                              | |
|  +------------------------------------------------------------------------------+ |
|                                                                                    |
+------------------------------------------------------------------------------------+
```

## Legend

| Symbol | Meaning |
|---|---|
| `[Inbox]` | Active nav item (red underline in both mockup systems) |
| `(All 3)*` | Filter chip; `*` marks the selected chip (`aria-current`), number is a live count |
| `[Awaiting instr.]` | State chip, amber (pending) semantics |
| `[Linked: Case 26001]` | State chip, neutral, naming the linked case reference |
| `( Search )` / `( Clear search )` | Secondary buttons — no red on this page |
| Whole table row | One link to the image-reference detail (page 10) |
