# Raw exports and captured EVA evidence

These files contain real or production-shaped predecessor data. They were inspected for structure and purpose only. Do not copy their records into tests, logs or current requirements, and do not infer a migration requirement: v2 starts clean.

## Spreadsheets and contact exports

| File | Brief contents | Current v2 comparison and caution |
| --- | --- | --- |
| [`backup_of_ce_job_sheet_260429.xlsm`](../imp-docs/backup_of_ce_job_sheet_260429.xlsm) | Macro-enabled admin workbook with Jobs, Own figures, Principals and Garages sheets; includes old case tracking, principal/EVA/Box instructions and contact data. | **Historical workflow evidence.** Useful for operator interviews about queues and principal setup. Never treat formulas, rows or macros as business rules. |
| [`~$providers-worked-on.xlsx`](<../imp-docs/~$providers-worked-on.xlsx>) | Hidden 165-byte Microsoft Office lock/owner file. It is not a valid workbook. | **No reusable content.** Preserve it as supplied; do not use it as evidence. |
| [`providers.xlsx`](../imp-docs/providers.xlsx) | 17,738-row case/location export with Case ID, registration, claimant, claim and inspection-location columns. | **Sensitive historical dataset.** Address prediction is deferred and predecessor cases are not migrated. |
| [`providers-worked-on.xlsx`](../imp-docs/providers-worked-on.xlsx) | Derived workbook splitting image-based and addressed cases, contacts, providers, repairers, inspection locations and frequency results. | **Historical analysis only.** It may inform a future operator-reviewed address model; it is not a current corpus or schema. |
| [`contacts/providers.xlsx`](../imp-docs/contacts/providers.xlsx) | Another 17,738-row case/location export. | **Sensitive historical dataset.** Do not assume it is distinct, complete or current. |
| [`contacts/contactseva_combined.csv`](../imp-docs/contacts/contactseva_combined.csv) | 528 EVA contact rows with codes, groups, addresses and contact fields. | **Potential party-directory input only after operator review.** No import is planned. |
| [`contacts/aALL.xls`](../imp-docs/contacts/aALL.xls) | 76-row general EVA contact export. | **Historical data; no import authority.** |
| [`contacts/agent.xls`](../imp-docs/contacts/agent.xls) | 3-row agent contact export. | **Historical data; no import authority.** |
| [`contacts/broker.xls`](../imp-docs/contacts/broker.xls) | 3-row broker contact export. | **Historical data; no import authority.** |
| [`contacts/client.xls`](../imp-docs/contacts/client.xls) | 5-row client contact export. | **Historical data; no import authority.** |
| [`contacts/legal.xls`](../imp-docs/contacts/legal.xls) | 440-row legal-contact export. | **Historical data; no external-user scope follows from this.** |
| [`contacts/other.xls`](../imp-docs/contacts/other.xls) | 5-row miscellaneous-contact export. | **Historical data; no import authority.** |
| [`contacts/private.xls`](../imp-docs/contacts/private.xls) | 11-row private-contact export. | **Potentially sensitive; do not expose or reuse records.** |
| [`contacts/REPAIRER.xls`](../imp-docs/contacts/REPAIRER.xls) | 72-row repairer contact export. | **Repairer concept may be planned; records and fields still require operator review.** |

## EVA screenshots

The fifteen images show one predecessor EVA case across different tabs. They reveal terminology and field groupings, not v2 acceptance criteria. Personal, claim and vehicle values visible in the images are deliberately not repeated here.

