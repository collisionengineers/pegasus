# Open questions — MAIL-10

No unresolved operator/product question remains.

- [x] **What is the exact manual behavior?** — One exact retained message resolves server-side to one Intake receipt. Link requires deliberate Case search, a business-readable target summary, a reason and explicit confirmation. Unlink applies only to the exact current active Case and requires its own reason/confirmation. Relink/correction is the staged reasoned unlink followed by a separate searched, summarized and confirmed link to the replacement Case; there is no hidden direct swap. Every transition uses current receipt/Case versions, the actor's exact Case lease and its own idempotency key, and preserves the original source/accepted lineage plus append-only before/after history.
- [x] **May staff link before classification is resolved?** — Yes, when the association evidence itself is sufficient. Classification, operational destination, Outlook folder and Case association remain separate facts. A message that refers to several Cases remains unlinked; no one-to-many association or copy is permitted.
- [x] **What must land first?** — TICK-051 / MAIL-09. It owns the final shared association transaction/stale-evidence seam and retained current-association projection. TICK-052 must refresh against its merged result and execute afterward, not concurrently.
- [x] **What live production verification is required and authorized?** — Acceptance requires one exact production link → reasoned unlink → reasoned replacement link journey. It is not standing write authority. Immediately before execution, obtain and record exact-target approval naming the retained message, initial Case, replacement Case and approved reasons. Capture target summaries, versions, actor, before/after/current state and every immutable history entry; abort on target mismatch, ambiguity or staleness. This is a Pegasus/Azure SQL business write only and authorizes no Graph, Outlook mailbox/folder/category/read-state/move/delete or Box mutation.

## Parked (explicitly deferred)

- [x] **Direct active-to-active atomic reassignment** — Deferred because neither FRD-08 nor the accepted live journey requires it and the existing explicit unlink-then-link transitions preserve both decisions and honest recovery. If later operator behavior requires a single all-or-nothing replacement, resolve that independently rather than smuggling an optional relink mode into the existing command.
- [x] **Automation exposure** — Deferred to [[AUTO-003]] after the Core action lands; MAIL-10 adds no MCP tool or Automation-specific policy.
