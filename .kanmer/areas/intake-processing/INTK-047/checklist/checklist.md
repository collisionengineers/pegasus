# Checklist — INTK-047

- [ ] Append `OperatorLabels.Upload` at the end of `OperatorLabels.cs`; nothing above it moved
- [ ] `Upload.cshtml` on §1.10: header only, drawn dropzone copy from the real `IntakeEnvelopeLimits`, Upload + Clear
- [ ] "What happens next" and "Accepted files" removed (page economy)
- [ ] `site.js` `[data-dropzone]` block: `.file-row` vocabulary, indeterminate `<progress>`, `reset` handler, form-scoped readout lookup
- [ ] `UploadStatus.cshtml` ported; `<h1>@Model.Heading</h1>` and `data-auto-refresh` preserved verbatim
- [ ] `UploadGroupStatus.cshtml` ported; every handler and the case-search contract unchanged
- [ ] `_UploadOutcome.cshtml` on the `btn`/`muted` vocabulary
- [ ] `Uploads/Request.cshtml` on the external card; explanatory paragraph deleted; no reference or expiry disclosed
- [ ] Browser test selectors follow the renamed markup; `<progress>` assertion added
- [ ] `dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false` — 0 `CS` diagnostics
- [ ] Focused `dotnet test` filter green, counts recorded
- [ ] Simplification pass run over the branch diff; findings dispositioned in `plan.md`
- [ ] Snapshots NOT regenerated; no feature flag touched; no live activation
