# Plan — CASE-022: Repair production public upload links

## Objective

Make the existing INT-31 public upload route retain accepted files in the
case's managed Box custody, commit matching SQL state exactly once, and prevent
the bearer token in the upload URL from entering request telemetry.

## Starting state

Production resolves `EfDocumentRequestStore` with
`BoxDocumentContentStore`. The former calls legacy `StoreAsync`; the latter
always refuses that method because Box needs a persisted
`ManagedDocumentContentAddress`. The observed production POST reproduced that
exception after successful SQL lookups. Production has one revoked link and no
accepted files, receipts, or request-upload occurrences. The current Container
App revision is healthy. The handler converts the exception to an HTTP 200 error
page, and Application Insights retains the secret-bearing request URL.

Evidence: `research/research.md`@`fb26b2a694773e02`,
`files/files.md`@`bf2eb00e554cb131`. No project research sources are
declared for this area and label set.

## Governing docs

- **Meets** `docs/frd/frd-02-intake-and-source-identity.md`: the real caller
  will satisfy durable custody and the existing prohibition on tokens in
  content-bearing telemetry. The FRD already states the requirement and is not
  modified.
- No ADR is needed. This reuses the accepted managed Box storage boundary.

## Required changes

- Allocate the next case document ordinal in the request-upload transaction,
  apply it to document and occurrence, carry `CustodyRootRemoteId`, build the
  existing `ManagedDocumentContentAddress`, and call `StoreVersionAsync`.
- Preserve upload policy, interim limits, counters, replay identity, workflow
  version, receipt persistence, and disposition-aware orphan cleanup. Storage
  failure stays generic and commits no accepted state.
- Cover the missed production boundary: the request store supplies the complete
  managed address, and the real Box store accepts that write shape.
- Register one existing-SDK Web telemetry initializer that canonicalises request
  telemetry under `/Uploads/` to `/Uploads/Request`, removing token, query,
  and fragment while preserving host, request name, result, and correlation.
- Update current-state docs with the diagnosed live defect and repaired
  repository path. Do not claim deployment before separately authorised live
  proof.

Before-fix reproduction: POST one permitted file with a valid active link under
production composition. SQL lookups succeed; Box's legacy method throws; the
page shows generic failure; no receipt commits.

Regression boundary: invalid, expired, revoked, exhausted, cross-request,
oversized, disallowed-media, conflict, rate-limited, and archived-case outcomes;
staff creation/revocation; local custody; normal case-document custody; Box
naming; and evidence readers stay unchanged.

## Expected files

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Modify | `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs` | Use the persisted managed Box address and existing write contract. |
| Add | `src/Pegasus.Web/PublicUploadTelemetryInitializer.cs` | Remove bearer tokens from request telemetry. |
| Modify | `src/Pegasus.Web/Program.cs` | Register the initializer with existing Application Insights composition. |
| Modify | `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs` | Prove complete address, SQL/content atomicity, cleanup, and retry. |
| Modify | `tests/Pegasus.IntegrationTests/BoxDocumentContentStoreTests.cs` | Prove managed Box accepts the request-upload address and expected flat file. |
| Add | `tests/Pegasus.IntegrationTests/PublicUploadTelemetryInitializerTests.cs` | Prove sanitisation and unrelated telemetry preservation. |
| Modify | `tests/Pegasus.IntegrationTests/ProductionCompositionTests.cs` | Prove production telemetry and upload services compose together. |
| Modify | `docs/current-architecture.md` | Describe repository behaviour without overstating deployment. |
| Modify | `docs/operations.md` | Record live failure, zero-success census, health, and undeployed repair. |

## Do not modify

- `docs/operator-notes.md`
- `docs/frd/frd-02-intake-and-source-identity.md`
- `docs/capabilities.md`
- `docs/open-decisions.md`
- `src/Pegasus.Core/Documents/RequestUploadPolicy.cs`

## Constraints

- Reuse `IDocumentContentStore.StoreVersionAsync`,
  `ManagedDocumentContentAddress`, Box flat naming, and
  `EfDocumentCustodyStore`'s ordinal pattern. Add no adapter, queue, staging
  path, or fallback.
- Core remains the sole policy owner. Limits and the later session behaviour
  remain with INTK-052/INTK-055.
- Preserve write-first/commit-second cleanup: failed SQL removes only content
  created by that attempt, never replayed content.
- Public results disclose no case, reference, exception, Box identity, previous
  upload, or link state.
- The bearer token appears in no telemetry, source, test, ticket document,
  command output, or proof.
