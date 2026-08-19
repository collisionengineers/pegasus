# Plan — TICK-093: version the canonical repair specification

## Approach

Evolve the existing assessment estimate-line collection into the single shared Core repair-specification aggregate; do not create renderer-owned lists or a second Audit model. One ordinary assessment has exactly one current accepted canonical specification per purpose/version. Per the completed [[TICK-205]] decision, an Audit intentionally has two role-labelled current accepted versions—conservative and maximised—using the same aggregate and correction rules. Each accepted version retains its identity, role/purpose, ordered lines, source route/artifact/version, acceptance actor/time, deterministic calculation inputs/totals, and supersession lineage.

This ticket stays inside Assessment/Core persistence and FRD-06. It does not edit SIMPLI-014's active Reports/Infrastructure renderer, templates, FRD-11, package locks, or workspace removal. [[TICK-092]] later projects an exact accepted version into the assessment render snapshot. [[TICK-098]] later consumes the Audit pair and computes monetary uplift after its representative template is approved; TICK-093 does not render Audit or define presentation.

Current dev at 4d1bff3 has no competing TICK-093 worktree/branch and SIMPLI-014 does not modify the owned Assessment/persistence files. TICK-093 is implementation-ready after this plan, subject to the normal take-time worktree/branch and overlap recheck.

## Governing docs

- docs/frd/frd-06-vehicle-and-engineering-evidence.md owns professional fact authority, accepted source provenance, Engineer review, and correction by reasoned superseding version. The implementation should update it narrowly to record the already approved one-version-per-role/purpose rule, ordinary singleton, Audit conservative/maximised pair, and shared correction/fail-closed behavior.
- FRD-11 remains the downstream report behavior owner and is not edited here while SIMPLI-014 owns active report changes. TICK-092/TICK-098 consume the accepted specification identities rather than duplicating FRD-06 rules.
- ADR-0025 remains satisfied: the future renderer receives accepted Core data only. No new project, store, runtime, deployment unit, or ADR is required.
- The FRD-06 clarification changes no unresolved meaning; it records the explicit 2026-08-19 operator resolution already proved by [[TICK-205]].

## Steps

1. **Narrowly record the accepted FRD-06 contract.** Define one canonical accepted specification per role/purpose/version; ordinary assessment permits one ordinary role, Audit permits one conservative plus one maximised role; neither Audit version overwrites or aliases the other. Define required provenance, Engineer acceptance, immutable accepted versions, reasoned correction/supersession, compatible calculation basis, and fail-closed ambiguity. Keep percentage uplift and Audit presentation out of scope.
2. **Create the shared Core aggregate by evolving estimate lines.** Introduce stable specification identity/version and a closed role/purpose vocabulary around the existing EstimateLineCodes, ordered line fields, actor/confirmation conventions, and action history. Preserve the existing technical line types as the single calculation vocabulary and add one Core-owned mapping to the three report display sections—new parts, repairs, additional operations—without duplicating lines or prices in a report model.
3. **Add draft, acceptance, and correction operations.** Replace whole-collection mutation with explicit version-aware operations while preserving current edit-lease, expected-version, operation-key, reason, actor, and idempotency conventions. Automation/imported proposals remain unaccepted until an authorized Engineer accepts the exact source and mapping. Acceptance enforces one current version per role; correction creates a new version and retains the predecessor and reason.
4. **Persist versions and migrate without fabricated authority.** Add the minimal entities/configuration/migration for specification identity, role/purpose, version, ordered lines, route/source artifact/version/hash, calculation basis/totals, acceptance, and supersession. Preserve existing estimate lines and history, but do not invent Glass's/Audatex/AI provenance or Engineer acceptance for legacy rows; migrate them as explicit legacy/unresolved draft data requiring authoritative review before use.
5. **Adapt the current assessment surface, not the renderer.** Keep existing assessment query/save behavior usable by projecting/editing the current draft specification and show role/version/source/acceptance/correction state and actionable validation. For ordinary assessment, prevent competing current accepted specifications. Support Audit pair storage/selection at the domain level only; do not add an Audit render action, template, wording, comparison UI, or uplift calculation.
6. **Prove aggregate, persistence, and downstream seams.** Add focused tests for route/source identity, line normalization/order and three-section mapping, automation-unconfirmed behavior, Engineer acceptance, ordinary uniqueness, Audit pair coexistence, duplicate-role rejection, compatible-basis metadata, correction/supersession, idempotency/concurrency, legacy migration, and reload/history. Prove TICK-092 can query an exact accepted specification version without renderer parsing or policy and that no SIMPLI-014-owned file changed.

## Verification

- Run focused Core Assessment policy/operation tests and persistence integration tests.
- Apply the migration to an empty database and a representative pre-migration database fixture; verify all existing lines/history survive and no acceptance/provenance is fabricated.
- Run tests for ordinary singleton, Audit conservative/maximised coexistence, role ambiguity rejection, independent correction histories, and one Core mapping to the three display sections.
- Run the canonical locked restore, Release build, and full test profile required by docs/runbook.md.
- Inspect the diff for one line vocabulary, one role vocabulary, one aggregate, no renderer/template/FRD-11/package-lock changes, and no provider integration.
- The post-implementation report must state that TICK-093 proves the shared aggregate only; TICK-092 owns render-input projection, TICK-098 remains presentation-deferred, and SIMPLI-014 owns the renderer.

## Risks and open questions

- Migration authority: current rows lack source route/version and accepted-spec identity. Mitigation: preserve them as unresolved drafts and require explicit review; never infer provenance.
- Compatibility: existing assessment saves replace all estimate lines. Mitigation: retain a narrow compatibility projection onto the current draft while making accepted versions immutable.
- Duplicate concepts: adding display categories beside technical line types can create two taxonomies. Mitigation: keep EstimateLineCodes as calculation truth and one derived mapping for names-only presentation.
- Audit scope creep: the aggregate must support two roles, but no approved Audit template exists. Mitigation: store/version/accept the pair only; leave pair selection for rendering, uplift, wording, and visual evidence to TICK-098 after its trigger.
- Active SIMPLI-014 overlap is avoided by excluding Reports, Infrastructure/Reports, templates, FRD-11, and package locks. Recheck worktrees immediately before take.
- No operator question remains. TICK-205 is completed decision authority, not a blocker; TICK-098 and TICK-092 are downstream consumers.

## Simplification pass — 2026-08-19

Independent four-lens review completed against `origin/dev`.

- **Reuse/duplication:** passed. The implementation reuses `EstimateLineCodes`, `AssessmentPolicy` normalization, `CaseMutationGuard`, the existing action/history conventions, and the existing assessment persistence boundary. The three-section display switch is the single plan-approved derived mapping; no second report model or role vocabulary was introduced.
- **Policy altitude:** one finding applied. The initial implementation inferred a specific 20% VAT formula and non-VAT-registered treatment without governing authority. That formula was removed; Core now validates non-negative recorded inputs and only the authorized arithmetic invariant `Total = Labour + Parts + PaintMaterials + SpecialistOther + Vat`, retaining VAT registration and the accepted calculation-policy version as provenance.
- **Unnecessary abstraction:** one low finding applied. Removed the unused private `CurrentDraftAsync` helper from `EfRepairSpecificationStore`.
- **Test/operational efficiency:** no material finding. Focused Core policy, SQL lifecycle, and pre-migration fixture coverage are proportional. No cloud or provider integration was added.
- **Scope:** passed. No Reports, renderer, template, FRD-11, or package-lock file is changed.
