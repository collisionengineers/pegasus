# Plan — CASE-012

Branch `task/case-012-case-workspace`, worktree
`../pegasus-worktrees/case-012-case-workspace`, from `origin/dev` 5ca2572c.
Build only (`dotnet build ./Pegasus.slnx --configuration Release`); the
orchestrator runs tests.

## Steps

1. **Model** (`Details.cshtml.cs`). `?section=` with `?tab=` aliases
   (evidence→files, history→notes); keep `AssessmentAccessState` (reuses
   `IGetAssessmentAccess`), `IsExported`; Engineer name via
   `IStaffAccountQueries.GetAsync` (the port `ActorDisplayNames` uses);
   `LoadedAtUtc` from `TimeProvider`; `Blockers` composed from Core facts
   already on `CaseDetails` (completeness values, custody preparations,
   due work) — presentation rows, no new rule; images load only for `files`.
2. **Frame** (`Details.cshtml`). Header (`page-header` markup: reference,
   "Case workspace · reg", Back to Cases, `_FreshnessBanner`), `record-ribbon`
   with `_StatusChip`, `presence-strip[data-edit-authority]` with
   `EditModeDisplay.HeldBy`, `record-bar` per §1.8 mapped to the existing
   handlers (ClaimLease, ReleaseLease + `_EditFinishConfirm`, RenewLease +
   `_EditHeartbeat`, Workflow/Hold|ReleaseHold via `_ReasonDialog`,
   Custody/CreateRequestUploadLink, Eva/Send link, Tasks/LinkReportEvidence
   via an evidence dialog rendered only when `AvailableReportSentEvidence`
   is non-empty (D10), Closure/Reopen dialogs, Open Assessment on `CanOpen`,
   Closure/Close dialog with outcome select), sticky `edit-bar` (lease text,
   Discard → finish form, Save `[data-edit-save]` → the edit form), then
   `case-workspace` = `_CaseWorkspaceNav` | `case-main` | `case-context`.
   `ViewData["WorkspaceRecord"]` set.
3. **Overview** (`_CaseSummary`). `workflow-stepper` from
   `OperatorLabels.CaseStage`, `workflow-exception` for Held/closed,
   Outstanding requirements (`blocker-list`) with the Confirm completeness
   form as the resolve action while editing, edit form (six fields + Reason,
   other editable fields hidden with current values), Case overview panel
   (Work facts / Parties / accident card; only populated facts).
4. **Other sections.** `_CaseVehicle` (moved vehicle facts + lookup forms,
   inactive buttons removed), inspection facts, `_CaseFiles` (custody panel
   + `_CaseDocuments` + galleries, moved), `_CaseHistory` (+ chase history,
   chaser draft, manual chase form). Task forms, report-approval form,
   return-to-review, engineer-finding, linked-replacement, archive and
   unlink forms leave the UI; handlers stay.
5. **EVA handoff** (`Eva/Send`). Allow Review/With Engineer/Complete; API
   only in Review; Engineer select (`IStaffAccountQueries`, Engineer role)
   posting Workflow/AssignEngineer while editing in ReportPreparation;
   Export ZIP (`Documents/Export` Bundle, button text kept "Download export"
   for the journey pin); vocabulary restyle.
6. **Create** vocabulary restyle; drop hint copy.
7. **Tests** retargeted at equal strength; new: no Open Assessment when
   access is refused, Report sent only with evidence, section aliases.
8. Catalogue branch text; merge `origin/dev`; simplification pass; report; PR.

## Reuse

`_StatusChip`, `_FreshnessBanner`, `_ReasonDialog`, `_EditFinishConfirm`,
`_EditHeartbeat`, `_Provenance`, `_ImageGallery`, `_EvidenceViewer`,
`OperatorLabels.CaseStage/OfficeTime/SourceChannel/CaseTypeName/
InspectionMode/ChaseReason`, `EditModeDisplay`, `CaseMutationPageModel`
lease state, `IStaffAccountQueries`, `AssessmentAccessState`.

## Recorded divergences

- Create upload link posts directly (the handler has no fields; a fieldless
  dialog would be a no-op frame) — CASE-029 adds the dialog.
- "Unsaved" chip omitted: no script tracks dirtiness and a static chip would
  assert a false state.
- Valuations is accepted as a section but has no nav item until CASE-027
  gives it content (D7: no inert control).
- `IListStaffAccounts` is Administrator-only; `IStaffAccountQueries` used.
- Report approval (typed identity + SHA-256) leaves the UI per PLAT-015;
  the handler remains for the automated route.
