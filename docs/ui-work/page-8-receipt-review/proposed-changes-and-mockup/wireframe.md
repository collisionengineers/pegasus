# Wireframe — Received item review

## Legend

```
[Inbox*]       active nav item (this page is reached from Inbox rows)
(chip:xxx)     state chip; navy=Ready to review, amber=Needs sorting/Missing,
               red=Blocked, green=Accepted, grey=neutral
[Button]       secondary button    [[Button]]  primary (red) button
<link>         text link           ~muted~     secondary/muted text
[____]         text input          [v]         select    [x]/[ ]  checkbox
▸ / ▾          collapsed / expanded section    ║  sticky rail boundary
─────          hairline rule
```

## Main state — ready to review (two columns, sticky action rail)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ COLLISION ENGINEERS | Pegasus   Dashboard [Inbox*] Upload Queues Cases       │
│                                 Administration        alex · Change pw · Out │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  Received item   (chip:navy Ready to review)             <Back to Inbox>     │
│  ~sample-instruction.pdf · Uploaded · 04 Aug 2026 16:30~                     │
│                                                                              │
│  ┌─ Details to confirm ────────────────────────────┐  ║ ┌─ Actions ───────┐  │
│  │ Principal            [Principal A_________]     │  ║ │ To accept:      │  │
│  │  ~Suggested from the sender's domain~           │  ║ │ ✓ Principal     │  │
│  │ Claimant name        [Sample Claimant_____]     │  ║ │   confirmed     │  │
│  │  ~From page 1 of the instruction~               │  ║ │ ✓ Case type     │  │
│  │ Claim number         [____________] (chip:amber │  ║ │   chosen        │  │
│  │                                      Missing)   │  ║ │ ○ Evidence      │  │
│  │ Vehicle registration [AB12 CDE___________]      │  ║ │   confirmed     │  │
│  │  ~From page 1 of the instruction~               │  ║ │                 │  │
│  │ Vehicle make         [____________] (chip:amber │  ║ │ [[Accept as     │  │
│  │                                      Missing)   │  ║ │    case]]       │  │
│  │ Vehicle model        [____________] (chip:amber │  ║ │                 │  │
│  │                                      Missing)   │  ║ │ [Save           │  │
│  │ Vehicle mileage      [____________]             │  ║ │  corrections]   │  │
│  │ Accident             [Rear-end collision__]     │  ║ │ [Block]         │  │
│  │  circumstances       ~From page 2~              │  ║ │                 │  │
│  │ Date of incident     [27/02/2025]               │  ║ │ ~More:~         │  │
│  │ Instruction date     [04/08/2026]               │  ║ │ <Re-evaluate>   │  │
│  │ Inspection address   [____________]             │  ║ │ <Link to a      │  │
│  │  ~No inspection address was found. Enter or     │  ║ │  case>          │  │
│  │  ~confirm one.                                  │  ║ └─────────────────┘  │
│  │ Inspection date      [dd/mm/yyyy]               │  ║   (rail is sticky    │
│  └─────────────────────────────────────────────────┘  ║    while scrolling)  │
│                                                                              │
│  ▸ Documents and images (3)                                                  │
│  ▸ How this was read (5)                                                     │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

Accept expanded (inside the rail, replaces the checklist until submitted):

```
║ ┌─ Accept as case ────────────┐
║ │ Reason                      │
║ │ [_______________________]   │
║ │ Principal    [Principal A]  │
║ │ Case type    [Inspection v] │
║ │ [x] Instruction evidence is │
║ │     complete and confirmed  │
║ │ [x] Image evidence is       │
║ │     complete and confirmed  │
║ │ [[Accept as case]] [Cancel] │
║ └─────────────────────────────┘
```

## Alternate state — vehicle images (image-only upload)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  Received item   (chip:grey Vehicle images)              <Back to Inbox>     │
│  ~sample-image.jpg · Uploaded · 04 Aug 2026 16:40~                           │
│                                                                              │
│  ┌─ Register vehicle images ───────────────────────┐  ║ ┌─ Actions ───────┐  │
│  │ ~Registering keeps these images filed under     │  ║ │ [[Register]]    │  │
│  │ ~the registration until a case claims them.     │  ║ │ [Block]         │  │
│  │ Vehicle registration [AB12 CDE________]         │  ║ │ ~More:~         │  │
│  │ Reason               [________________]         │  ║ │ <Re-evaluate>   │  │
│  │                                                 │  ║ └─────────────────┘  │
│  │ Reading results                                 │  ║                      │
│  │ ─────                                           │  ║                      │
│  │ No readable registration · 04 Aug 2026 16:40    │  ║                      │
│  └─────────────────────────────────────────────────┘  ║                      │
│                                                                              │
│  ▸ Documents and images (1)                                                  │
│  ▸ How this was read (1)                                                     │
└──────────────────────────────────────────────────────────────────────────────┘
```

## Header variant — accepted

```
│  Received item   (chip:green Accepted · Case 26001)      <Back to Inbox>     │
│  ~sample-instruction.pdf · Uploaded · 04 Aug 2026 16:30~                     │
│  … left column read-only …                    ║ ┌─ Actions ────────────┐     │
│                                               ║ │ [[Open case 26001]]  │     │
│                                               ║ └──────────────────────┘     │
```
