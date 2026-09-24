# Case record

- **Parent:** [Cases list](../cases-index/README.md)
- **Mockup route:** [pegasus_case_record_v29.html](../../../current/pegasus_case_record_v29.html) (open it from `current/`; presets are in the state picker)
- **Live source:**
  - `src/Pegasus.Web/Pages/Cases/Details.cshtml`
  - `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`, `Details.Frame.cs`, `Details.Report.cs`
  - `src/Pegasus.Web/Pages/Cases/Shared/_CaseDialogs.cshtml`, `_CaseReport.cshtml`, `_CaseOverview.cshtml`, `_CaseOriginalReport.cshtml`, `_CaseDocuments.cshtml`, `_CaseHistory.cshtml`
  - `src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs`, `OperatorLabels.cs`, `ShellPreferences.cs`
  - `src/Pegasus.Web/wwwroot/js/case-workspace.js`
  - `src/Pegasus.Core/Lifecycle/CreateAuditCase.cs`
  - `src/Pegasus.Infrastructure/Persistence/EfCreateAuditCaseStore.cs`
- **Dialogs:** [Create audit](dialogs/create-audit/README.md)

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

- Baseline, read: [1580](../../../current/v29-shots/s01-case-record-1580.png) · [1440](../../../current/v29-shots/s01-case-record-1440.png) · [760](../../../current/v29-shots/s01-case-record-760.png)
- Baseline, edit session: [1580](../../../current/v29-shots/s02-case-record-editing-1580.png) · [1440](../../../current/v29-shots/s02-case-record-editing-1440.png) · [760](../../../current/v29-shots/s02-case-record-editing-760.png)
- Audit view (P1, P4, P6): [1580](../../../current/v29-shots/p01-audit-view-1580.png) · [1440](../../../current/v29-shots/p01-audit-view-1440.png) · [760](../../../current/v29-shots/p01-audit-view-760.png)
- Audit view, Report (P4): [1580](../../../current/v29-shots/p02-audit-view-report-1580.png) · [1440](../../../current/v29-shots/p02-audit-view-report-1440.png) · [760](../../../current/v29-shots/p02-audit-view-report-760.png)
- Inspection view (P1, P3): [1580](../../../current/v29-shots/p03-inspection-view-1580.png) · [1440](../../../current/v29-shots/p03-inspection-view-1440.png) · [760](../../../current/v29-shots/p03-inspection-view-760.png)
- Audit view, Files (P6): [1580](../../../current/v29-shots/p04-audit-view-files-1580.png) · [1440](../../../current/v29-shots/p04-audit-view-files-1440.png) · [760](../../../current/v29-shots/p04-audit-view-files-760.png)
- Inspection view, Report (P4): [1580](../../../current/v29-shots/p05-inspection-view-report-1580.png) · [1440](../../../current/v29-shots/p05-inspection-view-report-1440.png) · [760](../../../current/v29-shots/p05-inspection-view-report-760.png)
- Audit reference on the ribbon (P2 variant): [1580](../../../current/v29-shots/p06-audit-view-ribbon-ref-1580.png) · [1440](../../../current/v29-shots/p06-audit-view-ribbon-ref-1440.png) · [760](../../../current/v29-shots/p06-audit-view-ribbon-ref-760.png)
- Audit view, edit session (P1): [1580](../../../current/v29-shots/p07-audit-editing-1580.png) · [1440](../../../current/v29-shots/p07-audit-editing-1440.png) · [760](../../../current/v29-shots/p07-audit-editing-760.png)
- Inspection report sent, before Create audit (P1, P4): [1580](../../../current/v29-shots/p08-sent-read-1580.png) · [1440](../../../current/v29-shots/p08-sent-read-1440.png) · [760](../../../current/v29-shots/p08-sent-read-760.png)
- One view shown as a tab (P1 variant): [1580](../../../current/v29-shots/p09-sent-read-single-tab-1580.png) · [1440](../../../current/v29-shots/p09-sent-read-single-tab-1440.png) · [760](../../../current/v29-shots/p09-sent-read-single-tab-760.png)
- Actions with Create audit (P5): [1580](../../../current/v29-shots/p10-sent-actions-1580.png) · [1440](../../../current/v29-shots/p10-sent-actions-1440.png) · [760](../../../current/v29-shots/p10-sent-actions-760.png)
- Standalone Audit (P1): [1580](../../../current/v29-shots/p12-standalone-audit-1580.png) · [1440](../../../current/v29-shots/p12-standalone-audit-1440.png) · [760](../../../current/v29-shots/p12-standalone-audit-760.png)
- AA option 2, ribbon switch, Audit view: [1580](../../../current/v29-shots/p22-alt2-ribbon-audit-1580.png) · [1440](../../../current/v29-shots/p22-alt2-ribbon-audit-1440.png) · [760](../../../current/v29-shots/p22-alt2-ribbon-audit-760.png)
- AA option 2, ribbon switch, Inspection view: [1580](../../../current/v29-shots/p23-alt2-ribbon-inspection-1580.png) · [1440](../../../current/v29-shots/p23-alt2-ribbon-inspection-1440.png) · [760](../../../current/v29-shots/p23-alt2-ribbon-inspection-760.png)
- AA option 2, ribbon switch, edit session: [1580](../../../current/v29-shots/p24-alt2-ribbon-editing-1580.png) · [1440](../../../current/v29-shots/p24-alt2-ribbon-editing-1440.png) · [760](../../../current/v29-shots/p24-alt2-ribbon-editing-760.png)
- AA option 3, section-row switch, Audit view: [1580](../../../current/v29-shots/p25-alt3-sectionrow-audit-1580.png) · [1440](../../../current/v29-shots/p25-alt3-sectionrow-audit-1440.png) · [760](../../../current/v29-shots/p25-alt3-sectionrow-audit-760.png)
- AA option 3, section-row switch, Inspection view: [1580](../../../current/v29-shots/p26-alt3-sectionrow-inspection-1580.png) · [1440](../../../current/v29-shots/p26-alt3-sectionrow-inspection-1440.png) · [760](../../../current/v29-shots/p26-alt3-sectionrow-inspection-760.png)
- AA option 4, Views card, Audit view: [1580](../../../current/v29-shots/p27-alt4-aside-audit-1580.png) · [1440](../../../current/v29-shots/p27-alt4-aside-audit-1440.png) · [760](../../../current/v29-shots/p27-alt4-aside-audit-760.png)
- AA option 4, Views card, Inspection view: [1580](../../../current/v29-shots/p28-alt4-aside-inspection-1580.png) · [1440](../../../current/v29-shots/p28-alt4-aside-inspection-1440.png) · [760](../../../current/v29-shots/p28-alt4-aside-inspection-760.png)
- AA option 5, compare in place: [1580](../../../current/v29-shots/p29-alt5-compare-1580.png) · [1440](../../../current/v29-shots/p29-alt5-compare-1440.png) · [760](../../../current/v29-shots/p29-alt5-compare-760.png)
- AA option 5, compare in place, Decisions: [1580](../../../current/v29-shots/p30-alt5-compare-decisions-1580.png) · [1440](../../../current/v29-shots/p30-alt5-compare-decisions-1440.png) · [760](../../../current/v29-shots/p30-alt5-compare-decisions-760.png)

## Notes

- Scope for v29: the ribbon (reference, Case type chip, Audit case and
  Original case links), the section row and its Scroll/Tabs switch, section
  order, the Actions menu, the Report section's generation and sent
  evidence, the post-report states and what an Audit Case adds. The Engineer
  sections' own fields are out of scope; v28's
  [case-record how-it-works](../../../../v28_planning/pages/cases/case-record/how-it-works.md)
  covers them as of 18 September 2026.
