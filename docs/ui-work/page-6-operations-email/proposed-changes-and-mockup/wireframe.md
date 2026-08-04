# Wireframe — E-mail activity drill-down

## Legend

```
[Dashboard*]   active nav item (parent section of this drill-down)
(chip:xxx)     state chip; colour role in brackets — amber=pending, red=failed,
               green=confirmed/succeeded, grey=neutral
[Button]       secondary button    [[Button]]  primary (red) button
<link>         text link           ~muted~     secondary/muted text
─────          hairline rule       ┆           column boundary (no visible line)
```

## Main state — items present, one failure

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ COLLISION ENGINEERS | Pegasus   [Dashboard*] Inbox Upload Queues Cases       │
│                                 Administration        alex · Change pw · Out │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  E-mail activity                                        <Back to Dashboard>  │
│                                                                              │
│  Received                                                                    │
│  ┌────────────────────────────────────────────────────────────────────────┐  │
│  │ Mailbox            Status         Last activity     Where it went      │  │
│  │ ────────────────────────────────────────────────────────────────────── │  │
│  │ Approved mailbox A (chip:green    04 Aug 2026 16:24 <Open in Inbox>    │  │
│  │                     Succeeded)                                         │  │
│  │ ────────────────────────────────────────────────────────────────────── │  │
│  │ Approved mailbox B (chip:red      04 Aug 2026 15:58 <Open Inbox>       │  │
│  │ ~The last message   Failed)                            [Retry]         │  │
│  │ ~could not be                                                          │  │
│  │ ~processed.~                                                           │  │
│  │ ────────────────────────────────────────────────────────────────────── │  │
│  │ Approved mailbox A (chip:amber    04 Aug 2026 15:41 <Open case 26001>  │  │
│  │                     Pending)                                           │  │
│  └────────────────────────────────────────────────────────────────────────┘  │
│  ~Showing the latest 25 items.~                                              │
│                                                                              │
│  Sent                                                                        │
│  ┌────────────────────────────────────────────────────────────────────────┐  │
│  │ Mailbox            Status         Last activity     Where it went      │  │
│  │ ────────────────────────────────────────────────────────────────────── │  │
│  │ Approved mailbox A (chip:green    04 Aug 2026 14:12 <Open case 26001>  │  │
│  │                     Succeeded)                                         │  │
│  │ ────────────────────────────────────────────────────────────────────── │  │
│  │ Approved mailbox B (chip:green    04 Aug 2026 13:05  ~—~               │  │
│  │                     Succeeded)                                         │  │
│  └────────────────────────────────────────────────────────────────────────┘  │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

## Alternate state — retry confirmation, then scheduled

Row after first click on [Retry] (inline confirm replaces the action cell):

```
│ ────────────────────────────────────────────────────────────────────────── │
│ Approved mailbox B (chip:red      04 Aug 2026 15:58  Retry processing for  │
│ ~The last message   Failed)                          this mailbox?         │
│ ~could not be                                        [[Retry]] [Cancel]    │
│ ~processed.~                                                               │
│ ────────────────────────────────────────────────────────────────────────── │
```

Row after the post succeeds (or replays — same rendering):

```
│ ────────────────────────────────────────────────────────────────────────── │
│ Approved mailbox B (chip:red      04 Aug 2026 15:58  (chip:green           │
│ ~The last message   Failed)                           Retry scheduled)     │
│ ~could not be                                                              │
│ ~processed.~                                                               │
│ ────────────────────────────────────────────────────────────────────────── │
```

Empty state (either section): the table is replaced by one muted line —
`~Nothing has been received recently.~` / `~Nothing has been sent recently.~`
