# Page 11 — Triage record — wireframe

One container: header, action bar, tabs (ui-standards §4 rule 13). The main-column/side-column
split is retired; Origin moves into the header and the Finding tab, and Complete moves to the
action bar where it is disabled with its condition named (rule 9).

## Main state (open record, finding not yet recorded)

```
+------------------------------------------------------------------------------------+
| ● COLLISION ENGINEERS | Pegasus   Dashboard  Inbox  Upload  [Queues]  Cases  Admin |
+------------------------------------------------------------------------------------+
|  Queues › Triage › AB12 CDE                                                        |
|  #############################  CONTAINER  #####################################   |
|  # AB12 CDE  [Open]  Sample Insurer Ltd · Assigned to alex ·                   #   |
|  #   Opened 4 Aug 2026 08:56                              <Back to Queues>     #   |
|  #------------------------------------------------------------------------------#  |
|  # ( Complete )^ ( Reassign ) ( More v )              | ( View e-mail )        #   | <- sticky
|  #------------------------------------------------------------------------------#  |
|  # | Finding* | Replies (1) | History (2) |                                    #   |
|  #------------------------------------------------------------------------------#  |
|  #  Origin   Sample Insurer Ltd · e-mail received 4 Aug 2026 08:55 (View e-mail)#  |
|  #  ─────                                                                      #   |
|  #  Roadworthiness   (o) Roadworthy   ( ) Unroadworthy                         #   |
|  #  Assessment       (o) Repairable   ( ) Total loss                           #   |
|  #  Reason           [_________________________________________________]       #   |
|  #                                                     (( Record finding ))    #   |
|  ################################################################################   |
+------------------------------------------------------------------------------------+

^ "Complete" is disabled; hover or focus shows "Available once a finding is recorded and a
  reply is linked". It stays in place rather than disappearing — this record will offer it.
```

Replies tab:

```
|  #  ~Replies to the finding e-mail appear here automatically. Link the reply    #  |
|  #  ~that answers this record.~                                                 #  |
|  #  principal-claims@sample.example                       ( Link this reply )   #  |
|  #  ~Re: AB12 CDE assessment · 5 Aug 2026 09:12~                                #  |
```

History tab:

```
|  #  Assigned to alex     4 Aug 2026 09:00 · alex                                #  |
|  #  Opened               4 Aug 2026 08:56                                       #  |
```

## Alternate state (styled not-found page — the state every /Triage/{id} URL shows today)

```
+------------------------------------------------------------------------------------+
| ● COLLISION ENGINEERS | Pegasus   Dashboard  Inbox  Upload  [Queues]  Cases  Admin |
+------------------------------------------------------------------------------------+
|                      +--------------------------------------+                      |
|                      |   H1  Record not found               |                      |
|                      |   This record does not exist or      |                      |
|                      |   you do not have access to it.      |                      |
|                      |   ( Go to Queues )                   |                      |
|                      +--------------------------------------+                      |
+------------------------------------------------------------------------------------+
```

## Legend

| Symbol | Meaning |
|---|---|
| `#` border | The record container — one shell around header, action bar and tabs |
| `[Queues]` | Active nav item — Triage is a tab inside Queues in the new IA |
| `[Open]` | State chip, amber (open/pending semantics) |
| `(o) / ( )` | Radio options (Roadworthy/Unroadworthy, Repairable/Total loss) |
| `(( Record finding ))` | Primary (red) commitment, inside the Finding tab with the form it submits |
| `( Complete )` | Action-bar item, disabled until finding + linked reply, with the condition on the control |
| `( More v )` | Menu holding Cancel, Await information, Reopen (state-dependent) |
| `( Link this reply )` | Per-row secondary action; opens a confirm dialog with reason |
| History lines | Event label · time · actor name — no version integers, no GUIDs |
