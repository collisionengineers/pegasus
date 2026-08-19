# Post-implementation report — TICK-204

## Summary

Defined the operator-confirmed four assessment-report outcomes in FRD-11 as one closed Core-owned contract. The docs-only change distinguishes each variant, fixes their shared bundle and headline/settlement meaning, requires accepted source-labelled inputs, and fails closed on incomplete or unaccepted content without implementing or activating the renderer. Review correction PR-003 now makes contract repair use the Core-computed VAT-inclusive repair total as its agreed cap; no separate capped-amount input is required.

## Changes

| File | Change | Why |
|---|---|---|
| docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md | Added the Assessment-report outcomes subsection and corrected contract-repair computation/readiness wording | Makes FRD-11 the single normative owner for the four-value vocabulary, shared bundle, variant distinctions, Core-owned calculation, fail-closed readiness, and unavailable-wording boundary without inventing an unsupported input. |

Commits:

- `545a287d50bc9ab223db632e4c1905e575f1121e` — define the assessment-report outcomes.
- `8124ae2abf0ccbe24f57b52703c4dc48e6e6719c` — resolve PR-003 by deriving the contract-repair cap from accepted raw costs.

## Governing docs

- **FRD-11 modified with explicit operator authorization:** the 2026-08-19 “all yes” resolution confirmed `total_loss | repairable | cash_in_lieu | contract_repair` and distinct contract-repair wording. The subsection records that behavior while preserving existing immutable artifact, approval, correction, provenance, and exact Sent-evidence rules.
- **Compute-once contract preserved:** the contract-repair cap is the VAT-inclusive repair total Core computes from accepted raw cost components. The readiness prose does not require a separately supplied/accepted capped amount.
- **ADR-0025 met:** `Pegasus.Core` remains outcome-selection and calculation owner; supplied templates/schema/samples are evidence rather than policy; no separate repository, service, package, API, MCP host, deployment, or Infrastructure-owned business rule is introduced.
- No ADR was added because this is feature behavior, not a new architectural mechanism.

## Risks / follow-ups

- SIMPLI-014 still owns the integrated Core port, Infrastructure adapter, real Web/Worker caller, and Azure composition; this change claims no implementation or deployment.
- TICK-206 owns the active capability/template boundary; this change does not expose a catalog.
- TICK-216 must resolve currently unaccepted category, recovery/storage, statement, qualification, and signature wording. FRD-11 keeps such content unavailable.
- The supplied DESIGN_SPEC.md retains two stale “three outcome” phrases; it remains reference evidence and was deliberately not edited.
- PR-003 identified an unsupported separate capped-amount input in the first revision. The owning PR now resolves it in commit `8124ae2a`; PR-003 remains for the independent review workflow to disposition.
- Simplification pass: n/a — docs-only. Focused diff review found no duplicated schema, allocation table, implementation detail, or extra file.

## Verification hand-off

On the PR head or merged target branch:

1. Run `git diff --check origin/dev...HEAD`; expect no whitespace errors.
2. Run `git diff --stat origin/dev...HEAD`; expect one changed file in FRD-11.
3. Run `rg -n "contract_repair|Core-computed VAT-inclusive repair total|accepted raw cost components|accepted capped amount|accepted VAT-inclusive contract-repair amount" docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`; expect the first three concepts and no obsolete separate-amount wording.
4. Run the broader outcome/authority search from the original report and inspect the complete FRD-11 diff against TICK-204 research, operator resolution, and PR-003. No build/test is required for this documentation-only change.
