## Files touched

### Core — shared resolution helper (new, single owner)
- `src/Pegasus.Core/Actors/ActorDisplayNames.cs` (new) — the one place an `ActorKind` + subject id becomes an operator-facing name. `Resolve` (per-kind fallback labels, never a GUID) and `ResolveStaffNamesAsync` (batches distinct staff ids through the existing `IStaffAccountQueries.GetAsync`). Reused by all three query owners below.

### Cases — the two named leak sites + the case-history sweep hit
- `src/Pegasus.Core/Cases/CaseQueries.cs` — `CaseHistoryEntry` gains `ActorDisplayName`; `CaseDetails` gains `ReportApprovedByDisplayName`. `GetCase` (existing composer, already injects several query ports) now also injects `IStaffAccountQueries` and resolves both projections after fetching `CaseDetails`.
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml` — Actor row renders `details.ReportApprovedByDisplayName` instead of `approval.ApprovedBy.SubjectId` (the ticket's named leak).
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseHistory.cshtml` — Actor column renders `entry.ActorDisplayName`; dropped the `title="@entry.Actor"` tooltip that leaked the raw subject id for Automation rows (sweep hit — same `CaseDetails` read model).

### Automation activity — the other named leak site
- `src/Pegasus.Web/Mcp/AutomationMcp.cs` — added `ClientDisplayName` const (the one name for the single Automation client, ADR-0011).
- `src/Pegasus.Web/Mcp/AutomationClientRegistry.cs` — its own `DisplayName` const removed; now references `AutomationMcp.ClientDisplayName` (dedupe, not a second list).
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — added `AutomationActorLabel(subjectId, configuredClientId)`, the existing "codes become operator words" home.
- `src/Pegasus.Web/Pages/Administration/Automation/Activity.cshtml.cs` — added `SubjectLabel(subjectId)`, resolving through `OperatorLabels.AutomationActorLabel` against the composed `AutomationMcpOptions.ClientId` (mirrors the existing `Registry()` optional-service pattern on the sibling `Index.cshtml.cs`).
- `src/Pegasus.Web/Pages/Administration/Automation/Activity.cshtml` — Subject column renders `Model.SubjectLabel(record.SubjectId)`.

### Sweep hits — Triage history and Mail classification history
- `src/Pegasus.Core/Triage/TriageContracts.cs` — `TriageHistoryEntry` gains `ActorDisplayName`.
- `src/Pegasus.Core/Triage/TriageQueryUseCases.cs` — `GetTriage` (existing composer) injects `IStaffAccountQueries` and resolves history actor names (always Staff on the one real write path).
- `src/Pegasus.Web/Pages/Triage/Details.cshtml` — history line renders `history.ActorDisplayName`.
- `src/Pegasus.Core/Intake/RetainedMail.cs` — `MailClassificationHistoryEntry`/`MailClassificationDossier` gain `ActorDisplayName`/`CurrentActorDisplayName`. New `MailClassificationActor` (format/parse the persisted `"{kind}:{subjectId}"` pair — the established repo-wide prefix convention, e.g. `"system-worker:"` in `MailboxIntake.cs`/`EmailEvidenceContracts.cs`, `"staff:"` in `Upload.cshtml.cs`, `"automation:"` in `IntakeMcpTools.cs`; not `ActorKind.ToString()`, which doesn't hyphenate `SystemWorker`). `CorrectRetainedMailClassification` now writes through `MailClassificationActor.Format`. `GetRetainedMail` (existing composer) injects `IStaffAccountQueries` and resolves both dossier fields.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml` — "Decided by" and correction-history lines render the resolved `*DisplayName` fields.

### Grep sweep performed
`grep -rn "SubjectId" --include=*.cshtml` (2 hits — the two named sites) and `grep -rn "\.Actor\b" --include=*.cshtml` (found `_CaseHistory.cshtml`, `Triage/Details.cshtml`, `Mail/Message.cshtml` — all fixed above) across `src/Pegasus.Web/Pages`. No other operator-facing raw-GUID actor renders found.

### Tests
- `tests/Pegasus.Core.Tests/Identity/ActorDisplayNamesTests.cs` (new) — the shared resolver's algorithm.
- `tests/Pegasus.Core.Tests/Triage/GetTriageDisplayNameTests.cs` (new) — `GetTriage` resolution wiring, with fakes.
- `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` — extended with `GetRetainedMail`'s composite-string resolution (staff + system-worker), plus the constructor fix for the new `IStaffAccountQueries` dependency.
- `tests/Pegasus.IntegrationTests/AutomationActorLabelTests.cs` (new) — `OperatorLabels.AutomationActorLabel` directly (the Activity page's decision logic).
- `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` — new `CaseHistoryShowsResolvedActorNamesAndNeverARawSubjectId` test (Staff + Automation rows, HTTP-level, asserts no raw GUID anywhere in the response).
- `tests/Pegasus.IntegrationTests/CaseReportApprovalWebTests.cs` — extended the existing report-approval flow test with a "no raw GUID, resolved name shown" assertion.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` — extended the existing message-detail test with a "Decided by" assertion against the real (unsubstituted) `GetRetainedMail`/EF pipeline.
