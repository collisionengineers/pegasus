# Plan: docs accuracy pass (task/docs-accuracy-pass)

Post-merge audit of dev after PRs #314/#315/#316 found eight statements that
no longer match the implementation. This task corrects them; no code changes.

## Findings to fix (from the independent audit, 2026-08-03)

1. architecture.md "every `/Intake` route returns 404 outside the two
   Development gates" — false after #316: the staff intake surface is served
   wherever intake is composed, including the Production profile; only the
   manual `ReceiveIntake` upload POST keeps the Development gates.
2. architecture.md local-development runbook repeats the same 404 claim.
3. architecture.md "bounded production adapters attach only at the Worker
   composition root" — the Web now composes Box custody and managed document
   content.
4. operations.md "The Web container app now declares Key Vault secret
   references" — premature: the references exist in bicep (merged), not in
   the deployed revision; reword to "from the composition-fix deployment".
   Applied after merging dev (text arrives with #316).
5. architecture.md "Implemented production targets" credits production
   composition to the Worker only — add the Web composition, tiered
   Implemented (merged), not Deployed.
6. architecture.md "Offline QDOS-alpha Web callers" heading and
   "Development-only Razor Page" slice label — stale framing; reword to staff
   callers.
7. engineering.md + ci.yml comment: "a CI-executed script" names an open set;
   the allowlist contains exactly `scripts/Invoke-QdosAlphaAcceptance.ps1`,
   and the doc-link script is deliberately excluded because its step always
   runs.
8. NOW.md release item "the two Box secrets the Web container app now
   references" — same premature "now" as finding 4; reword to the
   composition-fix release. Applied after merging dev.

## Non-goals

No behavior, code, or CI logic changes; the ci.yml edit touches a comment
only. No changes to claims by other agents in NOW.md.

## Verification

`pwsh ./scripts/Test-DocumentationLinks.ps1` passes; independent two-question
review on the PR.
