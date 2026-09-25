# Triage record

- **Parent:** [Cases list](../../cases/cases-index/README.md) (Triage tab)
- **Mockup route:** [pegasus_triage_case_v29.html](../../../current/pegasus_triage_case_v29.html) (open it from `current/`; presets are in the state picker)
- **Live source:**
  - `src/Pegasus.Web/Pages/Triage/Details.cshtml`, `Details.cshtml.cs`
  - `src/Pegasus.Web/Pages/Triage/Index.cshtml`, `Index.cshtml.cs` (the `/Triage` redirect)
  - `src/Pegasus.Core/Triage/TriageContracts.cs`, `TriageLifecycle.cs`, `TriageQueryUseCases.cs`, `EmailEvidenceContracts.cs`
  - `src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs`
  - `src/Pegasus.Web/Mcp/TriageMcpTools.cs`

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

- Baseline, /Triage/{id}: [1580](../../../current/v29-shots/s04-triage-record-1580.png) · [1440](../../../current/v29-shots/s04-triage-record-1440.png) · [760](../../../current/v29-shots/s04-triage-record-760.png)
- Triage Case: live page with the Case's Files (P7, P9): [1580](../../../current/v29-shots/p13-triage-case-1580.png) · [1440](../../../current/v29-shots/p13-triage-case-1440.png) · [760](../../../current/v29-shots/p13-triage-case-760.png)

## Notes

- v29 proposes that Triage becomes a Case type shown at `/Cases/{id}`. This
  folder records the separate `/Triage/{id}` page as it is, and every place
  that links to it.
