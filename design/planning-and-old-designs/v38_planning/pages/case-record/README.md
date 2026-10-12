# Case record: the Repair Spec section

Temporary review artifact; see the [round README](../../README.md).

- **Mockup route:** `current/pegasus_repair_spec_v38.html`; `mode=read|edit`, `spec=1|2`, `design=today|proposal`, `opt=` per proposal, `dialog=` by name.
- **Live source:** `src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml` and `_RepairSpecDiff.cshtml`; styles `wwwroot/css/case-workspace.css`; behaviour `wwwroot/js/case-workspace.js`; Core `src/Pegasus.Core/Assessment/Estimates.cs` and `RepairSpecificationActs.cs`.
- [Dialogs](dialogs/README.md) · [States](states/README.md) · [Panels](panels/README.md)
- [How it works today](how-it-works.md) · [How it should work](how-it-should-work.md)

## Screenshots

Listed in [`current/verification.json`](../../current/verification.json) and cited by number in the [notes](../../current/v38-notes.md): both designs, read and edit, both specs, at 1580, 1440 and 760; the full page in edit; Compare, Versions, the agreed sum and the Target % preview per design; the button and rate variants; the proposal with P1 off; the read-mode Supplementary; Diff; Split; the print sheet; the two confirmation dialogs.

## Notes

- The section is rendered from the reference file's own repair grid (14 lines, and the supplementary estimate's 17), mapped to today's seven line types; the rest of the page is the v36 frame.
- The fixture Case has two specs so that Compare, Versions and Supplementary have content: the earlier spec carries an imported version and a sent version; the Current spec was imported today.
