# Files and impact map

## Change files

| File or module | Intended change | Risk |
| --- | --- | --- |
| `docs/prd/pegasus-product.md` | Add the near-real-time truthful-intake product outcome and quality target. | Must state outcomes, not mechanics. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Define immediate-after-commit publication, recovery, stage timing, and truthful states for both intake routes. | Must preserve fail-closed receipt/case rules and Worker ownership. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Define Graph wake-up, subscription lifecycle, fallback polling, and unresolved-sender display. | Must not authorize mailbox mutation or move classification policy into Web. |
| `docs/adr/0032-near-real-time-durable-intake-triggering.md` | Record the single trigger architecture and partial supersession of ADR-0002. | One decision only; dated cost belongs in operations/research, not ADR. |
| `docs/adr/0002-dotnet-modular-monolith-on-azure.md` | Mark only polling/outbox-trigger portions superseded by ADR-0032. | Preserve the rest of the modular-monolith decision. |
| `docs/adr/README.md` | Add ADR-0032 and reflect ADR-0002's partial supersession. | Index must match frontmatter. |
| `docs/capabilities.md` | Register INT-33 with canonical owner and target. | Schedule registry only; no duplicate normative behaviour. |
| `docs/index.md` | Link new ADR if the index's current conventions require it. | Navigation only. |

## Ripple effects

- INTK-003, INTK-042, MAIL-013, INTK-001, and INTK-043 consume this contract and remain separate implementation tickets.
- PLAT-036 supplies usable telemetry within the existing spend boundary.
- DELIV-021 owns deployed proof, current-state doc refresh, seven-day cost observation, and any separately approved warm-instance decision.
- Tests and runtime configuration are intentionally outside this documentation ticket and belong to the implementation tickets.

## Context files

| File | Why read it |
| --- | --- |
| `AGENTS.md` | Documentation authority, simplicity, cloud approval, and invariant rules. |
| `docs/index.md` | Authority chain and navigation conventions. |
| `docs/operator-notes.md` | Protected business truth; this ticket does not alter its meaning. |
| `docs/engineering.md` | Evidence tiers, one-Core-owner rule, and plan sizing. |
| `docs/operations.md` | Current deployed/runtime evidence owner; updated only by the release ticket. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Current durable receipt, dispatch, queue-processing, and case-creation route. |
| `src/Pegasus.Core/Intake/MailboxIntake.cs` | Current mailbox-to-shared-intake convergence. |
| `src/Pegasus.Worker/IntakeFunctions.cs` and `MailboxFunctions.cs` | Timer and queue triggers that currently schedule work. |
| `src/Pegasus.Web/Pages/Upload.cshtml.cs` | Manual upload staging boundary. |
| `src/Pegasus.Worker/AzureQueueIntakeWorkQueue.cs` | Existing queue adapter currently trapped in Worker composition. |

## Out of scope

- Implementing Graph subscriptions, queue adapters, recovery, UI, tracing, or source-reader changes.
- Deploying, changing Azure configuration, enabling always-ready instances, or mutating a mailbox.
- Preserving the obsolete tight-polling route as a second normal implementation.
