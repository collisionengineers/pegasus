# Plan — UIIMP-005

Diff estimate: ~260 lines across 9 files (≈150 C#, ≈40 PowerShell, ≈45 YAML, ≈10 Markdown). No new packages, no new projects, no snapshot regeneration in this PR.

## Premises checked (read-only)

- Committed pages still carry run-specific values: `name="OperationId"` (`case-create--default`), `name="ExternalReceiptToken"` (`upload--default`), `name="Token"` (`upload-request--default`), a `case-attach-<32 hex>` receipt id (`upload-group-status--processing`), and the W3C trace id in `<code id="support-reference">` (`error--default`). `GuidRegex` only matches D-format, so N-format survives.
- `data-auto-refresh` (6), `data-mail-preview-url` (1) and `data-case-search-url` rewritten to `upload-group-status--default.html` (2) are present in committed pages. `site.js` treats each missing attribute as a guarded no-op.
- `ReadAssetsAsync` registers `assets["/Received/{guid}/Image"]` for the first captured image, so any uncaptured receipt was rendered with another receipt's bytes (3 pages carry `data:image`).
- `TestUiFocusedRenderTests.OpenUnidentifiedDetailRendersThroughRazor` registers `SubmissionGroup(Guid.NewGuid())` with `"test detail"` / `"test-worker"`; the catalogue branch says "Open item with retained source receipt". `ProcessQueuedIntake` registers an image-only receipt as Unidentified once `FakeVrmRecognitionEngine` abstains, and `IUnidentifiedStore.GetByOriginAsync(UnidentifiedOrigin.Receipt(id))` finds it.
- `Update-TestUiSnapshots.ps1` capture filter has no `Category!=Corpus` and no `MaxParallelThreads` cap; the browser job uses `-- xUnit.MaxParallelThreads=2`.
- CI never runs `Test-UiCatalogue.ps1` or the snapshot verify; `Get-CiChangeFlags.ps1` build pattern excludes `docs/`.
- `core.autocrlf=true` on this machine; the verify compares `File.ReadAllText` byte-for-byte against `\n`-normalised generated text.

## Steps

1. **Determinism** (`TestUiSnapshotTests.cs`). Extend `VolatileGuidValueRegex` names with `operationid|externalreceipttoken|token`; add a `SupportReferenceRegex` for the `/Error` `<code id="support-reference">` content; add a 32-hex `CompactGuidRegex` mapped through the same `test-ui-guid-N` sequence as the D-format regex (reuses the existing `guids` dictionary and numbering). Reuses the existing `NormalizeAndRewrite` pipeline order.
2. **Offline attributes** (`TestUiSnapshotTests.cs`). New `LiveAttributeRegex` strips `data-auto-refresh`, `data-mail-preview-url` and `data-case-search-url` attributes before URL rewriting; drop `data-case-search-url` from `ApplicationUrlRegex`. State matching still runs on the raw candidate HTML, so `data-auto-refresh="2000"` keeps selecting the processing states.
3. **Per-receipt evidence images** (`TestUiSnapshotTests.cs`, `IntakeWebTestSupport.cs`, `TestUiFocusedRenderTests.cs`). Remove the `{guid}` asset alias in `ReadAssetsAsync`/`NormalizeAndRewrite`. A candidate is eligible for a state only when every `/Received/{id}/Image` it references was captured; when a state matched candidates but none is eligible, the assertion names the state and the missing receipt image paths. `IntakeWebDriver.GetHtmlAsync` fetches the referenced receipt images when capture is on (one shared helper, already used by every group/status page test); the focused Unidentified render fetches its own receipt image.
4. **Fixture** (`TestUiFocusedRenderTests.cs`). Replace the hand-registered item with `IntakeWebDriver.UploadAndProcessAsync("vehicle.png", MultiFormatFixture.TinyPngBase64)` under `FakeVrmRecognitionEngine` (the same shape as `ImageIntakeWebTests`), then `GetByOriginAsync`. No literal domain strings remain in the test.
5. **Verify hardening** (`TestUiSnapshotTests.cs`). Verify mode normalises the committed file's newlines before comparing (reuses `NormalizeNewLines`) and fails on any `pages/*.html` not in the generated set (mirrors `WriteGenerated`'s orphan sweep).
6. **Capture collisions** (`TestUiResponseCapture.cs`). Write each capture into `<hash>.<Guid:N>` and `Directory.Move` it to `<hash>`; if `<hash>` already exists at either point the temp directory is deleted — same hash means same request and body.
7. **Script** (`Update-TestUiSnapshots.ps1`). Wrap the capture filter as `(…)&Category!=Corpus` and append `-- xUnit.MaxParallelThreads=2`.
8. **CI** (`ci.yml`, `Get-CiChangeFlags.ps1`, `Test-CiChangeFlags.ps1`). `documentation` gains a `Test-UiCatalogue.ps1` step (every change set). New `test-ui` job, `needs: changes`, `if: build == 'true'`, `windows-latest`, mirrors the `browser` job's Playwright cache/install, runs `./scripts/Update-TestUiSnapshots.ps1 -Verify` (one capture, then verify) and `./scripts/Test-UiCatalogue.ps1`. Build pattern gains `^docs/design/test-ui/` and `^scripts/(Update-TestUiSnapshots|Test-UiCatalogue)\.ps1$`; two regression cases added.
9. **AGENTS.md** `## Commands`: one paragraph stating regenerate (`Update-TestUiSnapshots.ps1`), verify (`-Verify`, `-Verify -SkipCapture` to reuse a capture) and `Test-UiCatalogue.ps1`, and that CI runs the verify in the build lane.

