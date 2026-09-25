# How Upload works now

Read from `origin/dev` at `6f501a6ca` on 24 September 2026. The baseline
[v28 Upload capture](../../../v28_planning/pages/upload/README.md) was used
for the actual Pegasus shell and original page geometry.

## What the pages do not show

`/Upload` does not choose a Case. Its successful post stores one submission
and redirects a single file to `/Upload/Status/{id}` or several files to
`/Upload/Group/{id}`. Processing then continues through the Worker. A queued
member may be Received, Processing, Complete, or Failed. The group page
refreshes while any member or group outcome is still moving.

## Owners

| Source | Responsibility |
| --- | --- |
| `docs/frd/frd-18-manual-upload.md` | Limits, one submission decision, per-file outcome, Cancel and custody |
| `docs/design/README.md` | Shell, visual vocabulary, action and state presentation |
| `CONTEXT.md` | Reserved business and interface terms |
| `src/Pegasus.Web/Pages/Upload.cshtml(.cs)` | Picker, validation, storage call, one-file/group redirect |
| `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml(.cs)` | Group outcome, member list, destination forms, discard and refresh |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml(.cs)` | Single-file status, timing, thumbnail and outcome |
| `src/Pegasus.Web/Pages/Shared/_UploadOutcome.cshtml` | Per-file outcome and available action |
| `src/Pegasus.Web/Presentation/UploadOutcome.cs` | Maps recorded outcome to operator-facing state and actions |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Shared labels and formatting |
| `src/Pegasus.Core/Intake/UploadedCorrespondence.cs` | Retained uploaded-email identity and folder; no UI policy |
| `src/Pegasus.Core/Intake/DiscardIntakeSubmissionGroup.cs` | Versioned discard command; retains source and processing record |

## Current controls and facts

- Upload: drop target and native file input, accepted file types/limits,
  selected names, sizes, per-file in-flight/stored/failure states, Upload and
  Clear. Validation covers empty selection, count, per-file and total size,
  content/type mismatch and retention failure.
- Group: page title and Upload another file, file count and member rows,
  thumbnails for processed images, member status/outcome, group result where
  one destination is settled, registration and reason form for eligible image
  groups, possible Cases, typed Case search and exact target confirmation,
  Cancel, and folded discard with checkbox and danger button.
- Single file: status heading, Refresh, Upload another file, received time,
  duplicate/failure reason, image thumbnail, resulting record link or open
  destination choice.

## Current decision and recovery

The group can offer one destination choice only when its roster is complete
and all members support the same decision. A unique suggested Case is never
selected automatically. Add to Case reviews the exact target and versions
before the existing leased link path runs. Cancel returns to Upload without
changing the kept material. Discard requires a complete current roster and
server-side group/member version match. Failed and unreadable members retain
their own reported outcome; a mixed group is not falsely reported as fully
successful.

## Gaps exposed in issue #830

The rendered status list changes row height during processing, generic
Success conceals the destination, and the discard consequence is repeated in
legalistic copy. The current FRD-18 page-shape clause explicitly prescribes
the checkbox and places files first. This round proposes presentation changes
only; it does not change custody or business decisions.

## Source recheck — 25 September 2026

Read from `origin/dev` **32dabfc59** before the five new proposals were built:
`Pages/Upload.cshtml`, `Pages/UploadStatus.cshtml`,
`Pages/UploadGroupStatus.cshtml` and `Pages/Shared/_UploadOutcome.cshtml`, with
FRD-18 and the current reserved vocabulary.

The initial form supports multiple files, a drop target, accepted extensions,
per-file name/size/state, validation, Upload and Clear. Status pages show received
time, duplicate/failure outcomes, originals, thumbnails and refresh. A complete
open group owns one Case decision. Search and explicit confirmation carry the
complete member versions and exact Case identity. Cancel leaves the upload;
discard retains source and processing records and has an acknowledgement checkbox.
New Case proposals open their existing owner. Automatic Image intake registration
and explicit manual association requirements apply to both single and group views.

The [new proposal coverage audit](../../current/upload-design-proposals.md#source-and-coverage-audit)
records each control/fact and the deliberate presentation departures. Older
manual-registration page-shape prose is superseded for this round by the
24 September decision already recorded in how-it-should-work.
