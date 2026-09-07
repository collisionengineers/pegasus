---
kind: proof-record
merged_sha: "987988e0b984ad63c1de4be3ad189f3afeb2928c"
environment: ".worktrees/verify-plat-028-987988e0b984ad63c1de4be3ad189f3afeb2928c; Windows x64, PowerShell 7, .NET 10, SQL Server LocalDB; root verifier"
verified_at: "2026-09-07T22:06:40Z"
result: PASS
attempts:
  - attempted_at: "2026-09-07"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-plat-028-987988e0b984ad63c1de4be3ad189f3afeb2928c"
    exit_code: 0
    result: PASS
    summary: "Root supplied locked restore PASS at exact merged SHA."
  - attempted_at: "2026-09-07"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-plat-028-987988e0b984ad63c1de4be3ad189f3afeb2928c"
    exit_code: 0
    result: PASS
    summary: "Root: zero warnings/errors, 58.32 seconds."
  - attempted_at: "2026-09-07"
    command: 'dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Cases.OrganizationAdministrationTests"'
    cwd: ".worktrees/verify-plat-028-987988e0b984ad63c1de4be3ad189f3afeb2928c"
    exit_code: 0
    result: PASS
    summary: "Root: 19 passed, 0 failed, 86 ms."
  - attempted_at: "2026-09-07T22:00:01.7236098Z"
    command: 'dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~OrganizationAdministrationWebTests|FullyQualifiedName~OrganizationAdministrationPersistenceTests|FullyQualifiedName~OrganizationDirectoryWebTests|FullyQualifiedName~PrincipalCredentialPersistenceTests|FullyQualifiedName~ProviderApiSubmissionTests|(FullyQualifiedName~AccessibilityTests.RealAuthenticatedRouteHasNoAxeViolationsAndNoInlineStyleAttribute&DisplayName~Administration/Principals)|FullyQualifiedName~QdosAllocationRecoveryBrowserTests.FailedAllocationShowsSafeRecoveryWithoutRawIdentifiers" --logger "trx;LogFileName=plat-028-merged.trx"'
    cwd: ".worktrees/verify-plat-028-987988e0b984ad63c1de4be3ad189f3afeb2928c"
    exit_code: 0
    result: PASS
    summary: "Root: 28 passed, 0 failed, 121 seconds; retained TRX counters independently read back."
---

# Exact-merge proof — PLAT-028

PR680 is MERGED at the full SHA above and reachable from origin/dev.
The verification worktree is clean and detached at that SHA. All 41 paths
changed by reviewed head d2bf633ec8ddc5b08b4052554d1d4e79f3930682 match
the merge (scoped diff exit0). Full trees differ by58 other-lane paths;
no whole-tree or binary equivalence is claimed.

Root ran the checks above; this worker reused the exact-merge evidence and
read the retained TRX, without starting duplicate verification. Date-only
attempt timestamps retain the execution date supplied by root; precise
integration start/finish are in the TRX (finish22:02:04.8449994Z).

TRX: tests/Pegasus.IntegrationTests/TestResults/plat-028-merged.trx
under the detached root; SHA256
E1785259DBC1FFB54F443CB1A20A8ACC71E5B57E807ADD377DC18E932438DFE5.
Counters: total/executed/passed28; failed/error/inconclusive/notExecuted0.

## Acceptance

Actual Principal create/list/settings/replacement routes, Core commands and
EF persistence prove flat atomic customer creation/replay, same-customer code
replacement with preserved default-location fields, independent real
directory behavior, provider credential lifecycle/show-once handling and
authorization. The exact merged browser cases prove Principals/Create
accessibility and QDOS prerequisite correction/recovery/replay.

Independent review556f03971e0d6572 passed the exact premerge head with F-001
fixed. The original needs-changes review0f92badd048442ee and report
6cdb9dc68164e418 retain the missed-browser-caller failure and correction.
There were no failed executable attempts in this exact-merge cohort.
Earlier focused tests and scoped snapshot/catalogue passes remain their
original-head evidence; all affected snapshot paths match the merge.

## Limits and handoff

Root's attempted file-URL visual inspection was blocked before merge; no full
manual viewport/zoom visual pass is claimed. This is ticket integration
proof, not deployment, live customer creation or whole-v1 acceptance.
Final integrated release CI/packaging and deployment remain root-owned.
No source change, new build/test, Done move or cleanup occurred in this
proof-writing step. Root approval precedes Done/closeout and claim release.
