## Post-implementation report

**PR**: https://github.com/collisionengineers/pegasus/pull/452 (`task/plat-011-actor-display-names` → `dev`)

### What changed
See `files` doc for the full list. Summary: added `Pegasus.Core.Actors.ActorDisplayNames` as the single actor-name resolver, wired it into the three Core query owners that produce actor-carrying read models (`GetCase`, `GetTriage`, `GetRetainedMail`), resolved the Automation client's name at the Web layer (`OperatorLabels.AutomationActorLabel` + `AutomationMcp.ClientDisplayName`), and updated five `.cshtml` surfaces to render the resolved name instead of the raw subject id: `Activity.cshtml`, `_CaseSummary.cshtml`, `_CaseHistory.cshtml`, `Triage/Details.cshtml`, `Mail/Message.cshtml`.

### Verification against the ticket's own checklist
- [x] Both named surfaces show names, never GUIDs; unknown actors show an honest label ("Unknown user" / "Unknown automation client"), not an invented one.
- [x] Query changes live in the existing query owners (`GetCase`, `GetTriage`, `GetRetainedMail`, plus the Web-layer `ActivityModel`/`OperatorLabels` for the OpenIddict-backed automation name, which cannot live in Core without crossing the layer boundary).
- [x] Web tests updated — see `files` doc's Tests section.
- [x] Browser suite green — `Browser/AccessibilityTests` 24/24, including `/Administration/Automation/Activity`.

### Test evidence (exact counts, this session)
- `dotnet build ./Pegasus.slnx -c Release --no-restore` — Build succeeded, 0 Warning(s), 0 Error(s)
- `Pegasus.Core.Tests` (full suite) — 701/701 passing
- `Pegasus.ArchitectureTests` (full suite) — 97/97 passing
- `Pegasus.IntegrationTests`, focused filters:
  - `CaseDetailsWebTests` (all partial-class files) — 23/23
  - `CaseReportApprovalWebTests` (`ReportApprovalPostUsesServerActor...`) — 1/1 (extended with no-GUID assertion)
  - `MailWorkspaceWebTests` — 15/15 (extended with "Decided by" assertion against the real, unsubstituted `GetRetainedMail`/EF pipeline)
  - `RetainedMailPersistenceTests` — 16/16 (confirms the `MailClassificationActor.Format` write-path refactor)
  - `AutomationMcpIngressTests` + `AutomationConnectorAuthorizationTests` — 10/10 (confirms `AutomationClientRegistry`'s dedupe onto `AutomationMcp.ClientDisplayName` is behavior-identical)
  - `AutomationActorLabelTests` (new) — 4/4
  - `Browser/AccessibilityTests` — 24/24

Did not run the full ~28-minute IntegrationTests suite; ran every filter that touches a file this ticket changed or its DI wiring, per the runbook's focused-filter guidance.

### Simplification pass
Recorded in the `plan` doc under "Simplification pass — 2026-08-20". One finding applied (double-parse in `GetRetainedMail`'s staff-id extraction, replaced with `.OfType<Guid>()`); no unapplied findings.

### Scope note
The sweep (grep `SubjectId` and `.Actor` in `.cshtml`) found three additional raw-subject renders beyond the two named in the ticket body: `_CaseHistory.cshtml`, `Triage/Details.cshtml`, `Mail/Message.cshtml`. All three were fixed the same way, in their existing query owners, per the launching instructions ("fix any found the same way — list them in the files doc"). No operator questions were parked; nothing was left out.
