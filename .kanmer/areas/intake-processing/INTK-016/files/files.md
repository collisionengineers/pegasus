# File map — INTK-016

## Modified

- `src/Pegasus.Web/Presentation/UploadOutcome.cs` — `UploadOutcomeView` gains an optional attach offer (receipt id + file/report context); `BuildAsync` sets it on `ReadyToCreate` / `PossibleMatch` / `ImageCaseRegistered`; Attached branch consults `receipt.CurrentCaseId` and words staff vs automatic provenance honestly.
- `src/Pegasus.Web/Pages/Shared/_UploadOutcome.cshtml` — renders the decision: existing primary/secondary actions, plus `<details>`-based "Add to an existing case" (search input + reason + confirm form) and "Cancel" link.
- `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml(.cs)` — `OnGetCaseSearchAsync` (JSON) + `OnPostAttachAsync` handlers; status/error notice rendering; partial wiring for the group-level outcome.
- `src/Pegasus.Web/Pages/UploadStatus.cshtml(.cs)` — same two thin handlers; notice rendering.
- `src/Pegasus.Web/wwwroot/js/site.js` — `[data-case-search]` combobox enhancement: debounced fetch, listbox rendering, keyboard navigation, `aria-expanded`/`aria-activedescendant` managed by script.
- `src/Pegasus.Web/wwwroot/css/site.css` — combobox listbox styles (tokens/hairline conventions).
- `src/Pegasus.Web/Program.cs` — DI registration for the shared decision service.
- `docs/frd/frd-02-intake-and-source-identity.md` — Upload confirmation surface section: attach decision now performed on the surface (search + explicit staff confirm), replay-safe; "never mutates anything" sentence replaced by the real contract.
- `docs/frd/frd-12-operator-experience.md` — Upload section: the three options and the autocomplete search behaviour.

## Added

- `src/Pegasus.Web/Presentation/UploadCaseDecision.cs` — shared presentation service: case-search suggestions (via `ISearchCases`) and the attach orchestration (`IGetCase` → `IAcquireCaseEditLease` → `ILinkIntake`; deterministic operation keys; already-linked short-circuit).
- `tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs` — autocomplete endpoint (authorised / anonymous redirect / matches), attach end-to-end on a fixture case (instruction receipt and image-registered group), report-not-reoffer when automation already attached, no-script reference resolution.
- Browser coverage — combobox accessibility added beside `Browser/UploadRowsBrowserTests.cs` (new test class or cases in it) + AccessibilityTests remain green.

## Read (unchanged, reused)

`src/Pegasus.Core/Cases/CaseQueries.cs` (`ISearchCases`, `IGetCase`), `src/Pegasus.Core/Workflow/CaseCommandContracts.cs` (`IAcquireCaseEditLease`), `src/Pegasus.Core/Intake/DurableIntake.cs` (`LinkIntake` — already calls `SyncMergeAfterLinkAsync`), `src/Pegasus.Core/ImageIntake/ImageIntakeCasePairing.cs`, `src/Pegasus.Web/Presentation/OperatorLabels.cs` (`CaseStage`), `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs`.
