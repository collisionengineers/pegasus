# Create audit

- **Mockup route:** the `case-create-audit-dialog` inside
  `pegasus_case_record_v28.html` in [`../../../current/`](../../../current/README.md)
- **Live source:** `Shared/_CaseDialogs.cshtml` (dialog markup), `Details.Frame.cs`
  (`CanCreateAudit`, `ProposedAuditReference`, `OnPostCreateAuditAsync`)
- Parent: [**Case record**](../case-record/README.md)

Create audit is a Case-record Actions-menu item, not a page of its own: a
compact confirmation dialog naming the Original Case's reference, its
recorded Outcome, and the derived Audit reference
(`AuditIdentity.Create(reference)`, shown once an outcome is recorded), with
one primary action and no reason field — the action is its own record.

## Screenshots

None taken separately — Create audit is an Actions-menu item on the Case record, captured within [`../case-record/README.md`](../case-record/README.md)'s screenshots rather than as its own surface.

## Notes

- Offered only when the Case type is Inspection + Audit, a report has ever
  been generated (`HasGeneratedReport`), no Audit Case or Original Case link
  already exists, the Case is not Created in error or archived, and the
  page-wide edit session is open (`CanCreateAudit`, `Details.Frame.cs`).
- This capture models the gate with one strip toggle,
  `crcasetype=inspectionaudit` plus `crhasreport=yes` (see
  `../case-record/states/README.md`); it does not model an existing
  Audit/Original link since the single-fixture capture never shows a second,
  already-linked Case.
- On success the live handler redirects the operator onto the new Audit
  Case; this capture's stub instead toasts and closes the dialog, matching
  every other not-fully-wired action in this lane.
