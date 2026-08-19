# Plan — TICK-204: Define the missing assessment-report outcome variants

## Approach

Make one governing, docs-only change to FRD-11: record the operator-confirmed four-value assessment outcome contract and each variant’s observable differences, shared bundle boundary, required accepted inputs, and fail-closed behavior. This keeps product behavior in its canonical FRD while leaving code, template routing, capability activation, wording approval, and Azure integration to their owning EPIC-004 tickets. Updating the reference evidence, creating a new ADR, or implementing the renderer here would duplicate authority or overlap SIMPLI-014.

## Governing docs

- **Modifies — docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md:** explicitly authorized by the operator’s request to work the renderer tickets and the recorded 2026-08-19 “all yes” resolution. Add the closed vocabulary `total_loss | repairable | cash_in_lieu | contract_repair`; define the distinct title/badge/key-figure/settlement meaning for each; state the common assessment bundle; require accepted Core-owned inputs and fail closed on missing, unknown, conflicting, or incomplete outcome data; preserve the existing immutable artifact, approval, provenance, correction, and delivery rules.
- **Meets — docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md:** keeps Core as report-policy/readiness owner and treats `reference/rendererref1` as evidence only. It makes no architecture change, creates no separate renderer boundary, and leaves the Infrastructure adapter and real caller to SIMPLI-014.
- **No new ADR:** this ticket records feature behavior, not a new technical mechanism. The accepted integration architecture already exists in ADR-0025.

## Steps

1. Edit the report behavior section of FRD-11 to add one canonical assessment-outcome subsection containing:
   - the four closed outcome values and operator-confirmed distinct contract-repair status;
   - the shared assessment bundle contents;
   - a compact mapping of each outcome to title, badge, headline figures, settlement heading/meaning, and outcome-specific required data;
   - Core-owned selection/computation and fail-closed rules;
   - an explicit boundary that unresolved category/statement/signature/recovery wording remains unavailable under TICK-216 and that rendererref1 is evidence rather than policy.
   Reuse FRD-11’s existing report correction/finality section, the established capability IDs, and its existing prose style; do not reproduce the full JSON schema or sample wording.
2. Review the docs-only diff against TICK-204 research, the resolved open question, RPT-02, EPIC-004 context, and ADR-0025. Remove any implementation mechanism, duplicate capability table, or behavior belonging to TICK-206, TICK-216, or SIMPLI-014.
3. Verify the resulting contract mechanically and by focused inspection: confirm all four values appear once in the canonical vocabulary, contract repair is distinct, the fail-closed and accepted-Core-data rules are present, existing immutable/correction/Sent-evidence behavior is unchanged, Markdown links resolve, and no file outside FRD-11 changed.

## Verification

- Run `git diff --check`.
- Run focused `rg` checks over FRD-11 for the four outcome values, `RPT-02`, fail-closed language, and links.
- Inspect `git diff -- docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` against the ticket research and operator resolution.
- Record the exact commands and the one-file diff result in the post-implementation report. This docs-only ticket needs no build or application test because it changes no executable source; proof later records the committed diff and command output at the documentation evidence tier.

## Risks / open questions

- **Authority duplication:** copying the rendererref1 schema or all sample prose into FRD-11 would create a second mutable payload specification. Mitigation: state only normative outcome behavior and required boundaries; cite the supplied evidence in the ticket, not as a second repository authority.
- **Scope overlap:** adding models, templates, capability activation, wording approvals, or deployment details would overlap SIMPLI-014, TICK-206, TICK-216, and other EPIC-004 tickets. Mitigation: keep the diff to FRD-11 only.
- **Premature wording approval:** rendererref1 names unresolved salvage, statement, signature, and recovery wording. Mitigation: explicitly keep those paths fail-closed until TICK-216 resolves them.
- **Open questions:** none. The operator confirmed all four outcome values and the distinct contract-repair behavior on 2026-08-19.

## Simplification pass

n/a — docs-only. Review still checks that the FRD adds one canonical compact mapping without duplicating schema, capability, or implementation detail.
