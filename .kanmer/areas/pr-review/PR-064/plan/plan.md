# Plan — PR-064

## Approach

Create PR-064’s isolated worktree and branch from exact PR-063 head `1cd0c4c1`, then open a stacked PR back into `task/pr-063-default-fidelity`. Correct the two canonical state claims, select the vehicle page’s valid no-images branch, and add one focused image-source rule to the existing catalogue validator. Re-audit all 39 defaults and amend upstream evidence documents with the actual rerun.

## Governing docs

`docs/frd/frd-12-operator-experience.md` requires states and evidence to be presented truthfully. This plan removes two false state claims and strengthens static evidence validation without changing product behavior. The existing design authority and Test UI boundary remain unchanged; no PRD, ADR, runtime, or deployment modification is needed.

## Steps

1. Create `task/pr-064-test-ui-contradictions` in `../pegasus-worktrees/pr-064-test-ui-contradictions` from exact PR-063 head `1cd0c4c1`, record the take, and confirm the diff base.
2. Update the organization-edit inventory description to the populated Work Provider branch already rendered by the page.
3. Update vehicle-image detail to the awaiting-instruction/no-images branch by removing the Images gallery and changing the canonical branch description; do not add new image data.
4. Extend `scripts/Test-UiCatalogue.ps1` within its existing per-file scan to report an error for any `img` start tag without a non-whitespace `src`, handling absent, empty, and whitespace-only values.
5. Run the validator successfully, then prove absent and empty/whitespace image-source negative fixtures fail by temporary worktree edits that are restored afterward.
6. Recheck the canonical branch claim and linked markup for all 39 visual defaults against current Razor/PageModel owners; record any further contradiction before claiming completeness.
7. Run PowerShell parsing, focused catalogue validation, documentation checks, locked restore/Release build, and `git diff --check task/pr-063-default-fidelity...HEAD`.
8. Run and record the required four-lens simplification pass over PR-064’s own diff, applying behavior-preserving findings only.
9. Amend [[PR-063]] and [[UIIMP-002]] checklist/report evidence truthfully, write PR-064’s implementation report, commit/push, open a PR targeting `task/pr-063-default-fidelity`, and move PR-064 to Review without self-review or merge.

## Proof

Review receives the corrected inventory/page diff, validator success, distinct missing/blank-source failure outputs, a complete 39-default recheck record, build/docs/diff results, simplification dispositions, and a stacked PR with deployment `n/a`.

## Risks and mitigations

- Regex validation must tolerate attribute order and casing: match complete `img` start tags case-insensitively and inspect each for a non-empty `src` attribute.
- The no-images branch must remain coherent: remove the whole conditional Images section, not only the broken element.
- Absolute upstream claims can drift: amend them only after the 39-default rerun.
- Stacked topology can move: branch from and diff against the recorded exact head; target only PR-063’s branch.
