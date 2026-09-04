---
kind: proof-record
merged_sha: "c90f2b8915186efd5bf932cec573846ae75ff1fe"
environment: "detached worktree .worktrees/verify-deliv-046-c90f2b8915186efd5bf932cec573846ae75ff1fe on Ubuntu/WSL, PowerShell 7.6.5"
verified_at: "2026-09-04T15:09:55.356Z"
result: PASS
attempts:
  - attempted_at: "2026-09-04T15:09:55.356Z"
    command: "git merge-base --is-ancestor origin/main HEAD"
    cwd: ".worktrees/verify-deliv-046-c90f2b8915186efd5bf932cec573846ae75ff1fe"
    exit_code: 0
    result: PASS
    summary: "The authorised main tip is an ancestor of the exact PR merge commit."
  - attempted_at: "2026-09-04T15:09:55.356Z"
    command: "git rev-list --left-right --count origin/main...HEAD"
    cwd: ".worktrees/verify-deliv-046-c90f2b8915186efd5bf932cec573846ae75ff1fe"
    exit_code: 0
    result: PASS
    summary: "Observed 0 left and 70 right; main has no commit absent from the merged dev history."
  - attempted_at: "2026-09-04T15:09:55.356Z"
    command: "git merge-base --is-ancestor e66e10699b54db5ba794a47292dc38729da24329 HEAD"
    cwd: ".worktrees/verify-deliv-046-c90f2b8915186efd5bf932cec573846ae75ff1fe"
    exit_code: 128
    result: FAIL
    summary: "Verifier supplied a nonexistent expanded SHA; Git reported not a valid commit name. No repository assertion ran."
  - attempted_at: "2026-09-04T15:09:55.356Z"
    command: "git merge-base --is-ancestor HEAD^1 HEAD"
    cwd: ".worktrees/verify-deliv-046-c90f2b8915186efd5bf932cec573846ae75ff1fe"
    exit_code: 0
    result: PASS
    summary: "The actual current-dev first parent e66e106993acbae39eaa6abd5c0e592a52302c61 is preserved."
  - attempted_at: "2026-09-04T15:09:55.356Z"
    command: "git diff --exit-code origin/main HEAD -- tests/Pegasus-Test-Logs/basic-intake-match-testing/test-cases/test1"
    cwd: ".worktrees/verify-deliv-046-c90f2b8915186efd5bf932cec573846ae75ff1fe"
    exit_code: 0
    result: PASS
    summary: "All four authorised test artifacts are byte-identical to origin/main."
  - attempted_at: "2026-09-04T15:09:55.356Z"
    command: "pwsh ./scripts/Test-DocsLinks.ps1"
    cwd: ".worktrees/verify-deliv-046-c90f2b8915186efd5bf932cec573846ae75ff1fe"
    exit_code: 64
    result: FAIL
    summary: "Verifier used a nonexistent shorthand script name; PowerShell rejected it before a check ran."
  - attempted_at: "2026-09-04T15:09:55.356Z"
    command: "pwsh ./scripts/Test-DocumentationLinks.ps1"
    cwd: ".worktrees/verify-deliv-046-c90f2b8915186efd5bf932cec573846ae75ff1fe"
    exit_code: 0
    result: PASS
    summary: "All relative Markdown links resolve; 125 files checked."
  - attempted_at: "2026-09-04T15:09:55.356Z"
    command: "pwsh ./scripts/Test-MarkdownPlacement.ps1 -Base HEAD^1 -Head HEAD"
    cwd: ".worktrees/verify-deliv-046-c90f2b8915186efd5bf932cec573846ae75ff1fe"
    exit_code: 0
    result: PASS
    summary: "Markdown placement passed for the exact merge delta."
  - attempted_at: "2026-09-04T15:09:55.356Z"
    command: "git diff --check HEAD^1..HEAD"
    cwd: ".worktrees/verify-deliv-046-c90f2b8915186efd5bf932cec573846ae75ff1fe"
    exit_code: 0
    result: PASS
    summary: "No whitespace errors in the exact merged delta."
---

# Verification proof — DELIV-046

## Outcome

PASS. GitHub reports PR #660 merged at the recorded merge SHA. The detached worktree is clean and detached at that exact SHA. Both the current dev parent and the authorised main history are ancestors, origin/main has zero commits absent from the merged history, and all four authorised artifacts match origin/main exactly.

## Retained failed attempts

Two command-entry errors are retained above: a nonexistent expanded SHA and a nonexistent shorthand documentation script. Each failed before testing repository behaviour. Their corrected exact commands passed in the same detached worktree.

## Hosted evidence

All required PR checks passed at the reviewed head before merge: unit, browser, Test UI, all three SQL integration shards and coverage, documentation, local-development scripts, reference data, and change classification. Infrastructure was intentionally skipped by path classification.

## Merge identity

PR: https://github.com/collisionengineers/pegasus/pull/660  
Merged at: 2026-09-04T15:08:06Z
