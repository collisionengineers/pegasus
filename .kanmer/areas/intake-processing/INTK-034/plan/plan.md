# Plan

## What this fixes

A triage request's damage photographs are viewable **nowhere** in the
application. The Triage page's `View e-mail` link lands on `/Received/{id}`,
which renders filename cards and no images. The engineer is asked to decide
whether a vehicle is repairable or a total loss and cannot open the photographs
that question is about.

## The change

`Pages/Triage/Details.cshtml.cs` loads the origin receipt in `LoadAsync` and
projects its evidence photographs to `GalleryImage`:

```csharp
Images = [.. InstructionEvidenceImages.Select(receipt.AssetRecords)
    .Select(asset => new GalleryImage(
        Url.Page("/Intake/Asset", new { id = receipt.Id, assetId = asset.Id })!,
        asset.FileName))];
```

`Pages/Triage/Details.cshtml` renders one section: a heading and the existing
gallery partial, present only when there is something to show.

**Reused, per step:** the receipt load copies
`Unidentified/Details.cshtml.cs:161-174` — an existing non-intake page that
loads its origin receipt and degrades silently when the reader lacks the right.
The projection copies `Cases/Details.cshtml:186-201`. The selection is
`InstructionEvidenceImages.Select`, already the one owner. The serve route and
the gallery partial are consumed unchanged.

## Why not retain the images under the Triage

Because it would duplicate custody of the same bytes under two identities with
no rule saying which is authoritative — a stop condition, not a preference. It
also needs a fifth Triage table and migration, a copy of a retention path that
is driven by a case payload and writes to a case folder (a Triage has neither),
and a Box location that does not exist because a Triage has no permanent
reference. Inventing one touches protected operator notes. It would need an ADR.
**And it would still need this ticket's UI on top of it.**

Nothing today says triage photographs must survive independently of the e-mail.
`operator-notes.md` § Stage 0 says only that the emails must be stored, which
receipt retention already satisfies.

## Verified, not assumed

- **Assets do not age out.** Read-only check of both production storage
  accounts (`pegcustody252ow37gij`, `pegtrans252ow37gij`): neither has a
  management policy — `ManagementPolicyNotFound`. So a read-only view over
  retained assets is durable, which is what makes option A safe. Had a lifecycle
  rule existed, this plan would have been wrong.
- The staged *source* is deleted after processing; the extracted **assets** are
  not. Only `TryDeleteCompletedStagingAsync` deletes, and it deletes the staged
  source key.

## Authorization

Already correct with no new code. The Triage page is
`[Authorize(Administrator, Engineer, User)]`; the asset route independently
requires `PerformCasework` — the same right the page's own reads use. A reader
without it sees the page without the gallery rather than an error, exactly as
the Unidentified page behaves.

## Design constraints

`docs/design/README.md` binds two things here, both cheap to obey:

- **No explanatory sentence** under the heading. The necessary-copy list is
  closed and contains nothing applicable.
- **No empty-state panel.** A read-only section with nothing recorded is
  *absent*, not a box saying it is empty.

So: a heading, the gallery, and nothing else.

## DOCS-011

Consume the partial; do not edit it. [[DOCS-011]] owns it, and consuming means
this page inherits its viewer with paging and download for free. Sequencing does
not matter; the two never touch the same lines.

## Verification

- `dotnet build --configuration Release`
- `dotnet test tests/Pegasus.Core.Tests`
- `dotnet test tests/Pegasus.IntegrationTests --filter "Category!=Corpus"`
- A web test asserting a Triage whose origin receipt carries photographs renders
  them, and one whose receipt carries none renders no section at all.
- Simplification pass over the branch diff before the PR, recorded here.

## Open question, carried not guessed

Whether the operator wants this recorded in FRD-03 as *required* behaviour
rather than left as an obvious view. Not blocking: the view is right either way.

## Simplification pass — 2026-08-24

Run over the branch's own diff with an independent lens before the PR.

**Applied — one of these was a real defect:**

- **The `try/catch` was dead code carrying a comment that promised protection it
  did not deliver.** It caught `UnauthorizedAccessException`, but rights denial
  in `GetIntake` throws `StaffAuthorizationException` — an unrelated type. And it
  was unreachable twice over: `GetTriage`, two lines above, already required the
  identical `PerformCasework` right from the identical actor, so control cannot
  reach the block without it. I copied the pattern from the Unidentified page
  without checking that its precondition holds here — it does not. That page
  loads its item through an unauthorised store call, so `getIntake` genuinely is
  its first rights check; its own catch is still the wrong exception type, which
  is its ticket, not this one. Deleted, with a comment saying why no guard is
  needed.
- **The test fixture declared `image/jpeg` over PNG bytes.** Green today only
  because nothing probes the bytes against the declared type. Now honest.
- **A test comment's stated reason was wrong.** Two identical attachments would
  not have "passed while proving less" — the second filename assertion would have
  failed loudly. The distinct bytes are needed to have two images at all.
- **`Assert.Contains("/Asset/")` proved only that *some* asset URL rendered.** Now
  asserts the full path for this receipt.

**Considered and deliberately not changed, with reasons:**

- **The projection lives in the view.** It mirrors `Cases/Details.cshtml` exactly
  — same `@{ }` block, same `Url.Page(...)`, same guard, same partial. It needs
  `Url`, which belongs in the view. This is the codebase's convention, twice over.
- **`EvidenceImages` exposes `IntakeAssetRecord` rather than a purpose-built
  projection.** Wider than the `CaseEvidenceImage` the Cases page uses, but
  introducing a second projection record for one caller trades a real rule
  against a cosmetic one. Left as is.
- **`CreateEmail` could collapse its branch through MimeKit's `BodyBuilder`.**
  The explicit construction is correct either way, and `BodyBuilder` also sets
  `ContentType.Name` alongside `FileName` — a harmless header difference, but a
  difference. Not worth the churn. Worth noting the new `ContentType.Parse` is
  strictly better than the existing precedent elsewhere, which splits on `/` and
  would throw on a media type without one.
- **The `Count > 0` guard before the partial is not redundant.** The partial
  renders its own empty-state sentence, which the design authority forbids in a
  read-only view. The second test pins its absence.
