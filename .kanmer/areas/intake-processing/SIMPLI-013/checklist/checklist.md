# SIMPLI-013 checklist

- [x] Import CompoundFile/Word/Msg sources under `src/Pegasus.Infrastructure/Intake/DocumentExtraction/`, renamespaced, internal, Word trimmed to text slice
- [x] Phase A fixes applied in imported source (reserved-field, CP1252, cbMac, surrogates, guard CP, \htmlrtf)
- [x] Adapter branches `Doc`/`Msg` in `MimeKitPdfPigOpenXmlIntakeSourceReader` (+ partial file), fail-closed fallback, attachment re-dispatch, transport evidence
- [x] Operator-facing issue text — no OperatorLabels change needed: issue reasons are self-carried sentences (same as `pdf-engine`/`openxml-engine`); the fail-closed design sets no new failure codes
- [x] Imported unit suites converted to xunit and green (134 tests); changed expectations annotated in the test diff
- [x] `.doc` and `.msg` end-to-end web tests green (programmatic fixtures; `.msg` upload reaches CaseCreated with QDOS fields from its body and its PDF attachment through PdfPig)
- [ ] Existing PDF/DOCX/EML intake tests green unchanged (MultiFormat/InlineForwarded/IntakeWebNegative/UploadOutcomeQueries run in progress)
- [x] Workspace deleted; `workspaces.yml` deleted; `workspaces/README.md` updated; FRD-05 sentence; runbook + capabilities register updated
- [x] Release build zero warnings (Infrastructure and full `Pegasus.slnx`); ArchitectureTests 97/97, Core.Tests 691/691
- [ ] Simplification pass recorded in plan
- [ ] Post-implementation report; PR to dev; ticket → review
