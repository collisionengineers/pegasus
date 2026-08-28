# Research — PLAT-023 Operations workspace redesign (wave 2, lane H)

Branch `task/plat-023-operations` off `origin/dev` @ 4d696225 (post CASE-012
#599, CASE-025 #596, KANMER-005 #593; TICK-061/PLAT-048 merged earlier).

## Contract sources read

- EPIC-011 `context.md` §1.11 (Operations), §1.15 defects, §2 decisions
  (D5–D7); `waves.md` lane H = `Pages/Operations/**`.
- `docs/design/README.md` — tokens/components/voice/banned words/absent-vs-
  disabled; Operations workspace contract §Operations `/Operations`.
- Prototype `Pegasus_UI_Assessment_Refined.html`, effective final render only:
  `renderOperations` (line 1802) = `pageHeader('Operations','',freshness+Refresh)`
  → partial-data warning notice → `stack` of `aiJobsPanel` + `serviceTable` +
  `operationsTables` + EVA handoffs panel. Fixture data is not domain data.
- dev shell: `_Layout.cshtml` (rail, utility bar, one `main.app-main`,
  `TempData["Confirmation"]` notice), `_FreshnessBanner` (freshness + Refresh
  GET form), `_StatusChip` (single tone map), `site.css` design block
  (`page-header`, `panel`, `notice`, `empty`, `table-wrap`, `no-border`,
  `row-confirm`).

## Premises verified by read-only checks

1. Current `/Operations` (dev) renders: legacy `page-heading` header,
   `status-card`, Attention required (Case/Work/Attempts/Failure/Retry this
   work — `OnPostRetryExternalAsync`), Active upload links (Case/Last
   activity/Accepted/Expires/Withdraw link — `OnPostRevokeLinkAsync` with
   lease + reason preserve), an "AI operations" placeholder section with an
   explanatory sentence, and a `LimitReached` partial-data line.
2. `GetServiceHealth` (PLAT-048) is merged in
   `src/Pegasus.Core/Operations/ServiceHealth.cs` with
   `ServiceHealthRow(Area, Service, State, LatestEvidenceAtUtc, Dependency,
   RetryTarget)`; `RetryTarget` carries exactly `RetryExternalWorkCommand`'s
   identity (work item id + expected attempts).
3. `GetServiceHealth` is registered **only** inside
   `AddPegasusAutomationMcp` (`src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs`),
   which is composed only when `Features:AutomationMcp` is enabled; its
   `IAutomationIngressStatusQueries` dependency exists nowhere else. In the
   default (feature-off) deployment the snapshot capability is not composed.
4. No cross-case EVA-handoff listing query exists on dev:
   `IEvaSubmissionQueries` exposes per-case `GetLatestAsync`, failures-only
   `GetRecentFailuresAsync`, and `GetActivityAsync` (counts + latest
   timestamp). `EvaSubmissions` rows carry no Route/Engineer; ZIP exports are
   per-case history events. The contract's EVA handoffs table (Case, Route,
   Engineer, State, Result) has no query to drive it.
5. `RequestOperationProjection` carries no upload-link Recipient and no
   external-work Item column value; it does carry `State` (unused by the
   current markup; `IndexModel.StateLabel` is a dead local map).
6. `tests/Pegasus.IntegrationTests/OperationsWebTests.cs` pins "Operations",
   "Attention required", "Active upload links", the AI placeholder sentence,
   the RevokeLink POST (antiforgery + operationKey + PRG), and obsolete-route
   404s. `Browser/OperatorJourneyTests.cs` pins (case-normalised)
   "ATTENTION REQUIRED", `accepted.Reference`, "temporarily unavailable",
   exact-name "Retry this work" button, and "scheduled for retry" status; the
   assertion uppercases the whole main text, so panel `h2` casing satisfies it.
7. Wave-2 precedent: CASE-025 added label entries to the shared
   `Presentation/OperatorLabels.cs`; no wave-2 lane owns `OperatorLabels.cs`
   or `Shared/_StatusChip.cshtml`.
8. Banned operator words (`docs/design/README.md`) hit two Core service names
   if rendered raw: "Intake dispatch" (`intake`) and "Automation ingress"
   (`ingress`). Web-side label maps must rename them.
9. `Operations.LimitReached` (mirrored by `ServiceHealthSnapshot.
   ExternalWorkLimitReached`) is the honest partial-data signal.

## Assumptions (not separately verified)

- The orchestrator's wave loop runs browser/snapshot suites; per the brief this
  ticket builds only (`dotnet restore --locked-mode`, `build -c Release`).
- MCP-enabled deployments render the Service health section; feature-off
  deployments render it absent — per the design authority's absent-vs-disabled
  rule, not per new composition of my own.

## Out-of-scope findings (report, do not fix)

- EVA handoffs section: needs a new Core listing query (Case, Route,
  Engineer, State, Result are not derivable from `IEvaSubmissionQueries`);
  belongs with a wave-3-style backend ticket.
- Attention required "Item" and upload links "Recipient" columns: need
  `RequestOperationsProjection`/store changes in Core.
- Prototype Service-health "View" button has no Pegasus handler (prototype
  only raises a toast); rendering it would be an inert control — a defect per
  the design authority. Omitted; Retry renders only where `RetryTarget`
  exists.
- AI Job List + "Send Unidentified to AI": PLAT-049 (wave 4).
