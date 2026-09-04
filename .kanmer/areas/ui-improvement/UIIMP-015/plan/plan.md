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
     the current fixed cohort as the `-CaptureFilter` default.
   - Save, set, and restore `PEGASUS_TEST_UI_SCOPE` alongside the existing
     variables. Explicitly clear it when `-Scope` is omitted so the default
     invocation cannot inherit an ambient scope.
   - Use the supplied capture filter for both existing browser and non-browser
     phases; retain the browser `MaxParallelThreads 2` split and all existing
     mode/capture-dir behaviour.
   - Keep `Category!=Corpus` outside the caller-supplied expression: make the
     `-CaptureFilter` default the cohort alternation alone and have the two
     phase filters append `&Category!=Corpus` themselves, so a scoped filter
     cannot drag Corpus tests in. Corpus-trait tests do live inside classes
     the cohort names (`QdosIntakeWebTests`, `MultiFormatGenuineCorpusWebTests`),
     so a supplied `FullyQualifiedName~QdosIntakeWebTests` would otherwise run
     them. The default invocation's two phase expressions then read
     `(<cohort>)&Category!=Corpus&Category=Browser` and its `Category!=Browser`
     twin — textually different from today, selecting exactly the same tests.
   - No phase-skipping machinery is needed: a phase whose filter matches no
     test exits 0. Verified 2026-09-04 with this project's runner versions
     (`Microsoft.NET.Test.Sdk` 17.14.1, `xunit` 2.9.3,
     `xunit.runner.visualstudio` 3.1.4, `net10.0`): a `Category=Browser` filter
     over a `Category=SqlServer`-only assembly returned exit code 0. This
     matters because `CaseDetailsWebTests` carries no `Browser` trait, so the
     ticket's own focused command runs an empty browser phase.
   - `-CaptureFilter` is only meaningful with `-Scope`; supplying it alone
     leaves generation unscoped and fails with the existing explicit "No
     captured Razor response matched" message. Document the pairing; add no
     guard for it.

