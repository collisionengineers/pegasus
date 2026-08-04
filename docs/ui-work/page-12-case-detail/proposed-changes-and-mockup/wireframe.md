# Page 12 — Case detail — wireframe

One container. Header, action bar, tabs. Everything below the tab row is a single panel at a
time, so the page has no vertical stack to scroll past.

## Main state (read-only, Review stage, Overview tab)

```
+--------------------------------------------------------------------------------------+
| ● COLLISION ENGINEERS | Pegasus   Dashboard  Inbox  Upload  Queues  [Cases]  Admin   |
+--------------------------------------------------------------------------------------+
|  Cases › 26001                                                                       |
|  ####################  CASE CONTAINER  #############################################  |
|  # Case 26001  [Review]   Principal A · AB12 CDE · Sample Claimant       ( Edit )  # |
|  #----------------------------------------------------------------------------------#|
|  # (Send to report preparation) (Assign engineer) (Hold) (Close) (More v) | (Export)#| <- sticky
|  #----------------------------------------------------------------------------------#|
|  # | Overview* | Evidence (7) | History (12) |                                     # |
|  #----------------------------------------------------------------------------------#|
|  #  CASE                 INSTRUCTION              ASSIGNMENT                        # |
|  #  Reference   26001    Received  4 Aug 16:30    Engineer    Unassigned            # |
|  #  Type        Inspection  Instr. date 4 Aug     Sent        —                     # |
|  #  Principal   Principal A  Origin   Manual upload  Report    Not started          # |
|  #  Claimant    Sample Claimant  Claim   Not recorded  Due      11 Aug 2026         # |
|  #                                                                                  # |
|  #  Case data                                        ( Show all 18 fields )         # |
|  #  ─────────────────────────────────────────────────────────────────────────────   # |
|  #  Registration     AB12 CDE            [Confirmed]                        (i)     # |
|  #  Incident date    Not recorded        Suggested 27 Feb 2025  ( Accept )   (i)    # |
|  #  Inspection mode  Image-based assessment  [Confirmed]                    (i)     # |
|  #  Instruction date 4 Aug 2026          [Not confirmed]                    (i)     # |
|  ####################################################################################  |
+--------------------------------------------------------------------------------------+
```

`(i)` is the **provenance icon** at the end of each row — hover or focus shows one word only:
`Staff` · `Extracted` · `AI` · `E-mail` · `Lookup` · `Principal` · `Automatic`.

## Evidence tab (sub-tabs)

```
|  #----------------------------------------------------------------------------------#|
|  # | Overview | Evidence (7)* | History (12) |                                     # |
|  #----------------------------------------------------------------------------------#|
|  #  ( Files 2 ) ( Images 4 ) ( E-mails 1 )                    ( Add file )          # |
|  #  ─────────────────────────────────────────────────────────────────────────────   # |
|  #  [ ] FILE              TYPE          CUSTODY     SIZE    ADDED      SRC   ...    # |
|  #  [ ] instruction.pdf   Instruction   [Confirmed] 1.1 MB  4 Aug 2026 (i)   (v)    # |
|  #  [ ] photos-front.jpg  Vehicle image [Confirmed] 2.4 MB  4 Aug 2026 (i)   (v)    # |
|  ####################################################################################  |
```

Images sub-tab — same row grammar, image vocabulary:

```
|  #  IMAGE REF     REGISTRATION   CUSTODY      ADDED         SRC   ...               # |
|  #  IMG-26014     AB12 CDE       [Confirmed]  4 Aug 2026    (i)   (v)               # |
|  #  IMG-26015     AB12 CDE       [Pending]    4 Aug 2026    (i)   (v)               # |
```

E-mails sub-tab — the messages linked to this case:

```
|  #  DIRECTION  FROM / TO           SUBJECT              WHEN          ...           # |
|  #  Received   claims@principal-a  Instruction — AB12   4 Aug 16:30   <Open>        # |
```

Empty states, one muted line each: `No files yet.` / `No vehicle images yet.` /
`No e-mail is linked to this case.`

## History tab

```
|  #  EVENT                ACTOR         WHEN               REASON                    # |
|  #  Sent to Review       alex          4 Aug 2026 16:41   Instruction reviewed      # |
|  #  Case data saved      alex          4 Aug 2026 16:38   Confirmed inspection      # |
|  #  Case created         [Automation]  4 Aug 2026 16:30   Received instruction      # |
```

## Alternate state — Not ready stage (Export disabled, fewer actions)

```
|  # Case 26002  [Not ready]  Principal B · CD34 EFG · Sample Claimant     ( Edit )  # |
|  #----------------------------------------------------------------------------------#|
|  # (Send to Review) (Assign engineer) (Hold) (More v)      | (Export) ~disabled~    # |
|  #                                          hover/focus: "Available in Review"      # |
```

The button stays in place, greyed, with its condition named. It is not removed — its absence
would read as "this case cannot be exported at all".

## Alternate state — editing, with the "Close case" dialog open

```
|  # Case 26001 [Review] [Editing]  Principal A · AB12 CDE    ( Finish editing )     # |
|  #   ~Changes save one at a time.~                                                  # |
|                                                                                      |
|         +------------------------------------------------------------+              |
|         |  Close case                                          ( x ) |              |
|         |------------------------------------------------------------|              |
|         |  Outcome  [ Post-report complete            v ]             |              |
|         |           (Post-report complete / Provider cancelled /      |              |
|         |            Collision Engineers rejected)                    |              |
|         |  Reason   [__________________________________________]      |              |
|         |  i Closing ends work on this case. It can be reopened      |              |
|         |    with a reason.                                          |              |
|         |                       ( Cancel )   (( Close case ))        |              |
|         +------------------------------------------------------------+              |
|  (page dimmed behind the dialog; contended edit shows instead:                       |
|   "Sample Colleague is editing this case" and a disabled Edit button)                |
```

## Legend

| Symbol | Meaning |
|---|---|
| `#` border | The case container — one shell around header, action bar and tabs |
| `[Cases]` | Active nav item |
| `[Review]` | Stage chip — navy (Review semantics); amber pending, green confirmed completion only |
| `[Editing]` | Edit-state chip, shown only while editing |
| `( Edit )` / `( Finish editing )` | The single edit toggle; replaces all lease narration |
| `(Export)` | Right-aligned in the action bar, separated by a rule; disabled outside Review |
| `( More v )` | Menu for rare actions (Reopen, Archive, Create linked replacement) |
| `(i)` | Provenance icon — tooltip is one word, on hover **and** focus |
| `(v)` | Per-row menu: Download / Remove… / Mark third-party vehicle… |
| `( Accept )` | Inline suggestion acceptance — opens a small confirm with reason |
| `(( Close case ))` | Primary (red) commitment inside the dialog only |
| `i …` | One consequence line, inline with the control it concerns |
| Sticky | Header + action bar stay put; only the tab panel scrolls |
