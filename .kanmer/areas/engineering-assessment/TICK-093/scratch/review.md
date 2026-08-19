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
