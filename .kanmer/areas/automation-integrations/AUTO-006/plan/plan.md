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
