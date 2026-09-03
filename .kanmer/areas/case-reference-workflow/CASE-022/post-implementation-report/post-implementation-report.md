# Post-implementation report — CASE-022

## Result

The existing public-upload caller now writes through the production managed
document custody contract. It allocates and persists the case document ordinal,
carries the persisted Box case-root identity in
`ManagedDocumentContentAddress`, and calls `StoreVersionAsync`. Database
rollback removes only content newly created by that attempt. Application
Insights request telemetry canonicalises `/Uploads/{token}` to
`/Uploads/Request` and removes query and fragment data.

Base SHA: `07ac7f1be9fc9fc04814fd5347ae5da30aff62da` (`dev`).

Implementation SHA: `251ad4493b4a11ac0b9d4e68055bf0bcedf10fef`.

## Changed files

- `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs` — use the
  managed custody address, positive persisted ordinal and disposition-aware
  cleanup.
- `src/Pegasus.Web/PublicUploadTelemetryInitializer.cs` — remove public bearer
  tokens, query and fragment from request URLs.
- `src/Pegasus.Web/Program.cs` — register the initializer in production
  Application Insights composition.
- `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs` — prove the
  complete address, SQL/content rollback, persisted ordinals and safe retry.
- `tests/Pegasus.IntegrationTests/BoxDocumentContentStoreTests.cs` — prove the
  request-upload address produces the expected managed Box filename.
- `tests/Pegasus.IntegrationTests/PublicUploadTelemetryInitializerTests.cs` —
  prove GET/POST redaction, correlation preservation and unrelated telemetry.
- `tests/Pegasus.IntegrationTests/ProductionCompositionTests.cs` — prove
  accepted production upload and telemetry composition.
- `docs/current-architecture.md` — record repaired repository behaviour without
  claiming deployment.
- `docs/operations.md` — record the live failure, zero-success census and
  explicitly undeployed repair.

## Governing-doc mapping

`docs/frd/frd-02-intake-and-source-identity.md` requires durable source custody
and prohibits credentials in content-bearing telemetry. The implementation
uses the already accepted managed Box boundary and removes the upload bearer
credential before request telemetry is emitted. No governing document changed.

## Verification

- Focused integration build: PASS, zero warnings.
- Focused storage/composition/telemetry tests: PASS, 32/32.
- Locked solution restore: PASS.
- Release solution build: PASS, zero warnings.
- Full non-Corpus solution tests: PASS, 2,525 total
  (1,185 Core, 100 architecture, 1,240 integration).
- `git diff --check` and declared-file scope inspection: PASS.
- The first pre-restore compile attempt returned `NETSDK1004` because the fresh
  worktree had no generated assets. The required locked restore then succeeded;
  all subsequent builds and tests passed.

## Risks and follow-ups

- Release 38 remains deployed and still contains the broken legacy write and
  unredacted URL behaviour. Deployment and live verification require separate
  authority.
- INTK-052 and INTK-055 retain the later limits/session changes; this branch
  does not alter them.
- No schema, permission, dependency, infrastructure or data cleanup is needed.

## Merged-result checks for kanmer-verify

At the exact merge SHA, repeat locked restore, Release build, and the full
non-Corpus test rail. Confirm the production caller chain ends at
`BoxDocumentContentStore.StoreVersionAsync`, the telemetry initializer remains
registered, and the merge contains only the nine declared files. Live Box/SQL
and token-free telemetry proof belongs after a separately authorised release;
do not infer it from merged source.