2. **Restrict the snapshot operation without changing its unscoped path.**

   - Reuse `Generate`, `BuildIndex`, `CommittedPages`, `WriteGenerated`,
     `VerifyOfflineBrowserRenderAsync`, and the existing `StateMatches`
     dictionary in `TestUiSnapshotTests`.
   - Touch only `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`.
   - Parse the scope once into trimmed page-name prefixes and use one shared
     matcher for `pages/<prefix>--`. The `--` is part of the match, so `case`
     does not select `cases--*`. Do not create a second state or prefix
     list; `StateMatches` remains the sole state-matching vocabulary.
   - When scoped, generate only catalogue states whose output file matches the
     shared matcher, so missing captures outside the selected cohort do not
     fail generation. Always add `index.html` through the existing
     `BuildIndex(manifest)` call, using the complete catalogue.
   - **Filter only `state.File` inside the existing per-state loop.** Do not
     filter `manifest`, and do not filter `entry.States` before `otherMatches`
     is computed: `otherMatches` is the set of *sibling* state matchers that
     decides which capture a matcher-less state selects, and the full manifest
     is what `NormalizeAndRewrite`'s `ApplicationUrlRegex` resolves internal
     links against. Narrowing either would change the bytes a scoped page
     generates relative to the same page generated unscoped, which is exactly
     what this ticket forbids.
   - Fail explicitly when a supplied prefix matches no catalogue state,
     naming the unmatched prefixes. Without it a typo is a silent no-op:
     update mode would write only `index.html` and delete nothing, and verify
     mode would pass vacuously.
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
   - Touch only `CLAUDE.md`, `AGENTS.md`, and `docs/runbook.md`. The three
     places are the `CLAUDE.md`/`AGENTS.md` paragraph at line ~168 ("After
     changing a routed Razor page…") and `docs/runbook.md` at line ~653 ("The
     Test UI files are generated from actual integration-test Razor
     responses…").
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
     CI path. `docs/engineering.md` is not touched: verified 2026-09-04 that
     its only `snapshot` mention is the D43 fixture rule, not this procedure.

4. **Prove the focused cohort and record artifact evidence.**

   - Before the scoped update, record sorted SHA-256 path/hash pairs for every
     `docs/design/test-ui/pages/*.html` file except
     `case-details--*.html`; compare the same inventory afterwards. It must be
     identical, including membership.
   - Run the focused update with `CaseDetailsWebTests`, then focused
     `-Verify -SkipCapture`. Confirm the snapshot test compares and
     Chromium-renders only the three selected case-details pages while
     regenerating the catalogue index.
   - Prove the unmatched-prefix guard once, with a deliberately wrong
     `-Scope` over the retained capture; record the message and exit code.
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
- A `-Scope` prefix matching no catalogue state fails, naming the prefix.
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

## Plan review (2026-09-04, Claude Opus)

Reviewed against the ticket body, EPIC-012 §Build policy, and the two source
files at `80f0ca26`. Verdict: the plan answers the ticket, stays inside the
owned paths (five files), breaks no repository rule, and is proportional to
its diff. Rule 24 is satisfied by step 3; no package, project, script, or
category trait is added; `.github/workflows/ci.yml` is untouched, and UIIMP-015
is the one lane the build policy allows to edit `TestUiSnapshotTests.cs` and
`scripts/*.ps1`. Findings, all applied to the plan text above:

| # | Finding | Evidence | Disposition |
| --- | --- | --- | --- |
| 1 | Byte-identity trap: filtering `entry.States` or `manifest` instead of `state.File` would change scoped output. `otherMatches` is built from the sibling states of the same entry and decides which capture a matcher-less state picks; `ApplicationUrlRegex` resolves internal links against the whole manifest. | `TestUiSnapshotTests.cs`, `Generate` and `NormalizeAndRewrite` | Fixed — step 2 names the exact filter point and forbids the other two. |
| 2 | A `-Scope` prefix matching nothing was a silent no-op: update writes only `index.html` and deletes nothing; verify passes vacuously. | `WriteGenerated` and the orphan check both operate on the generated set | Fixed — step 2 requires an explicit failure naming unmatched prefixes; acceptance condition, proof step and checklist item added. |
| 3 | A caller-supplied `-CaptureFilter` would drop `Category!=Corpus`, and Corpus-trait tests sit inside cohort classes (`QdosIntakeWebTests` 6, `MultiFormatGenuineCorpusWebTests` 5). | grep for the Corpus trait over `tests/Pegasus.IntegrationTests` | Fixed — step 1 moves the exclusion into the phase builder; default-path test selection is unchanged. |
| 4 | Premise checked, not argued: the ticket's own focused command runs a browser phase matching zero tests, because `CaseDetailsWebTests` carries only `Category=SqlServer`. Zero matches exit 0 on this runner stack, so no phase-skipping is needed. | Probe project pinned to `Microsoft.NET.Test.Sdk` 17.14.1 / `xunit` 2.9.3 / `xunit.runner.visualstudio` 3.1.4 on `net10.0`: a `Category=Browser` filter over a SqlServer-only assembly returned exit code 0 | Recorded — step 1 states the verified fact; no design change. |
| 5 | The "engineering.md is not touched" claim was unverified. | Its only `snapshot` match is line 203, the D43 fixture rule | Confirmed — the plan now cites the check. |
| 6 | The checklist's bare `--filter "FullyQualifiedName~TestUiSnapshotTests"` run executes with `PEGASUS_TEST_UI_MODE` unset, so the test returns immediately; it proves compilation only. | `CapturedRazorResponsesMatchCommittedTestUiSnapshots` returns when the mode is blank | Accepted as-is — it is the cheap build/discovery check; the report must not present it as snapshot evidence. |
| 7 | `-CaptureFilter` without `-Scope` narrows capture but not generation, failing on "No captured Razor response matched". | `Generate`'s `missing` assertion | Accepted risk — documented as a pair in step 1; no guard, per the no-speculative-abstraction rail. |

Nothing the ticket body implies is unaddressed, and no operator question is
open.
