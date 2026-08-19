# Plan — TICK-205: Resolve the canonical repair-specification versus dual-Audit-specification conflict

## Diff estimate

No repository diff. The operator decision is already recorded, the capability registry already distinguishes ENG-01 from RPT-03, and the resolved model has been consumed by [[TICK-093]] and [[TICK-098]] research. This ticket closes the apparent conflict without modifying SIMPLI-014's active assessment/fee-note integration branch.

## Approach

Reconcile the historical question as a resolved ownership rule: an ordinary accepted assessment has one canonical repair specification per purpose/version; an Audit intentionally has two immutable, role-labelled accepted versions—`conservative` and `maximised`—and Core derives their monetary uplift once. Neither Audit version overwrites or aliases the other. The shared versioned repair-specification aggregate and its FRD-06 behaviour belong to ENG-01/TICK-093; later Audit consumption and FRD-11 behaviour belong to RPT-03/TICK-098. Audit rendering remains unavailable behind TICK-207 until a representative template is supplied or approved. This Kanmer-only reconciliation beats adding a second implementation, editing files concurrently owned by SIMPLI-014, or inventing Audit presentation.

## Governing docs

- **Meets FRD-06:** preserves the authorised-human/Core ownership, source-labelled provenance, and correction-by-superseding-version rules for each repair specification. TICK-093 is the owner that will evolve the singleton estimate-line model into the shared versioned aggregate and make any eventual FRD-06 change.
- **Meets FRD-11:** preserves deterministic computation, exact accepted-version binding, immutable report provenance, fail-closed inputs, and correction/addendum rather than overwrite. TICK-098 will add/implement the later Audit behaviour only after its data and template prerequisites are accepted; this ticket does not edit FRD-11 while SIMPLI-014 owns its active assessment/fee-note change.
- **Meets ADR-0025:** keeps repair policy in Core and future rendering in the integrated Infrastructure adapter. No workspace, renderer template, standalone package/service/host, or deployment boundary is added.

No governing document is modified by TICK-205. FRD changes are deliberately left to the capability owners that implement the behaviour, avoiding a premature second normative copy.

## Steps

1. Reconcile TICK-205's Outcome and acceptance statements to the accepted dual-role model: exactly one accepted canonical version per role/purpose; Audit requires `conservative` plus `maximised`; monetary uplift is Core-derived; percentage uplift remains undefined.
2. Record explicit subsumption: TICK-093 owns the shared repair-specification aggregate/provenance/correction model, and TICK-098 owns later Audit selection/uplift/report behaviour. Keep TICK-205 linked to both.
3. Record explicit deferral: TICK-207 owns representative Audit template/wording approval; until then Audit rendering is unavailable and assessment/rendererref1 templates cannot be repurposed.
4. Write a zero-diff post-implementation report tying the decision to the three governing refs and proving no overlap with SIMPLI-014 source/templates/FRD-11 work.
5. Verify the operator resolution, capability rows, downstream research adoption, resolved/parked questions, and empty repository diff; capture proof only at the decision/ownership tier.

## Verification

The post-implementation report and later proof will record:

- Kanmer reads showing the resolved TICK-205 question and links to TICK-093, TICK-098, and TICK-207;
- `rg -n -C 4 "ENG-01|RPT-03|conservative|maximised|uplift" docs/capabilities.md`, confirming one canonical ENG-01 capability and the intentional RPT-03 pair;
- direct comparison of TICK-093 and TICK-098 research, confirming both already consume the same role-labelled/versioned model;
- SIMPLI-014 plan/diff inspection confirming its current activation is assessment/fee-note only and contains no Audit model/template;
- `git diff --stat origin/dev...HEAD` on the TICK-205 branch, expected empty;
- evidence that no FRD, Core, persistence, renderer, template, infrastructure, cloud, or `main` change was made.

This proves resolution and allocation only. It does not prove the versioned aggregate, Audit rendering, Audit wording/layout, persistence migration, or deployed behaviour.

## Risks / open questions

- Risk: closing the decision is mistaken for implementing ENG-01 or RPT-03. Mitigation: Outcome/proof name the downstream owners and evidence boundary explicitly.
- Risk: a second repair-line or Audit-only aggregate appears in SIMPLI-014. Mitigation: SIMPLI-014 stays assessment/fee-note only and must not add the deferred Audit contract.
- No operator-only question remains. Monetary uplift means the derived difference between accepted totals. Percentage uplift stays parked until denominator and rounding are separately accepted.
- Audit rendering stays parked under TICK-207; this is the required deferred next step, not a blocker to SIMPLI-014's assessment integration.

## Operator correction — shared Audit/Inspection physical report — 2026-08-19

This supersedes any earlier plan statement that Audit rendering requires a separate representative template, layout, wording artifact, dormant family, or future activation ticket. The operator confirmed that Audit and Inspection processes differ internally, but the physical report output has no differences. Reuse the approved inspection/assessment report template and presentation through the existing Core render contract. Preserve Audit-specific workflow/data rules in their owning Core capabilities; do not create a second renderer template or presentation policy.

## Operator correction — no Audit uplift or dual specification — 2026-08-19

This supersedes the entire earlier dual-role/uplift plan. The operator clarified that Audit reports are identical to Inspection reports and that the only difference is internal workflow/reference identity. Do not implement conservative/maximised Audit specification roles, monetary uplift, percentage uplift, an Audit-only aggregate, or related presentation. [[TICK-098]] must reconcile the stale governing capability/FRD wording and reuse the Inspection report path with the existing `a.` / `ap.` reference rule.
