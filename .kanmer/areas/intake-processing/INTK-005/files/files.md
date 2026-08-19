# Files — INTK-005

## Change surfaces

| Path/module | Why it is touched | Risk |
|---|---|---|
| `src/Pegasus.Web/Pages/Upload.cshtml` | Enable multiple selection/drop and show the selected batch using the PLAT-006 dropzone convention. | Accessibility or JS/no-script regression; accidental conflict with PLAT-006. |
| `src/Pegasus.Web/Pages/Upload.cshtml.cs` | Bind and validate a file collection, assign per-file replay identities, and call existing `IIntakeSubmission` for each file. | Duplicate source identity, excessive buffering, or silent partial loss. |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml(.cs)` or a batch-result page under `Pages/Upload*` | Present every accepted/failed file and link to its existing receipt status. | Inventing a second status vocabulary or hiding partial outcomes. |
| `src/Pegasus.Web/wwwroot/js/site.js` | Extend PLAT-006's data-attribute dropzone enhancement from one file to a list. | Browser `DataTransfer` compatibility and keyboard/no-script behaviour. |
| `src/Pegasus.Web/wwwroot/css/site.css` | Style the selected/result list within existing Upload patterns. | Unnecessary new design primitives or responsive regressions. |
| `tests/Pegasus.IntegrationTests/QdosIntakeWebTests.cs` and focused Upload/browser tests | Prove several files stage separately, replay safely, and remain visible under mixed outcomes. | Tests that check only HTML selection and miss durable receipts/work items. |

## Ripple effects

- Request/form limits and memory use must be checked against the real ASP.NET/Container Apps boundary; retain the existing per-file limit.
- Each accepted file must create its own staged receipt and Worker item through the existing Core port.
- Receipt/status queries remain single-receipt owners; any batch view composes rather than duplicates them.
- PLAT-006 must merge first or this ticket must rebase around its Upload markup to avoid overwriting owned work.

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/frd/frd-02-intake-and-source-identity.md` | Durable receipt, idempotency, and fail-closed intake requirements. |
| `docs/frd/frd-12-operator-experience.md` | Operator feedback and surface requirements. |
| `docs/design/README.md` | Existing Upload/dropzone visual authority. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Existing staging and replay implementation to reuse. |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | One source identity and outcome vocabulary per file. |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml.cs` | Current single-receipt status model. |
| PLAT-006 ticket documents | The immediately preceding Upload markup/JS changes and conflict boundary. |

## Deliberately out of scope

- Changing per-file format/size policy, Worker processing, case-allocation policy, or public request-upload limits.
- A new batch store, new intake service, or second status taxonomy.
- Literal unbounded buffering of arbitrary files in one HTTP request.
