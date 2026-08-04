# Wireframe — Staff account

One container: header, action bar, body (ui-standards §4 rule 13). No tab row — this record has
a single section, and tabs are for alternatives. The account action leaves the page body for a
dialog off the bar.

```
+--------------------------------------------------------------------------------------------+
| COLLISION ENGINEERS  Pegasus   Dashboard Inbox Upload Queues Cases [Administration]  alex  |
+--------------------------------------------------------------------------------------------+
| Administration / Staff accounts / jane.smith                                               |
| ############################  CONTAINER  #################################               |
| # jane.smith  [Enabled]  Staff account · Password set ·                  #               |
| #   Last access review 14 Jul 2026            <Back to Staff accounts>   #               |
| #-------------------------------------------------------------------------#              |
| # [ Disable account ]  [ More v ]                                        #               |
| #-------------------------------------------------------------------------#              |
| #  ACCOUNT DETAIL                                                        #               |
| #  Password              Set                                            #               |
| #  Last access review    14 Jul 2026 09:42 ->                            #               |
| ###########################################################################               |
|                                                                                            |
| (i) status card - post-action confirmation, rendered inside the container under the bar     |
+--------------------------------------------------------------------------------------------+
```

## Disable dialog

```
        +------------------------------------------------------------+
        |  Disable jane.smith                                  ( x ) |
        |------------------------------------------------------------|
        |  Reason                                                    |
        |  [______________________________________________]          |
        |  ~Kept on the administration record.~                      |
        |  ! Disabling signs this person out everywhere and cannot   |
        |    be undone from this screen. The account stays on the    |
        |    administration record.                                  |
        |                     ( Cancel )   (( Disable account ))     |
        +------------------------------------------------------------+
```

## Disabled variant

The header chip reads `[Disabled]`, the action bar carries no Disable action, and the body adds
one line: *This account is disabled and stays on the administration record.*

## Legend

| Symbol | Meaning |
|---|---|
| `#` border | The record container — one shell around header, action bar and body |
| `[Enabled]` | Status chip in the header — the single statement of account status |
| `->` | "14 Jul 2026 09:42" links to the Access review page; London time, never raw UTC |
| `[ Disable account ]` | Action-bar item; opens the dialog that carries the reason and consequence |
| `(( Disable account ))` | Primary (red) commitment, inside the dialog only |
| `(i)` | Status card region, rendered only after an action |

Notes: duplicate "Status" detail row removed (header chip owns it); "First password change"
becomes a "Password" fact (Set / Temporary chip).
