# Wireframe — Upload links and external work drill-down

## Legend

```
[Dashboard*]   active nav item (parent section of this drill-down)
(chip:xxx)     state chip; amber=pending/limit, navy=active, red=failed,
               green=completed, grey=neutral (expired/revoked/unknown)
[Button]       secondary button    [[Button]]  primary (red) button
<link>         text link           ~muted~     secondary/muted text
[____]         text input          ─────       hairline rule
```

## Main state — links and external work present

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ COLLISION ENGINEERS | Pegasus   [Dashboard*] Inbox Upload Queues Cases       │
│                                 Administration        alex · Change pw · Out │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  Upload links                                           <Back to Dashboard>  │
│                                                                              │
│  ┌────────────────────────────────────────────────────────────────────────┐  │
│  │ Case      Principal    Status        Used              Expires         │  │
│  │ ────────────────────────────────────────────────────────────────────── │  │
│  │ <26001>   Principal A  (chip:navy    2.4 MB of 25 MB · 11 Aug 2026     │  │
│  │                         Active)      3 of 10 files       [Revoke link] │  │
│  │ ────────────────────────────────────────────────────────────────────── │  │
│  │ <26002>   Principal B  (chip:amber   24.9 MB of 25 MB · 09 Aug 2026    │  │
│  │                         Limit        10 of 10 files      [Revoke link] │  │
│  │                         reached)                                       │  │
│  │ ────────────────────────────────────────────────────────────────────── │  │
│  │ <26003>   Principal A  (chip:grey    1.1 MB of 25 MB ·  ~Expired~      │  │
│  │                         Expired)     1 of 10 files                     │  │
│  └────────────────────────────────────────────────────────────────────────┘  │
│  ~Showing the latest 50 items.~                                              │
│                                                                              │
│  External work                                                               │
│  ┌────────────────────────────────────────────────────────────────────────┐  │
│  │ Case      Work             Status        Attempts   Last activity      │  │
│  │ ────────────────────────────────────────────────────────────────────── │  │
│  │ <26001>   Report delivery  (chip:green   1 attempt   04 Aug 2026 14:12 │  │
│  │                             Completed)                                 │  │
│  │ ────────────────────────────────────────────────────────────────────── │  │
│  │ <26002>   Report delivery  (chip:red     3 attempts  04 Aug 2026 15:58 │  │
│  │  ~The report could not     Failed)                       [Retry]       │  │
│  │  ~be delivered.~                                                       │  │
│  └────────────────────────────────────────────────────────────────────────┘  │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

## Alternate state — revoke confirmation expanded on a row

```
│ ────────────────────────────────────────────────────────────────────────── │
│ <26001>   Principal A  (chip:navy    2.4 MB of 25 MB ·  11 Aug 2026       │
│                         Active)      3 of 10 files                        │
│           ┌────────────────────────────────────────────────────────────┐  │
│           │ Revoke this upload link? The recipient will no longer be   │  │
│           │ able to send files.                                        │  │
│           │ Reason                                                     │  │
│           │ [__________________________________________________]       │  │
│           │ [[Revoke link]]  [Cancel]                                  │  │
│           └────────────────────────────────────────────────────────────┘  │
│ ────────────────────────────────────────────────────────────────────────── │
```

If the case is being edited by someone else, posting returns the row with a
designed failure line instead of the confirm panel:

```
│           ~This link's case is open for editing by someone else.           │
│           ~Try again in a few minutes.                                     │
```

Empty states: each table is replaced by one muted line —
`~No upload links have been issued.~` / `~No external work is recorded.~`
