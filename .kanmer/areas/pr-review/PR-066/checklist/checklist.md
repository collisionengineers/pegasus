# Checklist

- [x] Confirm the invalid value exists only on PR #560, not `origin/dev`.
- [x] Select direct amendment by the INTK-043 owner; avoid a duplicate branch/worktree.
- [x] Change only the Bicep always-ready designation to `function:UnifiedWorkFunction`.
- [x] Update the C# and PowerShell assertions.
- [x] Run focused architecture/deployment-plan validation and `git diff --check`.
- [x] Push the amended PR #560 head.
- [x] Confirm CI is green on exact head `520827c5744bd151464280ca2c5f1c315f19a5ba`.
- [x] Record corrected-head evidence and release the PR-066 review blocker.

## Evidence

Commit `912cb49c` changes exactly the Bicep designation and its two assertions. Activation contract tests 14/14 PASS, local deployment-plan/compiled-Bicep validation PASS, and `git diff --check` PASS. GitHub run `32981774968` passed 11/11 required checks on the corrected parent PR head.
