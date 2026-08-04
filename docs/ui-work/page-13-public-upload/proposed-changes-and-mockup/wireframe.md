# Page 13 — Public upload — wireframe

No application navigation appears on any state of this page. The external audience sees the
Collision Engineers mark, one card, and nothing else.

## Main state — upload (desktop, ~560px card centred on plain paper)

```
                                                                    (plain paper background)
                        +--------------------------------------------------+
                        |  ● COLLISION ENGINEERS                           |   <- mark only,
                        +--------------------------------------------------+      not a link
                        |                                                  |
                        |  H1  Upload the documents requested              |
                        |      for your claim                              |
                        |                                                  |
                        |  Collision Engineers asked for these documents   |
                        |  by e-mail. Add them below and they will be      |
                        |  sent straight to the person handling your       |
                        |  claim.                                          |
                        |                                                  |
                        |  +--------------------------------------------+  |
                        |  |            [ up-arrow icon ]               |  |
                        |  |   Drag your documents here, or             |  |
                        |  |          ( Choose files )                  |  |  <- drop zone,
                        |  +--------------------------------------------+  |     44px+ targets
                        |  PDF, JPG, PNG or Word documents ·               |
                        |  up to 10.0 MB each · up to 5 documents          |
                        |                                                  |
                        |  YOUR DOCUMENTS                     2 selected   |
                        |  ----------------------------------------------  |
                        |  repair-invoice.pdf      1.2 MB  [Ready]    (x)  |
                        |  damage-front.jpg        2.4 MB  [Ready]    (x)  |
                        |  scan.tiff              12.8 MB  [Too large](x)  |
                        |  ----------------------------------------------  |
                        |                                                  |
                        |             (( Upload documents ))               |  <- only red control
                        |                                                  |
                        +--------------------------------------------------+
                        |  This link was created for you and is not        |
                        |  shared.  Not expecting this? Reply to the       |
                        |  e-mail you received.                            |
                        +--------------------------------------------------+
```

## Success state

```
                        +--------------------------------------------------+
                        |  ● COLLISION ENGINEERS                           |
                        +--------------------------------------------------+
                        |                (v)   <- green tick               |
                        |                                                  |
                        |  H1  Thank you — your documents                  |
                        |      have been received                          |
                        |                                                  |
                        |  RECEIVED                    2 of 5 documents    |
                        |  ----------------------------------------------  |
                        |  repair-invoice.pdf   1.2 MB   4 Aug 2026 16:42  |
                        |  damage-front.jpg     2.4 MB   4 Aug 2026 16:42  |
                        |  ----------------------------------------------  |
                        |                                                  |
                        |  You can add more documents using the same link  |
                        |  if you were asked for anything else.            |
                        |                                                  |
                        |             ( Add more documents )               |  <- secondary only
                        +--------------------------------------------------+
```

## Expired / revoked link state (replaces today's raw browser 404)

```
                        +--------------------------------------------------+
                        |  ● COLLISION ENGINEERS                           |
                        +--------------------------------------------------+
                        |                (!)   <- amber, not red           |
                        |                                                  |
                        |  H1  This upload link is no longer active        |
                        |                                                  |
                        |  Upload links stay open for a limited time.      |
                        |  Reply to the e-mail that sent you this link     |
                        |  and we will send you a new one.                 |
                        |                                                  |
                        |  (no controls; nothing is said about which of    |
                        |   expired / revoked / used up / superseded       |
                        |   applies)                                       |
                        +--------------------------------------------------+
```

## Failure state (inline on the upload card)

```
                        |  +--------------------------------------------+  |
                        |  | (!) We could not save your documents.      |  |  <- red hairline,
                        |  |     Please try again.                      |  |     red text
                        |  +--------------------------------------------+  |
                        |  (the chosen files stay listed — nothing has to  |
                        |   be selected again)                             |
```

## Mobile (under 480px)

```
+----------------------------+
| ● COLLISION ENGINEERS      |
+----------------------------+
| Upload the documents       |
| requested for your claim   |
|                            |
| +------------------------+ |
| |   Drag or choose       | |   <- full-bleed card, drop zone
| |   ( Choose files )     | |      is the camera/file affordance
| +------------------------+ |
| PDF, JPG, PNG or Word      |
| up to 10.0 MB each         |
|                            |
| repair-invoice.pdf 1.2 MB  |
| [Ready]               (x)  |
|                            |
| (( Upload documents ))     |   <- full-width button
+----------------------------+
```

## Legend

| Symbol | Meaning |
|---|---|
| `● COLLISION ENGINEERS` | Text mark; **not** a link, and no product name is shown |
| *(no nav row)* | Deliberate — the application navigation never renders on this route |
| `[Ready]` | Neutral chip: file selected and passing the browser-side checks |
| `[Too large]` / `[Not accepted]` | Amber chip against the offending row, before anything is sent |
| `[Uploaded]` | Green chip — confirmed completion only |
| `(( Upload documents ))` | The single red primary action on the page |
| `( Choose files )` / `( Add more documents )` | Secondary controls |
| `(x)` | Remove this file from the list |
| `(v)` | Green success tick |
| `(!)` | Amber (expired) or red (failure) alert glyph, always paired with text |
| `2 of 5 documents` | Running count against the configured document limit |
