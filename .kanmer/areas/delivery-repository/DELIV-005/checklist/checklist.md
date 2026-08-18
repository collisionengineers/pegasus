# Checklist — DELIV-005

- [x] Remove only the Markdown-placement workflow step from `.github/workflows/ci.yml`.
- [x] Confirm the placement regression and documentation-link steps remain.
- [x] Run the retained documentation scripts and whitespace check.
- [x] Record `n/a — docs-only` simplification pass and commit the one-file change.
- [x] Open a PR to `dev` with the verification results.

## Progress notes

- 2026-08-18: Removed the CI gate that evaluates Markdown file placement.
  Retained checks pass: `Test-TestMarkdownPlacement.ps1` and
  `Test-DocumentationLinks.ps1` (221 files); `git diff --check` passed.
- 2026-08-18: Opened PR #401 against `dev`.

## Closeout — DELIV-005 (2026-08-18)

- [x] PR #401 MERGED 2026-08-18T09:41:45Z
- [x] proof.md written on main; moved to Done; Outcome recorded
- [x] Worktree `../pegasus-worktrees/deliv-005-remove-markdown-ci` removed; local + remote branch deleted; prune
- [x] Released
