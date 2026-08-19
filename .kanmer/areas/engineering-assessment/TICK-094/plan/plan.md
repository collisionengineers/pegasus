# Plan — TICK-094: Engineer-owned accepted report inputs

## Approach

Implement only the missing Core bridge from the existing confirmed assessment record to the integrated renderer's accepted immutable input; do not rebuild the assessment UI, persistence, renderer, or report workflow. Current Core already owns the closed field vocabulary, staff-Engineer confirmation, per-field provenance, total-loss salvage requirements, unroadworthy reason, and readiness blockers. [[SIMPLI-014]] is concurrently introducing the four-outcome report snapshot and Core calculation/presentation policy. TICK-094 therefore waits for that ticket to merge, then reuses those types and adds the single mapping/acceptance policy that turns current, confirmed Engineer decisions plus the accepted case/repair-spec versions into render inputs without retyping.

The ticket is **not ready for execution now**. [[SIMPLI-014]] is still Implementing, [[TICK-092]] has not supplied the accepted immutable case/engineering source snapshot, and [[TICK-093]] / EXT-09 have not supplied the versioned canonical repair specification and accepted calculation inputs. Starting before those owners land would duplicate contracts or guess formulas. Once those prerequisites merge, this remains one Core-focused unit; downstream deterministic rendering belongs to [[TICK-096]], combined RPT-02 acceptance to [[TICK-097]], and automatic invocation/custody to [[DOCS-001]].

## Governing docs

- **Modifies and meets — `docs/frd/frd-06-vehicle-and-engineering-evidence.md`.** Under the already approved four-outcome decision recorded by [[TICK-204]], reconcile the stale “Repairable or Total loss” sentence with the closed `total_loss | repairable | cash_in_lieu | contract_repair` assessment vocabulary. Preserve that roadworthiness is a separate named-Engineer finding, automation data remains unconfirmed, and corrections supersede rather than overwrite. Add no renderer mechanics.
- **Meets — `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`.** Supply only accepted source-labelled Engineer decisions and raw economic inputs to its Core-owned four-outcome/calculation boundary. Missing, ambiguous, stale, conflicting, or unconfirmed values fail closed; no caller-supplied derived total is accepted.
- **Meets — `docs/adr/0021-automation-actor-direct-write-assessment-contract.md`.** Reuse the existing staff-Engineer-only confirmation boundary and structurally keep automation unable to confirm findings or approve reports.
- **Meets — `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md`.** Work stays in existing `Pegasus.Core` assessment/report policy and consumes the single integrated renderer port from SIMPLI-014; no new project, adapter, service, API, MCP tool, or deployment unit is introduced.

## Existing code reused

- `AssessmentVocabulary`, `AssessmentPolicy.ValidateAndNormalize`, `ValidateMergedState`, and `EvaluateReadiness` remain the single owners of field codes, confirmation, conditional requirements, and actionable blockers.
- `CaseAssessmentProjection`, `AssessmentFieldValue`, and `CaseEstimateLineRecord` remain the current accepted-data/provenance representation; extend or adapt only where merged dependency contracts prove a missing version identity.
- The merged SIMPLI-014 `AssessmentReportSnapshot`, `AssessmentReportOutcome`, `ReportRepairCosts`, presentation/calculation policy, and renderer port are reused rather than duplicated.
- TICK-092 owns the immutable aggregate source/version boundary; TICK-093/EXT-09 own the canonical repair specification and accepted raw estimate semantics.
- Existing assessment persistence, Web controls, automation commands, edit leases, operation keys, and version guards remain unchanged unless a focused failing test proves a caller-backed gap.

## Steps

1. **Wait for and rebase onto the prerequisite owners.** Require merged, independently reviewed evidence from SIMPLI-014, TICK-092, and TICK-093/EXT-09. Confirm their types provide one immutable accepted source version, one canonical repair-specification version, and the integrated report snapshot/calculation contract. If any remains absent, stop and route the gap to that owner rather than adding a TICK-094 substitute.
2. **Reconcile the governing professional-finding vocabulary.** Update only FRD-06's stale two-outcome sentence to the already approved four-outcome assessment vocabulary, while preserving separate roadworthiness, named-Engineer authority, unconfirmed automation data, and superseding corrections. Do not duplicate FRD-11's report presentation/calculation table.
3. **Add one Core accepted-input mapper/policy.** From the merged accepted case/engineering snapshot, select only current confirmed fields and canonical repair lines; map outcome, Engineer value and any accepted deductions, salvage category/value, roadworthiness/reason, named Engineer identity, decision times, and source versions into the existing report input. Reject unknown codes, missing conditional fields, stale source versions, unconfirmed values/lines, conflicting versions, absent Engineer identity, and caller-supplied derived totals.
4. **Keep calculations and narratives at their existing owners.** Pass accepted raw inputs into the single merged Core report calculation/presentation policy; do not calculate in the mapper, Infrastructure, templates, Web, or persistence. Outcome-specific narrative selection remains the report policy from SIMPLI-014/TICK-096, while TICK-094 proves that the selected inputs came from the named Engineer's accepted record.
5. **Add focused Core tests only.** Cover all four outcomes, roadworthy/unroadworthy reason, total-loss salvage, accepted deductions where the merged contract defines them, named-Engineer confirmation, automation/unconfirmed rejection, stale/conflicting versions, incomplete inputs, and rejection of supplied totals. Assert the mapper carries source/version/actor/time provenance and does not mutate the assessment projection.
6. **Run proportional verification and document the ownership boundary.** Run locked restore/build, focused Assessment/Reports Core tests, full Core tests, and architecture checks. Record that renderer/PDF parity, automatic caller, persistence/custody, Azure runtime, and later Audit/diminution/addendum inputs remain with their owning tickets; perform the required simplification pass over the branch diff and write the PIR.

## Verification

The post-implementation report and later proof will record:

- `dotnet restore --locked-mode`;
- `dotnet build --configuration Release --no-restore`;
- focused `Pegasus.Core.Tests` Assessment and Reports tests for the accepted-input mapper and rejection paths;
- the full `Pegasus.Core.Tests` project and dependency-direction architecture tests;
- `rg`/diff evidence that no second outcome vocabulary, Engineer identity table, calculation formula, editable report record, renderer adapter, persistence stream, endpoint, or deployment unit was introduced;
- merged dependency SHAs/proof for SIMPLI-014, TICK-092, and TICK-093/EXT-09.

This ticket proves Core accepted-input selection and provenance only. It does not prove PDF rendering, all-four-variant visual parity, automatic assessment-complete invocation, immutable artifact custody, deployment, approval, sending, receipt, or invoicing.

## Risks / open questions

- **Blocked readiness:** SIMPLI-014, TICK-092, and TICK-093/EXT-09 are not merged. Mitigation: step 1 is a hard stop; do not execute yet.
- **Overlap with SIMPLI-014:** its current branch already owns the accepted report tuple and calculations. Mitigation: merge/rebase first and add only projection-to-snapshot selection/provenance that remains missing.
- **Formula authority:** current AssessmentPolicy explicitly defers estimate derivation until EXT-09 authority. Mitigation: never derive totals in TICK-094; consume the accepted canonical inputs and reuse TICK-096/SIMPLI-014's single calculation owner.
- **FRD consistency:** FRD-06 still names only Repairable/Total loss while merged FRD-11 names four outcomes. The operator-approved TICK-204 decision resolves the product choice; this plan performs the narrow consistency edit without changing meaning.
- **Open operator questions:** none. Audit, diminution, and addendum inputs remain explicitly deferred to their own approved contracts.
