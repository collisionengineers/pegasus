# Impact — TICK-012 (INT-25)

This is a plan-and-accept ticket for an **already-implemented, wired and
test-covered** capability. No new business behaviour is proposed. The table
below is therefore a *contract map* of the files that OWN the capability (so
the plan pins the exact caller/contract), plus the only files this ticket
actually changes — Kanmer pipeline docs and, if the user accepts, a
capability-note/follow-up edit. If research surfaces a real defect, that becomes
a separate `bugs`-area ticket, not scope creep here.

| File / module | Change | Risk |
|---|---|---|
| `src/Pegasus.Core/Intake/IntakeAllocation.cs` | None — contract record only (Core owner `AllocateIntake`, entry `AttemptAutomaticAsync` :213, bounded failure taxonomy, frozen-command retry) | Editing a working invariant-bearing path would risk the replay-safety guarantees; explicitly out of scope |
| `src/Pegasus.Core/Intake/AcceptIntake.cs` | None — contract record (caller-independent acceptance boundary; Audit re-validation :60-72) | Same — do not touch |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | None — contract record (`ProcessQueuedIntake` :589, allocation call :742, association-before-allocation ordering) | Reordering association/allocation would break unique-match bypass |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs` | None — contract record (definitive-Audit triple condition + literal/negation regexes) | Literal regexes are correctness-critical; out of scope |
| `src/Pegasus.Core/Cases/CaseContracts.cs` | None — contract record (`AuditIdentity.Create` `a.`/`ap.` :93-108; removed activation gate note :56-73) | — |
| `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs` | None — contract record (reference minting :252, initial `Not ready` state :258-260, audit-custody enqueue :391-393) | Reference format is immutable-after-allocation product invariant; out of scope |
| `src/Pegasus.Worker/IntakeFunctions.cs` + `WorkerDependencyInjection.cs` | None — contract record (timer + queue triggers, DI wiring) | — |
| `.kanmer/…/TICK-012/plan.md, checklist.md, proof.md` | **Written** — the actual deliverable (contract + caller + failure + tests record; acceptance decision; local test evidence) | Low — documentation/proof only |
| `docs/capabilities.md` (INT-25 row) | **Possibly** — only if the user accepts recording the QDOS-only / OCR-literal boundary or a live-tier deferral note | Low, but authoritative-doc edit — needs user sign-off before touching |

## Ripple effects

- **Tests to run as proof (no change to them):** the focused Core intake suite
  (`AllocateDefinitiveIntakeTests`, `DefinitiveIntakeCaseTypeTests`,
  `Qdos/QdosMailClassificationPolicyTests`,
  `CaseMatching/EvaluateIntakeCaseMatchTests`) and the integration recovery suite
  (`QdosAllocationRecoveryTests`, `IntakeAllocationConsumerTests`,
  `CaseAcceptanceReplayTests`). Their pass output becomes proof.md's local tier.
- **Follow-up tickets (candidates, not this ticket):** multi-provider breadth
  beyond QDOS; OCR/scanned-report handling for automatic Audit outcomes. Raise
  only if the user wants them tracked.
- **DOC-01 (TICK-017)** consumes the reference contract minted here; its plan
  depends on this one being pinned (blocks edge already recorded).
- **NOW.md / release record** owns the live tier-5 deployed-caller acceptance;
  this ticket does not and cannot close that (requires-live-approval).

## Out of scope

- Any edit to the Core intake/allocation path or the QDOS provider policies —
  the mechanism is proven and invariant-bearing; changing it is a stop
  condition, not a planning-ticket action.
- Live/deployed case-creation journey against the estate (Azure/Worker) —
  requires-live-approval; explicitly deferred, not attempted here.
- INT-26 manual create screen behaviour (separate capability) beyond noting the
  shared `AttemptStaffCreateAsync` entry point.
- Reference-format or immutability changes — product invariant.
