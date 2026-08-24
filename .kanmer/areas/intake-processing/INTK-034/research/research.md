# Research

## The gap is worse than the ticket assumed

I wrote this ticket saying *"nothing is lost today, because the attachments are
retained as receipt assets and the Triage detail page links straight to them."*
**The link is real; the destination shows nothing.**

`Pages/Triage/Details.cshtml:56` renders `View e-mail` →
`asp-page="/Intake/Details"`, which resolves to `/Received/{id}`. That page
renders each asset as a `field-card` (`Intake/Details.cshtml:430-461`) carrying a
filename, a `SourceLabel · Kind · MediaType` line and a duplicate count — **no
`<img>`, and no link to the asset route**. The single `<img>` on that page
(`:101-106`) renders only when the *receipt itself* is an image, which a
`message/rfc822` triage e-mail never is.

So a triage request's damage photographs are **viewable nowhere in the
application**. The engineer can see that four JPEGs arrived and cannot open one,
on the exact work whose entire purpose is *"determine if the vehicle is
repairable or a total loss"*.

That changes this from a convenience to a functional gap.

## Does a Triage have evidence in the domain? No.

| Thing | Evidence |
| --- | --- |
| `TriageRecord` | `TriageContracts.cs:34-41` — id, origin, registration, state, assignee, linked case, version. No documents, no assets, no reference of its own. |
| `TriageDetail` | `:280-286` — record, created, findings, response evidence, history, candidates. |
| The only "evidence" | `TriageResponseEvidenceLink` (`:224-230`) — the **outbound** reply-chain Sent item that gates completion. Not an inbound file; carries no content. |
| Persistence | `PegasusDbContext.cs:41-45` — exactly four tables: `Triage`, `TriageFindings`, `TriageResponseEvidenceLinks`, `TriageHistory`. No asset table. |
| FRD-03 | mentions evidence only as accepted-match evidence and completion evidence. Inbound photographs are not a concept. |

So retaining images *under* the Triage is a domain change, not a UI job.

## What already exists and is reusable

- **Retention** — `IntakeReceipt.AssetRecords`, each with a `StorageKey` into
  `IIntakeArtifactStore`. The *staged source* is deleted after processing
  (`DurableIntake.cs:602-604`); the extracted **assets are not**.
- **An authorised serve route** — `/Received/{id}/Asset/{assetId}`
  (`Intake/Asset.cshtml.cs:15-69`): `PerformCasework`-gated, SHA-256 re-verified
  against the recorded hash, hard-gated to `image/*` inline with `nosniff`.
- **The one selection owner** — `InstructionEvidenceImages.Select`: image
  attachments plus embedded images ≥ 40 KB, minus letterhead art by the ≤ 3.0
  side-ratio rule, de-duplicated by content hash.
- **The gallery partial** — `Pages/Shared/_ImageGallery.cshtml` with
  `GalleryImage(Href, FileName)`. (Note: the ticket and DOCS-011 both cite
  `Pages/Cases/Shared/_ImageGallery.cshtml`; that path does not exist.)
- **The precedent for a non-intake page loading its origin receipt** —
  `Unidentified/Details.cshtml.cs:161-174` injects `IGetIntake`, loads
  `SourceReceipt`, and degrades silently on `UnauthorizedAccessException`.

## Box holds none of this

FRD-05 gives a Box folder to an allocated Case/PO and to an Image-initiated
Case, each named for a **permanent reference**. Custody promotion runs off a
case payload (`EfQueuedCustodyProcessor.cs:280-350`). A Triage has no reference
and no folder.

## The two options, costed

**A — surface the receipt's assets read-only on the Triage page.** Zero domain
change, zero custody change, zero migration. The bytes stay in one place with
one hash-verified authorised route. Reuses three existing owners and one
existing page pattern. Roughly one page-model field, one projection, one
section.

**B — retain the images again under the Triage.** A fifth Triage table plus
migration; a copy of a retention path that is driven by a case payload and
writes to a case folder, neither of which a Triage has; a Box location that does
not exist, requiring a permanent Triage reference that would touch protected
operator notes; duplicate custody of the same bytes under two identities with no
rule saying which is authoritative — a stop condition under CLAUDE.md's "second
business implementation". Almost certainly an ADR. **And it still needs A's UI
on top.**

**A, decisively.** B should not be built without an explicit operator
instruction that Triage photographs must survive independently of the receipt,
and even then it wants its own ticket and an ADR.

## DOCS-011 collision — benign if this ticket only consumes

[[DOCS-011]] replaces `_ImageGallery.cshtml` with an in-place viewer. If
INTK-034 **consumes** the partial and does not edit it, the two touch disjoint
lines and this ticket inherits the viewer for free. It becomes a real conflict
only if INTK-034 modifies the partial. So: consume only.

## Premises

**Verified by reading source** (task worktree, `e6144344`): the Triage domain has
no asset concept (four DbSets, both records); `Triage/Details.cshtml:56` links to
`/Received/{id}`; `Intake/Details.cshtml:430-461` renders filename cards with no
image and the `:101-106` thumbnail is gated on the receipt itself being an image;
`_ImageGallery` is used only by `Cases/Details.cshtml` and
`ImageIntake/Details.cshtml`; the asset route is authorised and hash-verified;
`InstructionEvidenceImages.Select` is the single selection owner; Box custody is
case-scoped.

**Assumed:** that both QDOS triage templates carry photographs as
attachments/embedded images — inherited from [[INTK-033]]'s corpus read, not
re-opened here.

**Assumed, and worth a read-only check before calling option A durable:** that no
blob lifecycle policy ages out intake **asset** artifacts. No deletion code
exists for asset storage keys and no retention rule appears in
`docs/operations.md`, but the deployed storage account's lifecycle-management
rules were not inspected. A comment at `InstructionEvidenceImages.cs:96-97`
(*"a case stops rendering the day its staging blobs age out"*) implies someone
expects aging eventually.
