# EPIC-011 scope boundary — 2026-08-29

## D18 — All discovered findings stay in EPIC-011

The wave-A remediation round filed 14 follow-up tickets, because AGENTS.md rule 22
requires every review finding to carry a disposition and "defer" means an actual
ticket exists. That took the epic from 43 to 61 members.

**Operator decision: keep all 61 in EPIC-011.** Nothing is re-parented to a
follow-up epic. The epic completes when all 61 complete.

Filed in wave A: AUTO-012, AUTO-013, DELIV-033, ENG-029, PLAT-056, PLAT-057,
PLAT-058, PLAT-059, PLAT-060, PLAT-061, PR-070, UIIMP-011, UIIMP-012 (plus the
earlier TICK-223).

## D19 — Prefer *fix* over *defer* from wave B onwards

D18 makes ticket-filing the thing that moves the finish line away from us. So the
disposition order changes for every remaining lane:

1. **Fix it in the lane** — the default, whenever the defect is inside the lane's
   own owned files. Do not file a ticket for something you could have fixed.
2. **Fix it in the lane anyway** when the defect is a one-line change in a file
   another lane owns *and that lane is not currently in flight* — but say so
   loudly in the report so the orchestrator can confirm the ownership call.
3. **Reject with a reason**, or **accept the risk** with a reason, where the
   finding does not hold or the cost is not worth it.
4. **Defer to a new ticket** — the LAST resort, only when the work genuinely
   belongs to another lane's in-flight files, needs an operator decision, or is
   large enough to need its own plan.

This does not license silencing a finding. Rule 22 still binds: every finding gets
one of the four dispositions, recorded under a dated heading in the ticket's plan.
It only changes which disposition is preferred.

## Practical consequence for the closing waves

Several already-filed tickets are small and can be **absorbed by the lane that
owns their file** when that lane runs, rather than run as separate tickets:

| Filed ticket | Natural owner lane |
| --- | --- |
| PLAT-061 (`.gated::after` empty pill) | UIIMP-009 or TICK-223 — both own shell/design-system files |
| UIIMP-011 (snapshot state constants) | UIIMP-005, which owns the snapshot tooling |
| PR-070 (stale catalogue reference) | UIIMP-005 / UIIMP-010, which own the catalogue gate |
| PLAT-058 (`MailActivityCounts.ReceivedToday`) | CASE-028, which reworks the counts |
| PLAT-060 (four Europe/London lookups) | PLAT-051, the named caller for the conversion |

Absorbing one means the absorbing lane closes it and the ticket moves to Done with
that lane's proof — not that the ticket is deleted. Archive, don't delete.
