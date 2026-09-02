---
id: PLAT-033
type: ticket
title: Origin reads Approved inbox instead of E-mail
status: done
area: platform-operations
order: 1650
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-22T00:49:01.309Z'
  implementing: '2026-08-22T00:49:03.977Z'
  review: '2026-08-22T00:51:19.247Z'
  verifying: '2026-08-22T03:45:25.016Z'
  done: '2026-08-22T03:45:32.367Z'
labels:
  - qdos26009
  - design
  - ui
links: []
refs:
  - docs/design/README.md
deployment: production
archived: false
created: '2026-08-21T23:30:28.075Z'
updated: '2026-09-01T14:44:33.244Z'
---

## Why — operator direction (2026-08-22)

> "Origin says 'Approved inbox' - this is dev speak leaking into the UI. It should say 'E-mail'."

## Evidence

`src/Pegasus.Web/Presentation/OperatorLabels.cs`:

```csharp
602:  IntakeSourceChannel.Mailbox => "Approved inbox",
612:  "mailbox" => "Approved inbox",
```

Both the typed and the string overload carry it, so changing one leaves the other showing the old word — the same one-list-per-concept trap that produced the Odometer/Mileage split.

"Approved inbox" describes how the system is configured, not what the operator sees: the case arrived by e-mail.

## Scope

One label, changed in both overloads. Check the other `IntakeSourceChannel` labels in the same table for the same problem while there.

## How to verify

A case created from a mailbox message shows Origin **E-mail**; both label overloads agree.
