# Plan — ENG-016: Collapse the EVA hand-off into Export as the single act

*Rewritten after the operator's 2026-08-24 clarification. This plan supersedes the earlier permissive-export plan; no implementation is performed by this revision.*

## Approach

Keep the simplification ENG-016 was meant to deliver—one manual Export action and no duplicate hand-off UI, routes, MCP surface or revision tables—but preserve the complete send-to-Engineer gate rather than the permissive download bar. The existing strict mapping and eligibility policies become the one Core-owned precondition for Export. Every successful Export writes permanent action history; the first successful Export also writes the once-per-Case Sent-to-Engineer proxy. Merge current `origin/dev` normally into the pushed branch, take current dev for unrelated stacked changes, and reapply only this focused final state. Use ADR-0030's accepted pre-cutover roll-forward rule for the destructive migration; do not add compatibility machinery for an unreleased product.

## Governing docs

- **`docs/operator-notes.md` — Modifies with explicit operator authorization.** Record the clarification supplied in this conversation: manual Export to EVA is today's send-to-Engineer route; a future EVA API is the second transport; direct estimating integrations plus Pegasus engineering/reporting eventually replace EVA. Export is unavailable until all required evidence/readiness conditions hold.
- **FRD-07 — Modifies with explicit operator authorization.** Replace the two-act strict hand-off/permissive export model with one strict manual Export/handoff. Define Review/current-version/custody/Audit-custody/accepted thirteen-field evidence/mapping/eligible-image gates; per-export action history; once-per-Case proxy; no claim of EVA receipt or named assignment; and the two future route boundaries.
- **FRD-04 / ACC-09 — Meets.** Reuse `ActionHistory` and the existing operation-key replay convention so every successful Export is attributable and evidence-bound. The proxy is not treated as a substitute for action history.
- **ADR-0030 — Meets.** Drop the dead hand-off tables directly before cutover, name the old-revision Case-workspace impact accurately, deploy roll-forward, and do not claim production rollback compatibility.
- **`docs/design/README.md` — Meets.** Keep the control terse, visible-but-disabled outside Review, and preserve the existing statement that a successful EVA JSON/image export is the Sent-to-Engineer proxy.
- **No new ADR.** The route choice and eligibility are FRD behaviour, action history follows an existing architecture, and migration policy is already decided by ADR-0030.

## Steps

1. **Separate local unrelated Git state before touching the ticket branch.** Preserve the staged `.gitignore`/untrack changes for `.codex/config.toml` and `.mcp.json` outside ENG-016; they must not enter PR #539. Restore a clean ENG-016 task worktree/claim location before the merge without stashing, resetting or discarding the user's files.

2. **Merge current `origin/dev` into the pushed task branch with a normal merge.** Do not rebase, force-push or reconstruct dev history. Resolve the predicted conflicts by rule:
   - QDOS instruction policy and its tests: take current `dev`; ENG-016 has no independent change there.
   - FRD-07 and capabilities: take current `dev` as the base, then apply this plan's one strict Export wording.
   - `CaseEvaMapping.cs`, `EvaBundleSchema.cs` and `EvaHandoffStore.cs`: take current ENG-014/ENG-015 behaviour from `dev`, then reapply only ENG-016's duplicate-surface deletion, strict Export and history changes.
   - `QdosBoundaryContractTests.cs`: preserve current dev coverage and replace the branch's permissive incomplete-export assertion with fail-closed coverage.
   - `EvaHandoffPersistenceTests.cs`: transfer any still-relevant strict gate/package tests to survivor suites, then accept deletion of tests for removed tables/use cases.
   After resolution, compare `git diff origin/dev...HEAD` and reject every unrelated change inherited only because the old branch was 53 commits behind.

3. **Make strict Export eligibility the single Core policy.** Remove `MapForOperatorExport`, `EvaOperatorExport`, empty-field continuation, suggestion-only acceptance, and the export-date default. Retain/reuse the existing strict accepted-evidence mapping: accepted Case, confirmed completeness, resolved inspection mode/address, all thirteen fields non-empty and accepted, source/version provenance, and accepted mapping. Retain/reuse the existing `EvaHandoffPolicy.Evaluate` checks for Review, non-archived/current accepted version, confirmed Case custody, Audit custody when required, accepted mapping and at least one eligible image. Call both policies inside `IExportCaseBundle.ExecuteAsync` so a direct POST cannot bypass the disabled button. Produce no archive, proxy or action history when any reason blocks.

4. **Keep one package/export surface.** Preserve ENG-016's deletion of the separate EVA panel, generate/download routes, hand-off query projection, MCP generate/status tools, duplicate ports and three revision/replay tables. Keep the authenticated Case Export POST, antiforgery, deterministic thirteen-key JSON and all eligible images. Restore `Content-Digest` from the archive SHA-256 on the successful response.

