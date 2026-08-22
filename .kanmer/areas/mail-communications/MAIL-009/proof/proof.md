# Proof — verified in production

**Shipped:** PR #505, commit `1a86f5db` · **Deployed:** Release 17 (`71911734`), still live on Release 18.

## The forward unwraps to the real sender

Production, for QDOS26010's message:

```
RetainedMailboxMessages.SenderAddress        desk@collisionengineers.co.uk
IntakeMailRouteDecisions.EffectiveSenderAddress   mhitchen@qdosassist.co.uk
```

The transport sender is the desk — it genuinely is a forward from the desk — and the
effective sender resolves to the person who actually sent the instruction. Subject
`Fw: (EREF10) RTA on 18/08/2026 : Mr Jame…`, so this is the staff-forward shape the ticket
is about.

That is the operator's complaint closed: *"The e-mail is showing from desk AGAIN… should
show as the original sender."*

## The timing window is what mattered

The defect was never the unwrap; it was that `EffectiveSenderAddress` is written by intake
processing, a later worker hop, so the list truthfully rendered the desk until processing
landed. Resolving at retention removes the window.

Measured for QDOS26010: message received 02:00:34, case created 02:01:04 — a 30-second gap
during which, before this fix, the inbox would have shown the desk address.

## Not claimed

The inbox has not been observed rendering during that window, because that needs an
authenticated session and a message arriving while watching. What is proved is that the
effective sender is resolved and persisted, which is the condition that made the flip
possible. The first-paint behaviour remains asserted by tests rather than by observation.
