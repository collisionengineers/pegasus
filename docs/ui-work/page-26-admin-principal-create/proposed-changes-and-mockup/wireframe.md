# Create principal — wireframe

One container: header, action bar, body (ui-standards §4 rule 13). This screen creates a record
rather than showing one, so it carries no state chip and no tabs — but it uses the same shell as
every other record screen, and the commitment sits in the action bar where it is visible without
scrolling. No lede, no eyebrow.

## Main state

```
+------------------------------------------------------------------------------------+
| COLLISION ENGINEERS | Pegasus   Dashboard Inbox Upload Queues Cases Administration* |
|                                                    alex · Change password · Sign out|
+------------------------------------------------------------------------------------+
|  Administration / Principals / Create principal                                    |
|  ###########################  CONTAINER  ########################################  |
|  # Create principal   A principal belongs to one Work Provider organisation     #  |
|  #                                                  <Back to Principals>        #  |
|  #--------------------------------------------------------------------------------#|
|  # [ Cancel ]                                          [[ Create principal ]]   #  |
|  #--------------------------------------------------------------------------------#|
|  #  Work Provider organisation                                                  #  |
|  #  [ Select an organisation                              v ]                   #  |
|  #  ~ Showing the first 50 organisations — search in Organisations to find one  #  |
|  #    that is not listed.                                                       #  |
|  #                                                                              #  |
|  #  Principal code                                                              #  |
|  #  [                                        ]                                  #  |
|  #  ~ Letters and numbers only — saved in capitals.                             #  |
|  #                                                                              #  |
|  #  Inspection mode                                                             #  |
|  #  [ Physical address                                    v ]                   #  |
|  #  ~ (shown when Image Based Assessment selected) Fills in the inspection      #  |
|  #    address on every new case for this principal; staff can change it on a    #  |
|  #    case with a reason.                                                       #  |
|  #                                                                              #  |
|  #  ! The code is permanent — a wrong code is corrected by replacing the        #  |
|  #    principal, not by editing it.                                             #  |
|  ##################################################################################  |
+------------------------------------------------------------------------------------+
```

## Alternate state — no Work Provider organisation exists

The container is absent (not disabled); the blocking card is the only content.

```
|  +------------------------------------------------------------+                    |
|  | (!) No Work Provider organisation exists yet. Create one    |                   |
|  |     before creating a principal.                            |                   |
|  |     [ Go to Organisations ]                                 |                   |
|  +------------------------------------------------------------+                    |
```

## Legend

- `#` border — the container: one shell around header, action bar and body.
- `*` — active nav item (red underline).
- `Administration / Principals / Create principal` — breadcrumb; replaces the eyebrow and back
  link in the body.
- `~` — field hint (muted, one line, `field-hint` style — not the `empty-state` class).
- `!` — the one-sentence consequence line, kept with the fields it concerns.
- `[[ Create principal ]]` — the screen's single primary (red) action, right-aligned in the
  action bar.
- `(!)` — attention status card (amber trio), with its recovery action inside the card.
- The organisation-overflow hint renders only when more Work Provider organisations exist than
  the select shows.
