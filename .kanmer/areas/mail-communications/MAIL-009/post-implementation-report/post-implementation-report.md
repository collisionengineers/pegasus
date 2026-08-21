# Post-implementation report

**Branch:** `task/qdos26008-regressions` · **PR:** #505 · **Commit:** `1a86f5db`

## What was built

The effective sender is resolved at **retention**, in the same write that creates the
retained row, through the same unwrap the route policy uses:

- `StaffForwardBodyCleaner.ForwardedSenderAddress` reads the forwarded header out of the
  retained body;
- `QdosMailRoutePolicy.ProvisionalEffectiveSender` applies the unwrap, and lives beside
  `Evaluate` so the provisional rule and the authoritative one cannot drift;
- `EfRetainedMailboxMessageStore` gains `BodyHead` (raw body, newlines intact, first 600
  characters) and writes the provisional sender.

`MailRouteDecision` remains authoritative and supersedes it as soon as intake processing
lands.

## Why this had regressed repeatedly

The operator noted it *"keeps regressing on subsequent deploys"*. The reason is that it was
never the unwrap logic. `EffectiveSenderAddress` is read off `MailRouteDecision`, written
by intake processing — a **later worker hop**. The retained row exists from the poll
carrying only the raw desk sender, so the list truthfully rendered what it had. Every
previous fix targeted the unwrap and left the timing window untouched.

Keeping `ProvisionalEffectiveSender` physically beside `Evaluate` is the guard against
this becoming two rules that disagree, which would reintroduce the same bug in a subtler
form.

## Fails closed

Anything that is not unambiguously a staff forward with one external original yields
nothing, and the list shows a neutral pending state — never the desk address dressed up as
the sender. That was the ticket's stated guard rail.

## Departure from the plan

`BodyHead` was not in the plan. The provisional unwrap needs the forwarded header from the
message body, and retention stores the body; reading a bounded 600-character head at
retention avoids either loading whole bodies into the list projection or adding a second
store. Newlines are preserved deliberately — the forwarded header block is line-structured
and a whitespace-normalised head cannot be parsed.

## Evidence

- `Pegasus.Core.Tests` — 908 passed, covering the unwrap and the fail-closed cases
- Full integration suite: recorded before merge
- Live: a fresh staff-forwarded email never rendering the desk address at any point,
  including first paint — Phase 6. This is the one that matters, because the defect is a
  timing window and only a real arrival exercises it.
