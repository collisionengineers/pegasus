# Plan — AUTO-006

One page, one page model, one label block, one new test file. Each step names
what it reuses.

## 1. Page model — real figures and one Save

*Reuses* `AdministrationPageModel` / `StaffPageModel` (`TryGetActor`,
`NewOperationKey`, `IsOperationKeyValid`), `AutomationClientRegistry`,
`ISendToAiControl`, `IAiChannelConnectorStore`, `AiChannelConnectorRules`.

- Add `AutomationComposed` (registry resolved) so the page can set
  `ViewData["AdminAutomationComposed"]` for the shared rail — the same value
  `Administration/Index.cshtml` already passes it.
- Add `JobCounts` from `IAiJobQueries.GetCountsAsync` (AUTO-011's ledger). No
  new query, no new port.
- Replace `SetSendToAiEnabled`, `UpdateConnector` and `RotateChannelToken` with
  a single `SaveAiSettings` handler: it updates the connector address and
  timeout, rotates the channel token when one was entered, and flips the
  Send-to-AI switch **only when the checkbox differs from the stored state** so
  a no-op save writes no history. Keep `SetEnabled` (the automation kill
  switch) and `ClearChannelToken` (remove the entered token).
- **Why one handler and not the three that exist:** §1.12 and the design
  authority both specify one *Save* for the AI settings panel. Three submit
  buttons and three Reason inputs in one panel contradict that and the page
  economy rule. The three Core operations are unchanged and each still writes
  its own attributed history entry.

## 2. Page — the design system

*Reuses* `_PageHeader`, `_AdminNav`, `_StatusChip`, `_ReasonDialog`, and the
`panel` / `panel-head` / `panel-body` / `fact-grid` / `field` / `grid grid-2` /
`cluster` / `button-row` / `btn btn--danger` vocabulary already in `site.css`.

- Drop the `back-link` (the rail is the way back — PLAT-023's ruling) and every
  paragraph listed in the research document.
- `admin-layout` = `_AdminNav` + a `stack` of the two panels.
- **Automation panel**, rendered only when the registry is composed: head is
  the heading plus an Enabled / Stopped chip; body is the three-fact grid
  (Registered clients = the one client's display name, Active jobs, Failed
  jobs) and one danger button opening the reason dialog. The dialog carries the
  single approved consequence sentence.
- **AI settings panel**, rendered only when Send to AI is composed: head is the
  heading plus its own Enabled / Stopped chip; body is one form —
  `grid grid-2` (Channel address, Timeout in seconds), New channel token,
  the enabled checkbox, Reason, "Save AI settings" — plus, only when a token is
  held, a danger "Remove the channel token" opening a second reason dialog.
- **"Proposal" is not drawn.** No stored setting backs it (research premise 5),
  and D7 permits a disabled control only for a named ticketed integration seam,
  which this is not. Drawing an inert select is forbidden; the item is handed to
  AUTO-010 with the note that it needs a Core setting first.
- **Chip tones follow the design authority, not the prototype's colours.** The
  prototype paints Stopped red and AI-enabled green. `docs/design/README.md`
  § Colour reserves green for confirmed completion and red for blocked /
  failed / closed-in-error. An administrator-chosen stop is a settled closed
  state, so both panels use the `_StatusChip` map unchanged: Enabled → navy,
  Stopped → neutral. No tone override is passed.

## 3. Labels

Append a new nested `static class Automation` **inside** `OperatorLabels`,
after the existing `Admin` block, holding the panel headings, the four fact
labels, the Enabled / Stopped words, the two action labels and the one
consequence sentence. Nothing existing is reordered or renamed.

## 4. Test

New `tests/Pegasus.IntegrationTests/AutomationAdministrationWebTests.cs`,
shaped on `OperationsWebTests` and reusing
`AutomationMcpTestSupport.WithAutomationMcp` and `IntakeWebApplicationFactory`:

1. With the automation gate composed and jobs seeded through `IAiJobStore`, the
   page shows the registered client, the real Active and Failed counts, the
   danger control and the consequence sentence — and none of the deleted
   explanatory paragraphs.
2. Posting the kill switch flips `AutomationClientRegistry` and redirects.
3. Without the gate the Automation panel is absent (no heading, no control) and
   the rail omits the row, while the AI settings panel still renders.
4. `SaveAiSettings` with the checkbox cleared disables the Send-to-AI switch and
   stores the timeout.

`SendToAiConnectorAdministrationTests` is retargeted at the one handler; its
assertions (override reaches the channel, token never rendered, both history
event kinds attributed, token protected at rest) are unchanged.

## 5. Verify

`dotnet build ./Pegasus.slnx --configuration Release`, then the focused filter
`FullyQualifiedName~AutomationAdministrationWebTests|FullyQualifiedName~SendToAiIntegrationTests`.
No full suite, no Browser category, no snapshot capture.

## Simplification pass

Recorded under its own dated heading below once the diff exists.

## Simplification pass — 2026-08-29

Run over this branch's own diff (page, page model, labels, tests) with the four
lenses. Every finding is behaviour-preserving; nothing was silenced.

| # | Lens | Finding | Disposition |
| --- | --- | --- | --- |
| S1 | Reuse | The page hand-rolled the unchecked-checkbox fallback (`<input type="hidden" name="SendToAiEnabled" value="false">`). ASP.NET's own `asp-for` tag helper emits both the box and the fallback. | **Fixed** — `<input asp-for="SendToAiEnabled" type="checkbox" />`; the hand-rolled hidden input is gone. Using the host's own mechanism rather than a parallel one. |
| S2 | Simplification | `SendToAiComposed` (`ISendCaseToAi` resolved) and `ConnectorSettings is not null` (`IAiChannelConnectorStore` resolved) are the same fact — `AddPegasusSendToAi` registers both together. Two properties, one concept. | **Fixed** — `SendToAiComposed` deleted; the panel and its dialog gate on `ConnectorSettings`. |
| S3 | Simplification | `AutomationComposed` and `Status is not null` are likewise one fact: `GetStatusAsync` is called exactly when the registry resolves. | **Fixed** — `AutomationComposed` deleted; the view sets `ViewData["AdminAutomationComposed"] = Model.Status is not null`. |
| S4 | Altitude | The kill-switch dialog carried the consequence sentence in both directions, so *starting* automation showed a warning notice. The rule allows one consequence sentence on a **destructive** action; starting destroys nothing. The prototype passes it unconditionally — that is a prototype defect, not the contract. | **Fixed** — the consequence is passed only when stopping. |
| S5 | Simplification | `Pegasus.Core.AiWork.AiChannelConnectorRules` was fully qualified three times in markup. | **Fixed** — one `@using Pegasus.Core.AiWork`. |
| S6 | Efficiency | `IAiJobQueries.GetCountsAsync` runs on every load. | **Accepted** — it runs only where the Automation panel renders (registry composed), it is the one query the panel's two figures need, and the counters must be live. No caching added. |
| S7 | Reuse | The new test file repeats the small `Form` / `AntiforgeryValue` / `InputValue` regex helpers that `OperationsWebTests` and `SendToAiIntegrationTests` also carry. | **Accepted** — this is the settled convention across the web-test files (each is self-contained); hoisting them into shared support would touch files four other lanes own while wave 2 is in flight. |

### Defects found outside this lane — reported, not fixed

| Where | Defect | Disposition |
| --- | --- | --- |
| `Pages/Administration/Automation/Activity.cshtml:67` | The Target column prints the raw `AggregateId`. Inherited from [[PLAT-015]] through this ticket's body. | **Deferred to [[PLAT-051]]** — §1.14 supersedes the whole page (Automation Activity → Action Logs) and [[UIIMP-009]] deletes it; PLAT-051's table has the Reference column that must carry a business reference. Fixing a file scheduled for deletion is throwaway work. |
| `Pages/Administration/Automation/Activity.cshtml:18` | "…each carries an activity reference you can filter by" — explanatory copy the design authority bans. | **Deferred to [[PLAT-051]]**, same reason. |
| `Pages/Administration/Index.cshtml` | Still exists and still links `/Administration/Automation/Index` and `/Administration/Organizations/Index`. `waves.md` wave 1 allocated its deletion to [[PLAT-029]]; it was not deleted. | **Reported** — PLAT-029's file, and [[UIIMP-009]] owns the removals wave. Not touched. |
