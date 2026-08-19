# Independent review — PR #412 — 2026-08-19

Reviewer did not implement TICK-204.

## Changes

- `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`: adds a single Assessment-report outcomes subsection with the four-value vocabulary, shared bundle, per-outcome title/badge/headline/settlement mapping, Core ownership/fail-closed rules, and evidence-versus-policy boundary.
- No executable, reference, capability-registry, architecture or deployment file changes.

## Comments

1. **Blocking — Contract repair introduces an unsupported separate capped-amount input.** The table says settlement “records the accepted VAT-inclusive contract-repair amount as a cap,” and the following readiness sentence requires “the accepted capped amount for contract repair.” The approved `reference/rendererref1/report_data_schema.json` permits raw cost components only and requires repair total to be computed; `DESIGN_SPEC.md` uses that repair total in the agreed/cannot-increase wording. This conflicts with the plan/research statement that Core owns compute-once derived figures and would create a new input without operator/schema authority.

## Disposition

1. Filed as [[PR-003]], which blocks TICK-204. Required correction: make the Contract repair cap the Core-computed VAT-inclusive repair total from accepted raw inputs, and remove any implication that a separate capped-amount field is required. Re-review PR #412 after that ticket lands in the PR and the report/checks are updated.

## Checks performed

- Ticket is in Review, taken on `task/tick-204-assessment-outcomes`, and all open questions are resolved.
- Plan and Governing docs section read; FRD-11 modification is operator-authorized and ADR-0025 architecture is otherwise respected.
- Post-implementation report matches the one-file/32-insertion diff and correctly states docs-only scope, except that it does not identify the Contract repair input mismatch above.
- Simplification disposition is honest: `n/a — docs-only`; no unrelated or duplicated repository files were added.
- Full PR diff inspected against TICK-204 research, files/ripple effects, rendererref1 schema/design, FRD-11 and ADR-0025.
- PR targets `dev`, head commit `545a287d50bc9ab223db632e4c1905e575f1121e`, mergeable.
- CI is green: changes, documentation and reference-data succeeded; executable lanes correctly skipped for the docs-only diff.

## Verdict

**Needs changes.** Do not merge or move TICK-204 to Verifying until [[PR-003]] is resolved and the amended PR is independently re-reviewed.

## Independent re-review — 2026-08-19

**Verdict: PASS.**

- Plan coverage: the one-file FRD-11 diff implements the planned closed four-outcome vocabulary, shared bundle, Core-owned computation/readiness, and fail-closed wording boundary without adding implementation or deployment scope.
- Implementation coverage: Contract repair now correctly uses the Core-computed VAT-inclusive repair total from accepted raw cost components as its non-increasing agreed cap. The unsupported separate capped-amount input identified by [[PR-003]] is absent.
- Governing documents: the change is authorised feature behaviour in FRD-11 and remains consistent with ADR-0025; no ADR or other repository file is required for this docs-only ticket.
- Simplification: correctly recorded as n/a — docs-only; the mapping is compact and does not duplicate the renderer schema or introduce a second policy owner.
- Evidence: PR #412 head `8124ae2abf0ccbe24f57b52703c4dc48e6e6719c`; all required GitHub checks succeeded; merge state CLEAN; `git diff --check origin/dev...HEAD` passed; worktree clean; complete diff is limited to FRD-11.
- Disposition: [[PR-003]] is resolved by commit `8124ae2a`. No remaining review findings. PR #412 may merge to `dev`.
