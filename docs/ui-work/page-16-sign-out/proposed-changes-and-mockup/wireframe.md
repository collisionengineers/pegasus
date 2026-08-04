# Page 16 — Sign out — wireframe

## Main state — signed-out confirmation (recommended; navless auth shell)

```
+--------------------------------------------------------------+
|                  (paper background, no nav)                  |
|                                                              |
|        +------------------------------------------+          |
|        |  COLLISION ENGINEERS                     |          |
|        |                                          |          |
|        |  (v) You are signed out             [H1] |          |
|        |                                          |          |
|        |  [############ Sign in ############] (P) |          |
|        +------------------------------------------+          |
|                                                              |
|              Pegasus · Collision Engineers                   |
+--------------------------------------------------------------+
```

Reached only as the one-time result of the nav's Sign out action; the sign-out itself remains the
existing direct POST from the navigation bar.

## Alternate — confirm interstitial (option considered and not recommended)

```
+------------------------------------------+
|  COLLISION ENGINEERS                     |
|                                          |
|  Sign out of Pegasus?               [H1] |
|                                          |
|  [##### Sign out #####] (P)  [ Cancel ]  |
|                                     (S)  |
+------------------------------------------+
```

Not recommended: no current flow reaches an interstitial (the nav posts directly and GET
redirects), and inserting one adds a click to every sign-out to prevent a misclick that costs only
a quick sign-back-in.

## Legend

| Key  | Meaning                                                              |
|------|----------------------------------------------------------------------|
| [H1] | The page's single heading                                            |
| (v)  | Green check indicator — confirmed-completion role, not colour-only   |
| (P)  | Primary action — red, full width (confirmation) / inline (confirm)   |
| (S)  | Secondary action — hairline button, returns to the previous page     |
