---
kind: proof-record
merged_sha: "2e50fde474ce35eb32eff8677eb2327cb6aad272"
environment: ".worktrees/verify-deliv-049-2e50fde474ce35eb32eff8677eb2327cb6aad272; Windows PowerShell 7"
verified_at: "2026-09-07T20:40:54.664Z"
result: PASS
attempts:
  - attempted_at: "2026-09-07T20:40:54.664Z"
    command: "git rev-parse HEAD; git symbolic-ref --short -q HEAD; git status --porcelain"
    cwd: ".worktrees/verify-deliv-049-2e50fde474ce35eb32eff8677eb2327cb6aad272"
    exit_code: 0
    result: PASS
    summary: "Assertion wrapper verified exact merge SHA, detached HEAD (symbolic-ref exit 1 expected), and clean worktree."
  - attempted_at: "2026-09-07T20:40:54.664Z"
    command: "git diff --check 3da60bd0c270111d5168dc17246dc831882108ea HEAD"
    cwd: ".worktrees/verify-deliv-049-2e50fde474ce35eb32eff8677eb2327cb6aad272"
    exit_code: 0
    result: PASS
    summary: "No whitespace errors."
  - attempted_at: "2026-09-07T20:40:54.664Z"
    command: "git grep -n NOW.md -- AGENTS.md docs scripts"
    cwd: ".worktrees/verify-deliv-049-2e50fde474ce35eb32eff8677eb2327cb6aad272"
    exit_code: 1
    result: PASS
    summary: "No active reference; grep exit 1 is the expected absence."
  - attempted_at: "2026-09-07T20:40:54.664Z"
    command: "pwsh -NoProfile -File scripts/Test-MarkdownPlacement.ps1 -Base 3da60bd0c270111d5168dc17246dc831882108ea -Head HEAD"
    cwd: ".worktrees/verify-deliv-049-2e50fde474ce35eb32eff8677eb2327cb6aad272"
    exit_code: 0
    result: PASS
    summary: "Markdown placement passed."
  - attempted_at: "2026-09-07T20:40:54.664Z"
    command: "pwsh -NoProfile -File scripts/Test-TestMarkdownPlacement.ps1"
    cwd: ".worktrees/verify-deliv-049-2e50fde474ce35eb32eff8677eb2327cb6aad272"
    exit_code: 0
    result: PASS
    summary: "Markdown placement regression tests passed."
---

# DELIV-049 verification

[PR 677](https://github.com/collisionengineers/pegasus/pull/677) merged into
dev on 2026-09-07 at 20:20:12Z after independent /root/pack_reconcile review.
This is the exact merge, not the author head. No application or deployment
claim follows from documentation-only verification. Historical CHANGELOG
mentions remain historical; current routing and placement exceptions are gone.
The earlier implementation diagnostic with an unretained process exit is
recorded as inconclusive in the post-implementation report, then separately
re-run successfully; none of these post-merge attempts failed.
