# Plan

Committed in `1a86f5db`.

## Root cause — a timing window, not a broken unwrap

The inbox showed the forwarding desk and then corrected itself. `EffectiveSenderAddress`
is read off `MailRouteDecision`, which **intake processing** writes — a later worker hop.
The retained row exists from the poll with only the raw desk sender, so the list rendered
desk until processing landed. The list was truthfully rendering what it had. The unwrap
logic was never wrong; this had regressed across deploys because each fix targeted the
unwrap.

## The change

The staff-forward unwrap is a pure function of the message headers and body — it needs
nothing from intake processing. Resolve the effective sender at **retention**, in the
same write that creates the row, through the same unwrap the route policy uses. Intake
processing then confirms rather than first-writes it, and the flip disappears.

Kept beside `QdosMailRoutePolicy.Evaluate` deliberately: a provisional rule that drifts
from the authoritative one would reintroduce exactly this bug in a subtler form.

**Fails closed.** Anything that is not unambiguously a staff forward with one external
original yields nothing, and the list shows a neutral pending state rather than the desk
address dressed up as the sender.

## Acceptance

- A staff-forwarded message resolves its original sender at retention. ✅
- An ambiguous forward yields nothing rather than guessing. ✅
- The route decision still supersedes. ✅
- Live: a fresh forward never renders the desk address at any point, including first
  paint — Phase 6.

## Simplification pass

2026-08-21. The provisional resolver reuses the existing unwrap rather than adding a
second one, and lives beside the rule it mirrors. `BodyHead` is a bounded read of what
retention already stores, not a new store. No findings deferred.
