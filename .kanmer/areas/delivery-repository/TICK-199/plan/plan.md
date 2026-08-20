## Plan — TICK-199: Retire `.infisical.json`

### Governing docs

This is repository hygiene with no product behaviour, no new capability, and no
architectural decision — nothing to record in a PRD/FRD, and no durable technical
decision to record in an ADR. `docs_todo: true` is set on the ticket instead of a
governing-doc ref, matching the precedent set by TICK-197 (same area, same profile,
same reasoning, both filed from the same legacy-tracker migration).

### Approach

Read-only investigation (research.md) found `.infisical.json` has no supported caller
anywhere in the repo. Remove it with `git rm`; nothing else references it by filename,
so there are no stale references to fix. The Infisical CLI itself stays documented as an
approved tool (runbook.md, Invoke-Doctor.ps1, PegasusPlatform.ps1) — unrelated and
untouched.

### Steps

1. `git rm .infisical.json` in the task worktree.
2. Re-run the repo-wide filename/keyword search from research.md against the working
   tree to confirm nothing else needs edits (already clean).
3. Commit, push, open PR (docs/repo-hygiene only — no build/test surface changed).

### Verification

- `git status` shows only the deletion.
- `grep -rn "\.infisical\.json"` over the tree returns nothing (already true before the
  change; confirms no dangling reference is introduced by removal).
- No secret value read, copied, rotated, or committed.

### Simplification pass

n/a — docs/repo-hygiene only, single file deletion, no code touched.
