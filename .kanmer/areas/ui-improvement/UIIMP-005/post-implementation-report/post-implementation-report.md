# Post-implementation report — UIIMP-005

Branch `task/uiimp-005-test-ui-gate`, worktree `../pegasus-worktrees/uiimp-005-test-ui-gate`, four commits on top of `origin/dev` (`783b4b88`): `40b56e8f`, `b4d34912`, `04e580c5`, `37e77e3a`. PR #588.

## What changed, per finding

| Finding | Change |
| --- | --- |
| Fresh-capture nondeterminism | `VolatileGuidValueRegex` now also blanks `OperationId`, `ExternalReceiptToken` and `Token`; `SupportReferenceRegex` replaces the `/Error` support reference with `{{request-id}}`; `CompactGuidRegex` maps bounded 32-hex identifiers through the same `test-ui-guid-N` sequence as hyphenated GUIDs. |
| Invented Unidentified fixture | `OpenUnidentifiedDetailRendersThroughRazor` uploads `vehicle.png` (`MultiFormatFixture.TinyPngBase64`) under `FakeVrmRecognitionEngine`, lets `ProcessQueuedIntake` register the receipt as Unidentified, looks it up with `GetByOriginAsync(UnidentifiedOrigin.Receipt(id))`, and fetches its receipt image. No literal domain text remains. |
| Script filter / parallelism / collisions | Capture filter is `(…)&Category!=Corpus` with `-- xUnit.MaxParallelThreads=2`; `TestUiResponseCaptureMiddleware.WriteOnceAsync` writes each capture to `<hash>.<guid>` and moves it into `<hash>`, dropping a duplicate arrival. |
| Verify never in CI / orphans / CRLF | Verify compares newline-normalised text and fails on any `pages/*.html` no state generates; new `test-ui` CI job (build lane, Windows, pinned Playwright Chromium) runs `Update-TestUiSnapshots.ps1 -Verify` — one capture, then verify; `documentation` runs `Test-UiCatalogue.ps1` on every change set. |
| Live attributes offline | `LiveAttributeRegex` strips `data-auto-refresh`, `data-mail-preview-url`, `data-case-search-url`; `data-case-search-url` left `ApplicationUrlRegex`, so the JSON handler is no longer rewritten to a page. `site.js` treats each missing attribute as a no-op (checked). |
| Evidence-image fallback | The `{guid}` asset alias is gone. A state candidate is eligible only when every `/Received/{id}/Image` it shows was captured; otherwise the generator fails naming the state and the uncaptured receipt images. `IntakeWebDriver.GetHtmlAsync` fetches a page's receipt images when `PEGASUS_TEST_UI_CAPTURE_DIR` is set, so every group/status page captured through the driver carries its own receipts. |
| AGENTS.md | `## Commands` gains the regenerate → verify → catalogue paragraph and the CI statement. |
| Change flags | Build pattern gains `^docs/design/test-ui/`, `Update-TestUiSnapshots.ps1`, `Test-UiCatalogue.ps1`; four regression cases added. |
| EvaSubmission unclassified on `dev` (found by the new gate) | Absorbed by orchestrator decision: `catalogue.json` gains the `Administration/Principals/EvaSubmission.cshtml` entry (visual, state `administration-principal-eva-submission--default`, branch "Loaded organization and principal with the EVA API submission settings form."); `StateMatches` marker `<h1>EVA API submission for `; `OrganizationAdministrationWebTests` fetches the page through `GetHtmlAsync` for its existing `WEBP` principal. The snapshot HTML is written by the orchestrator's capture run, not by this branch. |

## Plan drift

- `Test-UiCatalogue.ps1` runs in the `documentation` lane only, not also in `test-ui` as the plan's step 8 said. Deliberate: `documentation` runs on every change set (so a page or catalogue edit without a build-relevant path is still checked), and a second run inside `test-ui` would gate nothing the first does not — it reads the same committed files, and the `-Verify` in `test-ui` already fails on a missing or orphaned page.
- `docs/design/test-ui/catalogue.json` and `OrganizationAdministrationWebTests.cs` joined the owned files after the initial plan (orchestrator decision, above).

## Evidence tier

- `dotnet build ./Pegasus.slnx --configuration Release` — green (implementer, after CA1859 fix); integration tests project rebuilt green after each later commit.
- `./scripts/Test-CiChangeFlags.ps1` — "CI change classification passed." (implementer).
- `./scripts/Test-UiCatalogue.ps1` — fails on this branch until the capture writes `pages/administration-principal-eva-submission--default.html` ("Prototype does not exist"); passes after the orchestrator's regeneration commit.
- Not run by the implementer, per the EPIC-011 rule: `dotnet test`, the snapshot capture/verify, browser tests. The orchestrator runs the wave loop on `37e77e3a`.

## Residual risks

- `CompactGuidRegex` could match a bounded 32-hex run inside a base64 data URL; the substitution is deterministic, and the only inlined assets are captured receipt images (the fixture PNG has no such run).
- Per-receipt eligibility can surface states whose producing test never fetched the thumbnails through `GetHtmlAsync` (a direct `client.GetAsync` bypasses the hook). The failure message lists the exact receipt image paths, so the fix is a one-line fetch in that test.
- Placeholder spelling is `{{requestid}}` (from `VolatileGuidValueRegex`, a lower-cased attribute name) beside `{{request-id}}` (support reference). Left as-is to avoid a code change racing the regeneration; a one-line unification is a candidate for the next touch of `TestUiSnapshotTests.cs`.
