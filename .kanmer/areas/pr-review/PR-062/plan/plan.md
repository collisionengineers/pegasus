# Plan

## Governing docs

Correct the machine-readable metadata of ADR-0002/ADR-0032 without changing their approved partial-clause decision. No other governing document changes.

## Steps

1. Branch from PR #547's head so the correction can merge into that review branch.
2. Empty only the two whole-ADR relationship arrays.
3. Run focused comparison with ADR-0030, documentation link checks, and `git diff --check`.
4. Record docs-only simplification as n/a, report, commit, push, and open a PR targeting the INTK-041 branch.
5. Obtain independent review, merge the correction, then re-review PR #547.

## Proof

The focused diff contains two metadata-line changes; prose/index retains the partial relationship; documentation validation passes.
