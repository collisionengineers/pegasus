# Files — CASE-009 (2026-09-02, gpt-5.6-terra medium, wrapper-checked)

Supersedes the 2026-08-21 files document, which targeted `_CaseSummary.cshtml`
and `Details.cshtml.cs`; both are now other lanes' files (N7 and N2).

## Proposed changes

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Core/Cases/CaseQueries.cs` | change | Add the read-only query-email row/list to `CaseDetails`. | `CaseDetails`, existing projection records |
| `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` | change | Project linked, qualifying retained messages for the current case. | `CurrentIntakeAssociations.ReadAsync`, retained-mail/receipt `ExternalReceiptToken` join, `EfEngineerActivityQueries` pattern, `MailOperationalDestinationPolicy` / `MailTaxonomy` |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseFiles.cshtml` | change | Replace the "Correspondence is absent" comment with the production partial caller. | Existing Files-section caller from `Details.cshtml` (`?section=case-files`) |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseCorrespondence.cshtml` | create | Render the read-only Queries table, empty state, and Open message links (`asp-page="/Mail/Message"`). | Razor partial conventions, `OperatorLabels` |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change, sequenced only (capacity-one lock, after N2 frame merges) | Add any new Queries table labels to the single operator-label owner. | `CaseWorkspace`, existing `MailOperationalDestination.Queries => "Queries"` |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | change | Prove qualifying display, exclusions, empty state, and absent mutation controls. | `RecordingCaseDetailsStore`, section tests |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | change only if necessary | Supply an end-to-end persisted retained-mail/receipt/association fixture if the Case Details fake cannot prove the EF projection. | Existing `RetainedMailboxMessage` and `StoreClassifiedReceiptAsync` helpers |

No migration: the projection reads existing tables only.

## Files not to touch (other EPIC-012 / EPIC-011 lanes)

- `src/Pegasus.Web/Pages/Cases/Details.cshtml` (N2 frame)
- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` (N2 frame)
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml` (N2)
- `src/Pegasus.Web/wwwroot/css/site.css`, `src/Pegasus.Web/wwwroot/js/site.js` (N2)
- `docs/design/README.md` (N2 / governing docs)
- `src/Pegasus.Web/Pages/Cases/Assessment/**` (N3)
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml`, `_CaseEstimate.cshtml`, `_CaseSettlement.cshtml`, `_CaseReport.cshtml` (N3)
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml`, `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml(.cs)` (N7)
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseInspectionAddress.cshtml` (N9)
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseVehicle.cshtml`, `_CaseValuation.cshtml`, `src/Pegasus.Web/Pages/Cases/Vehicle.cshtml.cs` (CASE-029)
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseEngineerNotes.cshtml`, `src/Pegasus.Web/Pages/Cases/Tasks.cshtml.cs` (N6)
- `src/Pegasus.Web/Pages/Cases/Index.cshtml(.cs)` (N12)
- `src/Pegasus.Web/Pages/Operations/**` (N11)
- `src/Pegasus.Web/Pages/Administration/Accounts/**` (N8)
- `src/Pegasus.Web/wwwroot/js/damage-diagram.js` (N5)
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseReportImages.cshtml`, `src/Pegasus.Web/wwwroot/js/cropper.js` (ENG-031)
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseFeeNote.cshtml` (N13)
- `docs/design/test-ui/**` (N14)
- `src/Pegasus.Infrastructure/Persistence/Migrations/**` (serialized; none needed)
- `src/Pegasus.Web/Pages/Mail/**`, `src/Pegasus.Core/Mail/OutboundMail.cs` (MAIL-026)
