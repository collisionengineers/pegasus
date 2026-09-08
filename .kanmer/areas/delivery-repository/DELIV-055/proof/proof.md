---
kind: proof-record
schema: 2
merged_sha: "a1f0bfe260ea05df531df6e0ca3109141e7697da"
environment: ".worktrees/verify-deliv-055-a1f0bfe260ea05df531df6e0ca3109141e7697da; Windows x64, PowerShell 7; sole verifier /root/agent_config_verifier"
verified_at: "2026-09-08T15:10:26.1581783Z"
result: PASS
receipts: []
attempts:
  - attempted_at: "2026-09-08T15:09:36.0852347Z"
    command: "pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1"
    cwd: ".worktrees/verify-deliv-055-a1f0bfe260ea05df531df6e0ca3109141e7697da"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "All relative Markdown links resolve; 140 files checked."
  - attempted_at: "2026-09-08T15:10:26.1581783Z"
    command: "pwsh -NoProfile -File ./artifacts/deliv-055-postmerge-parse.ps1"
    cwd: ".worktrees/verify-deliv-055-a1f0bfe260ea05df531df6e0ca3109141e7697da"
    exit_code: 0
    result: PASS
    authority: authoritative
    summary: "Non-executing Parser.ParseInput validation found exactly five indented PowerShell fences; all five parsed successfully. Together with the preceding link check and independent semantic review, all documentation obligations pass."
---

# Exact-merge verification

PR #705 was confirmed MERGED at the recorded SHA before detached verification.
The declared pr.yml / verify / push receipt lookup returned HTTP 404 because
the workflow is absent. This is the normal missing-receipt fallback, not a
passing CI receipt; every relevant check ran locally.

The worktree was exact, detached and clean before and after verification.
The temporary ignored parse harness used an indentation-tolerant fence reader
and PowerShell's language parser only, required exactly five blocks, and was
removed after the successful check. No embedded recipe was executed.
Markdown placement is not applicable: only two existing Markdown files changed,
with no added, renamed or copied Markdown path.

The independent review retained semantic mapping to the current Production
host configuration and confirmed no secret material, duplicate procedure,
dangling runbook dependency or release-order change. Prior execution/check
history remains in scratch/execution and scratch/verify; no failed verification
attempt has been discarded.

This is acceptance of the documentation correction integrated on dev.
No application build/test, migration, bootstrap, Azure write, promotion or
deployment occurred. PLAT-046 containment remains unresolved and outside
this ticket. Next: Done gate, then kanmer-closeout.
