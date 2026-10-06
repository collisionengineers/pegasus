# Case record: Report (delivery form)

- **Mockup route:** [`pegasus_case_report_delivery_v34.html`](../../../current/pegasus_case_report_delivery_v34.html) with `?state=plain`, `?state=rules`, `?state=stop` or `?state=missing`. Each state is a capture of the as-built section (`current/captured/delivery-*.html`).
- **Live source:** `src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml` (the `data-send-report` form), `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` (`OnPostSendReportAsync`, `PostedReportDecisions`, `DeliveryRecipientSuggestions`, `DeliveryDispatchPlan`), `src/Pegasus.Web/Pages/Cases/Details.Report.cs` (`ReportDeliveryFileName`, `ReportDeliveryMessage`), `src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs` (`ReportDelivery`), `src/Pegasus.Core/Reports/ReportDispatchPolicy.cs`, `src/Pegasus.Core/Reports/CaseReportDelivery.cs`, `src/Pegasus.Infrastructure/Persistence/EfReportRecipientSuggestionQueries.cs`.
- **Dialogs:** [`dialogs/`](dialogs/README.md) · **States:** [`states/`](states/README.md) · **Panels:** [`panels/`](panels/README.md)

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

Five per state, in the order whole page at 1580, the section alone at 1580, then 1580×1000, 1440×900 and 760×1000.

- `plain`: [01](../../../current/v34-shots/01-case_report_delivery-plain-full.png) · [02](../../../current/v34-shots/02-case_report_delivery-plain-surface.png) · [03](../../../current/v34-shots/03-case_report_delivery-plain-1580.png) · [04](../../../current/v34-shots/04-case_report_delivery-plain-1440.png) · [05](../../../current/v34-shots/05-case_report_delivery-plain-760.png)
- `rules`: [06](../../../current/v34-shots/06-case_report_delivery-rules-full.png) · [07](../../../current/v34-shots/07-case_report_delivery-rules-surface.png) · [08](../../../current/v34-shots/08-case_report_delivery-rules-1580.png) · [09](../../../current/v34-shots/09-case_report_delivery-rules-1440.png) · [10](../../../current/v34-shots/10-case_report_delivery-rules-760.png)
- `stop`: [11](../../../current/v34-shots/11-case_report_delivery-stop-full.png) · [12](../../../current/v34-shots/12-case_report_delivery-stop-surface.png) · [13](../../../current/v34-shots/13-case_report_delivery-stop-1580.png) · [14](../../../current/v34-shots/14-case_report_delivery-stop-1440.png) · [15](../../../current/v34-shots/15-case_report_delivery-stop-760.png)
- `missing`: [16](../../../current/v34-shots/16-case_report_delivery-missing-full.png) · [17](../../../current/v34-shots/17-case_report_delivery-missing-surface.png) · [18](../../../current/v34-shots/18-case_report_delivery-missing-1580.png) · [19](../../../current/v34-shots/19-case_report_delivery-missing-1440.png) · [20](../../../current/v34-shots/20-case_report_delivery-missing-760.png)

The file list and viewports are in `current/v34-shots/verification.json`.

## Notes

- This folder covers the delivery form only. The rest of the Report section (head, preview card, report fields, wording, Fee tab) is unchanged by this round.
- The form is drawn only in the edit session, so every state is captured while editing.
