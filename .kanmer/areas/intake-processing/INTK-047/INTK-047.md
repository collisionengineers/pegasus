---
id: INTK-047
type: ticket
title: 'Port Upload, upload status pages and the public upload request'
status: review
area: intake-processing
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-29T16:39:42.128Z'
  review: '2026-08-29T17:20:03.797Z'
taken_at: '2026-08-29T16:41:46.733Z'
branch: task/intk-047-upload-pages
worktree: ../pegasus-worktrees/intk-047-upload-pages
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
commits:
  - cc499e9e
  - 0c1d4839
  - 2b5675f1
  - 124c037e
  - 95223e8a
  - 940a4053
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/627'
archived: false
created: '2026-08-28T08:35:23.964Z'
updated: '2026-08-29T17:20:47.134Z'
---

## What

Wave 2 lane G of [[EPIC-011]]. Port `Pages/Upload.cshtml`, `UploadStatus`, `UploadGroupStatus`, `_UploadOutcome` and the external `Pages/Uploads/Request.cshtml` to `context.md` §1.10: dropzone copy exactly as drawn, multi-file rows with native `<progress>`, per-file outcomes reusing `UploadCaseDecision` links, Upload + Clear; public request card on the external shell.

## Owns

`src/Pegasus.Web/Pages/Upload*.cshtml(.cs)`, `Pages/UploadConfirmationPageModel.cs`, `Pages/Uploads/**`, `Pages/Shared/_UploadOutcome.cshtml`, `Presentation/UploadOutcome.cs`, `UploadCaseDecision.cs`, tests `UploadConfirmationWebTests.cs`, `Browser/Upload*BrowserTests.cs`, `MultiFormatIntakeWebTests.cs`.

## Blocked by

[[PLAT-029]] and [[INTK-001]] (in flight on `UploadStatus*`).

## Verification

- [ ] Upload limits and outcomes unchanged; no clipped text/overflow at 1580/1100/760.
