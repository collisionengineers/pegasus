# Create principal — wireframe

Proposed layout at 1280px+. One form panel, one primary action, consequence sentence at the
button. No lede, no eyebrow.

## Main state

```
+------------------------------------------------------------------------------------+
| COLLISION ENGINEERS | Pegasus   Dashboard Inbox Upload Queues Cases Administration* |
|                                                    alex · Change password · Sign out|
+------------------------------------------------------------------------------------+
|  Administration / Create principal                                                 |
|                                                                                    |
|  Create principal                                                                  |
|                                                                                    |
|  +------------------------------------------------------------+                    |
|  | Work Provider organisation                                 |                    |
|  | [ Select an organisation                              v ]  |                    |
|  | ~ Showing the first 50 organisations — search in           |                    |
|  |   Organisations to find one that is not listed.            |                    |
|  |                                                            |                    |
|  | Principal code                                             |                    |
|  | [                                        ]                 |                    |
|  | ~ Letters and numbers only — saved in capitals.            |                    |
|  |                                                            |                    |
|  | Inspection mode                                            |                    |
|  | [ Physical address                                    v ]  |                    |
|  | ~ (shown when Image Based Assessment selected)             |                    |
|  |   Fills in the inspection address on every new case for    |                    |
|  |   this principal; staff can change it on a case with a     |                    |
|  |   reason.                                                  |                    |
|  |                                                            |                    |
|  | The code is permanent — a wrong code is corrected by       |                    |
|  | replacing the principal, not by editing it.                |                    |
|  | [ Create principal ]                                       |                    |
|  +------------------------------------------------------------+                    |
+------------------------------------------------------------------------------------+
```

## Alternate state — no Work Provider organisation exists

The form is absent (not disabled); the blocking card is the only content.

```
|  Create principal                                                                  |
|                                                                                    |
|  +------------------------------------------------------------+                    |
|  | (!) No Work Provider organisation exists yet. Create one    |                   |
|  |     before creating a principal.                            |                   |
|  |     [ Go to Organisations ]                                 |                   |
|  +------------------------------------------------------------+                    |
```

## Legend

- `*` — active nav item (red underline).
- `Administration / Create principal` — breadcrumb; replaces the eyebrow and back link.
- `~` — field hint (muted, one line, `field-hint` style — not the `empty-state` class).
- `[ Create principal ]` — the page's single primary (red) action; the one-sentence
  consequence line sits immediately above it.
- `(!)` — attention status card (amber trio), with its recovery action inside the card.
- The organisation-overflow hint renders only when more Work Provider organisations exist
  than the select shows.
