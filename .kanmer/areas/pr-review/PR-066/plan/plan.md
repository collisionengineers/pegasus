# Plan — correct PR #560 Flex always-ready designation

## Governing docs

No governing PRD, FRD, or ADR changes are required. This is a deployment-contract correction within INTK-043's already accepted warm unified consumer design; its linked ADR-0033 remains unchanged.

## Approach

Have the existing INTK-043 owner amend PR #560 directly. This is the only proportional route because the affected code is not on `origin/dev`; a separate PR-066 branch would duplicate INTK-043 or require an exception to the repository's `dev` PR target rule.

## Steps

1. On PR #560's existing branch, change `infra/modules/platform.bicep` from `UnifiedWorkFunction` to `function:UnifiedWorkFunction` only inside `alwaysReady[]`.
2. Update the exact matching regex in `WorkerActivationReleaseContractTests.cs` and `Test-AzureDeploymentPlan.ps1`.
3. Confirm the Function census and `AzureWebJobs.UnifiedWorkFunction.Disabled` retain the bare runtime Function name.
4. Run the focused architecture test, local deployment-plan validation including Bicep compilation, and `git diff --check`.
5. Push the amended PR #560 head and require its CI rerun to pass before INTK-043 review continues.
6. Record proof of the corrected head on PR-066; create no duplicate product branch, worktree, or PR.

## Proof

The amended PR #560 diff contains the prefixed scale-group designation in Bicep and both assertions, local focused checks pass, and GitHub CI is green on that exact corrected head.

## Risks

A broad replacement would corrupt Function activation setting names. The edit and assertions must be limited to the Flex `alwaysReady` designation.
