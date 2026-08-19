# Post-implementation report — PR-009

## Summary

The apparent Chromium pagination omission was upstream template truncation. Scriban's `TemplateContext` defaults `LimitToString` to 1,048,576 characters and silently appends `...` when a render exceeds it. Eight accepted base64 photos pushed the composed assessment past that limit during the third image, so Chromium never received the remaining photos, Statement of Truth or signature. The fix sets Scriban's documented unlimited mode on the existing context. Governed rendererref1 HTML/CSS, content, ordering, density and page furniture are unchanged.

## Files changed

| File | Change and rationale |
| --- | --- |
| `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` | Sets `TemplateContext.LimitToString = 0` before the existing render. This removes silent truncation without imposing a product content cap or adding a render/layout path. |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` | Reuses a shared provider helper and adds the real-Chromium 80×3-list/8-photo regression. It asserts multi-page flow, terminal entries, every-page case reference, at least eight PDF images, Statement of Truth, accepted engineer identity and no placeholders. |

No template, stylesheet, Core contract, package, lock, project, workflow or documentation file changed.

## Failing-before / passing-after evidence

Before the fix, the new test failed consistently after rendering 11 pages: all three terminal `080` items survived, but PdfPig found only the first two photos and no Statement of Truth. Diagnostic HTML was exactly 1,048,598 bytes (the 1,048,576-character Scriban cap plus ellipsis/encoding edge), ended during the third photo and lacked the tail. Layout experiments did not change the boundary and were reverted.

After `LimitToString = 0`, the unchanged regression passed through real Chromium with all asserted content. The full renderer Browser class passed 6/6.

## Governing docs

- **FRD-11:** complete accepted assessment content and ordered custody-validated photos now reach Chromium; exact wording/signature and fixed no-density activation remain unchanged.
- **ADR-0025:** correction remains inside the existing Infrastructure adapter and integration test project; no service, project, API, runtime or policy owner was added.
- **EPIC-004 / rendererref1:** normal styling, two-column 48mm photos, no captions, order and page furniture remain unchanged.

## Verification

- `dotnet restore --locked-mode` — passed.
- `dotnet build --configuration Release --no-restore` — passed in 28.85s, 0 warnings/errors.
- new real-Chromium regression — failing before, passing 1/1 after.
- `dotnet test ... --filter FullyQualifiedName~AssessmentReportRendererTests` — 6/6 passed through real Chromium (final run 27s).
- focused Core report tests — 11/11 passed.
- dependency-direction tests — 39/39 passed.
- `git diff --check` — clean.
- focused source search — no density/compact/ultra/multipass/truncation/cap path introduced.

## Simplification

The final production diff is one existing-context property. All exploratory layout changes and diagnostic artifact writes were removed. The test reuses the existing composition fixture via one extracted provider helper. No simplification finding was deferred.

## Risks and follow-ups

Setting zero delegates size to accepted input and host memory rather than silently dropping content, which is required here because the approved report may contain every accepted photo and no product cap is authorized. Existing readiness/custody validation remains in Core. [[TICK-213]] can resume its normal-density evidence after this PR merges.

## Verification hand-off

On merged `dev`, rerun locked restore/Release build and the focused renderer Browser suite, and confirm the stress assessment contains terminal lists, eight images, Statement of Truth/signature and every-page reference. This is source/local/CI renderer evidence only, not Azure deployment or live caller proof.
