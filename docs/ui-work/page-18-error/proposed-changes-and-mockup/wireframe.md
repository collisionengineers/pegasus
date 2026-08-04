# Page 18 — Error — wireframe

## Main state — request failed (navless centered card)

```
+--------------------------------------------------------------+
|                  (paper background, no nav)                  |
|                                                              |
|        +-|----------------------------------------+          |
|        | |  COLLISION ENGINEERS                   |          |
|        | |                                        |          |
|        |R|  We could not complete that       [H1] |          |
|        | |  request                               |          |
|        | |                                        |          |
|        | |  What you submitted may not have       |          |
|        | |  been saved. Try again, and if it      |          |
|        | |  keeps failing, tell your              |          |
|        | |  administrator the reference below.    |          |
|        | |                                        |          |
|        | |  [## Try again ##] [ Return to     (P) |          |
|        | |   (P)                Dashboard ]   (S) |          |
|        | |                                        |          |
|        | |  ----------------------------------    |          |
|        | |  Support reference               (M)   |          |
|        | |  00-bf31b4c0...696071-00  [Copy]       |          |
|        +-|----------------------------------------+          |
|                                                              |
|              Pegasus · Collision Engineers                   |
+--------------------------------------------------------------+
```

## Alternate state — page not found (new sibling; replaces raw browser 404s)

```
+------------------------------------------+
|  COLLISION ENGINEERS                     |
|                                          |
|  We could not find that page        [H1] |
|                                          |
|  The link may be out of date, or the     |
|  address may have been mistyped.         |
|                                          |
|  [ Return to Dashboard ]             (S) |
+------------------------------------------+
```

No red rule and no support reference: nothing failed, so there is nothing to correlate.

## Legend

| Key  | Meaning                                                                  |
|------|--------------------------------------------------------------------------|
| [H1] | The page's single heading (kicker, lede, and duplicate h2 removed)       |
| |R|  | 3px red left rule on the error card only — the sole red on the family    |
| (P)  | Primary action — red; "Try again" hidden when no safe return target      |
| (S)  | Secondary action — hairline button                                       |
| (M)  | Demoted support handle: 13px muted label + monospace value + Copy button |
