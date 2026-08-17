# Files — BUG-001

## Change surface

No source change is currently justified. The implementation phase, if later authorised, is an evidence/disposition task unless verification finds a concrete defect.

| Path/module | Why it matters | Risk |
| --- | --- | --- |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Worker-owned queued intake and automatic allocation orchestration | Replay or caller gaps can strand definitive receipts |
| `src/Pegasus.Core/Intake/IntakeAllocation.cs` | Definitive intake allocation policy | Incorrect eligibility can create a false case or suppress a valid one |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeAllocationStore.cs` | Persists replay-safe allocation outcome and custody work | Transaction/replay defects can duplicate or strand work |
| `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs` | Creates case custody, retains sources, records success/failure | External effects must be lease-fenced and fail closed |
| `src/Pegasus.Infrastructure/Custody/BoxCaseCustody.cs` | Production Box folder/file adapter | Wrong root or naming can affect real custody |
| `src/Pegasus.Worker/IntakeFunctions.cs` | Production queue/timer caller | Registration alone does not prove execution |
| `src/Pegasus.Worker/WorkerDependencyInjection.cs` | Worker composition | Must bind the real processing and custody implementations |
| `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs` | Primary allocation/replay evidence | Long-running integration suite; require a conclusive result |
| `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` | Custody/outbox/recovery evidence | Local adapter proof is narrower than controlled live Box proof |
| `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs` | Mailbox receipt-to-durable-intake evidence | Fake/local mailbox evidence is not live Graph execution |
| `tests/Pegasus.ArchitectureTests/WorkerCompositionTests.cs` | Ensures production Worker resolves Box and intake owners | Structural evidence, not runtime evidence |
| `docs/operations.md` | Owns deployed source and live-evidence claims | Must not be advanced beyond observed facts |
| `docs/current-architecture.md` | Owns as-built architecture | Refresh only if deployment/current state actually changes |
| BUG-001 `proof.md` | Final evidence record if the ticket proceeds after merge/deployment verification | Must separate local, deployed, and live evidence tiers |

## Ripple effects

- [[TICK-116]] and [[TICK-117]] are archived historical proof tickets consolidated into BUG-001; their approval boundaries remain binding.
- INT-25, DOC-01, and DOC-02 capability status must remain consistent with the evidence actually obtained.
- A failed local test should become a narrow defect only after the failing boundary is reproduced and isolated.
- A failed production journey must not be “fixed” by manual data edits; preserve receipt, allocation, case, custody, and caller evidence.

## Context files

| Path | What an implementer must learn |
| --- | --- |
| `AGENTS.md` | Live external writes need explicit exact-target approval; proof tiers must remain distinct |
| `docs/frd/frd-02-intake-and-source-identity.md` | Receipt is not case creation; definitive intake allocation and failure rules |
| `docs/frd/frd-05-documents-extraction-and-custody.md` | Box custody follows immutable Case/PO allocation and fails closed |
| `docs/capabilities.md` | INT-25/DOC-01/DOC-02 current acceptance wording and remaining live-proof gap |
| `docs/operations.md` | Currently documented deployed SHA and explicit absence of journey proof |
| `docs/runbook.md` | Approved Box root and exact live-operation procedure |

## Deliberately out of scope

- No product-code changes without a reproduced current defect.
- No new PRD, FRD, ADR, top-level component, migration, or compatibility path.
- No Outlook, Azure, SQL, credential, Box, deployment, or other external write without fresh exact-target approval.
- No claim that a build, registration, deployment, or local test proves the complete production journey.
