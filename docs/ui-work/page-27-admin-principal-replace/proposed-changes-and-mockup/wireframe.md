# Replace principal — wireframe

Proposed layout at 1280px+. Predecessor facts left, successor form right; consequence
sentence at the confirm button; no lede, no GUIDs, no version integer.

## Main state (predecessor active, not yet replaced)

```
+------------------------------------------------------------------------------------+
| COLLISION ENGINEERS | Pegasus   Dashboard Inbox Upload Queues Cases Administration* |
|                                                    alex · Change password · Sign out|
+------------------------------------------------------------------------------------+
|  Administration / Replace principal                                                |
|                                                                                    |
|  Replace ALPHA1                                                                    |
|                                                                                    |
|  PREDECESSOR                            SUCCESSOR                                  |
|  +---------------------------------+   +---------------------------------------+  |
|  | Organisation   Organisation A   |   | Successor Work Provider organisation  |  |
|  | Allocated cases 12              |   | [ Select an organisation         v ]  |  |
|  | Status         [Active]         |   | ~ Showing the first 50 organisations  |  |
|  +---------------------------------+   |   — search in Organisations to find   |  |
|                                        |   one that is not listed.             |  |
|                                        |                                       |  |
|                                        | Successor principal code              |  |
|                                        | [                        ]            |  |
|                                        | ~ Letters and numbers only — saved    |  |
|                                        |   in capitals.                        |  |
|                                        |                                       |  |
|                                        | Reason for replacement                |  |
|                                        | [                        ]            |  |
|                                        | [                        ]            |  |
|                                        | ~ Recorded permanently against both   |  |
|                                        |   principals.                         |  |
|                                        |---------------------------------------|  |
|                                        | ALPHA1 stops taking new work          |  |
|                                        | immediately; its existing cases and   |  |
|                                        | references stay with ALPHA1.          |  |
|                                        | [ Disable ALPHA1 and create successor]|  |
|                                        +---------------------------------------+  |
+------------------------------------------------------------------------------------+
```

## Alternate state — already replaced

The successor form is absent (not disabled).

```
|  PREDECESSOR                            SUCCESSOR                                  |
|  +---------------------------------+   +---------------------------------------+  |
|  | Organisation   Organisation A   |   | (i) ALPHA1 has already been replaced. |  |
|  | Allocated cases 12              |   |     [ View its successor ]            |  |
|  | Status         [Disabled]       |   +---------------------------------------+  |
|  +---------------------------------+                                              |
```

## Alternate state — predecessor disabled, no successor

```
|                                        +---------------------------------------+  |
|                                        | (i) ALPHA1 is disabled. A disabled    |  |
|                                        |     principal cannot be replaced.     |  |
|                                        +---------------------------------------+  |
```

## Legend

- `*` — active nav item (red underline).
- `Administration / Replace principal` — breadcrumb; replaces eyebrow, back link and the
  floating heading chip.
- `[Active]` / `[Disabled]` — status chips (green for Active; muted/grey for Disabled);
  rendered once, inside the Predecessor panel.
- `~` — field hint (muted, `field-hint` style).
- `[ Disable ALPHA1 and create successor ]` — single primary (red) action; the
  one-sentence consequence line sits immediately above it, separated by a hairline.
- `(i)` — attention status card (amber trio); "View its successor" is a real link, present
  only in the already-replaced state.
- ALPHA1 / Organisation A / counts are schematic placeholder data.
