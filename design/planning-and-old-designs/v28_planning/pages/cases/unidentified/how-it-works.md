Read from the live source on 18 September 2026.

## What this page does not show

- No lede or explanatory sentence — the panel headings ("Retained e-mail" /
  "Retained files", "Resolve" / "Resolution", "Registration readings",
  "History") name themselves.
- The list has no Principal filter (`IndexModel.ListsCases("unidentified")`
  is false), only the `Show` select.
- The record shows no "State" fact row in the list's quick-detail panel —
  `UnidentifiedRow`/`ClosedUnidentifiedRow` never set `Chip`, so
  `Cases/Index.cshtml`'s conditional `detail.StateChip` fact never renders
  for this row kind; the open/closed distinction is carried entirely by the
  `Notice` banner (amber "warning" tone for the open reason, green
  "success" tone for the closed outcome).

## Source table

| File | Owns |
| --- | --- |
| `Pages/Cases/Index.cshtml` (tab `unidentified`) | The list: columns, Show filter, quick-detail pane |
| `Pages/Cases/Index.cshtml.cs` | `UnidentifiedRow`/`ClosedUnidentifiedRow`, `Columns` for `"unidentified"`, `ShowingClosed`, `Handle` |
| `Pages/Unidentified/Index.cshtml.cs` | The permanent redirect to `/Cases?tab=unidentified` — never renders |
| `Pages/Unidentified/Details.cshtml` | The item: ribbon, material panel, Resolve/Resolution panel, Registration readings, History, all dialogs |
| `Pages/Unidentified/Details.cshtml.cs` | `MaterialLabel`, `Handle`, `SourceLabel`, `CouldNotBeRead`, `ClosedOutcome`, `CanLinkCase`/`CanCreateCase`/`CanRegisterImages`/`CanOpenTriage`, dialog id constants |
| `Pegasus.Web.Presentation.OperatorLabels` | `UnidentifiedReason`, `UnidentifiedState`, `UnidentifiedMediaKind`, `EmailHandle`, `UnidentifiedResolutionTarget` |
| `Pages/Shared/_ImageGallery.cshtml` | The "Extracted photographs" gallery when images were pulled from the retained material |
| `Pages/Shared/_EvidenceViewer` | The shared lightbox the gallery tiles open into |

## Behaviours

### The list is a tab, not a page

`/Unidentified` (`Pages/Unidentified/Index.cshtml`) is a bare
`RedirectPermanent` to `/Cases?tab=unidentified`. The actual list is
`IndexModel.UnidentifiedRow`/`ClosedUnidentifiedRow` on the shared Cases
queue page: columns `Reference, Received, Material, Reason, Source` when
open, `Reference, Received, Material, Outcome, Source` when the `Show`
filter reads Closed items — the open list is queried directly
(`UnidentifiedCount` counts open items only; a closed item is never
counted), and the closed list is a separate read behind the filter.

### Six reason codes, one of them special

`UnidentifiedReasonCode` has six values (Unreadable or corrupt content, No
usable identification, Ambiguous ownership or destination, Conflicting
identification, Unsupported content, Technical processing failure — read
through `OperatorLabels.UnidentifiedReason`). Only "Could not be read"
(`DetailsModel.CouldNotBeRead`) additionally carries a file kind and shows
its own warning banner above the fact grid; the other five show only in the
"Needs attention" fact.

### Closed with a reason, and reopenable

A readable item that must not become a Case is closed with a required
reason (`OnPostCloseAsync`); the ribbon chip reads "Closed" and the
Resolution panel's Outcome fact reads `Closed · <reason>` (or plain
"Closed" with no reason recorded). A closed item's only action is Reopen,
which is the same required-reason dialog shape. A resolved item (linked to
a Case, registered as images, or opened as a Triage) is a different
terminal state from closed — its Outcome fact reads the resolution
target's reference/description instead of a reason — but both non-open
states share the same Resolution panel and the same Reopen control.

### Four resolve actions, gated independently

`CanLinkCase`, `CanCreateCase`, `CanRegisterImages` and `CanOpenTriage` are
four independent policy checks (`IntakeAssociationDestinationPolicy`,
`Context.CanRegisterImages`, `Context.CanOpenTriage`); an open item can
offer any subset of them alongside the always-available "Close with
reason". An upload-group item substitutes a "View files" link to
`/UploadGroupStatus` for Link/Register when it is a manual-upload
submission group rather than a single receipt.

### Registration readings are decided per-reading, not per-item

Each VRM recognition reading on the item carries its own Decision
(`Not decided`/`Dismissed`/`Accepted`) and, while `Pending` and the item is
open, its own inline Dismiss control with a required reason — dismissing
one reading never affects the others or the item's own open/closed state.

## Things the FRD does not settle

None found in the read source for this page.
