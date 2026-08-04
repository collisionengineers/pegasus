# Workflow configuration — wireframe

Proposed layout at 1280px+. One panel, one form; checkboxes are the display of current
state. No lede, no policy key, no version integer.

## Main state

```
+------------------------------------------------------------------------------------+
| COLLISION ENGINEERS | Pegasus   Dashboard Inbox Upload Queues Cases Administration* |
|                                                    alex · Change password · Sign out|
+------------------------------------------------------------------------------------+
|  Administration / Workflow configuration                                           |
|                                                                                    |
|  Workflow configuration                                                            |
|                                                                                    |
|  +------------------------------------------------------------+                    |
|  | Last changed 28 Jul 2026 14:05 by alex                     |                    |
|  |                                                            |                    |
|  | A case cannot be sent to an Engineer until                 |                    |
|  |  [x] Its instructions are complete                         |                    |
|  |  [x] Its images are complete                               |                    |
|  |  [x] A staff member has reviewed the instructions          |                    |
|  |  [x] A staff member has reviewed the images                |                    |
|  |                                                            |                    |
|  | Reason for this change                                     |                    |
|  | [                                            ]             |                    |
|  | [                                            ]             |                    |
|  | ~ Recorded permanently with the change.                    |                    |
|  |------------------------------------------------------------|                    |
|  | Applies to every case not yet sent to an Engineer, from    |                    |
|  | the moment you save.                                       |                    |
|  | [ Save requirements ]                                      |                    |
|  +------------------------------------------------------------+                    |
+------------------------------------------------------------------------------------+
```

## Alternate state — another administrator saved first (conflict)

```
|  Workflow configuration                                                            |
|                                                                                    |
|  +------------------------------------------------------------+                    |
|  | (!) These requirements changed while you had this page     |                    |
|  |     open. Reload to see the current settings, then         |                    |
|  |     reapply your change.                                   |                    |
|  +------------------------------------------------------------+                    |
|  +------------------------------------------------------------+                    |
|  | (form as above, values as submitted)                       |                    |
```

## Alternate state — saved

```
|  +------------------------------------------------------------+                    |
|  | (ok) Requirements saved.                                   |                    |
|  +------------------------------------------------------------+                    |
```

## Legend

- `*` — active nav item (red underline).
- `Administration / Workflow configuration` — breadcrumb; replaces eyebrow and back link.
- `[x]` — checkbox, checked = required; the checkboxes both display and edit the state
  (no separate read-only mirror).
- `Last changed …` — muted single line; replaces the internal "Version" integer.
- `~` — field hint (muted).
- `[ Save requirements ]` — single primary (red) action; one-sentence consequence line
  above it, separated by a hairline.
- `(!)` — attention status card (amber trio), conflict/stale-save state.
- `(ok)` — success status card (green), after save.
