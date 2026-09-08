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

- 2026-09-08T22:31:44.965Z lease-phase running-command → implementing (lease eef8e3c2-cb07-4588-a7f9-5cb09a3433fc rev 62; expires 2026-09-08T23:01:44.954Z)

## Sole-host regression result — post-retirement full Release rails — 2026-09-08 — FAIL

Verifier `/root/final_verifier` held the sole CEALEX-May25 host slot for INTK-066. Frozen input was `C:/Users/Alex/Documents/GitHub/pegasus/.worktrees/INTK-066`, branch `INTK-066-manual-upload-confirmation`, clean HEAD `75f112a18ef7c228a044772686d8cbb403a4825d`.

Fresh resumed whole-ticket execution packet was ready. Plan/files explicitly authorized deletion of only `tests/Pegasus.IntegrationTests/QdosExtractionCoverageTests.cs`; preflight confirmed it absent. Preflight at `2026-09-08T21:32:14.2345529Z` passed: PowerShell 7.6.5; exact clean head/branch/worktree/common repository; no competing ticket location or host verifier; no dotnet/MSBuild/testhost/vstest process; external SQL override variables unset; Windows MSSQLLocalDB available. Read-only project census corrected the grant's mistyped Architecture path before invocation to the existing `tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj`; no nonexistent command ran.

