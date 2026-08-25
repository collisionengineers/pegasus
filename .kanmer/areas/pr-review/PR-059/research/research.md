# Research — PR-059: reconcile ENG-016 evidence

## Question

What is the smallest evidence-only change that makes ENG-016's Kanmer record accurately describe the final reviewed implementation and its governing requirements?

## Findings

- ENG-016's current body and final plan state the operator's final rule: `Review` is the sole business readiness owner; Export permits populated suggestions, optional VAT/mileage, requires a unit only with mileage, defaults a missing Inspection Date, and does not repeat Case/Audit-custody or accepted-only gates. Source: ENG-016 body and `plan/plan.md`.
- ENG-016 `files/files.md` contradicts that final rule. It still directs the implementation to retain the strict accepted-evidence mapping, all thirteen required fields, Case/Audit custody gates, and no Inspection Date default. Its ripple-effects section repeats those rejected blockers. Source: ENG-016 `files/files.md`.
- ENG-016 research is a chronological record containing mutually superseded conclusions: permissive export, then accepted-only/custody export, then the final one-Review rule. The final clarification is present, but a reviewer must reconstruct which earlier claims are obsolete. Source: ENG-016 `research/research.md`.
- ENG-016 has no governing-document refs, so its feature-profile governing-doc gate is unsatisfied even though its plan cites business and migration rules. Source: `get_doc_gates ENG-016` (`references: []`, `refs: []`, `governing-doc: false`).
- The relevant governing sources are already clear:
  - `docs/frd/frd-07-eva-and-external-engineering-handoff.md` owns the one-Review/one-Export behaviour and the three routes.
  - `docs/frd/frd-04-parties-accounts-and-access.md` requires permanent action history for every export.
  - `docs/adr/0030-non-additive-schema-changes-before-cutover.md` permits direct pre-cutover removal but requires roll-forward recovery and an operations release record.
  - `docs/adr/0021-automation-actor-direct-write-assessment-contract.md` still names the deleted EVA MCP tools; PR-057, not PR-059, owns superseding that accepted decision.
- PR #539 currently changes 46 files. ENG-016's report is a feature summary, not an exact file-by-file inventory, and says all findings were applied even though independent review filed PR-055 through PR-060. Source: `gh pr view 539 --json files`, ENG-016 report, and ENG-016 `scratch/review.md`.
- The blockers have distinct owners and should not be reimplemented here: PR-055 atomic replay, PR-056 unconditional completeness, PR-057 ADR/MCP consistency, PR-058 batch image reads, and PR-060 migration commentary. PR-059 should reconcile evidence after those diffs land, accurately naming their dispositions.
- All 11 checks were green at reviewed head `cf28b8b0`, but that result predates the blocker fixes and is not final evidence for the amended PR. Source: `gh pr view 539 --json statusCheckRollup`.

## Implications

PR-059 is documentation/traceability work only:

1. Link ENG-016 to FRD-07, FRD-04, ADR-0030, and the ADR that resolves PR-057 once known.
2. Append a concise final research reconciliation that explicitly marks prior accepted-only/custody conclusions as superseded; preserve history rather than deleting it.
3. Replace ENG-016 `files.md` with an exact final surface map derived from the amended PR diff. The map must describe the final one-Review rule and include every changed file or a precise grouped entry whose rationale is identical.
4. Reconcile ENG-016 plan/checklist/report with the blocker dispositions and final SHA/test/CI evidence. Do not keep checked claims that the final code has not yet proved.
5. Update the ENG-016 report and PR description only after PR-055/056/057/058/060 are integrated, so one final inventory can name the actual outcome.
6. Add no product code, abstraction, compatibility layer, migration, or additional policy. This ticket fixes the record, not the design.

## Open questions

None. The operator has resolved the product rule, and the independent review has assigned each technical/doc conflict to a named blocking ticket.
