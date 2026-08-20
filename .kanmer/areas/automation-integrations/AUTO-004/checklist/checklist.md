# Checklist — AUTO-004

- [x] Add the Unidentified/Triage/source tool names to the governed MCP inventory and create focused HTTP test fixtures using existing Automation test support.
- [x] Register and complete Unidentified list/detail/source/resolve for receipt and submission-group origins through existing Core/store ports.
- [x] Add typed Triage list/detail/source tools through `IListTriage`, `IGetTriage`, and `IDownloadIntakeSource`.
- [x] Add typed Triage Awaiting-information, finding, superseding-finding, response-link/unlink, complete, cancel, and reopen tools over existing Core commands.
- [x] Add typed Triage Case link/unlink tools using existing Case version/edit-lease and Triage version guards.
- [x] Register `TriageMcpTools` under `automation.intake` and prove discovery, success, denial, validation, replay, integrity, evidence, lease, attribution, and prohibited-surface behavior.
- [x] Update FRD-10, capabilities, and current-state documentation to the exact evidence tier without claiming an unapproved deployment.
- [x] Run and record the dated simplification pass across reuse, simplification, efficiency, and altitude; apply behavior-preserving findings.
- [x] Run locked restore/build, focused Core/Automation/Triage/Unidentified integration tests, ArchitectureTests, documentation links, and the full solution verification; summarize results in the post-implementation report.

## Progress notes

(append with `set_ticket_doc(doc: "checklist", append: true)`)

- 2026-08-20: Both tickets claimed on the shared worktree/branch. Implemented 35-tool governed inventory, Unidentified receipt/group projection and source download, typed Triage read/source/lifecycle/evidence/Case-association adapters, focused real-HTTP fixtures, and source-tier documentation. Focused MCP plus existing Triage replay/association tests are green (15 tests).

- 2026-08-20 verification: locked restore and Release build green; task-focused HTTP/connector rerun 4/4; existing Triage/MCP set 15/15; Core 758/758; Architecture 98/98; documentation links 192/192. Full IntegrationTests: 797 passed, 14 corpus-gated skips, 2 failures — the inventory-count duplication was corrected and its focused rerun passed; the unrelated Playwright `/Administration/Configuration` navigation timeout passed alone on rerun.
