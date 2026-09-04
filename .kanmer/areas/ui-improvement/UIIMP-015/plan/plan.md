# Plan — UIIMP-015 (2026-09-04, gpt-5.6-terra high)

Starting state verified at `80f0ca26`: the script has one fixed capture
filter and `Invoke-TestUiPhase`; `TestUiSnapshotTests` generates all visual
states, deletes all non-generated committed pages, and offline-renders all
generated pages. `case-details` has three `pages/case-details--*.html` states.
CI already invokes unscoped `-Verify`.

Governing documents: this is local tooling, not product behaviour, so no PRD,
FRD, or ADR changes are needed. Follow `AGENTS.md`, `docs/runbook.md`, and
`docs/design/README.md`; the EPIC-012 build policy prohibits a local full
capture or full browser/integration suite.

1. **Add the scoped script interface and environment lifecycle.**

   - Reuse `Invoke-TestUiPhase`, the existing `$captureFilter` flow, and the
     existing `PEGASUS_TEST_UI_MODE` / `PEGASUS_TEST_UI_CAPTURE_DIR`
     save-and-restore pattern.
   - Touch only `scripts/Update-TestUiSnapshots.ps1`.
   - Add `-Scope <page-prefix,...>` and `-CaptureFilter <xUnit filter>`, with
     the current fixed filter as the `-CaptureFilter` default.
   - Save, set, and restore `PEGASUS_TEST_UI_SCOPE` alongside the existing
     variables. Explicitly clear it when `-Scope` is omitted so the default
     invocation cannot inherit an ambient scope.
   - Use the supplied capture filter for both existing browser and non-browser
     phases; retain the browser `MaxParallelThreads 2` split and all existing
     mode/capture-dir behaviour.

2. **Restrict the snapshot operation without changing its unscoped path.**

   - Reuse `Generate`, `BuildIndex`, `CommittedPages`, `WriteGenerated`,
     `VerifyOfflineBrowserRenderAsync`, and the existing `StateMatches`
     dictionary in `TestUiSnapshotTests`.
   - Touch only `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`.
   - Parse the scope once into trimmed page-name prefixes and use one shared
     matcher for `pages/<prefix>--`. Do not create a second state or prefix
     list; `StateMatches` remains the sole state-matching vocabulary.
   - When scoped, generate only catalogue states whose output file matches the
     shared matcher, so missing captures outside the selected cohort do not
     fail generation. Always add `index.html` through the existing
     `BuildIndex(manifest)` call, using the complete catalogue.
   - Apply the same matcher to committed-page orphan detection and update-mode
     deletion. Verification compares the scoped generated pages plus
     `index.html`; offline Chromium renders only the scoped page output.
   - When scope is unset, preserve existing ordering, normalization, full-page
     generation, orphan checking, deletion, and offline rendering exactly.
   - No new test project, test class, fake, package, UI route, Core policy, or
     catalogue state is needed.

3. **Document the supported focused command.**

   - Reuse the existing Test UI command paragraphs; do not touch the
     Kanmer-managed block.
   - Touch only `CLAUDE.md`, `AGENTS.md`, and `docs/runbook.md`.
   - Document the paired focused example:

     ```powershell
     pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 `
       -Scope case-details `
       -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests"
     ```

     and the retained-capture focused verification:

     ```powershell
     pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 `
       -Verify -SkipCapture -Scope case-details
     ```

   - Keep the ordinary unscoped refresh and verify commands as the default and
     CI path. `docs/engineering.md` is not touched: it does not document this
     procedure.

4. **Prove the focused cohort and record artifact evidence.**

   - Before the scoped update, record sorted SHA-256 path/hash pairs for every
     `docs/design/test-ui/pages/*.html` file except
     `case-details--*.html`; compare the same inventory afterwards. It must be
     identical, including membership.
   - Run the focused update with `CaseDetailsWebTests`, then focused
     `-Verify -SkipCapture`. Confirm the snapshot test compares and
     Chromium-renders only the three selected case-details pages while
     regenerating the catalogue index.
   - Open each selected artifact and record its byte size, `<!DOCTYPE html>`
     doctype, and expected marker in the post-implementation report:
     `Case Overview` for default, `Case unavailable` for unavailable, and
     `case changed` for conflict.
   - Run `Test-UiCatalogue.ps1`. Do not run an unscoped local snapshot capture,
     whole integration suite, or whole browser suite; GitHub CI proves the
     unscoped `-Verify` path on the exact PR head and must show no snapshot
     diff.

Acceptance conditions:

- `-Scope case-details -CaptureFilter
  "FullyQualifiedName~CaseDetailsWebTests"` affects only
  `pages/case-details--*.html` and rebuilds `index.html`.
- `-Verify -SkipCapture -Scope case-details` checks and offline-renders only
  those scoped pages.
- No-scope invocations retain the prior complete-cohort behaviour; exact-head
  CI passes its existing unscoped `-Verify` with no committed snapshot diff.
- `scripts/Test-UiCatalogue.ps1` passes.
- The diff contains only the five planned source/document files; no CI,
  package, catalogue, route, Core, or `OperatorLabels` change.

Binding design and engineering rules: this tooling change adds no operator UI
copy or labels, preserves exact existing state labels and absent-versus-disabled
behaviour, leaves business policy in Core, uses one prefix list/matcher, adds no
package, and relies on the focused integration/snapshot assertions to prove the
claim.

Stop condition: after the focused checks and required CI are green, write the
post-implementation report, open the PR targeting `dev` with
`Kanmer: UIIMP-015`, and move the ticket to **Review**. Do not merge it.
