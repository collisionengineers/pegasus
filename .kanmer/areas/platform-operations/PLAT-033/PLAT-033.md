---
id: PLAT-033
type: ticket
title: Origin reads Approved inbox instead of E-mail
status: backlog
area: platform-operations
assignee: ''
profile: fix
labels:
  - qdos26009
  - design
  - ui
links: []
refs:
  - docs/design/README.md
deployment: not-deployed
archived: false
created: '2026-08-21T23:30:28.075Z'
updated: '2026-08-21T23:30:28.075Z'
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
