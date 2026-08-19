# Plan — TICK-092: derive one accepted report-input snapshot

## Approach

Implement CASE-31 as the smallest Core-owned derivation from existing accepted case, assessment, repair-specification, Engineer-decision, and custodied-document owners. Do not create a second editable report record. After [[SIMPLI-014]], [[TICK-093]], and [[TICK-094]] merge, evolve their actual contracts: reuse the integrated typed assessment snapshot/renderer port from SIMPLI-014, the versioned canonical repair specification from TICK-093, the Engineer-confirmed values/outcome from TICK-094, current accepted CaseData projections, current document/photo custody identities, and AssessmentPolicy.EvaluateReadiness.

The output is one immutable derived assessment-report input with exact source identities/versions and deterministic payload hash. It is the upstream input consumed atomically by [[DOCS-001]], which owns render-request/reference creation, idempotency, persistence of the generated result, and automatic invocation. CASE-31 does not call the renderer, create report artifacts, or add another persistence aggregate for rendered reports.

This is a separate implementation slice, but it is not ready to execute while SIMPLI-014 and the two domain prerequisites are unmerged. Re-read their merged shapes before taking TICK-092; do not plan against current uncommitted names.

## Governing docs

- docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md requires accepted Core-owned data, deterministic template/payload identity, fail-closed readiness, retained provenance/custody, and no implication that generation is approval or sending. This plan implements that source-snapshot boundary without changing the document's meaning.
- FRD-06 remains the owner of professional engineering facts, repair-specification provenance, Engineer confirmation, and superseding corrections through [[TICK-093]] and [[TICK-094]]. CASE-31 consumes those owners and does not restate their policy.
- ADR-0025 remains satisfied because the snapshot and readiness live in Core and the later renderer stays an Infrastructure adapter. No new project, store, runtime, API, service, or ADR is required.
- No governing-doc edit is planned. If post-merge inspection exposes missing normative behavior, stop and route a narrowly scoped documentation prerequisite rather than silently inventing it in code.

## Steps

1. **Reconcile merged prerequisites before implementation.** After [[SIMPLI-014]], [[TICK-093]], and [[TICK-094]] merge, inspect their exact Core contracts, persistence versions/concurrency, tests, and FRD alignment. Name the existing report snapshot/renderer types, accepted repair-specification identity, Engineer finding identity, accepted case-data version, and custody types to reuse. Update the file map if names moved; stop if another active ticket owns the same files.
2. **Define the derived accepted-source contract in Core.** Evolve the merged SIMPLI-014 assessment snapshot rather than adding a parallel DTO. Bind the Case/reference/principal and incident/vehicle facts, accepted assessment version, canonical repair-specification version, Engineer finding/version, selected Engineer identity, fee, ordered photo/document custody identities, calculation/template payload version, and each source hash/version. Make the snapshot immutable and non-editable; deferred Audit/addendum/diminution shapes get no speculative wrapper or optional fields.
3. **Centralize readiness and deterministic derivation.** Extend AssessmentPolicy.EvaluateReadiness or its merged single owner so the active four-outcome assessment schema uses accepted values only and returns stable actionable blockers. Reuse RPT-02's closed outcome-specific requirements and the merged snapshot validation rather than copying required-field lists. Reject missing, ambiguous, unconfirmed, stale, cross-case, outcome-mismatched, superseded, or uncustodied inputs before a snapshot/hash exists.
4. **Read one consistent source boundary.** Add the minimal Core query port and Infrastructure implementation needed to compose the snapshot from the accepted owners at one concurrency/version boundary. Detect change between reads and retry or fail closed; never copy values into a separately editable report table. Produce the deterministic payload hash from one canonical representation and return the exact source identities/versions with it. Leave atomic render-job/reference creation and idempotent result storage to [[DOCS-001]].
5. **Expose truthful readiness without activating rendering.** Adapt the existing assessment/case projection used by staff to show Ready only when the derived snapshot can be produced and otherwise show the exact stable blockers. Do not add a render button/caller, mark a report generated, imply approval/sending, or expose unsupported template families; DOCS-001 owns the real automatic caller and generation state.
6. **Prove the source boundary and hand it to DOCS-001.** Add focused Core and persistence/integration tests for every approved outcome, accepted-only selection, exact source/version/hash binding, ordered repair/photo content, stale/concurrent change detection, cross-case/mismatched/custody failures, deterministic equal-input hashes, changed-version hashes, and immutable earlier snapshots after correction. Run canonical verification and document the precise contract/query DOCS-001 must consume without remapping policy.

## Verification

- Record the prerequisite merge SHAs and exact merged types/ports reused.
- Run focused Assessment, CaseData, Documents/Custody, repair-specification, Engineer-finding, and Reports Core tests.
- Run focused persistence/integration tests proving a consistent source version boundary, correction history, deterministic hashing, concurrency failure/retry, and no editable report copy.
- Run the canonical locked restore, Release build, and full test profile required by docs/runbook.md.
- Inspect the branch diff for one readiness owner, one outcome/required-field vocabulary, no renderer invocation, no duplicate report aggregate, and no unsupported/deferred capability abstraction.
- The post-implementation report must distinguish Core/source-snapshot proof from DOCS-001 caller/idempotency/artifact proof and from PLAT-007 deployment proof.

## Risks and open questions

- Active overlap: SIMPLI-014 currently introduces the report snapshot/port while TICK-093 and TICK-094 will change its inputs. Mitigation: hard blockers and mandatory post-merge reconciliation before taking TICK-092.
- Cross-aggregate consistency: reading accepted facts separately can produce a mixed snapshot. Mitigation: capture and validate exact concurrency/source versions at one Infrastructure read boundary; fail closed on change.
- Duplicate readiness lists: AssessmentPolicy and the merged report snapshot may both validate fields. Mitigation: designate one Core policy path and reuse its typed results; formatting validation must not become a second business list.
- Premature generalization: CASE-31's title names later reports/statistics, but only the assessment caller is concrete now. Mitigation: implement the concrete derived assessment snapshot and shared source identities only; extend when a second approved caller supplies real requirements.
- No operator question remains. Implementation readiness requires merged SIMPLI-014, TICK-093, and TICK-094 plus a conflict-free file ownership check. DOCS-001 is downstream, not a blocker of CASE-31.

## Operator correction — shared Audit/Inspection physical report — 2026-08-19

This supersedes any earlier plan statement that Audit rendering requires a separate representative template, layout, wording artifact, dormant family, or future activation ticket. The operator confirmed that Audit and Inspection processes differ internally, but the physical report output has no differences. Reuse the approved inspection/assessment report template and presentation through the existing Core render contract. Preserve Audit-specific workflow/data rules in their owning Core capabilities; do not create a second renderer template or presentation policy.
