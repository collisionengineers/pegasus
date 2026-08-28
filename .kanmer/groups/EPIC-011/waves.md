# EPIC-011 waves and file ownership

A ticket owns whole files. Two tickets in one wave never share a path. Each wave merges to `dev` green; `main` is promoted after wave 5.

## Wave 0 — board, gate, governing docs
KANMER-006 (setup drift) · UIIMP-005 (snapshot tooling + CI gate) · UIIMP-006 (`docs/design/README.md`) · UIIMP-007 (`frd-12`, `capabilities.md`, `boundaries.md` except the correspondence row, `index.md`) · PLAT-047 (`frd-01`, `frd-04`) · AUTO-009 (`frd-10`, `frd-11`, `adr/0035`, `adr/README.md`) · MAIL-024 (`frd-08`, `adr/0036`, boundaries correspondence row).

## Wave 1 — design system + shell (single ticket, merges before any page)
PLAT-029: `wwwroot/css/site.css`, `wwwroot/js/site.js`, `wwwroot/fonts/inter/**`, `Pages/Shared/*` (layouts, sprite, partials, new `_ShellDialogs`, `_AdminNav`), `Presentation/RailCountsPageFilter.cs`, `Presentation/OperatorLabels.cs`, route moves + 301 stubs (`Triage/Index`→`Cases/Index`, `Cases/Index`→`Search/Index`, `/Triage`, `/Unidentified` stubs), delete `ImageIntake/Index`, `Administration/Index`, auth/error/status/Connect frames, shell tests, new `Browser/LayoutIntegrityTests`, class touch-ups in the 12 class-referencing test files, `docs/design/test-ui/catalogue.json` structural edits.

## Wave 2 — page ports (parallel by folder; all blocked by PLAT-029; ≤4 lanes)
A Work Centre (`Pages/Index.*`, `Core/Operations/DashboardCounts.cs`) · B Inbox + message (`Pages/Mail/**`) · C1 Cases queues (`Pages/Cases/Index.*`, `Unidentified/Index.*`) · C2 Triage/Unidentified detail + Received + image record (`Pages/Triage/Details.*`, `Unidentified/Details.*`, `Intake/**`, `ImageIntake/Details.*`) · D Search (`Pages/Search/**`) · E1 CASE-012 Case workspace frame + Overview (`Pages/Cases/Details.*`, `Cases/Shared/_CaseSummary/_CaseWorkflow/_CaseHistory`, `_CaseWorkspaceNav`, `Workflow.*`, `Closure.*`, `Create.*`, `Eva/Send.*`) · E2 Vehicle/Inspection/Case Files/Notes views (`Vehicle.*`, `Custody.*`, `Tasks.*`, `_CaseDocuments`, `Documents/**`) — blocked by E1 · F Assessment shell (`Pages/Cases/Assessment/**`) · G Upload + public request (`Upload*`, `Uploads/**`, upload presentation) — blocked by INTK-001 · H PLAT-023 Operations (`Pages/Operations/**`) · I1 PLAT-027 accounts & roles · I2 PLAT-025 configuration · I3 PLAT-026 mail settings · I4 PLAT-028 principals (read/edit) · I5 AUTO-006 automation panel (`Pages/Administration/<area>/**` each).

## Wave 3 — backend (parallel by Core folder; one unmerged migration at a time)
AI job ledger + connector tools (`Core/AiWork/**`, `Web/Mcp/AiJobMcpTools.cs`) → TICK-058+TICK-061 Provider API + credentials → Estimates (`Core/Assessment/Estimates.cs`, `RepairSpecifications.cs`, report projection) → Valuations (`Core/Assessment/Valuations.cs`) → Timeline + Action logs + rail/stage counts (`Core/Cases/CaseTimeline.cs`, `Identity/ActionLogs.cs`, `Operations/RailCounts.cs`, `DashboardCounts.cs`, `Actors/ActorDisplayNames.cs`) → Service health + Engineer report (`Core/Operations/ServiceHealth.cs`, `Core/Reports/EngineerActivityReport.cs`) → Outbound mail + flag + delete + EVA-sent detection (`Core/Mail/OutboundMail.cs`, Graph adapter, Worker).

## Wave 4 — feature UIs (blocked by wave-2 page + wave-3 backend)
Operations AI Job List/Service health/Send Unidentified to AI · Automation & AI settings · Principal settings dialog · Admin Action Logs/Reports/Service health · Case Notes/Valuations/Vehicle checks/upload-request fields · Assessment estimate editor + Send to Claude · Mail composer/Flag/Delete + correspondence actions · Report-sent confirm + Return to Engineer.

## Wave 5 — removals, catalogue, current-state docs, final walk (serial)
Delete legacy CSS block and superseded stubs/partials · `docs/current-architecture.md`, `docs/operations.md`, release record · final browser-walk proof at 1580/1100/760.

## Orchestrator loop (per merge)
`dotnet restore ./Pegasus.slnx --locked-mode` → `dotnet build ./Pegasus.slnx --configuration Release --no-restore` → `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"` → Browser filter with `xUnit.MaxParallelThreads=2` → `scripts/Update-TestUiSnapshots.ps1` (+ `-Verify -SkipCapture`) → `scripts/Test-UiCatalogue.ps1` → `scripts/Test-MigrationGrants.ps1` for migrations. Snapshot regen once per merge on the merging branch only. ≤3 build PRs open at once. Independent review per PR.

## Allocated ticket ids (2026-08-28)

Wave 0: KANMER-006, UIIMP-005, UIIMP-006, UIIMP-007, PLAT-047, AUTO-009, MAIL-024 (all merged except UIIMP-005 #588).
Wave 1: PLAT-029.
Wave 2: A UIIMP-008 · B MAIL-025 · C1 CASE-025 · C2 INTK-046 · D CASE-026 · E1 CASE-012 · E2 CASE-027 · F ENG-025 · G INTK-047 · H PLAT-023 · I1 PLAT-027 · I2 PLAT-025 · I3 PLAT-026 · I4 PLAT-028 · I5 AUTO-006.
Wave 3 (migration order): AUTO-011 (merged 658a7984) → TICK-061 (+TICK-058) → ENG-026 → ENG-027 → CASE-028 → PLAT-048 (no migration) → MAIL-027. Added 2026-08-28 by the operator: KANMER-005 (exclusive edit leases across staff and Automation Actors) ships with CASE-024 (PR #581 merged 1f2cf4a6); Case/Assessment lanes rebase over both.
Wave 4: PLAT-049, AUTO-010, PLAT-050, PLAT-051, CASE-029, ENG-028, MAIL-026, CASE-030.
Wave 5: UIIMP-009 → DELIV-030 → UIIMP-010.
