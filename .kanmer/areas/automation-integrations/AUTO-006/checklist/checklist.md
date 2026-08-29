# Checklist — AUTO-006

- [x] Merge `origin/dev` into `task/auto-006-automation-admin` before any edit
      (fast-forward to `b92cb9a7`, no conflict)
- [x] Page model: `JobCounts` from `IAiJobQueries.GetCountsAsync`
- [x] Page model: single `SaveAiSettings` handler replaces `SetSendToAiEnabled`,
      `UpdateConnector`, `RotateChannelToken`
- [x] Page: `admin-layout` + `_AdminNav` (read, never modified), no `back-link`
- [x] Page: Automation panel — status chip, Registered clients, Active jobs,
      Failed jobs, danger Stop/Start → `_ReasonDialog` with one consequence
      sentence, and only on the destructive half
- [x] Page: AI settings panel — channel token state, Channel address, Timeout,
      new token, enabled checkbox, Reason, Save; Remove-token danger behind a
      reason dialog
- [x] Every explanatory paragraph listed in the research document deleted
- [x] No inert control: "Proposal" not drawn (no backing setting, no ticketed
      seam), Activity link dropped (§1.14)
- [x] Labels appended inside a new `OperatorLabels.AutomationAdmin` nested class
      only; nothing existing reordered
- [x] `dotnet build ./Pegasus.slnx --configuration Release` — succeeded,
      0 warnings, 0 errors
- [x] Focused web-test filter run — 14 passed, 0 failed, 0 skipped
- [x] Simplification pass recorded in the plan under a dated heading
- [x] Post-implementation report written; PR #618 opened against `dev`, not
      merged
