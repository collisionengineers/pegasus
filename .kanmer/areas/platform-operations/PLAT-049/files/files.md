# PLAT-049 file map

## Changed by this lane

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Operations/Index.cshtml.cs` | Inject `IAiJobQueries`, `ICreateAiJob`, `IConfirmAiJob`, `ICancelAiJob`, `IUnidentifiedStore`. Load the job list and the open-Unidentified picker. Add `OnPostSendUnidentifiedToAi`, `OnPostCompleteAiJob`, `OnPostCancelAiJob`. Add the record-route helpers the markup needs. |
| `src/Pegasus.Web/Pages/Operations/Index.cshtml` | New **AI Job List** panel first (meta, Send Unidentified to AI, table Job/Record/Started by/Created/State/Action). Service health second, per §1.11 order; its Action cell gains an explicit `—` where no retry target exists. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Append one nested static class `AiJobs` (kind names, state names, state tones, the job-count meta, the queue record label). Nothing existing is reordered or edited. |
| `tests/Pegasus.IntegrationTests/OperationsWebTests.cs` | New tests for the panel, the three handlers, and the `—` fallback. Extend the recording store with an AI-job fake. |

## Read, not changed

`src/Pegasus.Core/AiWork/AiJobs.cs`, `AiJobOperations.cs`,
`AiWorkContracts.cs`, `src/Pegasus.Core/Operations/ServiceHealth.cs`,
`RequestOperations.cs`, `src/Pegasus.Core/Intake/Unidentified/UnidentifiedContracts.cs`,
`src/Pegasus.Core/Eva/EvaApiContracts.cs`,
`src/Pegasus.Infrastructure/Persistence/EfAiJobStore.cs`,
`src/Pegasus.Infrastructure/DependencyInjection.cs`,
`src/Pegasus.Web/Pages/Shared/_StatusChip.cshtml`.

## Deliberately not changed

- `Pages/Shared/_StatusChip.cshtml` — shell lane's file, and UIIMP-009 /
  TICK-223 are in flight over the design-system files. Tone is supplied
  through the partial's own `ViewData["StatusTone"]` contract instead.
- `Core/Eva/**`, `Core/Operations/RequestOperations.cs`,
  `Core/Operations/ServiceHealth.cs` — the five gaps in `research` all live
  here and are outside this lane's boundary.
- `docs/design/test-ui/**` — snapshots are regenerated once per merge on the
  merging branch only (EPIC-011 decisions, 2026-08-29).

## Adversarial verifier corrections - 2026-08-29

This section supersedes conflicting entries in the changed-file table above.

- `Index.cshtml.cs` no longer loads an open-Unidentified picker. GET reads no
  Unidentified rows; POST validates one canonical reference and reuses
  `IUnidentifiedStore.GetByReferenceAsync`.
- `Index.cshtml` renders one reference input rather than an unbounded select.
- `OperatorLabels.AiJobs` now adds 67 lines against `origin/dev` and
  `StateToneOverride` contains only Queued, Taken and Draft ready. The shared
  chip owns its existing Completed, Failed, Cancelled and Expired mappings.
- `OperationsWebTests.cs` covers the effective Expired row, one rail queue query
  on GET, the point lookup on POST, a not-open refusal, and canonical `U412`.
