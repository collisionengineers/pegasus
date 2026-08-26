# Research — PR-066

## Question

Where is the invalid Flex Consumption always-ready scale-group name introduced, and what is the smallest safe correction?

## Findings

- PR #560 head `ec39cc18` configures `alwaysReady[].name` as bare `UnifiedWorkFunction` in `infra/modules/platform.bicep`.
- The same invalid value is locked by one C# architecture assertion and one PowerShell deployment-plan assertion.
- Azure Flex identifies an individually scaled non-HTTP function as `function:<FUNCTION_NAME>`; the required value is therefore `function:UnifiedWorkFunction`. The ordinary `AzureWebJobs.UnifiedWorkFunction.Disabled` setting remains a function-name setting and must not gain the prefix.
- Current `origin/dev` at `54ad60ea` does not contain the unified-function change. The affected files exist only on open PR #560's branch.
- A branch from `origin/dev` cannot express this focused correction without copying the broader INTK-043 implementation. The minimal code change must be applied on top of PR #560.

## Implication

Change exactly the Bicep designation and its two matching assertions. Do not rename the Function, queue, activation setting, or telemetry. The branch/base exception must be resolved before implementation.
