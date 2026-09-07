# Post-implementation report — MAIL-036

## Result and changes

Implemented in MAIL-036-wipe-boundary from dev
2e50fde474ce35eb32eff8677eb2327cb6aad272. No live wipe or cloud mutation.

- Existing wipe calls one SQL body with a UTC cutoff in its deletion
  transaction, after verifying Worker Stopped. Missing poll rows are seeded;
  scope rebinding cannot lower an existing later boundary.
- Graph rejects old notifications before MIME download.
- User's notification-flow clarification removed the routine 50-message
  delta scan. Existing PollApprovedInbox notification handling processes
  only its exact message and releases the lease without advancing the
  recovery cursor. Timer/lifecycle recovery remains.
- Updated FRD-08, runbook, AGENTS and the existing wipe skill. No schema,
  dependency, new worker, or alternative policy owner.

## Verification — Windows PowerShell 7

All commands ran from .worktrees/mail-036; root is the sole host verifier.

| Command | Exit | Observed |
| --- | --- | --- |
| dotnet restore ./Pegasus.slnx --locked-mode | 0 | Locked restore succeeded. |
| dotnet build ./Pegasus.slnx --configuration Release --no-restore | 0 | Initial wipe fix; 0 warnings/errors, 2m18s. |
| dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ApprovedMailboxEstateIntegrationTests\|FullyQualifiedName~ProductionGraphSourceTests" -- xUnit.MaxParallelThreads=2 | 0 | 72 passed, 0 skipped/failed, 1m32s. |
| dotnet build ./Pegasus.slnx --configuration Release --no-restore | 0 | After explicit notification-flow clarification; 0 warnings/errors, 1m10s. |
| dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter FullyQualifiedName~PollApprovedInboxTests | 0 | 27 passed, 0 skipped/failed, 167ms. |
| dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~WipeBoundarySurvivesMissingPollStateAndGraphScopeRefresh -- xUnit.MaxParallelThreads=2 | 0 | 3 passed, 0 skipped/failed, 47s; now includes real lease completion preserving cursor. |
| PowerShell Parser.ParseFile on scripts/Invoke-IntakeDataWipe.ps1 | 0 | Syntax passes; wipe script never invoked. |
| git diff --check | 0 | No whitespace errors. |

No assertion failure occurred. One apply_patch attempt had a mismatched
context; it changed no files and was corrected after rereading the exact
test lines. No test infrastructure or fabricated domain evidence was added;
Graph tests reuse the existing structural protocol fixture.

## Production diagnostics (read-only; not acceptance)

The live Inbox boundary is 2026-08-27T10:20:33 UTC, identical to activation;
the subscription is Active and expires 2026-09-13. Worker availability Normal,
state Running. Timer configuration is five-minute Inbox recovery and
one-minute pending-work recovery. Last 24h App Insights shows 286 recovery
invocations and two webhook requests; both are subscription validations,
not email notifications. This does not prove notification delivery works.
Azure CLI KQL quoting rejected two filtered queries (exit 1); a read-only
query via the same App Insights API returned the above conclusive validation
flags. No diagnostic failure was counted as PASS.

## Scope/simplification

The only new port operation completes the existing mailbox lease without
pretending a delta scan happened. Reuses UpdateOwnedStateAsync and the sole
existing test fake. Reset SQL is consumed by the actual wipe and the tests,
not copied into a second implementation. Existing Core cutoff checks remain
the business owner. Canonical docs own behavior; local pack is evidence only.

## Handoff

Independent review after pushed exact head. Corrective-PR verification uses
the focused evidence above and no duplicate full CI run; the final integrated
v1 head still owes its release verification. Live notification and reset
acceptance remain explicit; this ticket does not authorize performing a wipe.
