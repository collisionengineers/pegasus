---
id: INTK-047
type: ticket
title: 'Port Upload, upload status pages and the public upload request'
status: backlog
area: intake-processing
assignee: ''
profile: feature
labels:
  - ui
  - wave-2
  - upload
groups:
  - EPIC-011
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-28T08:35:23.964Z'
updated: '2026-08-28T08:35:23.964Z'
---

## What

Wave 2 lane G of [[EPIC-011]]. Port `Pages/Upload.cshtml`, `UploadStatus`, `UploadGroupStatus`, `_UploadOutcome` and the external `Pages/Uploads/Request.cshtml` to `context.md` §1.10: dropzone copy exactly as drawn, multi-file rows with native `<progress>`, per-file outcomes reusing `UploadCaseDecision` links, Upload + Clear; public request card on the external shell.

## Owns

`src/Pegasus.Web/Pages/Upload*.cshtml(.cs)`, `Pages/UploadConfirmationPageModel.cs`, `Pages/Uploads/**`, `Pages/Shared/_UploadOutcome.cshtml`, `Presentation/UploadOutcome.cs`, `UploadCaseDecision.cs`, tests `UploadConfirmationWebTests.cs`, `Browser/Upload*BrowserTests.cs`, `MultiFormatIntakeWebTests.cs`.

## Blocked by

[[PLAT-029]] and [[INTK-001]] (in flight on `UploadStatus*`).

## Verification

- [ ] Upload limits and outcomes unchanged; no clipped text/overflow at 1580/1100/760.
