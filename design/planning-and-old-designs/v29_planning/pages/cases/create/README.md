# Create case

- **Parent:** [Cases list](../cases-index/README.md)
- **Mockup route:** [pegasus_work_centre_v29.html](../../../current/pegasus_work_centre_v29.html) (open it from `current/`; presets are in the state picker)
- **Live source:**
  - `src/Pegasus.Web/Pages/Cases/Create.cshtml`, `Create.cshtml.cs`
  - `src/Pegasus.Core/Cases/ManualCaseCreation.cs`
  - `src/Pegasus.Core/Intake/InstructionDraftCompleteness.cs` (`MissingIdentityCriticalFieldNames`)

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

- Baseline: [1580](../../../current/v29-shots/s08-create-case-1580.png) · [1440](../../../current/v29-shots/s08-create-case-1440.png) · [760](../../../current/v29-shots/s08-create-case-760.png)
- Triage chosen (P11): [1580](../../../current/v29-shots/p17-create-triage-1580.png) · [1440](../../../current/v29-shots/p17-create-triage-1440.png) · [760](../../../current/v29-shots/p17-create-triage-760.png)

## Notes

- Scope for v29: which Case types the page offers, its required fields and
  its validation, in both the manual and the from-receipt forms.
