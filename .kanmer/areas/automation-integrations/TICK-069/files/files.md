# Files — TICK-069

## Where the change lands

| Path | Why |
|---|---|
| `docs/frd/frd-02-intake-and-source-identity.md` | Record the accepted WhatsApp behaviour after the provider and pilot are agreed. |
| `src/Pegasus.Infrastructure/` | Add the selected provider adapter. |
| `src/Pegasus.Web/` or `src/Pegasus.Worker/` | Receive provider events through the host selected by the provider contract. |
| `src/Pegasus.Core/Intake/` | Reuse the current receipt, duplicate, processing and case-matching rules; change only if WhatsApp exposes a genuinely new business rule. |
| `tests/Pegasus.Core.Tests/Intake/` and `tests/Pegasus.IntegrationTests/` | Prove message identity, media download, redelivery, failure, matching and coexistence. |
| `docs/current-architecture.md` and `docs/operations.md` | Record the implemented and deployed state after activation. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/operator-notes.md` | WhatsApp’s current role, accepted material and the need to preserve staff-controlled linking. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Rules for receipt identity, duplicate delivery, failure and case association. |
| `docs/frd/frd-05-documents-extraction-and-custody.md` | Manual WhatsApp evidence remains supported. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Existing intake entry point and durable processing flow. |
| `src/Pegasus.Core/Intake/MailboxIntake.cs` | Existing automated-channel pattern. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Existing provider registration pattern. |

## Ripple effects

Provider onboarding may require a Meta business account, number verification, credentials, a public event endpoint and new Azure configuration. All live setup requires approval. The pilot also needs real, approved sample messages and media.

## Out of scope

Outbound campaigns, automated chasers, replacing staff WhatsApp use, moving historic network-drive material, and changing Box custody are separate decisions.
