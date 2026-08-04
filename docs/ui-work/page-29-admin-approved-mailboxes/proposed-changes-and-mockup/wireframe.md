# Approved mailboxes — wireframe

Proposed layout at 1280px+. Read view and edit state separated: clean table, one row
expands to edit; single add form below with the page's one consequence sentence.

## Main state (one row expanded for editing)

```
+------------------------------------------------------------------------------------+
| COLLISION ENGINEERS | Pegasus   Dashboard Inbox Upload Queues Cases Administration* |
|                                                    alex · Change password · Sign out|
+------------------------------------------------------------------------------------+
|  Administration / Approved mailboxes                                               |
|                                                                                    |
|  Approved mailboxes                                                                |
|                                                                                    |
|  +------------------------------------------------------------------------------+  |
|  | ADDRESS                    | READS                      | STATUS     | EDIT   |  |
|  |----------------------------+----------------------------+------------+--------|  |
|  | instructions@example.co.uk | Receiving (Inbox)          | [Approved] | Edit   |  |
|  |----------------------------+----------------------------+------------+--------|  |
|  | reports@example.co.uk      | Receiving (Inbox) ·        | [Approved] | Close  |  |
|  |                            | Sent evidence              |            |        |  |
|  |  +------------------------------------------------------------------------+  |  |
|  |  | Approved address                                                       |  |  |
|  |  | [ reports@example.co.uk                    ]                           |  |  |
|  |  | What Pegasus reads                                                     |  |  |
|  |  |  [x] Receiving (Inbox)                                                 |  |  |
|  |  |  [x] Sent evidence (Sent Items)                                        |  |  |
|  |  | State            Reason                                                |  |  |
|  |  | [ Approved  v ]  [                              ]                      |  |  |
|  |  |                  ~ Recorded permanently with the change.               |  |  |
|  |  | [ Save ]                                                               |  |  |
|  |  +------------------------------------------------------------------------+  |  |
|  |----------------------------+----------------------------+------------+--------|  |
|  | archive@example.co.uk      | Sent evidence              | [Disabled] | Edit   |  |
|  +------------------------------------------------------------------------------+  |
|                                                                                    |
|  ADD AN APPROVED ADDRESS                                                           |
|  +------------------------------------------------------------+                    |
|  | Approved address                                           |                    |
|  | [                                            ]             |                    |
|  | What Pegasus reads                                         |                    |
|  |  [ ] Receiving (Inbox)                                     |                    |
|  |  [ ] Sent evidence (Sent Items)                            |                    |
|  | State                                                      |                    |
|  | [ Approved                                    v ]          |                    |
|  | Reason                                                     |                    |
|  | [                                            ]             |                    |
|  |------------------------------------------------------------|                    |
|  | Pegasus reads e-mail only from the addresses approved      |                    |
|  | here.                                                      |                    |
|  | [ Add address ]                                            |                    |
|  +------------------------------------------------------------+                    |
+------------------------------------------------------------------------------------+
```

## Alternate state — no approved addresses

```
|  Approved mailboxes                                                                |
|                                                                                    |
|  +------------------------------------------------------------------------------+  |
|  |  No addresses are approved — Pegasus is not reading any e-mail.              |  |
|  |  Add an address below.                                                       |  |
|  +------------------------------------------------------------------------------+  |
|                                                                                    |
|  ADD AN APPROVED ADDRESS                                                           |
|  (form as above)                                                                   |
```

## Alternate state — another administrator saved first

```
|  +------------------------------------------------------------------------------+  |
|  | (!) This address's approval changed while you had it open. Reload to see     |  |
|  |     the current settings, then reapply your change.                          |  |
|  +------------------------------------------------------------------------------+  |
|  (table follows)                                                                   |
```

## Legend

- `*` — active nav item (red underline).
- `Administration / Approved mailboxes` — breadcrumb; replaces eyebrow and back link.
- `[Approved]` — green-bordered status chip; `[Disabled]` — muted chip; label always
  present, never colour-only.
- `Edit` / `Close` — per-row action; only one row expands at a time; no-script fallback is
  `?edit={id}` server-side expansion.
- `READS` column shows labelled scopes only ("Receiving (Inbox)", "Sent evidence") — the
  raw enum join never renders.
- `~` — field hint (muted).
- `[ Save ]` / `[ Add address ]` — primary (red) actions; the page's single consequence
  sentence sits above "Add address", separated by a hairline.
- `(!)` — attention status card (amber trio), stale-save state.
- No "Version" column anywhere; version travels as a hidden field only.
