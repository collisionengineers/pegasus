# Plan — TICK-097: accept four-outcome assessment rendering

## Approach

Treat RPT-02 as a coordinated capability-acceptance ticket, not an independent implementation branch. [[SIMPLI-014]] owns the single integrated renderer adapter and approved rendererref1 assessment/fee-note resources; [[TICK-092]], [[TICK-093]], and [[TICK-094]] own the accepted source snapshot, canonical repair specification, and Engineer-confirmed outcome/economics; [[TICK-096]] owns deterministic validation, compute-once figures, and fixed-design acceptance; [[DOCS-001]] owns the real complete-assessment caller, idempotency, immutable artifact identity, provenance, and custody. TICK-097 closes only when their combined merged evidence proves the RPT-02 behavior below.

No separate RPT-02 Core model, renderer, template catalogue, persistence stream, endpoint, service, worktree, or Azure unit is planned. Any gap is fixed in its owning dependency or raised as a narrowly scoped follow-up rather than duplicated here.

## Governing docs

- docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md owns the four closed outcomes, shared and conditional content, Core-computed figures, fail-closed readiness, human-review boundary, and acceptance evidence.
- docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md owns monolith integration behind a Core port.
- No new governing document is required: merged FRD-11 and ADR-0025 already own the behavior and mechanism.
- reference/rendererref1/ remains immutable evidence, not runtime policy.

## Steps

1. **Accept the single adapter and active resource set from [[SIMPLI-014]].** Confirm the merged branch exposes one Core-owned rendering port with one Infrastructure implementation; embeds only the approved rendererref1 assessment and fee-note family; keeps unsupported catalogue entries inactive; and produces the assessment artifact plus fee-note presentation without generic payload-authored policy. Reuse the integrated adapter and canonical resources exactly; do not create an RPT-02 renderer.
2. **Accept the upstream immutable render input.** Confirm [[TICK-092]] supplies one accepted case/engineering snapshot, [[TICK-093]] supplies one versioned repair specification mapped to new parts, repairs, and additional operations, and [[TICK-094]] supplies the selected Engineer's confirmed outcome, values, salvage/category, roadworthiness/reason, and Core-derived economics. The render snapshot must reference these owners and versions rather than copy editable report data.
3. **Accept deterministic four-outcome behavior with [[TICK-096]].** Through one closed outcome vocabulary, prove Total loss, Repairable, Cash in lieu, and Contract repair use the shared assessment sections and their correct conditional title/badge/figure/narrative. Prove Core computes VAT, repair total, total-loss settlement, cash-in-lieu amount, Contract-repair VAT-inclusive cap, and fee totals once; Infrastructure/templates only format them. Confirm selected Engineer/signature/qualification matching and exact approved wording fail closed when unavailable.
4. **Accept the real caller and retained result from [[DOCS-001]].** From a complete accepted assessment, prove the composed application invokes rendering once per accepted source version and retains immutable report/fee-note relationship, reference, version, hashes, template/payload/calculation versions, source provenance, custody, and correction lineage. Retries reconcile the same result; missing, ambiguous, unconfirmed, stale, mismatched, or uncustodied required inputs do not render. Generation remains a draft/review event, not approval, sending, receipt, invoicing completion, or case closure.
5. **Record combined evidence and disposition.** Collate focused Core tests for readiness/calculations/mapping and Infrastructure/integration evidence for all four representative rendererref1 jobs, both repairer VAT bases, roadworthy/unroadworthy conditions, ordered photos, long repair lists, fee-note values, invalid inputs, retries, and version correction. Confirm real Chromium PDFs have the expected page/artifact structure, stable hashes for identical inputs, distinct hashes/identity for corrections, and no inactive family can be selected. If all dependency evidence is merged and green, record TICK-097 as subsumed/accepted with no repository diff; otherwise route each concrete gap to its owning dependency.

## Verification

- Inspect merged diffs and proof for [[SIMPLI-014]], [[TICK-092]], [[TICK-093]], [[TICK-094]], [[TICK-096]], and [[DOCS-001]] on the current delivery branch.
- Run the focused Core assessment/report test suites and report integration/render tests named by those tickets.
- Render and inspect the four supplied representative outcome jobs and their fee-note output with the production Chromium path; retain generated evidence under artifacts/, never reference/.
- Run the canonical locked restore/build/test profile required by docs/runbook.md before final acceptance.
- Verify no second renderer/policy owner, inactive template activation, report-only editable record, or separate runtime/deployment unit was introduced.

## Risks and open questions

- Dependency ownership is the main coordination risk: overlapping changes in Core assessment/report contracts must land through their owning tickets, not through TICK-097.
- Representative evidence may establish whether fee-note presentation is a page in one artifact or a separately retained linked artifact. Whichever the approved samples prove, its identity/hash relationship must be explicit; ambiguity blocks acceptance and is resolved from supplied evidence before code changes.
- No operator question is open. The four outcomes, active family boundary, approved wording, qualifications, and signatures are resolved.

## Operator correction — shared Audit/Inspection physical report — 2026-08-19

This supersedes any earlier plan statement that Audit rendering requires a separate representative template, layout, wording artifact, dormant family, or future activation ticket. The operator confirmed that Audit and Inspection processes differ internally, but the physical report output has no differences. Reuse the approved inspection/assessment report template and presentation through the existing Core render contract. Preserve Audit-specific workflow/data rules in their owning Core capabilities; do not create a second renderer template or presentation policy.
