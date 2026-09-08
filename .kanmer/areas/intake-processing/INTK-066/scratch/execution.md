# INTK-066 execution — current state

## Preserved history

Operator approved splitting the oversized execution log on 8 September 2026. Original version `c6871c457b830386` contained 70,088 characters. All original content, including every failure, deviation and disposition, is preserved in order:

1. [History 1](execution-history-1.md)
2. [History 2](execution-history-2.md)
3. [History 3](execution-history-3.md)
4. [History 4](execution-history-4.md)

Each file has explicit boundary comments. Removing only the opening/closing envelope lines and concatenating the four enclosed segments reconstructs the original string exactly. This was checked against read-back content before replacing the current log. Do not trim segment whitespace.

## Current handoff

INTK-066 remains Implementing in its recorded worktree and branch. Last frozen source head begins e843; full exact identity and evidence are in history 4. Latest verifier returned IDLE after solution build (zero warnings/errors), 56 fixture tests and 140 browser tests passed, with no failures/skips in those completed test runs. Prior failures remain preserved; these passes are not post-merge proof. Canonical host owner remains DELIV-053 scratch/execution, returned IDLE by the verifier; re-read before future grants.

Operator explicitly requested removal of only QdosExtractionCoverageTests.cs (unsupported extraction-percentage gate), retaining exact-value, routing and classification tests. That deletion has NOT occurred. A fresh ready execution packet is required before resuming source work, followed by recording the scope disposition and remaining verification. No INTK-066 push, PR, merge or deployment has occurred. PLAT-046 PR 711 is separately in Review; do not absorb its scope.


## Sole-host grant — retired unsupported gate — 8 September 2026

Root confirms prior canonical IDLE version 3b17ce1f86f555a0. Sole CEALEX-May25 verifier /root/final_verifier now ACTIVE for INTK-066 at frozen clean HEAD 75f112a18ef7c228a044772686d8cbb403a4825d in .worktrees/INTK-066 / INTK-066-manual-upload-confirmation. The only delta from browser-passed e843 is the operator-approved deletion of QdosExtractionCoverageTests.cs; plan/files explicitly record that disposition, keeping all substantive tests and prior failure evidence. Execution log was losslessly split into ordered history files and fresh packet passed.

Run sequentially after fresh packet, exact Git/process/LocalDB preflight and engineering/runbook checks: dotnet build ./Pegasus.slnx --configuration Release --no-restore; dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build; dotnet test ./tests/Pegasus.Architecture.Tests/Pegasus.Architecture.Tests.csproj --configuration Release --no-build; dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category!=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2. Verify actual Architecture project name before starting; a mismatch stops for correction, not invention. Corpus immutable; do not add exclusions, edit tests, or waive failures. Stop first actual command failure; preserve all outputs/exits. Previous full aggregate remains INCONCLUSIVE, not retrospectively passed.

Same bounded resource cleanup authority as prior queue: after completed parent exit only, revalidate exact owned MSBuild nodes by PID/start/parent/executable/nodemode and stop only matching nodes if necessary; no foreign processes or blanket termination. No snapshots, source edits, packaging, push/PR, production/cloud/Outlook/Box or external SQL. Sole verifier owns 5-minute lease heartbeats until return. Record results in both owning scratch files and explicitly return canonical IDLE, lease implementing. Further snapshot/documentation work needs separate grant.

## Transitions

- 2026-09-08T21:30:28.250Z lease-phase implementing → running-command (lease eef8e3c2-cb07-4588-a7f9-5cb09a3433fc rev 46; expires 2026-09-08T23:00:28.237Z)

Primary preflight command correction: read-only `rg --files tests -g '*.csproj'` resolved the existing Architecture project as `tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj`. The grant's `Pegasus.Architecture.Tests` spelling was incorrect. Before any invocation of that step, replace only its path with the observed existing project: `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build`. No nonexistent command was required to run, no test assertion changed, and all grant boundaries remain. This resolves the named-command mismatch as the primary correction permitted by the packet.
