# Direction C — case-first shell

Status: **Candidate, unapproved.** A landing/shell strategy over the shared complete Intake, Triage, Case and Administration flows in [the UI specification](../ui-spec.md).

Comparison preview: [candidate C — Case-first](../mockups/candidate-c-case-first.png). The raster is a manually reviewed selection aid, not an approved requirement or implementation.

```text
CE logo | Operations | Intake | Triage | Cases | Administration | Search | User
Case identity: Case/PO | principal | registration | type/Audit identity | state | due | lease
Case context | record/document/evidence work | business action history / external status
```

Landing is Cases/search and deep case work; **Operations remains a full named route** with the exact V1 queues, Due today, and paired day/week outcomes. Intake and Triage remain dedicated pre-case routes. The case record exposes immutable identity, documents, manual WhatsApp material, vehicle/MOT, inspection address/Image Based Assessment, chasers/tasks, EVA/report evidence, lifecycle and reopened/terminal exception states. At constrained desktop, context/history become ordered named sections.

Trade-off: clearest auditability and case depth, but cannot be the earliest V1 implementation and makes shared queue scanning less immediate. No generic Close, notes, percentage completeness, named Engineer assignment, inline external editing, estimator/valuation/finance/AI controls, or mobile workflow. V2/V3 UI re-enters the route.
