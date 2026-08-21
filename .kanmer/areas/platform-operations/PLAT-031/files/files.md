# Files

Committed in `1a86f5db`, corrected in `fef817b8`.

| File | Change | Reuses |
| --- | --- | --- |
| `src/Pegasus.Core/Eva/CaseEvaMapping.cs` | `IsSwitchedOn(EvaMappingAcceptance)` | the existing acceptance value |
| `src/Pegasus.Core/Eva/EvaBundleSchema.cs` | `HandOffSwitchedOn` on the preparation; `IsWorthShowing` | — |
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` | `GetPreparationAsync` reports whether the hand-off is switched on instead of returning `null` | — |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml` | Panel gated on `IsWorthShowing` — one line | the existing panel |

## Untouched

`CaseEvaMapping.ActivationGateReason` stays as the server-side guard. This changes what is
**displayed**, not what is **enforced**.
