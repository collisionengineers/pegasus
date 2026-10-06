# Contacts: a Principal's Report sending panel

- **Mockup route:** [`pegasus_contact_report_sending_v34.html`](../../../current/pegasus_contact_report_sending_v34.html) with `?state=kerr-edit`, `dfd`, `ax`, `mp`, `pch`, `rjs` or `qdos`. Each is a capture of the as-built Contact page (`current/captured/contact-*.html`) holding the rules the migration seeds for that Principal.
- **Live source:** `src/Pegasus.Web/Pages/Administration/Contacts/Edit.cshtml` (the `data-report-sending-editor` form), `src/Pegasus.Web/Pages/Administration/Contacts/Edit.cshtml.cs` (`OnPostUpdateReportSendingAsync`, `PostedReportSending`, `LoadReportSendingChoicesAsync`, `ReportSendingRulesErrorMessage`), `src/Pegasus.Web/wwwroot/js/contacts-edit.js`, `src/Pegasus.Web/Presentation/OperatorLabels.cs` (`PrincipalAdministration`), `src/Pegasus.Core/Reports/PrincipalReportSendingRules.cs`, `src/Pegasus.Core/Cases/OrganizationAdministration.cs`, `src/Pegasus.Infrastructure/Persistence/Migrations/20261007090000_PrincipalReportSendingRules.cs`.
- **Dialogs:** [`dialogs/`](dialogs/README.md) · **States:** [`states/`](states/README.md) · **Panels:** [`panels/`](panels/README.md)

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

Five per state: whole page at 1580, the panel surface at 1580, then 1580×1000, 1440×900 and 760×1000.

- `kerr-edit`: [46](../../../current/v34-shots/46-contact_report_sending-kerr-edit-full.png) · [47](../../../current/v34-shots/47-contact_report_sending-kerr-edit-surface.png) · [48](../../../current/v34-shots/48-contact_report_sending-kerr-edit-1580.png) · [49](../../../current/v34-shots/49-contact_report_sending-kerr-edit-1440.png) · [50](../../../current/v34-shots/50-contact_report_sending-kerr-edit-760.png)
- `dfd`: [51](../../../current/v34-shots/51-contact_report_sending-dfd-full.png) · [52](../../../current/v34-shots/52-contact_report_sending-dfd-surface.png) · [53](../../../current/v34-shots/53-contact_report_sending-dfd-1580.png) · [54](../../../current/v34-shots/54-contact_report_sending-dfd-1440.png) · [55](../../../current/v34-shots/55-contact_report_sending-dfd-760.png)
- `ax`: [56](../../../current/v34-shots/56-contact_report_sending-ax-full.png) · [57](../../../current/v34-shots/57-contact_report_sending-ax-surface.png) · [58](../../../current/v34-shots/58-contact_report_sending-ax-1580.png) · [59](../../../current/v34-shots/59-contact_report_sending-ax-1440.png) · [60](../../../current/v34-shots/60-contact_report_sending-ax-760.png)
- `mp`: [61](../../../current/v34-shots/61-contact_report_sending-mp-full.png) · [62](../../../current/v34-shots/62-contact_report_sending-mp-surface.png) · [63](../../../current/v34-shots/63-contact_report_sending-mp-1580.png) · [64](../../../current/v34-shots/64-contact_report_sending-mp-1440.png) · [65](../../../current/v34-shots/65-contact_report_sending-mp-760.png)
- `pch`: [66](../../../current/v34-shots/66-contact_report_sending-pch-full.png) · [67](../../../current/v34-shots/67-contact_report_sending-pch-surface.png) · [68](../../../current/v34-shots/68-contact_report_sending-pch-1580.png) · [69](../../../current/v34-shots/69-contact_report_sending-pch-1440.png) · [70](../../../current/v34-shots/70-contact_report_sending-pch-760.png)
- `rjs`: [71](../../../current/v34-shots/71-contact_report_sending-rjs-full.png) · [72](../../../current/v34-shots/72-contact_report_sending-rjs-surface.png) · [73](../../../current/v34-shots/73-contact_report_sending-rjs-1580.png) · [74](../../../current/v34-shots/74-contact_report_sending-rjs-1440.png) · [75](../../../current/v34-shots/75-contact_report_sending-rjs-760.png)
- `qdos`: [76](../../../current/v34-shots/76-contact_report_sending-qdos-full.png) · [77](../../../current/v34-shots/77-contact_report_sending-qdos-surface.png) · [78](../../../current/v34-shots/78-contact_report_sending-qdos-1580.png) · [79](../../../current/v34-shots/79-contact_report_sending-qdos-1440.png) · [80](../../../current/v34-shots/80-contact_report_sending-qdos-760.png)

## Notes

- The six "read" states are the same editable form as `kerr-edit`, opened on another Principal. The Contact page has no read mode.
- What each Principal's rules do on a delivery is the [Report delivery](../../case-record/report/how-it-works.md) page.
