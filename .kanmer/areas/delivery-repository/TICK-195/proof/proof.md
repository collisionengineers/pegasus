# Proof — TICK-195 (verified on merged `main`, 2026-08-18)

- Delivered: PR #384 merged 2026-08-17T06:35:48Z and shipped to `main` in #394 — the `documentation` job ran `Test-TestMarkdownPlacement.ps1` and the `Test-MarkdownPlacement.ps1` gate; both were green on the `main` push run 32038177963.
- Subsequently **removed by operator decision** ([[DELIV-005]], PR #401, 2026-08-18): the placement gate rejected a non-product asset README (`src/Pegasus.Web/wwwroot/images/marks/README.md`) and blocked the release; the operator directed its removal as unnecessary CI policy. On `main` `f1e116c6` the `documentation` job keeps `Markdown placement regression tests` (`Test-TestMarkdownPlacement.ps1` → `Markdown placement regression tests passed.`) and `Documentation links`, but no longer runs the gate; `scripts/Test-MarkdownPlacement.ps1` remains in the tree, uncalled by CI.
- `git diff --check` clean on the DELIV-005 commit; `Test-DocumentationLinks.ps1` → 222 files resolve.

Outcome: the ticket's deliverable was shipped and verified, then rolled back by a later decision recorded on DELIV-005; nothing further is owed here.
