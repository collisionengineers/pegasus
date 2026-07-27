# Direction A — operations-first shell

Status: **Selected for the planned V1 shell.** This approves the landing/navigation strategy over the shared complete Intake, Triage, Case and Administration flows in [the UI specification](../../product/ui-spec.md), not every raster detail and not a partial product.

Comparison preview: [candidate A — Operations-first](../mockups/candidate-a-operations-first.png). The raster remains a selection aid; approved behavior comes from the product specification and this selected direction, not pixel-for-pixel reproduction.

```text
CE logo | Operations | Intake | Triage | Cases | Administration | Search | User
Operations
Not ready | Review | Held | Needs sorting | Blocked intake | Triage | Due today
In today | Sent to Engineer: today / week | Reports sent: today / week | Updated | Refresh
Exact filtered queue list                         | selected summary / next safe action
```

Landing is Operations. Every metric is an exact query link; `Blocked intake` is exact wording and pre-case. Day/week use London day and Monday-week definitions; stale/unavailable differs from zero. The shared focused routes supply definitive/staff-resolved intake, Triage, full case lifecycle/evidence/lease flows and Administrator permissions. At constrained desktop or 200% zoom, the summary becomes an ordered labelled section; this remains desktop, never mobile.

Trade-off: strongest shared-office awareness and daily/week visibility, but depends on truthful independent queries and can become dense. It assumes no saved views, bulk actions, inline mutation, calendar, assignments or later email queues. V2/V3 UI re-enters the route.
