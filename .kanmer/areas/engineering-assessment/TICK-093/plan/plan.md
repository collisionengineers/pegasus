# Plan — TICK-093: one canonical versioned repair specification

## Approach

Correct PR #420 to implement exactly one case-scoped canonical accepted repair specification. Remove the rejected purpose/role branching and all Audit-only conservative/maximised semantics. Retain the authorized shared behavior: immutable versions, ordered existing estimate lines, source provenance, accepted calculation inputs, Engineer acceptance, reasoned correction/supersession, exact-version retrieval, replay/history/concurrency conventions, and fail-closed legacy migration.

The PR is unmerged, so amend its branch-owned `20260819100144_VersionedRepairSpecifications` migration and generated model artifacts directly. Do not rewrite any migration already shared on `dev`.

## Governing docs

- `docs/frd/frd-06-vehicle-and-engineering-evidence.md` owns the canonical repair specification, professional authority, provenance, Engineer acceptance, and correction behavior. Reconcile it to one shared specification.
- [[TICK-205]] Outcome/open questions record the binding operator correction. Its older plan/PIR/proof are stale and must not drive implementation.
- [[PR-011]] is the review blocker this correction resolves.
- ADR-0025 remains satisfied: policy stays in Core and rendering remains downstream. No new project, store, runtime, or ADR.

## Steps

1. Remove `RepairSpecificationPurpose`, `RepairSpecificationRole`, purpose/role aggregate fields, request/query parameters, validation, and Audit-only tests.
2. Make draft creation/version assignment, acceptance uniqueness, correction lineage, and current-accepted lookup case-scoped while retaining edit lease, expected version, operation key, actor/reason, replay, and history.
3. Remove purpose/role persistence columns, constraints, and composite indexes. Enforce one current accepted row per case and unique case/version; keep case/operation uniqueness.
4. Amend only the unmerged branch migration and model snapshot. Preserve legacy lines as one version-1 `LegacyUnresolved` draft with no fabricated provenance or acceptance.
5. Reconcile FRD-06, research, files, questions, checklist, body, PIR, PR description, and traceability to one shared canonical specification.
6. Prove Core policy/mapping, SQL accept/correct/replay/exact query, combined migration manifest, pre-migration preservation, build/architecture, and scoped diff. Run the canonical full profile proportionally; isolated PR CI remains the merge authority.

## Reuse

Reuse `EstimateLineCodes`, `AssessmentPolicy.NormalizeRepairSpecificationLines`, `CaseMutationGuard`, existing edit-lease/replay/history conventions, existing EF assessment boundary, and the single Core-derived three-section display mapping. No second policy/list/adapter is introduced.

## Verification

- `dotnet restore --locked-mode`
- `dotnet build --configuration Release --no-restore`
- focused Core Assessment tests
- focused repair-specification and migration SQL tests
- architecture tests
- canonical `dotnet test --configuration Release --no-build` with a proportional local ceiling; PR CI supplies isolated authoritative completion
- `git diff --check` and scoped diff inspection for absence of Audit/conservative/maximised/uplift and Reports/renderer/FRD-11/package-lock changes

## Risks

- Existing dual-role code can survive in generated migration/model artifacts. Mitigation: repository-wide targeted search and focused schema test.
- Current accepted uniqueness can be weakened while removing composite keys. Mitigation: filtered unique `CaseId` index plus unique `CaseId, Version`.
- No operator question remains.

## PR-011 simplification pass — 2026-08-19

Independent four-lens review completed after removing the rejected role model.

- **Policy altitude:** applied. FRD-06 now describes one purpose-neutral case-scoped specification feeding the Case's report projections; it names no Audit-specific specification semantics.
- **Canonical versus edit state:** applied. The general assessment projection now prefers the current accepted specification and falls back to a draft only before any acceptance. Correction drafts remain addressable through the explicit repair-specification version/edit operations and cannot silently displace canonical accepted data.
- **One policy owner:** applied. Engineer authorization is exposed once by `RepairSpecificationPolicy.RequireEngineer`; Infrastructure no longer repeats the business role rule.
- **Reuse/duplication:** applied. Initial and cloned estimate lines now share the single `NewLine` entity factory.
- **Efficiency:** passed. Serializable mutation and the intermediate supersession save remain necessary for concurrency and the filtered one-accepted index.
- **Scope:** passed. Targeted search found no Audit purpose/role, Conservative/Maximised, dual-specification, or uplift semantics in the corrected Core/persistence/migration/tests/FRD contract.
