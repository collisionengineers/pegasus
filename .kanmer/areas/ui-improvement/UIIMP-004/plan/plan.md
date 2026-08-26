# Plan — UIIMP-004

## Current target

Replace the parallel handwritten Test UI with deterministic HTML captured from the real Razor application. The current catalogue has 52 routed sources and 57 renderable visual states.

Investigation removed three obsolete branches that current PageModels cannot render (`dashboard--stale`, `received-details--partial`, and `operations--failed`) and renamed three reworked outcomes to their current terms (`inbox--unavailable`, and upload group/status `--needs-decision`).

## Implementation

1. Keep route/state ownership in `docs/design/test-ui/catalogue.json` and generate the index from it.
2. Reuse `IntakeWebApplicationFactory` and existing integration scenarios; install a test-only response-capture middleware only when `PEGASUS_TEST_UI_CAPTURE_DIR` is set.
3. Add focused current-branch renders only for states not already exercised by the existing suite.
4. Select each manifest state by route plus current rendered branch marker.
5. Normalize antiforgery/operation/cache values, mapped-static-asset fingerprints and trailing indentation; rewrite root-relative assets and visual navigation to repository-local targets. Preserve rendered elements, attributes, form wiring, SVGs, data hooks, layout and scripts.
6. Generate all pages only after every manifest state has a captured match; remove only orphaned generated HTML in the catalogue pages directory.
7. Verify by a clean application recapture followed by byte comparison with the committed normalized output.
8. Keep Test UI disconnected from application/publish inputs and retain the existing Live/Test launcher boundary.
9. Update the design authority, README and runbook.

## Verification

- Clean capture suite: 260 passed, 11 expected corpus skips, 0 failed.
- Snapshot update and verify: 57/57 generated states.
- Catalogue: 52 routed sources, 57 prototypes, 0 broken local references.
- Release build: 0 warnings, 0 errors.
- Live/Test UI launcher checks and `git diff --check`: pass.
- Deployment: n/a.

## Simplification pass — 2026-08-26

- Reuse: existing integration factories, authentication, database fixtures, failure doubles, Web tests, CSS/JS and launcher retained.
- Simplification: removed the separate handwritten inventory and six obsolete/renamed files; one JSON manifest now owns classification and states.
- Efficiency: capture is environment-gated and piggybacks on existing tests; no second web host, template engine, converter, runtime mode or production service was added.
- Altitude: the implementation stays test/documentation-only; application behavior and deployment composition are unchanged.
- Disposition: no further behavior-preserving simplification identified.

## CI-resolution scope expansion — 2026-08-26

The operator explicitly asked to check and resolve the failing GitHub issue on PR #562. The runner used SDK 10.0.400 despite the shared build action describing a pinned SDK, while the exact 306-test SQL shard passes on the repository machine with SDK 10.0.303. Keep this expansion limited to making the existing SDK contract deterministic: install 10.0.303 in the shared action and restrict `global.json` roll-forward to patch servicing. Do not change the unrelated MailWorkspace behavior or weaken its assertion. Acceptance is a fresh GitHub run that reports the effective 10.0.303 SDK and passes the previously failing SQL shard; otherwise revert this hypothesis and continue diagnosis.

### CI-resolution correction

The SDK hypothesis was disproven by a clean GitHub run under 10.0.303 and all SDK/workflow changes were reverted. Narrowing the regression assertion to the exact matching case anchor then confirmed the GitHub/Linux rendering defect: that candidate URL omitted the active mailbox context. Under the operator-authorized request to resolve this PR's GitHub failure, replace only that candidate anchor's individual Tag Helper route attributes with the existing `QueryHelpers.AddQueryString` convention over the same eight route values, and assert the decoded URL of the exact `targetCaseId` anchor. Acceptance is the focused local test plus a fresh green GitHub run, including SQL shard 1; no other MailWorkspace behavior is in scope.
