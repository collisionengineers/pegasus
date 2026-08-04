# Page 17 — Access denied — wireframe

## Main state (navless centered card)

```
+--------------------------------------------------------------+
|                  (paper background, no nav)                  |
|                                                              |
|        +------------------------------------------+          |
|        |  COLLISION ENGINEERS                     |          |
|        |                                          |          |
|        |  Access denied                      [H1] |          |
|        |                                          |          |
|        |  Your account does not have access       |          |
|        |  to this page.                           |          |
|        |                                          |          |
|        |  [ Return to Dashboard ]             (S) |          |
|        +------------------------------------------+          |
|                                                              |
|              Pegasus · Collision Engineers                   |
+--------------------------------------------------------------+
```

## Alternate state — current treatment, for contrast (what is being removed)

```
+------------------------------------------+
|  [x Denied]                          (R) |   <- red lock chip: removed
|                                          |
|  Access denied                      [H1] |
|                                          |
|  Your current staff role does not        |   <- two sentences of policy
|  authorize this page or action. If       |      narration: reduced to one
|  your access was changed, sign out       |      plain sentence
|  and sign in again.                      |
|                                          |
|  [ Return to Pegasus ]               (S) |   <- relabelled "Return to
+------------------------------------------+      Dashboard"
```

## Legend

| Key  | Meaning                                                             |
|------|---------------------------------------------------------------------|
| [H1] | The page's single heading — the chip that duplicated it is removed  |
| (S)  | Secondary action — hairline button; nothing on this page earns red  |
| (R)  | Red chip in the current build — shown only to document its removal  |