- No schema, permissions, dependency, infrastructure, or cost change is needed.
- Later read-only verification targets tenant
  `858cf5b3-aa0a-47a6-9b40-4851fd0afa94`, subscription
  `e6076573-23a5-46a8-acef-7e22d264e5db`, resource group
  `rg-pegasus-prod`, region `uksouth`, through the already authenticated
  operator identity. This plan authorises no Azure write or deployment.
- Before deployment, rollback is a branch revert. Any deployed rollback uses
  the repository's prior-revision release route under separate authority.

## Ordered steps

### Step 1 — Write through managed Box custody

- Preconditions: Pinned evidence is current; production still refuses legacy
  `StoreAsync`.
- Files: `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs`,
  `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs`,
  `tests/Pegasus.IntegrationTests/BoxDocumentContentStoreTests.cs`.
- Change: Allocate and persist the document/occurrence ordinal, carry the case
  root, build the full managed address, call `StoreVersionAsync`, and use its
  disposition for cleanup. Test the complete address and real Box write.
- Preserved behaviour: Authorization, counters, case version, receipt identity,
  safe name, hash, replay, and transaction order.
- Forbidden: Legacy storage call, duplicate Box naming, second custody path, or
  transaction bypass.
- Negative cases: Missing case root or Box failure commits nothing; failed SQL
  deletes only newly created content; replay/conflict makes no duplicate.
- Tests: `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs`,
  `tests/Pegasus.IntegrationTests/BoxDocumentContentStoreTests.cs`.
- Commands: `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~DocumentCustodyDurabilityTests|FullyQualifiedName~BoxDocumentContentStoreTests"`.
- Expected output: Exit 0; address, Box file, rollback, and retry assertions pass.
- Done when: One accepted production-shaped upload creates one managed Box file
  and commits one matching SQL receipt/state change.
- Deviation stop: Stop for any required schema change, unsupported existing Box
  contract, or cleanup that cannot distinguish created from replayed content.

### Step 2 — Sanitize public-upload telemetry

- Preconditions: Web still uses `AddApplicationInsightsTelemetry` and has no
  request URL sanitizer.
- Files: `src/Pegasus.Web/PublicUploadTelemetryInitializer.cs`,
  `src/Pegasus.Web/Program.cs`,
  `tests/Pegasus.IntegrationTests/PublicUploadTelemetryInitializerTests.cs`,
  `tests/Pegasus.IntegrationTests/ProductionCompositionTests.cs`.
- Change: Add/register one initializer that case-insensitively canonicalises
  URLs under `/Uploads/` to `/Uploads/Request`, dropping query and fragment.
- Preserved behaviour: Entra ingestion, exceptions, dependencies, result,
  duration, host, name, correlation, and non-upload URLs.
- Forbidden: Token hash/prefix logging, global telemetry removal, middleware,
  route changes, or packages.
- Negative cases: Null/non-request telemetry and other URLs are unchanged;
  upload GET/POST, mixed case, query, and fragment retain no credential data.
- Tests: `tests/Pegasus.IntegrationTests/PublicUploadTelemetryInitializerTests.cs`,
  `tests/Pegasus.IntegrationTests/ProductionCompositionTests.cs`.
- Commands: `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~PublicUploadTelemetryInitializerTests|FullyQualifiedName~ProductionCompositionTests"`.
- Expected output: Exit 0; registration and sanitisation assertions pass.
- Done when: Emitted upload request URLs cannot hold bearer tokens while route
  identity and correlation remain available.
- Deviation stop: Stop if the credential is recorded in additional telemetry
  fields or safe handling would require globally disabling useful telemetry.

### Step 3 — Reconcile current-state documentation

- Preconditions: Steps 1–2 are green.
- Files: `docs/current-architecture.md`, `docs/operations.md`.
- Change: Record the managed repository path and sanitisation; record the live
  exception, healthy revision, zero-success census, and undeployed repair.
- Preserved behaviour: Release history and deployment evidence tiers.
- Forbidden: A deployment claim, governing-FRD edit, operator-note edit, or
  future limits/session change.
- Negative cases: No token, Box-outage claim, or successful-live-upload claim.
- Tests: Markdown/diff inspection.
- Commands: `git diff --check`; `rg -n "CASE-022|request upload|upload link|StoreVersionAsync|not yet deployed" docs/current-architecture.md docs/operations.md`.
- Expected output: Exit 0; both current-state documents agree.
- Done when: Repository and deployed states are clearly distinguished.
- Deviation stop: Stop for an FRD conflict or any required change to
  `docs/operator-notes.md`.

### Step 4 — Prove the branch and prepare review

