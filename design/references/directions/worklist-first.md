# Direction B — worklist-first shell

Status: **Candidate, unapproved.** A landing/shell strategy over the shared complete Intake, Triage, Case and Administration flows in [the UI specification](../../product/ui-spec.md).

Comparison preview: [candidate B — Worklist-first](../mockups/candidate-b-worklist-first.png). The raster is a manually reviewed selection aid, not an approved requirement or implementation.

```text
CE logo | Work: Not ready [case-queue selector] | Intake | Triage | Cases | Administration
Exact filters / search / freshness / refresh
Results: one named case queue                 | read-only selected summary
Open case workspace                           | identity, state, evidence, safe next step
```

Landing is one named case queue, initially Not ready; its selector is limited to Not ready, Review and Held, never a generic cross-feature list. Needs sorting and Blocked intake remain dedicated Intake work; Triage remains dedicated. Results are keyboard-operable; the summary is read-only and consequential actions open focused flows. Constrained desktop places the summary after results without losing selection context.

Trade-off: highest repeated case throughput but weak whole-office day/week visibility. No cross-feature records, bulk actions, saved personal queues, inline lifecycle mutation or speculative email work. All shared intake/Triage/case/Admin states and exceptions remain exactly specified; `Next`/`unallocated`/`Later`/`unallocated` UI re-enters the route.
