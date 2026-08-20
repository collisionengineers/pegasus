# SIMPLI-013 checklist

- [ ] Import CompoundFile/Word/Msg sources under `src/Pegasus.Infrastructure/Intake/DocumentExtraction/`, renamespaced, internal, Word trimmed to text slice
- [ ] Phase A fixes applied in imported source (reserved-field, CP1252, cbMac, surrogates, guard CP, \htmlrtf)
- [ ] Adapter branches `Doc`/`Msg` in `MimeKitPdfPigOpenXmlIntakeSourceReader` (+ partial file), fail-closed fallback, attachment re-dispatch, transport evidence
- [ ] OperatorLabels entries for new issue codes
- [ ] Imported unit suites converted to xunit and green; changed expectations annotated
- [ ] `.doc` and `.msg` end-to-end web tests green (programmatic fixtures)
- [ ] Existing PDF/DOCX/EML intake tests green unchanged
- [ ] Workspace deleted; `workspaces.yml` deleted; `workspaces/README.md` updated; FRD-05 sentence
- [ ] Release build zero warnings; focused test runs recorded
- [ ] Simplification pass recorded in plan
- [ ] Post-implementation report; PR to dev; ticket → review
