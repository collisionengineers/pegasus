# Checklist — AUTO-006

- [ ] Merge `origin/dev` into `task/auto-006-automation-admin` before any edit
- [ ] Page model: `AutomationComposed`, `JobCounts` from `IAiJobQueries`
- [ ] Page model: single `SaveAiSettings` handler replaces `SetSendToAiEnabled`,
      `UpdateConnector`, `RotateChannelToken`
- [ ] Page: `admin-layout` + `_AdminNav`, no `back-link`
- [ ] Page: Automation panel — status chip, Registered clients, Active jobs,
      Failed jobs, danger Stop/Start → `_ReasonDialog` with one consequence
      sentence
- [ ] Page: AI settings panel — Channel address, Timeout, new token, enabled
      checkbox, Reason, Save; Remove-token danger behind a reason dialog
- [ ] Every explanatory paragraph listed in the research document deleted
- [ ] No inert control: "Proposal" not drawn, Activity link dropped
- [ ] Labels appended inside a new `OperatorLabels.Automation` nested class only
- [ ] `dotnet build ./Pegasus.slnx --configuration Release` clean
- [ ] Focused web-test filter run; real counts reported
- [ ] Simplification pass recorded in the plan under a dated heading
- [ ] Post-implementation report written, PR opened against `dev`, not merged
