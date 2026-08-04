# Page 12 — Case detail — wireframe

## Main state (read-only, Review stage — whole page)

```
+--------------------------------------------------------------------------------------+
| ● COLLISION ENGINEERS | Pegasus   Dashboard  Inbox  Upload  Queues  [Cases]  Admin   |
+--------------------------------------------------------------------------------------+
|  Cases › 26001                                                                       |
|  H1  Case 26001   [Review]      Principal A · AB12 CDE · Sample Claimant   ( Edit )  |
|--------------------------------------------------------------------------------------|
|  | Overview* | Actions | Documents (2) | History (4) |          <- sticky section nav |
|--------------------------------------------------------------------------------------|
|                                                                                      |
|  OVERVIEW                                                                            |
|  +---------------------------------------+  +-------------------------------------+  |
|  | CASE                                  |  | INSTRUCTION                         |  |
|  | Reference        26001                |  | Received         4 Aug 2026 16:30   |  |
|  | Case type        Inspection           |  | Instruction date 4 Aug 2026         |  |
|  | Principal        Principal A          |  | Origin           Manual upload      |  |
|  | Engineer         Unassigned           |  | Claim number     Not recorded       |  |
|  +---------------------------------------+  +-------------------------------------+  |
|                                                                                      |
|  +--------------------------------------------------------------------------------+  |
|  | CASE DATA                                          ( Show all 18 fields )      |  |
|  |--------------------------------------------------------------------------------|  |
|  | Registration      AB12 CDE (confirmed)                                         |  |
|  | Incident date     Not recorded   Suggested: 27 Feb 2025 — from the             |  |
|  |                                  instruction PDF          ( Accept )+          |  |
|  | Inspection mode   Image-based assessment (confirmed)                           |  |
|  | Instruction date  4 Aug 2026     Suggested from receipt date                   |  |
|  +--------------------------------------------------------------------------------+  |
|                                                                                      |
|  ACTIONS                                                                             |
|  +--------------------------------------------------------------------------------+  |
|  | Progress:  ( Send to report preparation )  ( Assign engineer )                 |  |
|  | Hold:      ( Hold case )                                                       |  |
|  | Closure:   ( Close case )  ( Create linked replacement )  ( More v )           |  |
|  |  i Each action asks for a reason before it is recorded.                        |  |
|  |  (Only actions valid for the Review stage are shown.)                          |  |
|  +--------------------------------------------------------------------------------+  |
|                                                                                      |
|  DOCUMENTS                                                                           |
|  +--------------------------------------------------------------------------------+  |
|  | [ ] FILE                TYPE            CUSTODY      SIZE     ADDED       ...  |  |
|  | [ ] photos-front.jpg    Vehicle image   [Confirmed]  2.4 MB   4 Aug 2026  (v)  |  |
|  | [ ] instruction.pdf     Instruction     [Confirmed]  1.1 MB   4 Aug 2026  (v)  |  |
|  |                                              ( Export selected ) ( Add doc. )  |  |
|  +--------------------------------------------------------------------------------+  |
|                                                                                      |
|  HISTORY                                                                             |
|  +--------------------------------------------------------------------------------+  |
|  | Sent to Review        alex          4 Aug 2026 16:41   "Instruction reviewed"  |  |
|  | Case data saved       alex          4 Aug 2026 16:38   "Confirmed inspection"  |  |
|  | Case created          [Automation]  4 Aug 2026 16:30   "Received instruction"  |  |
|  +--------------------------------------------------------------------------------+  |
+--------------------------------------------------------------------------------------+
```

## Alternate state (editing; "Close case" dialog open)

```
|  H1  Case 26001   [Review]   Principal A · AB12 CDE   [Editing]  ( Finish editing )  |
|      quiet microcopy under header: Changes save one at a time.                       |
|                                                                                      |
|         +------------------------------------------------------------+              |
|         |  Close case                                          ( x ) |              |
|         |------------------------------------------------------------|              |
|         |  Outcome                                                   |              |
|         |  [ Post-report complete            v ]                     |              |
|         |    (Post-report complete / Provider cancelled /            |              |
|         |     Collision Engineers rejected)                          |              |
|         |                                                            |              |
|         |  Reason                                                    |              |
|         |  [__________________________________________________]      |              |
|         |                                                            |              |
|         |  i Closing ends work on this case. It can be reopened      |              |
|         |    with a reason.                                          |              |
|         |                                                            |              |
|         |                       ( Cancel )   (( Close case ))        |              |
|         +------------------------------------------------------------+              |
|                                                                                      |
|  (page dimmed behind the dialog; contended edit shows instead:                       |
|   "Sample Colleague is editing this case" and a disabled Edit button)                |
```

## Legend

| Symbol | Meaning |
|---|---|
| `[Cases]` | Active nav item |
| `[Review]` | Stage chip — navy (Review semantics); other stages: amber pending, green confirmed-completion only |
| `[Confirmed]` | Custody chip in Documents |
| `[Editing]` | Edit-state chip shown only while editing |
| `( Edit )` / `( Finish editing )` | The single edit toggle; replaces all lease narration |
| `( Accept )+` | Inline suggestion acceptance — opens a small confirm with reason |
| `( More v )` | Menu for rare actions (Reopen, Archive) |
| `(v)` | Per-row menu: Download / Remove… / Mark third-party vehicle… |
| `(( Close case ))` | Primary (red) commitment inside the dialog only |
| `i …` | One consequence line, inline with the control it concerns |
| Section nav row | Sticky in-page navigation; counts show section volume |
