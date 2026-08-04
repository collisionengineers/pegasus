# Page 10 — Image reference detail — wireframe

One container: header, action bar, tabs (ui-standards §4 rule 13). The four stacked cards are
retired; the actions that used to sit inside card bodies move to the bar.

## Main state (awaiting instruction, one case candidate)

```
+------------------------------------------------------------------------------------+
| ● COLLISION ENGINEERS | Pegasus   Dashboard  [Inbox]  Upload  Queues  Cases  Admin |
+------------------------------------------------------------------------------------+
|  Inbox › Vehicle images › AB12CDE-01                                               |
|  #############################  CONTAINER  #####################################   |
|  # Image reference AB12CDE-01  [Awaiting instruction]                          #   |
|  #   AB12 CDE · Received 4 Aug 2026 14:02 · Not linked  <Back to Vehicle imgs> #   |
|  #------------------------------------------------------------------------------#  |
|  # [[Link to a case]] ( View original upload ) ( More v )                      #   | <- sticky
|  #                                 | 1 open case matches this registration     #   |
|  #------------------------------------------------------------------------------#  |
|  # | Overview* | Matching cases (1) | Readings (2) |                           #   |
|  #------------------------------------------------------------------------------#  |
|  #  RECORD                          ORIGIN                                     #   |
|  #  Registration   AB12 CDE         Source     Manual upload                   #   |
|  #  Received       4 Aug 14:02      Uploaded   4 Aug 2026 14:01                #   |
|  #  Linked case    Not linked       Images     3                               #   |
|  ################################################################################   |
+------------------------------------------------------------------------------------+
```

Matching cases tab:

```
|  #  Case 26001   Principal A   AB12 CDE   ( Open case ) (( Link to this case ))#   |
|  #  i Linking keeps this image reference permanently; it can be reversed       #   |
|  #    before the report is sent.                                               #   |
```

Readings tab:

```
|  #  AB12 CDE                  High confidence                    4 Aug 2026    #   |
|  #  No registration readable  One image                          4 Aug 2026    #   |
```

## Alternate state (linked to a case)

```
|  # Image reference AB12CDE-01  [Linked to Case 26001]                          #   |
|  #   AB12 CDE · Received 4 Aug 2026 14:02 · Case 26001  <Back to Vehicle imgs> #   |
|  #------------------------------------------------------------------------------#  |
|  # ( Open case 26001 ) ( Unlink from this case ) ( View original upload )      #   |
|  #------------------------------------------------------------------------------#  |
|  # | Overview* | Link | Readings (2) |                                         #   |
|  #------------------------------------------------------------------------------#  |
|  #  Linked to Case 26001 on 4 Aug 2026 by alex.                                #   |
|  #  i Unlinking needs a reason and is recorded; it is only possible before      #   |
|  #    the report is sent.                                                       #   |
```

The Matching cases tab is replaced by Link once the record is linked — the tab set follows the
state rather than showing a tab that can only say "already linked".

## Legend

| Symbol | Meaning |
|---|---|
| `#` border | The record container — one shell around header, action bar and tabs |
| `[Inbox]` | Active nav item |
| `[Awaiting instruction]` | State chip, amber (pending) |
| `[Linked to Case 26001]` | State chip, neutral |
| `( Open case )` | Secondary button |
| `[[Link to a case]]` | The container's one commitment action; opens a confirm dialog that requires a reason |
| `( More v )` | Menu for rare actions |
| `i …` | Single inline consequence line, placed next to the control it concerns |
| Sticky | Header + action bar stay put; only the tab panel scrolls |
