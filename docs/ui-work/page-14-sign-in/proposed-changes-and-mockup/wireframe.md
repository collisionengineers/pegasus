# Page 14 — Sign in — wireframe

## Main state (unauthenticated, no navigation)

```
+--------------------------------------------------------------+
|                                                              |
|                  (paper background, no nav)                  |
|                                                              |
|        +------------------------------------------+          |
|        |  COLLISION ENGINEERS                     |          |
|        |                                          |          |
|        |  Sign in to Pegasus                 [H1] |          |
|        |                                          |          |
|        |  Username                                |          |
|        |  [________________________________]      |          |
|        |                                          |          |
|        |  Password                                |          |
|        |  [________________________________]      |          |
|        |                                          |          |
|        |  [############ Sign in ############] (P) |          |
|        +------------------------------------------+          |
|                                                              |
|              Pegasus · Collision Engineers                   |
+--------------------------------------------------------------+
```

## Alternate state — invalid credentials

```
+------------------------------------------+
|  COLLISION ENGINEERS                     |
|                                          |
|  Sign in to Pegasus                 [H1] |
|                                          |
|  ! The username or password is       (A) |
|  ! incorrect. If your access has         |
|  ! changed, contact an administrator.    |
|                                          |
|  Username                                |
|  [alex____________________________]      |
|                                          |
|  Password                                |
|  [________________________________] (F)  |
|                                          |
|  [############ Sign in ############] (P) |
+------------------------------------------+
```

## Alternate state — rate limited (replaces the raw HTTP 429)

```
+------------------------------------------+
|  COLLISION ENGINEERS                     |
|                                          |
|  Too many sign-in attempts          [H1] |
|                                          |
|  Wait a minute, then try again.          |
|                                          |
|  [ Back to sign in ]                 (S) |
+------------------------------------------+
```

## Legend

| Key  | Meaning                                                            |
|------|--------------------------------------------------------------------|
| [H1] | The page's single heading (one heading stack, no eyebrow kicker)   |
| (P)  | Primary action — red, full width                                   |
| (S)  | Secondary action — hairline button                                 |
| (A)  | Inline alert, red left rule; username retained, password cleared   |
| (F)  | Focus returned here after a failed attempt                         |
| `[_]`| Text input, full card width                                        |
