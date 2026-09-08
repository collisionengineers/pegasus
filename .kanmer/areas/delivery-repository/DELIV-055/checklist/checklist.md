# Checklist — DELIV-055

- [x] Step 1 — Replace the stale runbook dependency with the self-contained
  manifest-bound migration-host environment recipe, including mapped
  non-secret values and inert placeholders.
- [x] Step 2 — Add only the permitted compact AGENTS link to the canonical
  migration recipe.
- [x] Step 3 — Inspect the bounded diff semantically, record omitted
  sole-verifier/review checks, and stop without commit, push, PR or release.

## Progress notes

- Planning records the deliberately unresolved PLAT-046 old-Web/Worker
  containment question as out of scope.
- The completed change is documentation-only and limited to the two expected
  files; `git diff --check` completed with exit code 0 (only working-copy
  line-ending warnings).