## Acceptance

- `dotnet build ./Pegasus.slnx --configuration Release` green (implementer).
- Orchestrator: `./scripts/Update-TestUiSnapshots.ps1` then `./scripts/Update-TestUiSnapshots.ps1 -Verify` (a second capture) exits 0 with no `git status` drift under `docs/design/test-ui/`; `./scripts/Test-UiCatalogue.ps1` exits 0; `./scripts/Test-CiChangeFlags.ps1` exits 0.
- Committed pages contain no `data-auto-refresh`, `data-mail-preview-url`, `data-case-search-url`, 32-hex or trace-id values after regeneration.

## Simplification pass — 2026-08-28

Lenses run over `git diff origin/dev...HEAD` (reuse, simplification, efficiency, altitude).

| Finding | Lens | Disposition |
| --- | --- | --- |
| `ReceiptImageUrlRegex` was defined in both `TestUiSnapshotTests` and `IntakeWebDriver` (second copy of one concept). | reuse | Fixed: one `internal` definition on `IntakeWebDriver`, reused by the snapshot generator. |
| `WriteGenerated` and the verify orphan check each enumerated `pages/*.html` with their own path juggling. | simplification | Fixed: one `CommittedPages` helper feeds both. |
| `CapturedAssetUrlRegex` callback decoded and split the URL inline, and the receipt-image check needed the same. | simplification | Fixed: `AssetPath` helper. |
| `MissingReceiptImages` runs twice per candidate on a miss (eligibility filter, then the failure message). | efficiency | Rejected: a few dozen small strings per state; a cached pair would cost more lines than it saves. |
| `CompactGuidRegex` (bounded 32-hex) could in principle match inside a base64 data URL. | altitude | Accepted risk: substitution is deterministic either way; the only data URLs are captured receipt images, and the 1x1 fixture PNG carries no such run. Noted in the report. |
| `using Pegasus.Core.Identity` left over after removing `ActionActor`. | simplification | Fixed. |
| CA1859 on `WriteGenerated(IReadOnlyDictionary)`. | analyzer | Fixed: takes the concrete `Dictionary` `Generate` returns. |
