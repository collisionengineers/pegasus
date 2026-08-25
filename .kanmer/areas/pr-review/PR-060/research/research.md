# Research — PR-060: migration commentary for replay and recovery

## Question

What is the final Export operation-key contract, and what is the smallest correction needed so the ENG-016 migration commentary agrees with ADR-0030, the PR description and the ticket report?

## Findings

- `ExportCaseBundleRequest` on PR #539 contains `OperationKey`, and `EvaHandoffStore` validates it as an N-format GUID. Source: `src/Pegasus.Core/Eva/EvaBundleSchema.cs` and `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` at PR head.
- The operation key identifies a distinct Export action in the existing `ActionHistory` table. The event is `eva_bundle_exported`; exact reuse is checked by `DocumentActionHistory.RequireExactReplay`. Source: `EvaHandoffStore.RecordExportAsync`.
- `EvaFirstHandoffProxies` is a different, once-per-case fact used by the dashboard. Its `CaseId` primary key supplies its idempotency. It does not need the per-export operation key merely because Export itself carries one. Source: `EvaHandoffStore`, `EfDashboardQueries`, and the surviving proxy model/configuration.
- The migration comment is false where it says the proxy loses `OperationKey` because “an export does not carry” one. The accurate reason is that per-export replay/history now lives in `ActionHistory`, while the proxy records only whether this case was first sent.
- ADR-0030 requires roll-forward recovery after a permitted pre-cutover non-additive migration. It expressly says “Roll forward, never back” and treats the rollback gap as accepted until compatibility is re-established before cutover. Source: `docs/adr/0030-non-additive-schema-changes-before-cutover.md` on `origin/dev`.
- The migration’s `Down()` mechanically recreates old schema objects, but cannot reconstruct old hand-off revisions for new proxy rows. Its comment telling an operator to clear proxy rows before rollback is therefore not the production recovery contract and would destroy a real first-sent fact.
- The current PR description and ENG-016 post-implementation report already state the correct result: direct pre-cutover removal under ADR-0030, no rollback/legacy compatibility machinery, and roll-forward recovery. They do not need behavioural expansion for PR-060.
- The historical migration shape itself remains valid development migration scaffolding. PR-060 does not need a new migration, a compatibility layer, a data converter, or changes to `Up()`/`Down()`; it needs comment/evidence correction only.

## Implications

Change only the leading commentary in `20260824123336_DropEvaHandoffTables.cs`:

1. Say Export carries an operation key and records it in `ActionHistory`; the proxy column is removed because the proxy is once per case and no longer owns replay.
2. Describe ADR-0030 recovery as roll-forward, not row-clearing plus rollback.
3. Describe `Down()` as development schema mechanics rather than a production recovery promise.

Confirm the PR description and ENG-016 report retain the same statement. No runtime behaviour or schema operation should change.

## Open questions

None. ADR-0030 and the final Export contract settle both points.
