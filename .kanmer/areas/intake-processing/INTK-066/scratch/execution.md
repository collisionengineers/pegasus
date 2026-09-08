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

## Conditional continuation — scoped Razor snapshots and documentation

Only after every currently granted build/Core/Architecture/full non-browser command passes, the SAME sole verifier may continue without transferring the slot. This is a separate explicit grant for the remaining planned evidence, not permission to bypass a failure. Preserve the clean frozen source HEAD 75f112a18ef7c228a044772686d8cbb403a4825d through regression; then permit only generated HTML changes under docs/design/test-ui/pages/{upload-status,upload-group-status,case-create,queues}--*.html. Do not hand-edit generated output or alter catalogue, scripts, source, tests or assets. Read Razor implementation skill and existing script first.

Root revalidated capture target C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/INTK-066/artifacts/test-ui-capture inside the exact worktree, artifacts is an ordinary non-linked directory, target absent. Revalidate resolved absolute target and absence/link-free containment immediately before script (its normal fresh-capture path removes this disposable capture directory if it exists). No broad cleanup target is allowed.

Sequential commands from recorded worktree:
1. pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope upload-status,upload-group-status,case-create,queues -CaptureFilter "FullyQualifiedName~UploadConfirmationWebTests|FullyQualifiedName~QdosIntakeWebTests|FullyQualifiedName~CaseCreateWebTests|FullyQualifiedName~TriageQueuesWebTests|FullyQualifiedName~UploadCaseSearchBrowserTests"
2. pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope upload-status,upload-group-status,case-create,queues
3. pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1
4. pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1
5. pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base 9ae9db753e3a3ecce1d9735d5c2fbe6fb5b0ff2c -Head 75f112a18ef7c228a044772686d8cbb403a4825d
6. git diff --check

The existing capture script appends TestUiFocusedRenderTests and runs Browser capture/build, non-browser capture, then snapshot update. Account for all phases; no zero-selection success as Browser evidence. Nine catalogue states are expected across the four scopes; unchanged generated bytes need not be modified. If index or any undeclared tracked path changes, stop and report without cleanup. Preserve captured inputs for Verify. Stop first genuine failure, do not retry or improvise a fixture/source fix. Final report exact commands/exits/state inventory/generated paths, preserve regression results separately, return both records IDLE and lease implementing. No commit/push/PR or live writes. Root will review generated diff and own final commit/report/PR.

Static investigation while frozen full non-browser command remains in flight: verifier observed QdosIntakeWebTests.ReadableManualUploadStagesPendingWorkAndOpensItsStatusPage line80 expecting old automatic-matching/Unidentified message. No final aggregate exit/count yet; conditional snapshot grant is blocked by this observed failure. Root is inspecting current FRD-02 eligible manual non-image decision and existing Core CanBecomeCase(NeedsSorting), plus Create's mandatory correction/acceptance, before deciding whether this is an obsolete direct-consumer expectation or product defect. /root/fixture_corrections assigned read-only diagnosis only, no edits or host commands. Preserve no Case/PO before acceptance and duplicate receipt assertions; do not simply remove assertions or change the source fixture to an automatic channel. Source remains frozen75f112a18, no correction/retry authorized yet.
