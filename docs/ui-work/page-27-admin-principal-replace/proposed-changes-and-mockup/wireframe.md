# Replace principal — wireframe

One container: header, action bar, body (ui-standards §4 rule 13). The predecessor's identity
and state move into the header band, the commitment moves into the action bar, and the successor
form is the body. No tabs — predecessor and successor are a reading order, not alternatives.
No lede, no GUIDs, no version integer.

## Main state (predecessor active, not yet replaced)

```
+------------------------------------------------------------------------------------+
| COLLISION ENGINEERS | Pegasus   Dashboard Inbox Upload Queues Cases Administration* |
|                                                    alex · Change password · Sign out|
+------------------------------------------------------------------------------------+
|  Administration / Principals / Replace ALPHA1                                      |
|  ###########################  CONTAINER  ########################################  |
|  # Replace ALPHA1  [Active]  Organisation A · 12 allocated cases                #  |
|  #                                                  <Back to Principals>        #  |
|  #--------------------------------------------------------------------------------#|
|  # [ Cancel ]                    [[ Disable ALPHA1 and create successor ]]      #  | <- sticky
|  #--------------------------------------------------------------------------------#|
|  #  PREDECESSOR                                                                 #  |
|  #  Organisation  Organisation A     Allocated cases  12     Status  [Active]   #  |
|  #                                                                              #  |
|  #  SUCCESSOR                                                                   #  |
|  #  Successor Work Provider organisation                                        #  |
|  #  [ Select an organisation                              v ]                   #  |
|  #  ~ Showing the first 50 organisations — search in Organisations to find one  #  |
|  #    that is not listed.                                                       #  |
|  #                                                                              #  |
|  #  Successor principal code                                                    #  |
|  #  [                                        ]                                  #  |
|  #  ~ Letters and numbers only — saved in capitals.                             #  |
|  #                                                                              #  |
|  #  Reason for replacement                                                      #  |
|  #  [                                        ]                                  #  |
|  #  ~ Recorded permanently against both principals.                             #  |
|  #                                                                              #  |
|  #  ! ALPHA1 stops taking new work immediately; its existing cases and          #  |
|  #    references stay with ALPHA1.                                              #  |
|  ##################################################################################  |
+------------------------------------------------------------------------------------+
```

The predecessor facts collapse to one row: the header already carries the identity, so the body
states only what the header cannot.

## Alternate state — already replaced

The action bar and successor form are absent (not disabled); the header chip changes.

```
|  # Replace ALPHA1  [Disabled]  Organisation A · 12 allocated cases              #  |
|  #--------------------------------------------------------------------------------#|
|  #  ALPHA1 has already been replaced.  <View its successor>                     #  |
```

## Alternate state — predecessor disabled, no successor

```
|  +---------------------------------------+                                        |
|  | (i) ALPHA1 is disabled. A disabled    |                                        |
|  |     principal cannot be replaced.     |                                        |
|  +---------------------------------------+                                        |
```

## Legend

- `#` border — the container: one shell around header, action bar and body.
- `*` — active nav item (red underline).
- `Administration / Principals / Replace ALPHA1` — breadcrumb; replaces eyebrow, back link and
  the floating heading chip.
- `[Active]` / `[Disabled]` — status chips (green for Active; muted/grey for Disabled), rendered
  once, in the header band.
- `~` — field hint (muted, `field-hint` style).
- `!` — the one-sentence consequence line, kept with the fields it concerns.
- `[[ Disable ALPHA1 and create successor ]]` — single primary (red) action, right-aligned in the
  action bar and visible without scrolling the form.
- `(i)` — attention status card (amber trio); "View its successor" is a real link, present only
  in the already-replaced state.
- ALPHA1 / Organisation A / counts are schematic placeholder data.
