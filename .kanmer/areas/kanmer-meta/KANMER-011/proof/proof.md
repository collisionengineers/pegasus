---
kind: proof-record
merged_sha: "659cec770c52d900c8c126e60a704482138665c3"
result: PASS
proof_type: command-log
verified_at: "2026-09-03T19:31:09Z"
---
# Proof — KANMER-011 (command-log)

Verified on merged `dev` at `659cec770c52d900c8c126e60a704482138665c3` (PR #652 merge commit) in a disposable detached worktree `../pegasus-worktrees/verify-kanmer-011-659cec77`.

| Command | cwd | Exit | Result |
|---|---|---|---|
| `pwsh ./scripts/Test-DocumentationLinks.ps1` | detached worktree | 0 | All relative Markdown links resolve (125 files checked). |
| `git grep -c "../../../../docs/manual"` | detached worktree | — | 0 matching files |
| PR CI run 33796353697 (`documentation`, `changes`, `local-development-scripts`, `reference-data`) | GitHub | — | all pass at head `f8933069`; heavy lanes path-skipped (Markdown-only) |

Result: **PASS** — the `documentation` job is green on the integrated state; no copied skill links outside the repository.
