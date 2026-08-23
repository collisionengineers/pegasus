---
id: MAIL-011
type: ticket
title: Read the forwarded sender from a header block that carries a Cc line
status: implementing
area: mail-communications
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-23T12:13:52.099Z'
taken_at: '2026-08-23T12:11:41.198Z'
branch: task/qdos26012-regressions
worktree: ../pegasus-worktrees/qdos26012-regressions
labels:
  - u34
  - production-defect
  - found-during-qa
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-23T12:10:00.492Z'
updated: '2026-08-23T12:13:52.099Z'
---

## One regex, four symptoms, on a routine forward

U34 (`625b6eab-fdbf-4a37-9d7f-f8a26eb47f88`, 2026-08-23 10:57:40Z) is a
staff forward from `desk@collisionengineers.co.uk` carrying one photograph
(`CL vehicle damage.jpg`, 2,835,039 bytes) and subject
*"Fw: Engineer Triage — Our Claim Reference 47939/1, Vehicle registration GD65TVY"*.

It was refused with `NoUsableIdentification`:
*"A staff-forwarded message requires exactly one consistent original sender."*
(`IntakeReceipts.4d90ec4e-5681-442a-accc-d8694571b86e`, `EvidenceJson` is
`{"version":1,"data":[]}` — nothing was extracted at all.)

The forwarded block in the retained body is perfectly well-formed:

```
From: Robin Anderson <randerson@qdosassist.co.uk>
Sent: 21 August 2026 11:18 PM
To: Desk <desk@collisionengineers.co.uk>
Cc: Qdos NewClaims <NewClaims@qdosassist.co.uk>
Subject: Engineer Triage - Our Claim Reference 47939/1, Vehicle registration GD65TVY
```

## Root cause

`MimeKitPdfPigOpenXmlIntakeSourceReader.InlineForwardedHeaderRegex` requires
`From:`, `Sent:`, `To:` and `Subject:` to be **directly adjacent**:

```
(?i)(?:\A|[\r\n])From:[\t ]*(?<from>[^\r\n]+)[\r\n]+Sent:[^\r\n]*[\r\n]+To:[^\r\n]*[\r\n]+Subject:[^\r\n]*
```

There is no allowance for a `Cc:` line. Outlook writes one whenever the
original had a Cc — as this one did.

Run against U34's own retained body:

| Pattern | Matches |
| --- | ---: |
| shipped regex | **0** |
| same, with an optional `Cc:` line | 1 → `Robin Anderson <randerson@qdosassist.co.uk>` |

Zero matches ⇒ `TryReadInlineForwardedOriginalSender` returns false ⇒ no
`InlineForwardedOriginal` identity ⇒ `QdosMailRoutePolicy` sees
`originalIdentities.Length == 0` ⇒ NeedsSorting.

## The four symptoms

1. **The instruction and its photograph were never identified** — U34 instead of a case.
2. **The inbox renders the message as from "Desk"** — `EffectiveSenderAddress` is
   null (no route decision), so `EfRetainedMailboxMessageStore` falls back to
   `QdosMailRoutePolicy.ProvisionalEffectiveSender`, which calls
   `StaffForwardBodyCleaner.ForwardedSenderAddress` — the same regex, and it
   fails identically. Every other row in the same inbox shows its original
   sender correctly; only this one shows the forwarding desk.
3. **The preview line shows the Collision Engineers signature**, not the
   message — `StaffForwardBodyCleaner.SplitForwardedHeader` finds no boundary,
   so nothing is trimmed.
4. Any later forward with a Cc will do the same. This is not an edge case.

## One list per concept

The regex is written out **twice**, verbatim, in two projects —
`MimeKitPdfPigOpenXmlIntakeSourceReader.InlineForwardedHeaderRegex` and
`StaffForwardBodyCleaner.ForwardedHeaderRegex`, the second carrying the comment
`// Mirrors MimeKitPdfPigOpenXmlIntakeSourceReader.InlineForwardedHeaderRegex.`
A comment is not a mechanism. The forward-header shape is one rule and belongs
in one place; fixing it in two files independently is how it drifts next time.

## Scope note

Widening the pattern must not widen what counts as *proof* of a route: the
policy still demands exactly one original sender, external to the staff domain.
This makes a well-formed header readable, it does not relax the route bar.
