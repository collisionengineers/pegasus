# Files — UIIMP-005

## Owned (modified by this ticket)

| File | Change |
| --- | --- |
| `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` | Normalise N-format GUIDs, the `/Error` support reference and the `OperationId` / `ExternalReceiptToken` / `Token` hidden values; strip live `data-auto-refresh`, `data-mail-preview-url`, `data-case-search-url`; per-receipt evidence-image substitution (no first-image fallback); verify rejects orphaned `pages/*.html` and compares newline-normalised text; `StateMatches` marker for the EvaSubmission page. |
| `tests/Pegasus.IntegrationTests/TestUiResponseCapture.cs` | Collision-safe capture directory writes (unique staging directory, then move; identical hash already present is a no-op). |
| `tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs` | Unidentified render driven by the real pipeline on the repository PNG fixture (`MultiFormatFixture.TinyPngBase64` + `FakeVrmRecognitionEngine`) instead of invented `test detail` / `test-worker` data; fetches the receipt image so the snapshot carries its own receipt. |
| `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` | `IntakeWebDriver.GetHtmlAsync` fetches the receipt images a captured page references when `PEGASUS_TEST_UI_CAPTURE_DIR` is set; single `ReceiptImageUrlRegex` definition. |
| `tests/Pegasus.IntegrationTests/OrganizationAdministrationWebTests.cs` | Fetches the EvaSubmission page through `GetHtmlAsync` for the existing `WEBP` principal so a capture records it. |
| `docs/design/test-ui/catalogue.json` | Entry for `Administration/Principals/EvaSubmission.cshtml` (visual, one `default` state, `pages/administration-principal-eva-submission--default.html`). Owned here by orchestrator decision on 2026-08-28, pre-empting PLAT-029's catalogue ownership for this one entry: a gate that fails on `dev` is this ticket's finding. |
| `scripts/Update-TestUiSnapshots.ps1` | `Category!=Corpus` on the capture filter; `-- xUnit.MaxParallelThreads=2`. |
| `scripts/Get-CiChangeFlags.ps1` | `docs/design/test-ui/` and the two Test UI scripts join the build pattern. |
| `scripts/Test-CiChangeFlags.ps1` | Regression cases for the new build paths. |
| `.github/workflows/ci.yml` | `documentation` runs `Test-UiCatalogue.ps1` on every change set; new `test-ui` job in the build lane runs one capture + verify on Windows with the pinned Playwright Chromium. |
| `AGENTS.md` | `## Commands`: the regenerate/verify convention. |

## Consumed, not modified

- `scripts/Test-UiCatalogue.ps1` — already rejects unlinked prototypes; CI now calls it.
- `docs/design/test-ui/pages/*.html`, `index.html` — not regenerated here (the orchestrator regenerates; `index.html` is rebuilt from the manifest by the generator).
- `.github/actions/dotnet-build/action.yml`, `tests/Pegasus.IntegrationTests/xunit.runner.json` — reused as-is.
- `src/Pegasus.Web/wwwroot/js/site.js` — verified that a missing `data-auto-refresh` / `data-mail-preview-url` / `data-case-search-url` attribute is a guarded no-op.
- `src/Pegasus.Web/Pages/Error.cshtml`, `Upload.cshtml`, `Cases/Create.cshtml`, `Uploads/Request.cshtml`, `UploadGroupStatus.cshtml`, `Administration/Principals/EvaSubmission.cshtml(.cs)` — sources of the values normalised and the branch classified.
- `docs/design/README.md` §Test UI — already states regenerate then verify; unchanged.

## Belongs to another ticket

- Nothing outstanding. The EvaSubmission classification gap (TICK-077) was absorbed here by orchestrator decision.
