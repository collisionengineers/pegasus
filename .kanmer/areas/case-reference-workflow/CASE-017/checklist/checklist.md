# Checklist

- [x] Decide the shape: a note is a history row, not a second store — no table, no migration
- [x] Core `AddCaseNote` with validation, a 2000-character bound and trimming
- [x] Staff-only: the Automation Actor holds casework rights and must not author a note
- [x] EF store writing `operator_note`, idempotent by operation key
- [x] Register both in `AddPegasusInfrastructure`
- [x] `OnPostAddNoteAsync` — no edit lease, no expected version
- [x] Tab and heading read **Notes**; the add-note form joins the panel
- [x] `operator_note` labelled "Note" in the one history-event label table
- [x] Core tests: trimming, empty refused, overlong refused, automation refused
- [x] Build clean; 916 Core tests pass
- [ ] Live: a note added through the UI appears beside the DVLA lookup entry, attributed

## Progress notes

**2026-08-22** — implemented in `5414997d`.

The automation check was not in the first draft. `StaffAuthorization.Require(…,
PerformCasework)` admits the Automation Actor, so the test asserting it could not write a
note failed — correctly. Notes now check the actor kind explicitly.

No new operator-facing sentence was written: the surface is a label, a textarea and a
button, per the design authority's page-economy rules.

The final item needs the deployed build and is Phase 6.
