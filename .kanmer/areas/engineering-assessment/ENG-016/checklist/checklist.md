# Checklist — ENG-016

*Derived from the revised strict-Export plan. No implementation boxes are checked by this planning pass.*

## Git and scope

- [ ] Preserve the local staged `.gitignore`/`.codex/config.toml`/`.mcp.json` change outside PR #539
- [ ] Restore a clean ENG-016 task worktree matching the Kanmer claim
- [ ] Fetch and normally merge current `origin/dev` into `task/eng-016-collapse-handoff-into-export`
- [ ] Resolve QDOS policy/test conflicts by taking current `dev`
- [ ] Resolve FRD-07/capabilities conflicts from current `dev` then apply the strict one-Export contract
- [ ] Resolve EVA Core/store conflicts from current `dev` then reapply only ENG-016
- [ ] Transfer still-relevant assertions before resolving `EvaHandoffPersistenceTests.cs` as deleted
- [ ] Audit `git diff origin/dev...HEAD` and remove every unrelated stale-stack change

## Strict Export policy

- [ ] Delete `MapForOperatorExport`, `EvaOperatorExport` and empty/default field continuation
- [ ] Reuse one strict mapping requiring all thirteen accepted, non-empty, provenanced fields
- [ ] Reuse one Core eligibility policy for Review, non-archived/current version, Case/Audit custody, mapping and eligible images
- [ ] Enforce strict eligibility inside `IExportCaseBundle.ExecuteAsync`, not only in the UI
- [ ] Prove a blocked Export writes no archive, proxy or success history
- [ ] Preserve one deterministic JSON/images package builder and one authenticated Export POST
- [ ] Preserve deletion of duplicate EVA routes, panel, query projection, ports, MCP tools and DI registrations
- [ ] Restore the successful archive `Content-Digest` header

## Permanent history and first proxy

- [ ] Add an operation key to the Export form, Web handler and Core request
- [ ] Reuse `DocumentActionHistory` for an attributed `eva_bundle_exported` event
- [ ] Persist Case version, mapping authority, field provenance, image identities and package hashes in structured history
- [ ] Commit first successful Export history and first-sent proxy atomically
- [ ] Prove a second distinct Export writes history but no second proxy
- [ ] Prove exact operation replay duplicates neither history nor proxy
- [ ] Prove mismatched operation-key reuse fails closed
- [ ] Preserve the dashboard Sent-to-Engineer count over `EvaFirstHandoffProxies`

## Migration

- [ ] Reconcile the generated drop migration/Designer/snapshot on current `dev`
- [ ] Keep the direct dead-table/column drop authorized by ADR-0030
- [ ] Correct the stated blast radius to the old-revision Case workspace
- [ ] State roll-forward recovery and remove any claim of production rollback compatibility
- [ ] Label `Down()` as fresh disposable LocalDB verification only
- [ ] Preserve bootstrap removed-table and historic grant-migration expectations
- [ ] Add no compatibility view, dual path, feature flag, data conversion or expand/contract staging

## Documentation

- [ ] Update protected `docs/operator-notes.md` with the explicitly authorized three-route model and strict Export gate
- [ ] Rewrite FRD-07 from strict hand-off plus permissive read into one strict manual Export/handoff
- [ ] Reconcile CASE-21, CASE-30, EXT-03 and relevant future-route capability wording
- [ ] Refresh current-architecture to the one-route implementation, per-export history and surviving proxy
- [ ] Preserve design authority's strict Sent-to-Engineer language and remove only stale two-act/revision wording
- [ ] Remove every repository-doc claim that missing fields/default dates may still export
- [ ] Update ENG-016's post-implementation report and PR description to remove the superseded permissive assumption

## Tests and proof

- [ ] Core tests pin all thirteen required accepted/provenanced fields
- [ ] Core tests pin Review/current-version/Case custody/Audit custody/mapping/image gates
- [ ] QDOS boundary test proves incomplete evidence cannot export
- [ ] Web test proves visible disabled non-Review control and direct POST server-side refusal
- [ ] Web test proves antiforgery, operation key and `Content-Digest`
- [ ] Integration tests prove first proxy and every-export action history semantics
- [ ] Integration tests prove replay and concurrent first-export idempotency
- [ ] Migration tests prove removed schema and surviving proxy constraints
- [ ] Run `dotnet restore`
- [ ] Run Release build with locked/no-restore profile
- [ ] Run focused Core EVA/QDOS and Architecture suites
- [ ] Run focused Web/Integration/history/migration suites
- [ ] Run the full Integration suite in repository chunks
- [ ] Run fresh disposable LocalDB migration up → down → up
- [ ] Run migration-grant, local deployment-plan and documentation-link scripts
- [ ] Run `git diff --check` and final branch scope audit
- [ ] Push normally and obtain green GitHub checks on the final head SHA
- [ ] If `changes` checkout times out again, coordinate the CI-owned repair and rerun; do not treat skipped jobs as evidence
- [ ] Independent reviewer confirms ticket → plan → implementation coverage and simplification dispositions

## Progress notes

- 2026-08-24: operator resolved that Export is the current send-to-Engineer route, must fail closed until ready, and does not require released-product rollback compatibility before cutover.
- 2026-08-24: live CI inspection found checkout timeout/cancellation, not an application test failure; downstream test jobs were skipped.
