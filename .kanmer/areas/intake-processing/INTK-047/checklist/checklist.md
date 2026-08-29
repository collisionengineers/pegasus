# Checklist — INTK-047

- [x] Append `OperatorLabels.Upload` at the end of `OperatorLabels.cs`; nothing above it moved
- [x] `Upload.cshtml` on §1.10: header only, drawn dropzone copy from the real `IntakeEnvelopeLimits`, Upload + Clear
- [x] "What happens next" and "Accepted files" removed (page economy)
- [x] `site.js` `[data-dropzone]` block: `.file-row` vocabulary, indeterminate `<progress>`, `reset` handler, form-scoped readout lookup
- [x] `UploadStatus.cshtml` ported; `<h1>@Model.Heading</h1>` and `data-auto-refresh` preserved verbatim
- [x] `UploadGroupStatus.cshtml` ported; every handler and the case-search contract unchanged
- [x] `_UploadOutcome.cshtml` on the `btn`/`muted` vocabulary
- [x] `Uploads/Request.cshtml` on the external card; explanatory paragraph deleted; no reference or expiry disclosed
- [x] Browser test selectors follow the renamed markup; `<progress>` assertion added
- [x] `dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false` — exit 0, 0 `CS` diagnostics
- [x] Focused `dotnet test` filters green — 71 passed and 52 passed, 0 failed
- [x] Simplification pass run over the branch diff; all four findings fixed, dispositions in `plan.md`
- [x] Snapshots NOT regenerated; no feature flag touched; no live activation

## Parked (explicitly deferred)

- [ ] The two edited `Browser` test classes are executed. The lane must not run
      the `Browser` category (`waves.md` orchestrator loop); their first real
      run is the wave loop's. Compile- and syntax-verified here.
- [ ] Visual proof at 1580/1100/760, from the ticket's own verification line.
      Browser walks are orchestrator-owned for the same reason.
