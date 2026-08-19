## Independent review — PR #420 at `405dc0e47cb6d1b39b51becfba4b24efbc307cd0` (2026-08-19)

### Change survey

- Core: introduces a versioned repair-specification aggregate, exact-version/current queries, immutable acceptance, source provenance, calculation basis, correction/supersession lineage, and display-list mapping.
- Infrastructure: adds EF entities/configuration/store, a SQL migration/model snapshot, associates estimate lines to a specification, and migrates legacy lines into an explicit `Draft` / `LegacyUnresolved` specification.
- Tests: adds Core policy tests, persistence/correction tests, and migration preservation/fail-closed tests.
- Governing docs: changes FRD-06. No renderer/template/runtime files are changed, so the branch does not overlap SIMPLI-014's integration implementation.

### Findings

1. **Blocking — filed as [[PR-011]].** The branch implements Audit-only `Conservative` / `Maximised` roles in Core policy, EF constraints/indexing, migration/model snapshot, tests, and FRD-06. The later operator correction in [[TICK-205]] explicitly requires one shared canonical accepted repair specification and rejects an Audit-only dual-specification aggregate, role pair, uplift calculation, or presentation. This is product behaviour without current authority and cannot merge. TICK-093's ticket documents must also be reconciled because they currently repeat the superseded premise.
2. The authorized portions are otherwise coherently shaped: legacy migration records missing authority as `Draft` / `LegacyUnresolved` with null acceptance and no fabricated source artifact/hash; accepted versions capture actor/time/source/calculation evidence; corrections retain predecessor/reason; SQL constraints and focused tests cover state/version/current uniqueness and line preservation.
3. The simplification record is candid: an invented VAT formula was removed and raw recorded inputs are retained. The reported local full Integration suite is qualified rather than overstated: no failures appeared before the proportional 25-minute ceiling, the run was stopped, and CI remains authoritative.

### Disposition

- Verdict: **needs changes**.
- PR comment: https://github.com/collisionengineers/pegasus/pull/420#issuecomment-5341253601
- PR #420 was not merged and TICK-093 remains in Review.

### CI completion

GitHub Actions run `32245784024` completed green for every required check: changes, documentation, reference-data, unit (4m45s), browser (9m01s), SQL integration shards 1/2/3 (10m58s / 10m32s / 11m33s), and SQL integration coverage (14s); infrastructure was correctly skipped. Green CI does not resolve [[PR-011]]'s product-authority conflict, so the verdict remains needs changes.

## Independent re-review — PR #420 at `b0596c9bd1df8642332ede63b6e0a849739709a3` (2026-08-19)

### Changes

- Core adds one purpose-neutral, case-scoped repair-specification aggregate with immutable versions, provenance, calculation basis, Engineer acceptance, correction lineage, exact/current queries, and one three-section display projection.
- Persistence adds one case-scoped EF entity/store/configuration, associates estimate lines with versions, enforces unique case/version and one filtered current accepted row per case, and preserves workflow lease/version/replay/history conventions.
- Migration `20260819112640_VersionedRepairSpecifications`, designer and snapshot add the singleton schema and backfill legacy lines into one version-1 `Draft` / `LegacyUnresolved` row without accepted actor/time or fabricated source artifact/version/hash.
- Focused Core, lifecycle, migration and schema-manifest tests cover the authorized contract.
- FRD-06 records one shared canonical version. No Reports/renderer/FRD-11/package-lock changes.

### Comments and dispositions

- **Blocking finding from prior review — resolved by [[PR-011]].** Repository-wide targeted inspection found no repair-specification purpose/role type, field, constraint, query, test or FRD wording and no Audit `Conservative`/`Maximised`, dual-specification or uplift behavior. Unrelated pre-existing conservative MOT and regional-uplift vocabulary is outside this contract. Disposition: fixed-in-PR; PR-011 archived and blocking edge removed.
- **Non-blocking — pass.** Authorized behavior remains complete: one current accepted version per case, immutable accepted evidence, source/calculation provenance, Engineer-only acceptance, reasoned successor/supersession, exact-version retrieval, deterministic line projection, and fail-closed legacy migration. Disposition: fixed-in-PR.
- **Non-blocking — pass.** The PIR matches the 15-file diff and governing FRD-06. The correction plan, checklist, open questions, ticket Outcome and PR description consistently follow the later TICK-205 operator correction. Disposition: fixed-in-PR.
- **Non-blocking — pass.** Simplification is honest and applied: accepted-first canonical projection, one Core Engineer-policy owner, one line factory, necessary serializable mutation/intermediate supersession save, and no deferred finding. Disposition: fixed-in-PR.
- **Non-blocking — pass after rerun.** GitHub Actions run `32249065655` initially timed out SQL shard 1 because uncached restore/build consumed roughly half the 20-minute job ceiling; there was no assertion failure. The failed job was rerun and passed in 9m14s. Final required checks are green: unit 4m49s, browser 8m06s, SQL shards 1/2/3 9m14s/10m15s/10m25s, SQL coverage 30s, changes/docs/reference; infrastructure skipped. Disposition: verified.

### Verdict

**Pass.** PR-011 is fully resolved, no replacement blocker is required, and PR #420 is ready to merge to `dev`.
