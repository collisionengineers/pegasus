# Upload

- **Mockup route:** `pegasus_mail_upload_v28.html` (`mu-area=upload`) in [`../../current/`](../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Upload.cshtml`, `UploadStatus.cshtml`, `UploadGroupStatus.cshtml`, `Shared/_UploadOutcome.cshtml`

- [**How it works**](how-it-works.md)

## Screenshots

- [s34-upload-idle-1580.png](../../current/v28-shots/s34-upload-idle-1580.png) · [1440](../../current/v28-shots/s34-upload-idle-1440.png) · [760](../../current/v28-shots/s34-upload-idle-760.png)
- [s35-upload-status-failed-1580.png](../../current/v28-shots/s35-upload-status-failed-1580.png) · [1440](../../current/v28-shots/s35-upload-status-failed-1440.png) · [760](../../current/v28-shots/s35-upload-status-failed-760.png)

## Notes

- **No public/external upload-link surface exists live.** `Upload.cshtml.cs`'s
  remarks (lines 11-21) describe the manual-upload route as "a surface of its
  own" reached only by an authenticated, role-gated staff session
  (`[Authorize(Roles = Administrator,Engineer,User)]`); there is no anonymous
  or token-based entry point anywhere in `Upload.cshtml(.cs)`,
  `UploadStatus.cshtml(.cs)` or `UploadGroupStatus.cshtml(.cs)`. This is
  consistent with PR #789 ("remove-public-upload-links", merged) having
  removed that feature entirely — nothing in the current source contradicts
  the removal, and no remnant string, route or dead link was found while
  reading these three pages. The `ExternalReceiptToken` hidden field on
  `Upload.cshtml` is a replay key for this same authenticated form post, not
  a public link token.
- The mockup-build reference doc's "Idle / Files chosen / Storing / Decided"
  wording does not match the live code: the real state names are the
  `QueuedIntakeStatusKind` heading words **Received / Processing / Complete
  / Failed** (`UploadStatusModel.Heading`), and "Decided" is not itself a
  drawn word — a `Complete` file either carries a settled report-only outcome
  or one of four states that still need a staff decision
  (`UploadOutcomeView.IsOpenDecision`). The mockup uses the actual heading
  words throughout.
- `UploadGroupStatus`'s per-file button rendering has a few overlapping
  conditionals (`UploadOutcomeCompact`, `OpenGroupDecision`,
  `RefreshAutomatically`) that can, on the live page, suppress or show a
  file's own action link depending on the *other* files' states at the same
  time. The mockup's `decided-attached` state shows one representative
  "Open case" link rather than reproducing every combination of that
  interaction; documented in `how-it-works.md`.
