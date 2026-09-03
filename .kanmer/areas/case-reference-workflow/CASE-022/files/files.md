# Files — CASE-022

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs` | Repair the production upload write by allocating the document/occurrence address required by Box and using the managed-content write contract without losing transaction, replay, or rollback behaviour. |
| `src/Pegasus.Web/Program.cs` and the existing telemetry configuration surface it uses | Prevent request-scoped upload bearer tokens from being retained in request telemetry; keep route matching and anonymous access unchanged. |
| `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs` | Extend persistence/rollback coverage to the managed-address write path and its ordinal/root-folder data. |
| `tests/Pegasus.IntegrationTests/QdosCustodialWebTests.cs` or the closest existing production-composition fixture | Add production-shaped proof that a real request upload reaches managed Box storage and that telemetry does not retain the token URL. Do not continue relying solely on a substituted upload command. |
| `docs/current-architecture.md` | Correct the as-built upload description if the repaired Box write or telemetry treatment is not already stated. |
| `docs/operations.md` | Record the diagnosed production defect and, only after a later approved deployment, the actual deployed repair evidence. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Ensure the canonical INT-31 behaviour explicitly covers secret-bearing link telemetry and managed Box custody if current text is insufficient. |
| `docs/open-decisions.md` / `docs/capabilities.md` | Reconcile only stale CASE-022/activation wording affected by the repair; preserve INTK-052/INTK-055 ownership of later limits and session behaviour. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs` | The existing production writer to reuse: ordinal allocation, `CustodyRootRemoteId`, `ManagedDocumentContentAddress`, `StoreVersionAsync`, and rollback disposition. |
| `src/Pegasus.Infrastructure/Custody/BoxDocumentContentStore.cs` | The production contract: legacy `StoreAsync` is intentionally refused; Box writes require the full persisted business address and track created files for rollback. |
| `src/Pegasus.Core/Documents/DocumentContracts.cs` | The single existing storage port; no parallel upload-specific storage abstraction is justified. |
| `src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs` | Shows the caught failure returning HTTP 200 and the present in-memory body copies; preserve generic, non-disclosing public outcomes. |
| `src/Pegasus.Core/Documents/RequestUploadPolicy.cs` | Owns token, media, size, rate, replay, and authorization policy; storage repair must not duplicate or alter those rules. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Proves production resolves `EfDocumentRequestStore` and `BoxDocumentContentStore`, while local tests resolve the local content store. |
| `infra/modules/platform.bicep` | Owns the currently deployed interim limits and activation keys; changing these is not part of the observed failure repair. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Current INT-31 authority, including non-disclosure and request-scoped link behaviour. |
| `docs/operations.md` | Release 37 activation, release 38 current revision, production target, and current-state evidence rules. |
| `tests/Pegasus.IntegrationTests/ProductionCompositionTests.cs` | Existing composition-gate coverage; it proves registrations, not successful production storage. |
| `tests/Pegasus.IntegrationTests/BoxDocumentContentStoreTests.cs` | Existing managed Box naming/write semantics and the appropriate fake HTTP boundary if present. |
| `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs` | Existing request-upload transaction failure, cleanup, and safe-retry assertions that must remain true. |
| `tests/Pegasus.IntegrationTests/QdosCustodialWebTests.cs` | Public-page PRG, non-disclosure, validation, and rate-limit behaviour; currently substitutes away the failing production store. |

## Ripple effects

The case document ordinal sequence, Box flat-file name, request receipt,
accepted counts, case workflow version, and orphan cleanup must remain one
coherent operation. Evidence/gallery/download callers should see the uploaded
file through the same document tables and Box naming convention as other case
evidence. Monitoring must continue to identify the route and failure class
without recording its bearer token. Routed-page changes require the repository's
Test UI snapshot regeneration and catalogue verification.

## Out of scope

- Raising the interim 10 MiB limits or adding the later fixed 15-minute
  add/replace session; INTK-052/INTK-055 and current governing documents own
  those changes.
- Creating a second storage adapter, compatibility path, queue, or staging
  system.
- Migrating or cleaning successful request uploads: the production census found
  none.
- Azure writes, deployment, revoking further links, or changing live
  configuration. This research used read-only diagnostics only.
- Unrelated case Evidence UI work owned by DOCS-012/CASE-044.