Sequential results:
1. `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — exit 0: build succeeded with 0 warnings and 0 errors in 00:01:48.74.
2. Build parent PID 27160 (created `2026-09-08T22:32:41.546185+01:00`) exited. Six exact invocation-created reusable nodes—PIDs 11368, 20016, 27668, 13872, 27884 and 30092—were revalidated with parent 27160, Program Files dotnet executable, creation after the parent via DateTimeOffset, and expected `MSBuild.dll ... /nodemode:1 /nodeReuse:true` command. Exact-PID cleanup stopped those nodes; none remained at `2026-09-08T21:34:49.3996647Z`.
3. `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` — exit 0: 1,955 passed, 14 skipped, 0 failed, 1,969 total.
4. `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` — exit 0: 116 passed, 0 skipped, 0 failed, 116 total, duration 31s.
5. `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category!=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2` — exit 1: 1,969 passed, 5 failed, 0 skipped, 1,974 total, duration 54m33s.

Retained failures:
- `QdosIntakeWebTests.ReadableManualUploadStagesPendingWorkAndOpensItsStatusPage`, line 80: `Assert.Contains` expected the obsolete “This could not be matched automatically …” presentation text, absent from the returned HTML.
- `InstructionDraftWebTests.SameManualUploadTokenReplaysOneReceiptDraftAndAssetSet`, line 48: expected 2, actual 1.
- `InstructionDraftWebTests.SameManualUploadTokenWithDifferentBytesShowsConflictWithoutSecondPersistenceOrArtifact`, line 83: expected 2, actual 1.
- `InstructionDraftWebTests.IdenticalBytesWithDifferentTokensPersistDistinctSourceIdentitiesWithMatchingHashes`, line 119: expected `Guid`, actual null.
- `InstructionDraftWebTests.UploadAndReviewPersistsTypedFieldsAndAutomaticallyAllocatesTheInstructedCase`, line 154: expected `IntakeAllocationState`, actual null.

The four InstructionDraft failures are the directly affected ManualUpload automatic-allocation consumer class identified by read-only source inspection; the QdosIntake presentation expectation is separate. This records diagnosis only, not authorization to edit or waive them.

A read-only liveness check during the long Integration command found the correct test parent/vstest/testhost chain present and responding. Testhost PID 16428 had 1,117.015625 cumulative CPU seconds and gained 0.3125 CPU seconds over a five-second sample, so no stall or timeout inference was made and no process was interrupted.

Final postcheck at `2026-09-08T22:31:23.8688063Z` passed: exact clean HEAD `75f112a18ef7c228a044772686d8cbb403a4825d`, recorded branch/common repository unchanged, and no dotnet/MSBuild/testhost/vstest process remained.

Disposition: **FAIL**, stopped on the first failing command. Build, Core and Architecture passes are retained, as are all five Integration failures. The conditional four-scope snapshot capture, retained verification, catalogue, documentation links, Markdown placement and diff check were **NOT RUN**. No retry, source/test/snapshot edit, capture, packaging, push/PR, live/cloud/Outlook/Box or external-SQL action occurred.

All invoked processes exited. INTK-066 lease phase returned to `implementing` at revision 62. Canonical CEALEX-May25 host slot is explicitly **IDLE / unassigned**.

Canonical IDLE returned by final_verifier after full nonbrowser exit1 (1969 passed,5 failed,0 skipped,1974 total,54m33s), no host processes, exact75f source clean. Root grants bounded two-file test-only correction in recorded INTK-066 worktree: root exclusively QdosIntakeWebTests one method; /root/fixture_corrections exclusively InstructionDraftWebTests four methods as files disposition. Agent may edit ONLY assigned file, static diff only, no tests/build/capture/commit/push. Root reviews both then commits/re-freezes for fresh affected-class + four-scope snapshot/doc verification. No new product policy, no Corpus mutation, no assertions weakened to mask regressions.

- 2026-09-08T22:36:34.391Z lease-phase implementing → running-command (lease eef8e3c2-cb07-4588-a7f9-5cb09a3433fc rev 64; expires 2026-09-08T23:36:34.374Z)

## Sole-host corrected-consumer and final snapshot grant — 8 September 2026

Canonical prior IDLE b119b7f0064cf190 read; fresh ready resumed packet and clean frozen HEAD 137230ca4f9b5115e1176f387fd360dac7370d65 validated. Same .worktrees/INTK-066 / INTK-066-manual-upload-confirmation. Sole CEALEX-May25 owner /root/final_verifier ACTIVE. Source authors idle. Root reviewed exact two-file correction and caught/fixed the new CaseIntakeLinks count helper allowlist before runtime. All exact extraction/hash/asset/conflict/replay checks preserved. No production delta since browser-passed e843; 75f removed unsupported operator-retired percentage gate; 137 only aligns two direct manual-upload test consumers.

Sequential exact queue after preflight and relevant skills/runbook:
1. dotnet build ./Pegasus.slnx --configuration Release --no-restore
2. dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "(FullyQualifiedName~QdosIntakeWebTests|FullyQualifiedName~InstructionDraftWebTests)&Category!=Browser&Category!=Corpus"
3. Only after both PASS, execute all six previously recorded scoped snapshot/documentation commands, with SAME four scopes and explicit capture filter, changing only MarkdownPlacement -Head to 137230ca4f9b5115e1176f387fd360dac7370d65. Script browser/nonbrowser/snapshot-update phases must all pass, then retained Verify, catalogue, Test-DocumentationLinks, MarkdownPlacement, diffcheck. Re-read full prior conditional grant and Razor implementation skill; its capture containment, nine-state expected inventory, no hand-edit and no undeclared tracked output rules remain exact. Freshly validate the absolute nonlinked capture target immediately before script's normal disposable cleanup.

Preserve full nonbrowser 75f FAIL(1969 pass,5 fail) and every prior failure; the fresh two-class PASS can close those five corrected assertions but is not a claim that an entire new-head full suite was rerun. Core1955pass14skip/Architecture116pass at75f and fullBrowser140pass at e843 have unchanged application inputs; no unnecessary repeat of unrelated suites for test-only changes. Existing plan proportional verification scope applies.

Sole verifier owns lease heartbeats. Same exact-owned-node cleanup authority after completed parent exit, DateTimeOffset identity comparisons; no foreign/broad process termination. LocalDB and immutable corpus only, no external SQL/cloud/Outlook/Box, packaging, commit/push/PR. Stop first genuine failure and return both records explicit IDLE/lease implementing; no autonomous retry/source/test/snapshot fixes. Generated changes are limited to pages/upload-status--*,upload-group-status--*,case-create--*,queues--*.html; any index/other tracked drift stops. Record all command results, capture phases and state/file census for root review.

- 2026-09-08T22:49:30.840Z lease-phase running-command → implementing (lease eef8e3c2-cb07-4588-a7f9-5cb09a3433fc rev 69; expires 2026-09-08T23:19:30.832Z)