| File | Screen shown | Current v2 relevance |
| --- | --- | --- |
| [`{0E6CBDDD-7C09-4088-A2F7-35C9041AAA42}.png`](<../imp-docs/eva_information/screenshots/{0E6CBDDD-7C09-4088-A2F7-35C9041AAA42}.png>) | Emails tab with sent/received message list and preview. | In-app email management is planned; EVA UI is not the v2 design. |
| [`{245DB80D-0EB3-42CF-9775-2CD24CEDF88A}.png`](<../imp-docs/eva_information/screenshots/{245DB80D-0EB3-42CF-9775-2CD24CEDF88A}.png>) | Valuation/settlement values, salvage and contact timeline. | Valuation and settlement workflows are deferred. |
| [`{28E72E59-7EE2-43DD-AA15-2F5E53DBCF6E}.png`](<../imp-docs/eva_information/screenshots/{28E72E59-7EE2-43DD-AA15-2F5E53DBCF6E}.png>) | Notes and case event history. | Supports discussion of permanent audit/history; exact EVA events are not v2 events. |
| [`{549C62EE-3D5E-4ADD-9F1A-714D4BBD46B9}.png`](<../imp-docs/eva_information/screenshots/{549C62EE-3D5E-4ADD-9F1A-714D4BBD46B9}.png>) | Account, Engineer payment and invoice records. | Invoice/accounting is deferred. |
| [`{93A1970E-71A7-48D5-B940-4CE4B98228B1}.png`](<../imp-docs/eva_information/screenshots/{93A1970E-71A7-48D5-B940-4CE4B98228B1}.png>) | Queries and Engineer-query tracking. | Post-report queries are in planned lifecycle scope; exact fields require operator review. |
| [`{94D6ED5C-348F-4E85-B941-ECB12AE1814C}.png`](<../imp-docs/eva_information/screenshots/{94D6ED5C-348F-4E85-B941-ECB12AE1814C}.png>) | Settlement/valuation panel. | Financial workflow is deferred. |
| [`{9666FEB7-AFB3-499F-9518-9AA5205CE954}.png`](<../imp-docs/eva_information/screenshots/{9666FEB7-AFB3-499F-9518-9AA5205CE954}.png>) | Parts and repair-estimate line items. | Repair-estimate workflow is deferred. |
| [`{9A82B1E4-2A4F-4B5E-8686-3C2F82E567F1}.png`](<../imp-docs/eva_information/screenshots/{9A82B1E4-2A4F-4B5E-8686-3C2F82E567F1}.png>) | Costs, repairer, claim type and assessed totals. | Estimating/cost workflow is deferred; roadworthiness and repairable/total-loss findings remain relevant. |
| [`{AF221409-D318-4F27-875F-12DEB9FA879E}.png`](<../imp-docs/eva_information/screenshots/{AF221409-D318-4F27-875F-12DEB9FA879E}.png>) | Letters list and email/print actions. | Report/correspondence custody is planned; old actions are not the required UI. |
| [`{B2206742-2C06-49AD-9E78-F2047E4F9220}.png`](<../imp-docs/eva_information/screenshots/{B2206742-2C06-49AD-9E78-F2047E4F9220}.png>) | Vehicle valuation sources and guide values. | Valuation integrations are deferred. |
| [`{B64D5E21-E7D4-44BC-A66A-2A42AF8A69C0}.png`](<../imp-docs/eva_information/screenshots/{B64D5E21-E7D4-44BC-A66A-2A42AF8A69C0}.png>) | Main case overview: parties, vehicle, inspection, dates, report and Case/PO. | Useful EVA export field review. Current case model/export is not implemented. |
| [`{C292430C-514C-4073-AECA-63E4F8D0ED78}.png`](<../imp-docs/eva_information/screenshots/{C292430C-514C-4073-AECA-63E4F8D0ED78}.png>) | Salvage values, movement and category tables. | Salvage workflow is outside current first-MVP scope. |
| [`{CCD7E916-7A98-488D-BDDE-8E85F7C9063F}.png`](<../imp-docs/eva_information/screenshots/{CCD7E916-7A98-488D-BDDE-8E85F7C9063F}.png>) | Raw VRM/vehicle lookup data tree. | DVLA/DVSA enrichment is planned; exact provider response is not adopted. |
| [`{D40D4374-F9FA-46DA-A042-6DDE90C00D6D}.png`](<../imp-docs/eva_information/screenshots/{D40D4374-F9FA-46DA-A042-6DDE90C00D6D}.png>) | Files tab with instruction, calculation and report PDFs. | Box custody/versioning and EVA handoff are planned; no current adapter exists. |
| [`{D9450032-911C-4ED4-BF53-669B626D33DE}.png`](<../imp-docs/eva_information/screenshots/{D9450032-911C-4ED4-BF53-669B626D33DE}.png>) | Photos tab showing registration, damage and corner images. | Image custody/review overlaps v2 plans; old naming/order is not automatically required. |

## Other imported reference

| File | Brief contents | Current v2 comparison |
| --- | --- | --- |
| [`llms.txt`](../imp-docs/llms.txt) | Approximately 100 KB of scraped Box documentation navigation and endpoint links. | **Not an integration design.** Select current official Box operations only when a caller is planned and approved. |

## Data-handling note

The exports and screenshots should be treated as sensitive local reference material. The guides describe structure without copying personal rows. No file was modified, converted, uploaded or imported.
