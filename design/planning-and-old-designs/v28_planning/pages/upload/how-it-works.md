Read from the live source on 18 September 2026.

## What this page does not show

- No "What happens next" steps and no "Accepted files" chip row beside the
  form — `Upload.cshtml`'s own source comment (lines 7-11) records that both
  were removed because they narrated the page's own mechanics, which the
  page-economy rule forbids; the accepted types already appear once, on the
  dropzone itself.
- **No public or external upload link.** See the README's Notes for the full
  trace: the page is authenticated staff-only
  (`[Authorize(Roles = Administrator,Engineer,User)]`), `ExternalReceiptToken`
  is a same-session replay key rather than a shareable token, and no route,
  string or dead link referencing a public/secure-file-request surface
  remains in `Upload.cshtml(.cs)`, `UploadStatus.cshtml(.cs)` or
  `UploadGroupStatus.cshtml(.cs)`. This confirms PR #789
  ("remove-public-upload-links", merged) left no live remnant on the Upload
  side.
- No progress percentage or byte counter — `data-upload-progress` on the form
  is a hook for `site.js`'s own enhancement, not markup this page draws
  itself.
- No per-file thumbnail grid on the plain Upload form; a thumbnail only
  appears later, on `UploadStatus`/`UploadGroupStatus`, and only for an
  image whose processing has resolved (`Outcome.ThumbnailReceiptId`).

## Source table

| File | Owns |
| --- | --- |
| `Pages/Upload.cshtml` | The dropzone, native file input, file-list rows, Upload/Clear buttons |
| `Pages/Upload.cshtml.cs` | Validation (file count/size/type), `groupedSubmission.ExecuteStreamedAsync`, the single-file-vs-group redirect |
| `Pages/UploadStatus.cshtml` | One file's heading, received/duplicate/reason facts, thumbnail, `_UploadOutcome` |
| `Pages/UploadStatus.cshtml.cs` | `Heading` (Received/Processing/Complete/Failed), `AutomaticRefreshMilliseconds`, `FailureReason`, the manual-sibling-group redirect |
| `Pages/UploadGroupStatus.cshtml` | The one-decision-per-submission card, registration form, "Add to an existing case" details, discard panel, per-file list |
| `Pages/UploadGroupStatus.cshtml.cs` | `DecideSubmission` (the shared-destination roll-up), `OpenGroupDecision`, `OfferGroupRegistration`, `CanDiscard`, the `RegisterGroup`/`AttachGroup`/`DiscardGroup` handlers |
| `Pages/Shared/_UploadOutcome.cshtml` | One file's report-or-decision card: chip, message, primary/secondary action, "Add to an existing case" |
| `Presentation/UploadOutcome.cs` | `UploadOutcomeKind` (the 9 states below), `UploadOutcomeQueries.BuildAsync` — the one place a file's outcome is decided |
| `Presentation/OperatorLabels.cs` | `Upload.*`, `UploadDecision.*`, `IntakeFailure`, `IntakeCannotBecomeCaseReason`, `AssociatedWithCase`, `FileSize` |

## Behaviours

### One upload becomes one file or one group, never a mixed redirect

`UploadModel.OnPostAsync` always calls the same grouped-submission port; a
single chosen file (`result.Members.Count == 1`) redirects to
`/UploadStatus`, and anything larger redirects to `/UploadGroupStatus`
(`Upload.cshtml.cs` lines 165-179, with the comment explaining the one-member
group keeps its own simpler status page rather than sending a lone file to
the group UI).

### Every distinct file outcome (`UploadOutcomeKind`)

`UploadOutcomeQueries.BuildAsync` (`Presentation/UploadOutcome.cs`) resolves
one of nine kinds, each captured as its own `mu-status-state` option:

| Kind | Heading / chip | Message | Primary action |
| --- | --- | --- | --- |
| `Working` (Received) | Received, no chip | "The file is safely received and waiting for background processing." | none |
| `Working` (Processing) | Processing, no chip | "The file is being processed." | none |
| `Attached` | Complete · Success | "This was automatically associated with case `<ref>`." | Open case |
| `ImageCaseRegistered` (awaiting instruction) | Complete · Success | "This was registered as a new vehicle-image case, `<ref>`. A staff member can explicitly add it to an existing case." | View + "Add to an existing case" |
| `ImageCaseRegistered` (merged) | Complete · Success | "This was registered as a new vehicle-image case, `<ref>`." | View |
| `NeedsReview` | Complete · Unidentified | "This could not be matched automatically and needs a staff decision." | Review |
| `Resolved` | Complete · Resolved Unidentified | "This was resolved to `<destination>`." | View |
| `PossibleMatch` | Complete · Pending | "Choose a case destination. Select a viable existing case and confirm it, or search for a different existing case." | "Add to an existing case" only |
| `ReadyToCreate` | Complete · Pending | "Choose a case destination. No viable existing case was found, so you can create a new case from the extracted details." | Create a new case + "Add to an existing case" |
| `CannotBecomeCase` | Complete · Failed | `IntakeCannotBecomeCaseReason(decision)` | Open file |
| `Failed` | Failed · Failed | `IntakeFailure(failureCode)` | none |

`Working` never shows the `_UploadOutcome` partial at all — the page falls
back to the bare heading and fact list, which is why `UploadStatus.cshtml`
guards the whole partial on `Model.Outcome is { } outcome`.

### The group page decides once, never per file

`DecideSubmission` (`UploadGroupStatus.cshtml.cs` lines 136-161) only returns
a `SubmissionDecision` once every member has settled to the *same* primary
action, and only for three shapes: all `Attached` ("Attached to a Case",
green), all `ImageCaseRegistered` ("Registered as vehicle images", green), or
all `NeedsReview`/`Resolved` ("Unidentified", amber). Any other mix leaves
`Decision` null and the per-file list stands on its own — there is
deliberately no "mostly settled" partial-decision card.

### The registration form only appears for an open, image-bearing group

`OfferGroupRegistration` requires `OpenGroupDecision`, no
`GroupRegistrationOutcome` yet, and at least one open member carrying a
thumbnail (`UploadGroupStatus.cshtml.cs` lines 490-494) — i.e. only when the
group is still undecided *and* is genuinely image-only material. A
non-image open group (e.g. all-instruction-document) only ever offers "Add
to an existing case".

### Discard is a terminal, retention-preserving action

`CanDiscard` requires the whole group complete, unregistered, and matching
its `ExpectedMemberCount` (`UploadGroupStatus.cshtml.cs` lines 466-484); the
confirmation checkbox text and the post-discard panel both state explicitly
that "the retained source and processing record remain available" — discard
never claims to delete anything, matching the platform-wide rule that
nothing under Pegasus intake is ever silently destroyed.

## Things the FRD does not settle

- The exact interaction of `UploadOutcomeCompact`, `OpenGroupDecision` and
  `RefreshAutomatically` on a settled group's per-file button row (noted in
  the README) was not independently proven against every combination while
  reading this source in isolation; the mockup shows one representative
  "Open case" link for the `decided-attached` state rather than the full
  cross-product.
