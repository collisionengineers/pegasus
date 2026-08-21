# Change files

| Area | Purpose and risk |
| --- | --- |
| `infra/modules/platform.bicep` | Replace the scheduled-query KQL and widen its window while preserving severity and action group. |
| Architecture/infrastructure tests | Lock query semantics and resource wiring. |
| KQL replay fixture/test asset in the existing test project | Prove incident and false-positive cases deterministically. |
| `docs/operations.md` | Refresh after approved deployment. |

# Context files

| File | Why read it |
| --- | --- |
| `docs/frd/frd-12-operator-experience.md` | OPS-08 alert outcome. |
| `docs/adr/0002-dotnet-modular-monolith-on-azure.md` | Azure monitoring boundary. |
| `infra/modules/platform.bicep` | Existing rule and action group. |

# Out of scope

No action-group recipient change, severity change, telemetry deletion, application instrumentation rewrite, or production deployment without approval.
