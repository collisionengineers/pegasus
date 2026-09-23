# Unidentified record

- **Parent:** [Cases list](../../cases/cases-index/README.md) (Unidentified tab)
- **Mockup route:** [pegasus_unidentified_search_v29.html](../../../current/pegasus_unidentified_search_v29.html) (open it from `current/`; presets are in the state picker)
- **Live source:**
  - `src/Pegasus.Web/Pages/Unidentified/Details.cshtml`, `Details.cshtml.cs` (`OnPostOpenTriageAsync`)
  - `src/Pegasus.Core/Intake/Unidentified/UnidentifiedItemContext.cs` (`CanOpenTriage`)
  - `src/Pegasus.Core/Triage/TriageLifecycle.cs` (`CreateTriageFromIntake`)
  - `src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs`

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

- Baseline: [1580](../../../current/v29-shots/s09-unidentified-record-1580.png) · [1440](../../../current/v29-shots/s09-unidentified-record-1440.png) · [760](../../../current/v29-shots/s09-unidentified-record-760.png)
- Open the Triage with Principal (P12): [1580](../../../current/v29-shots/p18-open-triage-1580.png) · [1440](../../../current/v29-shots/p18-open-triage-1440.png) · [760](../../../current/v29-shots/p18-open-triage-760.png)
- Live dialog, registration only (P12 variant): [1580](../../../current/v29-shots/p19-open-triage-no-principal-1580.png) · [1440](../../../current/v29-shots/p19-open-triage-no-principal-1440.png) · [760](../../../current/v29-shots/p19-open-triage-no-principal-760.png)

## Notes

- Scope for v29: the record's **Open the Triage** action and what it needs.
  v28's [Unidentified how-it-works](../../../../v28_planning/pages/cases/unidentified/how-it-works.md)
  covers the rest of the record as of 18 September 2026.
