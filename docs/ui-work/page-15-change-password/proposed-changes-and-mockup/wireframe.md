# Page 15 — Change password — wireframe

## Main state — forced first sign-in (navless)

```
+--------------------------------------------------------------+
|                  (paper background, no nav)                  |
|                                                              |
|        +------------------------------------------+          |
|        |  COLLISION ENGINEERS                     |          |
|        |                                          |          |
|        |  Set a new password before          [H1] |          |
|        |  continuing                              |          |
|        |                                          |          |
|        |  You cannot use Pegasus until the    (C) |          |
|        |  password issued to you is replaced.     |          |
|        |                                          |          |
|        |  Current password                        |          |
|        |  [________________________________]      |          |
|        |                                          |          |
|        |  New password                            |          |
|        |  At least 8 characters. Any          (H) |          |
|        |  characters are allowed.                 |          |
|        |  [________________________________]      |          |
|        |                                          |          |
|        |  Confirm new password                    |          |
|        |  [________________________________]      |          |
|        |                                          |          |
|        |  [######## Change password ########] (P) |          |
|        +------------------------------------------+          |
+--------------------------------------------------------------+
```

## Alternate state — passwords do not match (field-level errors)

```
+------------------------------------------+
|  COLLISION ENGINEERS                     |
|                                          |
|  Set a new password before          [H1] |
|  continuing                              |
|                                          |
|  Current password                        |
|  [________________________________]      |
|                                          |
|  New password                            |
|  At least 8 characters. Any          (H) |
|  characters are allowed.                 |
|  [________________________________]      |
|                                          |
|  Confirm new password                    |
|  [________________________________] (E)  |
|  ! The passwords do not match.       (E) |
|                                          |
|  [######## Change password ########] (P) |
+------------------------------------------+
```

## Voluntary variant heading (reached from the app; nav may remain)

```
| Dashboard · Inbox · Upload · Queues · Cases · Administration   alex · ... |
|                                                                          |
|        +------------------------------------------+                      |
|        |  Change password                    [H1] |                      |
|        |  ... (identical fields and states) ...   |                      |
+--------------------------------------------------------------------------+
```

## Legend

| Key  | Meaning                                                                |
|------|------------------------------------------------------------------------|
| [H1] | The page's single heading; forced and voluntary variants differ        |
| (C)  | Consequence sentence — allowed guidance, one sentence, forced variant  |
| (H)  | Requirement hint — stated once, beside the field it governs            |
| (P)  | Primary action — red, full width                                       |
| (E)  | Field-level error: red input border + one sentence under the field     |
| `[_]`| Password input, full card width                                        |
