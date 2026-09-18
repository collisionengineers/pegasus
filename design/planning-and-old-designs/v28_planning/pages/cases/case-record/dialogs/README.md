# Case record dialogs

- **Parent:** [Case record](../README.md)
- **Live source:** `src/Pegasus.Web/Pages/Cases/Shared/_CaseDialogs.cshtml`, `src/Pegasus.Web/Pages/Shared/_ReasonDialog.cshtml`, `src/Pegasus.Web/Pages/Shared/_EditFinishConfirm.cshtml`

These dialogs exist only inside the edit session, so they are presets over that state. Each is opened by the live page's own opener.

## Captured states

Each state is the running application's own HTML for the route shown, saved with the live CSS and JS. Nothing in it is transcribed.

| State | Live route | Open | Screenshots |
| --- | --- | --- | --- |
| Place on Hold dialog (on "Page-wide edit session") | `/Cases/f3dbeb86-4ab8-4d5b-ade3-dea35c995b1f` | [frame](../../../../current/pegasus_case_record_v28.html#case-record-editing?dialog=case-hold-dialog) | [1580](../../../../current/v28-shots/s94-case-record-hold-dialog-1580.png) · [1440](../../../../current/v28-shots/s94-case-record-hold-dialog-1440.png) · [760](../../../../current/v28-shots/s94-case-record-hold-dialog-760.png) |
| Close Case dialog (on "Page-wide edit session") | `/Cases/f3dbeb86-4ab8-4d5b-ade3-dea35c995b1f` | [frame](../../../../current/pegasus_case_record_v28.html#case-record-editing?dialog=case-close-dialog) | [1580](../../../../current/v28-shots/s95-case-record-close-dialog-1580.png) · [1440](../../../../current/v28-shots/s95-case-record-close-dialog-1440.png) · [760](../../../../current/v28-shots/s95-case-record-close-dialog-760.png) |
| Correct principal dialog (on "Page-wide edit session") | `/Cases/f3dbeb86-4ab8-4d5b-ade3-dea35c995b1f` | [frame](../../../../current/pegasus_case_record_v28.html#case-record-editing?dialog=case-correct-principal-dialog) | [1580](../../../../current/v28-shots/s96-case-record-correct-principal-dialog-1580.png) · [1440](../../../../current/v28-shots/s96-case-record-correct-principal-dialog-1440.png) · [760](../../../../current/v28-shots/s96-case-record-correct-principal-dialog-760.png) |
| Return to Review dialog (on "Page-wide edit session") | `/Cases/f3dbeb86-4ab8-4d5b-ade3-dea35c995b1f` | [frame](../../../../current/pegasus_case_record_v28.html#case-record-editing?dialog=case-return-review-dialog) | [1580](../../../../current/v28-shots/s97-case-record-return-review-dialog-1580.png) · [1440](../../../../current/v28-shots/s97-case-record-return-review-dialog-1440.png) · [760](../../../../current/v28-shots/s97-case-record-return-review-dialog-760.png) |
| Import estimate dialog (on "Page-wide edit session") | `/Cases/f3dbeb86-4ab8-4d5b-ade3-dea35c995b1f` | [frame](../../../../current/pegasus_case_record_v28.html#case-record-editing?dialog=import-estimate-dialog) | [1580](../../../../current/v28-shots/s98-case-record-import-estimate-dialog-1580.png) · [1440](../../../../current/v28-shots/s98-case-record-import-estimate-dialog-1440.png) · [760](../../../../current/v28-shots/s98-case-record-import-estimate-dialog-760.png) |

## Not captured

- Create audit, Release Hold, reopen and the delete-estimate confirmation. The fixture Case does not offer them in its current state.
