# Create audit dialog

- **Parent:** [Case record](../../README.md)
- **Mockup route:** [pegasus_case_record_v29.html](../../../../../current/pegasus_case_record_v29.html) (open it from `current/`; presets are in the state picker)
- **Live source:**
  - `src/Pegasus.Web/Pages/Cases/Details.cshtml` (the Actions menu item)
  - `src/Pegasus.Web/Pages/Cases/Details.Frame.cs` (`CanCreateAudit`, `ProposedAuditReference`, `OnPostCreateAuditAsync`)
  - `src/Pegasus.Web/Pages/Cases/Shared/_CaseDialogs.cshtml` (`case-create-audit-dialog`)
  - `src/Pegasus.Core/Lifecycle/CreateAuditCase.cs`
  - `src/Pegasus.Infrastructure/Persistence/EfCreateAuditCaseStore.cs`

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

- Create audit dialog (P5): [1580](../../../../../current/v29-shots/p11-create-audit-dialog-1580.png) · [1440](../../../../../current/v29-shots/p11-create-audit-dialog-1440.png) · [760](../../../../../current/v29-shots/p11-create-audit-dialog-760.png)

## Notes

- v29 proposes that Inspection and Audit live on one Case, which would
  replace this action. This folder records the action as it is, for
  comparison.
