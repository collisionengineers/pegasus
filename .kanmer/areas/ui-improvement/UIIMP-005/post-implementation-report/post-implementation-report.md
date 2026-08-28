# Post-implementation report — UIIMP-005

Branch `task/uiimp-005-test-ui-gate`, worktree `../pegasus-worktrees/uiimp-005-test-ui-gate`, three commits on top of `origin/dev` (`783b4b88`).

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

## Evidence tier

- `dotnet build ./Pegasus.slnx --configuration Release` — green (implementer, after CA1859 fix).
- `./scripts/Test-CiChangeFlags.ps1` — "CI change classification passed." (implementer).
- Not run by the implementer, per the EPIC-011 rule: `dotnet test`, the snapshot capture/verify, browser tests. The orchestrator runs the wave loop.

## Found on `origin/dev`, not absorbed

`./scripts/Test-UiCatalogue.ps1` fails on current `dev`: `src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml` (a visual page, TICK-077 / PR #574) has no `catalogue.json` entry. The new `documentation` and `test-ui` lanes will therefore be red on this PR until that page is classified and its snapshot generated. That classification is a `catalogue.json` structural edit (waves.md lists the file under PLAT-029) and the snapshot needs a regeneration run; both are outside this ticket's owned files.

## Residual risks

- `CompactGuidRegex` could match a bounded 32-hex run inside a base64 data URL; the substitution is deterministic, and the only inlined assets are captured receipt images (the fixture PNG has no such run).
- Per-receipt eligibility can surface states whose producing test never fetched the thumbnails through `GetHtmlAsync` (a direct `client.GetAsync` bypasses the hook). The failure message lists the exact receipt image paths, so the fix is a one-line fetch in that test.
