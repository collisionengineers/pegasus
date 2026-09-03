---
kind: review-attestation
pr: "650"
head_sha: "251ad4493b4a11ac0b9d4e68055bf0bcedf10fef"
verdict: needs-changes
reviewer: "case-022-review-agent"
independent: true
plan_hash: "5e3b80375761703d"
ticket_updated: "2026-09-03T16:22:55.937Z"
board_sha: "8a7ec7e29901a3310647a1ff780397e947986843"
expected_reviewers:
  - "case-022-review-agent"
threads_snapshot: []
findings: []
---

# Review — CASE-022 / PR #650

## Decision

No findings in the code or documentation diff. The implementation matches the
ticket packet, plan, governing FRD, and named production caller. The Kanmer
verdict is nevertheless `needs-changes` because the exact-head GitHub checks
are not green. There is no in-scope blocker or major review finding to return
for implementation, so CASE-022 remains in Review on its existing branch,
worktree, and PR.

The expected reviewer set settled on head
`251ad4493b4a11ac0b9d4e68055bf0bcedf10fef` with this independent
whole-diff review. GitHub had no reviews, comments, review requests, or review
threads at the final gather, so `threads_snapshot` is truthfully empty.

## Changes reviewed

The nine-file merge diff from `dev` to the exact head was reviewed against
the complete CASE-022 packet and
`docs/frd/frd-02-intake-and-source-identity.md`. It replaces the request
upload store's unsupported legacy content write with the existing managed
custody write, adds token-free public-upload request telemetry, exercises the
real production composition, and distinguishes repaired repository state from
the still-unreleased production state in the current-state documents.
`git diff --check` passed and the implementation worktree was clean.

## Production caller

The production chain is complete and uses the existing functions:

1. `POST /Uploads/{token}` is the routed Razor endpoint
   (`src/Pegasus.Web/Pages/Uploads/Request.cshtml:1`).
2. `RequestModel.OnPostAsync` invokes its injected `IUploadToRequest`
   (`Request.cshtml.cs:12,95`).
3. Production infrastructure maps `IUploadToRequest` to
   `EfDocumentRequestStore`
   (`src/Pegasus.Infrastructure/DependencyInjection.cs:483`).
4. `EfDocumentRequestStore` builds the persisted
   `ManagedDocumentContentAddress` and calls
   `IDocumentContentStore.StoreVersionAsync`
   (`src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:302`).
5. Production document storage maps `IDocumentContentStore` to
   `BoxDocumentContentStore`
   (`src/Pegasus.Infrastructure/DependencyInjection.cs:546,602`), whose
   managed write is implemented at
   `src/Pegasus.Infrastructure/Custody/BoxDocumentContentStore.cs:50`.
6. The Web composition root calls both infrastructure registration and
   production document storage
   (`src/Pegasus.Web/Program.cs:633,642`).

This proves the requested caller:
`POST /Uploads/{token}` → `RequestModel` → `IUploadToRequest` →
`EfDocumentRequestStore` → `IDocumentContentStore.StoreVersionAsync` →
`BoxDocumentContentStore`.

## Acceptance and test evidence

The focused production-boundary run passed 32 of 32 tests with exit code 0.
The tests prove complementary layers rather than replacing the production
caller:

- `DocumentCustodyDurabilityTests` invokes the real EF request store through
  `IUploadToRequest`, rejects any legacy `StoreAsync` use, captures the
  complete managed address, and proves ordinal, receipt, rollback, retry,
  counters, and persisted custody state.
- `BoxDocumentContentStoreTests` invokes the real
  `BoxDocumentContentStore.StoreVersionAsync` and proves the expected
  case/reference/ordinal flat Box filename.
- `ProductionCompositionTests` proves the accepted production profile
  resolves `EfDocumentRequestStore`, `BoxDocumentContentStore`, and the
  telemetry initializer together.
- `PublicUploadTelemetryInitializerTests` prove GET, POST, mixed-case,
  query, and fragment credential removal while retaining safe request and
  correlation fields.

The implementation report also records a locked restore, zero-warning Release
build, and full non-Corpus result of 2525 of 2525 passing. Those author results
are supporting evidence; the exact-head CI state below remains authoritative
for the review gate.

## Simplicity assessment

The change passes the repository's anti-overengineering rails:

- **Reuse before build:** it reuses `IDocumentContentStore`,
  `ManagedDocumentContentAddress`, `StoreVersionAsync`,
  `EfDocumentCustodyStore`'s ordinal convention, Box flat naming, and the
  existing rollback disposition.
- **One owner/list per concept:** Core remains the only upload-policy owner;
  no duplicate state vocabulary, media list, naming rule, or telemetry route
  table was added.
- **No abstraction without need:** the single telemetry initializer is an
  Application Insights boundary with a production registration; no wrapper,
  new port, factory, package, queue, or staging service was introduced.
- **Existing convention wins:** managed custody allocation, naming,
  write-first/commit-second cleanup, DI, and telemetry composition follow
  existing repository patterns.
- **Proportional scope:** the nine declared files contain only the custody
  repair, telemetry protection, targeted tests, and current-state
  reconciliation. There is no route, schema, permission, infrastructure,
  dependency, limits, or session redesign.
- **No fallback or compatibility path:** the unsupported legacy call was
  replaced rather than retained beside the managed write.

The dated simplification pass in the plan covers reuse, simplification,
efficiency, and altitude and records no deferred or unapplied finding.

## Exact-head checks

GitHub's repository-check run `33778198608` is fully settled for the exact
head.

Passed: `changes`, `local-development-scripts`, `reference-data`,
`unit`, `sql-integration (1)`, `sql-integration (3)`, and
`sql-integration-coverage`. `infrastructure` was skipped.

Not green:

- `documentation` failed on the pre-existing
  `.opencode/skills/kanmer-setup/SKILL.md` link to missing
  `docs/manual/greenfield.md`. CASE-022 does not change that path, the same
  broken link and missing target exist on the base SHA, and KANMER-011 already
  tracks it.
- `browser` failed the unchanged
  `MailWorkspaceBrowserTests.SubjectSelectsTheServerRenderedPreviewAndThePaneOpensFullDetailWithoutJavaScript`
  while waiting for NetworkIdle; 118 of 119 tests passed. CASE-022 changes no
  MailWorkspace or browser-test file.
- `sql-integration (2)` was cancelled at its 20-minute job limit without a
  named failing assertion. The complementary shard-coverage job passed, and
  the focused and full local evidence above passed.
- `test-ui` was cancelled after 40 minutes 20 seconds when its snapshot
  capture/verify step reached the configured 35-minute step limit. CASE-022
  changes no routed Razor markup and the plan correctly requires no snapshot
  update.

These are check-gate blockers, not defects introduced by the reviewed diff.
They do not create fabricated `F-###` code findings. A successful exact-head
rerun, together with resolution of the pre-existing documentation gate, is
required before a `pass` attestation can replace this record.

## Findings and dispositions

No review findings. No GitHub threads require disposition. The unrelated
documentation defect is already deferred to KANMER-011. The other terminal
check outcomes remain visible gate evidence and are not accepted as green.

## Residual risk and handoff

The managed write has production-shaped local coverage but has not been
merged, deployed, or live-verified against Box and SQL; that proof correctly
belongs after an authorized merge and release. Leave CASE-022 in Review.
Resolve or rerun the non-green exact-head checks, then run a fresh independent
`kanmer-review`. Do not merge or deploy from this attestation.
