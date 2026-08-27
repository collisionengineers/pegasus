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

### Final CI root cause correction

A fresh run proved the explicit URL-builder workaround still omitted `mailbox`, so that workaround is removed and the original Tag Helper restored. The isolated candidate URL establishes that `MailboxFilter` is null during the GitHub GET even though `CaseQuery` binds and the raw request contains `mailbox=instructions`. The final fix reads the canonical `mailbox` value directly from `Request.Query` in `OnGetAsync`, applies the existing trim/null normalization, and leaves all other route generation unchanged. Acceptance remains the exact-anchor focused test and a fresh green SQL shard 1.

### Combined boundary fix

The GitHub evidence matrix isolates two independent failures: renamed-property binding plus Tag Helper generation fails; explicit candidate URL with the unpopulated property fails; direct query population plus the Tag Helper still fails. Therefore the final correction combines both narrow changes: populate `MailboxFilter` from the canonical GET query and generate only the case-candidate URL explicitly with `QueryHelpers` over the same eight values. No other links or handlers change. Acceptance remains focused exact-anchor pass, independent review, and fresh green SQL shard 1.

### Exact corrupted-value evidence

The custom failure message captured the full GitHub candidate URL: `mailbox` was an unrelated GUID while `pageNumber=2`, `caseQuery=MAIL31001`, and the target case were correct. Therefore the bound `MailboxFilter` is corrupted rather than absent. The candidate URL must source its mailbox value from the immutable raw `Request.Query["mailbox"]`; the existing direct normalization remains for other PageModel consumers. The exact-anchor test keeps the full diagnostic on failure. Acceptance remains focused local pass, independent review, and fresh green shard 1.

### Authentication-ID overwrite root cause

The exact corrupt GUID is `DevelopmentOfflineIdentity.AdministratorId`, proving the public bindable mailbox value and render-time query view are overwritten after the handler begins. Capture the canonical raw mailbox at the start of `OnGetAsync` into a private non-bindable field, assign the public property for existing consumers, and have the candidate URL prefer that immutable field with the bound property only as the non-GET fallback. Acceptance remains exact-anchor focused pass, independent review, and fresh green shard 1.

### Null mailbox distinction

Independent review identified that a nullable snapshot alone cannot distinguish an initialized GET with no mailbox from a non-GET render. Track initialization separately: an initialized GET uses its captured nullable value, while only a render without any GET snapshot may fall back to the bound property. Extend the existing no-mailbox case-candidate test to assert the exact result anchor has no mailbox parameter. Both focused candidate tests must pass.

## Scope correction — 2026-08-27

Every section above from "CI-resolution scope expansion" onward is withdrawn.
The GitHub failure was never a Message-page defect: the candidate URL the
diagnostic printed carried `mailbox=49f47eb9-c5b0-464f-b8f0-8c90ba061728`,
which is `TestMailboxId.From("instructions")` — the value the test itself
requested. MAIL-013 had switched the request to the stable GUID and left one
assertion expecting the literal `mailbox=instructions`. That assertion is
corrected under [[MAIL-016]] (PR #567). The private route snapshot, the
initialized flag, the `QueryHelpers` candidate builder and the regex-based test
were reverted, so `src/` carries no diff against `origin/dev`; the ticket is
back to its planned Test UI scope.

## Review dispositions — codex comments on PR #562

All 18 P1/P2 findings are quality/determinism issues in the test-only tooling,
not correctness of any product path. Disposition: **defer to [[UIIMP-005]]**
(nondeterministic tokens and trace ids, fabricated fixture data in
`TestUiFocusedRenderTests`, missing `Category!=Corpus` and browser thread cap
in the update script, concurrent capture collisions, no CI verify, orphaned
snapshots, CRLF, offline auto-refresh/preview/JSON endpoints, image fallback,
AGENTS.md convention). Reason: the tooling was independently approved at
`44d16f46`; the operator chose to merge the reviewed scope and harden it on its
own record.
