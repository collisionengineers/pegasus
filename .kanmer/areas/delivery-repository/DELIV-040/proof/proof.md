---
kind: proof-record
merged_sha: "0f0e90ae44ffda7339ca2a460310deeb98121afa"
environment: "detached Windows PowerShell verification worktree .worktrees/verify-deliv-040-0f0e90ae44ffda7339ca2a460310deeb98121afa"
verified_at: "2026-09-02T11:45:29.189Z"
result: PASS
attempts:
  - attempted_at: "2026-09-02T11:45:29.189Z"
    command: "./scripts/Test-DocumentationLinks.ps1"
    cwd: ".worktrees/verify-deliv-040-0f0e90ae44ffda7339ca2a460310deeb98121afa"
    exit_code: 0
    result: PASS
    summary: "All relative Markdown links resolve; 87 files checked."
  - attempted_at: "2026-09-02T11:45:29.189Z"
    command: "./scripts/Test-MarkdownPlacement.ps1 (without mandatory Base and Head)"
    cwd: ".worktrees/verify-deliv-040-0f0e90ae44ffda7339ca2a460310deeb98121afa"
    exit_code: null
    result: INCONCLUSIVE
    summary: "No placement check ran because mandatory Base and Head parameters were omitted; retained before corrected invocation."
  - attempted_at: "2026-09-02T11:45:29.189Z"
    command: "./scripts/Test-MarkdownPlacement.ps1 -Base '0f0e90ae44ffda7339ca2a460310deeb98121afa^1' -Head '0f0e90ae44ffda7339ca2a460310deeb98121afa'"
    cwd: ".worktrees/verify-deliv-040-0f0e90ae44ffda7339ca2a460310deeb98121afa"
    exit_code: 0
    result: PASS
    summary: "Markdown placement passed for the exact merge diff."
  - attempted_at: "2026-09-02T11:45:29.189Z"
    command: "GitHub required checks for PR #643"
    cwd: "GitHub Actions"
    exit_code: 0
    result: PASS
    summary: "changes, documentation, local-development-scripts and reference-data passed; code-only lanes skipped for the docs-only diff."
  - attempted_at: "2026-09-02T11:45:29.189Z"
    command: "deployment verification"
    cwd: "not applicable"
    exit_code: null
    result: NOT_APPLICABLE
    summary: "Operator confirmed DELIV-040 is docs-only and requires no deployment; ticket deployment remains n/a."
---

# Verification proof

PR: https://github.com/collisionengineers/pegasus/pull/643

Merged: 2026-09-02T11:25:11Z

The exact GitHub merge commit passed the repository's documentation checks.
Deployment is not applicable to this docs-only delivery. Deferred scope is
recorded in [[DOCS-017]] and [[INTK-055]].
