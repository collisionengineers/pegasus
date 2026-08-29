# Research — AUTO-006 Automation & AI administration area

Wave 2 lane I5 of [[EPIC-011]]. Port `Pages/Administration/Automation/**` onto
the design system delivered by [[PLAT-029]].

## The binding contract

`context.md` §1.12 and `docs/design/README.md` § Administration both say:

> **Automation & AI:** Automation panel (status, Registered clients, Active
> jobs, Failed jobs, Stop / Start automation danger → reason) and AI settings
> (Proposal, Timeout, enabled checkbox, Save).

The effective prototype layer is `automationSettings()` at line 1547 of
`Pegasus_UI_Assessment_Refined.html` — verified by reading the monkey-patch
chain: line 1551 routes `key==='automation'` to it, and the two later patches
(1572 accounts-only, 1873 reports/action-logs) do not override `automation`.
The earlier `adminContent` layers at 1136/1417 are dead. The prototype's
values ("QDOS provider intake · Assessment worker", "2", "1 retryable") are
fixture data and are not copied.

The kill-switch dialog is line 1458: title "Stop automation" / "Start
automation", one consequence sentence — *In-flight work remains visible and no
result is discarded.* — danger when stopping.

## What exists on `dev` (read, not assumed)

| Thing | Where | State |
| --- | --- | --- |
| Page | `Pages/Administration/Automation/Index.cshtml` (205 lines) | pre-shell frame: `back-link`, `section-label`, `detail-list`, `primary-action`/`secondary-action`, four inline `<form>`s each with its own Reason input |
| Page model | `Index.cshtml.cs` (260 lines) | handlers `SetEnabled`, `SetSendToAiEnabled`, `UpdateConnector`, `RotateChannelToken`, `ClearChannelToken` |
| Client registry | `Web/Mcp/AutomationClientRegistry.cs` | `AutomationClientStatus(ClientId, IsRegistered, IsEnabled, DisplayName, GrantedScopes)`; `SetEnabledAsync` is the kill switch, writes attributed history |
| Job counts | `Core/AiWork/AiJobs.cs` → `IAiJobQueries.GetCountsAsync` → `AiJobCounts(Active, Failed)` | shipped by AUTO-011 (merged); `EfAiJobStore.GetCountsAsync` computes Active from the effective (lease/expiry aware) state and Failed from the persisted state |
| DI | `Infrastructure/DependencyInjection.cs:337` | `IAiJobQueries` is registered unconditionally — the counters need no new query and no new composition |
| Send to AI switch | `Core/AiWork/AiWorkContracts.cs` → `ISendToAiControl` | absent row means enabled |
| Connector settings | `IAiChannelConnectorStore` → `AiChannelConnectorSettings(ChannelBaseUrl, TimeoutSeconds, TokenHeld, TokenRotatedAtUtc, Version)` | bounds owned by `AiChannelConnectorRules` |
| Rail | `Pages/Administration/Shared/_AdminNav.cshtml` | already renders the Automation row behind `ViewData["AdminAutomationComposed"]`; read-only for this lane (PLAT-025/026/027/028 share it) |
| Reason dialog | `Pages/Shared/_ReasonDialog.cshtml` | `DialogId`, `DialogTitle`, `DialogConsequence`, `DialogActionUrl`, `DialogHiddenFields`; posts `Reason` plus the hidden fields with an antiforgery token |
| Closest ported page | `Pages/Operations/Index.cshtml` (PLAT-023, merged) | `page-header`, `panel` / `panel-head`, `notice`, `btn btn--*`, no back-link |

## Verified premises (read-only checks, not reasoning)

1. **`IAiJobQueries` is composed in every deployment.** `DependencyInjection.cs`
   registers it beside `IAiJobStore` with no feature gate, so Active/Failed are
   real figures wherever the page renders.
2. **The Automation client registry is gated.** `AutomationMcpOptions.TryCreate`
   returns `null` unless `Features:AutomationMcp` is on, and
   `AutomationMcpExtensions` is the only registration of
   `AutomationClientRegistry`. In a deployment without the gate the registry is
   absent — so the Automation panel is **absent**, per § Absent versus disabled,
   and `_AdminNav` already omits the row.
3. **Exactly one automation client exists.** The registry seeds and reconciles
   the single configured `ClientId`; "Registered clients" is therefore that
   client's display name (`AutomationMcp.ClientDisplayName`), not a list.
4. **`Features:AutomationMcp` is composable in tests** —
   `AutomationMcpTestSupport.WithAutomationMcp` already does it, so the panel
   can be proven over HTTP rather than asserted from a stub.
5. **No setting backs the prototype's "Proposal" select.** `SendToAiControl`
   carries `Enabled`, `ChannelBaseUrl`, `TimeoutSeconds`, `ChannelTokenProtected`,
   `TokenRotatedAtUtc` and nothing else; no Core port exposes a proposal kind.
6. **The switch and the connector share one singleton row and neither dedupes
   on the operation key**, so one Save may drive both writes; each still writes
   its own attributed history entry (`send_to_ai_enabled`/`_disabled`,
   `send_to_ai_connector_updated`, `send_to_ai_channel_token_rotated`).

## Copy that must go (design authority § Voice, § No explanatory copy)

- the "The Automation actor is not a staff account…" lede;
- "Automation is not part of this deployment: the configuration gate is off, no
  endpoint or token route exists…" — and its Send-to-AI twin: an uncomposed
  capability is absent, not narrated;
- the five trailing "Disabling refuses new tokens immediately…", "Saved values
  apply from the next hand-off…", "The token applies from the next hand-off and
  cannot be viewed again…", "Removing it returns the connector…" paragraphs;
- the "Every Automation action and every denied automation request is recorded
  in permanent history." Activity lede.

Only one consequence sentence survives, on the destructive kill switch.

## Inherited PLAT-015 scope — reported, not fixed here

The ticket body inherits two Activity-page defects: the raw `AggregateId` in
the Target column (`Activity.cshtml:67`) and the "…each carries an activity
reference you can filter by" narration (`Activity.cshtml:18`). Both are
confirmed present. `context.md` §1.14 supersedes the whole page ("Automation
Activity page → Action Logs"); PLAT-051 builds Action Logs with a Reference
column and UIIMP-009 deletes `Activity.*`. Fixing them in a file scheduled for
deletion is throwaway work, so they are carried to PLAT-051's Reference column
and reported rather than patched.

## Not in this lane

`_AdminNav.cshtml`, `site.css`, `site.js`, `Pages/Shared/**`, and every other
`Pages/Administration/**` folder (PLAT-025/026/027/028 are in flight).