5. **Add permanent action history without conflating it with the proxy.** Add a caller-supplied operation key to the Export request and form using the existing Case page convention. For each distinct successful Export, append one `ActionHistory` event (proposed event kind `eva_bundle_exported`) with Case aggregate, actor/roles, timestamp, operation key, mapping key/version/evidence reference, accepted Case version, bundle and JSON hashes, thirteen-field source/version/status evidence, and exported image occurrence/version/hash identities. Save that history atomically with the first-proxy insert when the proxy is absent. A later distinct Export writes another history event but no second proxy. An exact retry with the same operation key must return/replay the same package result without duplicating history or proxy; reuse with different package/evidence must fail closed. Use `DocumentActionHistory` and the existing document-export replay pattern rather than inventing a second history framework.

6. **Keep the destructive migration simple and truthful.** Retain the direct drop of `EvaHandoffRevisions`, `EvaHandoffOperations` and `EvaHandoffDownloadOperations` plus obsolete proxy FK/index/columns. Update the migration, PR and release-plan wording to say:
   - Pegasus has not cut over; ADR-0030 authorizes this non-additive change.
   - migrations run before new packages activate, so the currently running old revision may fail every Case workspace during that short interval because its Case projection still reads `EvaHandoffRevisions`.
   - deployment proceeds roll-forward; a failure is fixed forward or uses the separately approved disposable pre-cutover data procedure, not application rollback.
   - `Down()` exists only for a clean scratch-database up/down/up test and is not a data-preserving production recovery route once new proxy rows exist.
   Do not add expand/contract tables, compatibility views, dual paths, feature flags or data conversion.

7. **Reconcile documentation to one route model.** Update protected operator notes (authorized), FRD-07, capabilities and current-architecture. Preserve current design statements that already require strict readiness. State the three planned routes without implementing the future two: manual Export to EVA now; direct EVA API when vendor-supported; direct estimating integrations/Pegasus engineering and report generation when EVA is replaced. Remove every claim that a missing field exports empty, that inspection date may be invented at Export, or that operator Export is a separate read that writes no proxy/history. Update the ENG-016 post-implementation report and PR description so neither repeats the superseded assumption.

8. **Verify behaviour and migration locally.** Run locked restore and Release build; Core, Architecture and focused EVA/QDOS suites; relevant Web/Integration suites; then the full integration suite in the repository's supported chunks. Prove: every strict gate blocks server-side; a ready Review Case exports; response digest matches; first Export creates one proxy plus one history event; second distinct Export creates only another history event; exact replay duplicates neither; failures create no archive/proxy/success history; removed routes/tools/tables are absent; dashboard count still reads the proxy; migration up/down/up works on a fresh disposable LocalDB; migration/grant/deployment-plan scripts pass; documentation links and `git diff --check` pass.

9. **Push normally and obtain real CI evidence.** The merge commit/new fixes trigger a fresh repository-check. The previous head has no complete build verdict: its `changes` job timed out during full-history checkout, so dependent application jobs were skipped. If the new run checks out successfully, require every applicable lane green. If checkout again exceeds its five-minute timeout, report it as an unrelated CI infrastructure failure and coordinate with the existing CI work rather than broadening ENG-016 into a workflow refactor. Re-run after that repair and do not merge until the PR's own head is green and independently reviewed.

## Verification

Proof for this revision will be the final post-implementation report plus command output for:

- `dotnet restore`
- `dotnet build --configuration Release --no-restore`
- focused Core EVA/QDOS and Architecture tests
- focused Case Export, custody/history, browser and migration integration tests
- full integration suite in the locked repository chunks
- scratch LocalDB migration up → down → up, explicitly labelled development-only
- `pwsh ./scripts/Test-MigrationGrants.ps1`
- `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local`
- `pwsh ./scripts/Test-DocumentationLinks.ps1`
- `git diff --check` and a final `origin/dev...HEAD` scope audit
- GitHub checks on the final head SHA, not the cancelled checks inherited from `30bb2791`.

## Risks / open questions

- **Resolved by operator:** Export is the current send-to-Engineer route and must fail closed until complete.
- **Resolved by operator/project state:** no released-product rollback compatibility is required before cutover; ADR-0030 roll-forward applies.
- **Risk:** operation replay could return a package built from changed evidence. Mitigation: bind history to operation key plus exact Case version/package/evidence snapshot and reject mismatched reuse.
- **Risk:** conflict resolution could resurrect stale stacked code or delete newer dev work. Mitigation: current dev wins outside ENG-016 and the final three-dot diff is audited file by file.
- **Risk:** the CI checkout timeout may recur independently of the code. Mitigation: do not interpret skipped tests as pass/fail; obtain a fresh completed run or coordinate the CI-owned fix.
- **No open product question remains for implementation.**