- Preconditions: Steps 1–3 and focused checks pass.
- Files: `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs`,
  `src/Pegasus.Web/PublicUploadTelemetryInitializer.cs`,
  `src/Pegasus.Web/Program.cs`,
  `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs`,
  `tests/Pegasus.IntegrationTests/BoxDocumentContentStoreTests.cs`,
  `tests/Pegasus.IntegrationTests/PublicUploadTelemetryInitializerTests.cs`,
  `tests/Pegasus.IntegrationTests/ProductionCompositionTests.cs`,
  `docs/current-architecture.md`, `docs/operations.md`.
- Change: Run independent simplification lenses on the branch diff, apply
  behaviour-preserving findings, record dispositions under a dated
  `Simplification pass` heading in this plan, run canonical verification,
  inspect scope/secrets, commit, and open the CASE-022 PR to `dev`.
- Preserved behaviour: Unrelated code/tests and production composition.
- Forbidden: Weakened assertions, failed evidence, dependency changes, merge,
  deployment, or Azure writes.
- Negative cases: Any failed test, secret match, undeclared file, generated
  artifact, or unresolved finding stops review handoff.
- Tests: Focused tests plus full non-Corpus solution rail.
- Commands: `dotnet restore ./Pegasus.slnx --locked-mode`;
  `dotnet build ./Pegasus.slnx --configuration Release --no-restore`;
  `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"`;
  `git diff --check`; `git status --short`.
- Expected output: All exit 0; only declared files, no secret or lock change,
  and all simplification dispositions recorded.
- Done when: The committed PR targets `dev` and is ready for independent
  `kanmer-review`.
- Deviation stop: Stop for non-zero exit, overlapping user work, undeclared
  file need, expanded scope, or lack of real production-path proof.

## Acceptance checks

- Production caller:
  `POST /Uploads/{token}` → `RequestModel.OnPostAsync` →
  `IUploadToRequest` → `EfDocumentRequestStore` →
  `IDocumentContentStore.StoreVersionAsync` →
  `BoxDocumentContentStore`.
- One accepted upload creates one Box file, document/version/occurrence,
  receipt, counter update, and case-version update; replay duplicates none.
- The deterministic legacy-call exception has production-shaped regression
  coverage.
- Storage or SQL failure leaves no accepted state or orphaned new Box content.
- Existing bounded public outcomes and non-disclosure remain.
- Application Insights keeps canonical route/correlation but no token, query,
  or fragment.
- No runtime dependency, schema, permission, IaC, or cost change.
- No Test UI snapshots: no routed Razor page or rendered markup changes.
- Production proof remains absent until separately authorised release and live
  verification.

## Commands

Run from the CASE-022 worktree with PowerShell 7:

`dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~DocumentCustodyDurabilityTests|FullyQualifiedName~BoxDocumentContentStoreTests|FullyQualifiedName~PublicUploadTelemetryInitializerTests|FullyQualifiedName~ProductionCompositionTests"`

`dotnet restore ./Pegasus.slnx --locked-mode`

`dotnet build ./Pegasus.slnx --configuration Release --no-restore`

`dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"`

`git diff --check`

`git status --short`

After a separately authorised deployment, verification must create a disposable
link, upload one permitted non-sensitive fixture, prove Box and SQL
receipt/counters, prove canonical token-free telemetry, revoke the link, and
refresh both current-state docs with the deployed SHA/revision. The credential
never enters evidence.

## Failure and deviation rules

Stop for any failed command; package, schema, permission, IaC, or route need;
governing conflict; unknown Box behaviour; unsafe rollback; additional
token-bearing telemetry field; or ticket overlap. Do not move limits/session
scope here, weaken tests, keep the broken call as fallback, or perform Azure
writes.

## Stop condition

Stop when the bounded branch is committed, its PR to `dev` is open with
focused and canonical evidence, simplification findings are dispositioned, and
it is ready for independent `kanmer-review`. Do not merge, deploy, change
production, or start another ticket.

## Simplification pass — 2026-09-03

- **Reuse:** Passed. The request path uses the existing
  `ManagedDocumentContentAddress`, `StoreVersionAsync`, persisted case-root
  identity, Box flat naming, and disposition-aware rollback contract.
- **Simplification:** Passed with no change. The ordinal query and telemetry
  initializer are the smallest direct additions; extracting another helper
  would add indirection without changing behaviour.
- **Efficiency:** Passed. The repair adds one bounded SQL aggregate before the
  external write and no extra Box round trip, cache, retry, or background work.
- **Altitude:** Passed. Core still owns upload policy, Infrastructure owns
  persistence/custody adaptation, and Web owns telemetry registration. No
  finding was deferred or left unapplied.
