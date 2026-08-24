# Post-implementation report

**Branch** `task/intk-034-triage-images` · **PR** [#529](https://github.com/collisionengineers/pegasus/pull/529) → `dev`
**Commits** `513981f7` (the change) · `44b56e5e` (simplification pass)

## The gap was worse than the ticket said

I filed this ticket claiming *"nothing is lost today, because the attachments are
retained as receipt assets and the Triage detail page links straight to them."*
The link is real; **the destination shows nothing.**

`Triage/Details.cshtml` renders `View e-mail` → `/Received/{id}`, and that page
renders each asset as a card carrying a filename, kind and media type — no
`<img>`, and no link to the asset route. Its single `<img>` renders only when the
*receipt itself* is an image, which a `message/rfc822` triage e-mail never is.

So a triage request's damage photographs were **viewable nowhere in the
application**, on work whose entire purpose is *"determine if the vehicle is
repairable or a total loss"*. A functional gap, not a convenience.

## What landed

The Triage page loads its origin receipt and renders that receipt's evidence
photographs through the existing gallery. Two files, no domain change.

| Owner | Reused for |
| --- | --- |
| `InstructionEvidenceImages.Select` | Which assets are photographs — the same rule the gallery, custody and [[CASE-021]]'s readiness gate ask |
| `/Received/{id}/Asset/{assetId}` | Serving them — already casework-gated, SHA-256 re-verified, `image/*`-only, inline, `nosniff` |
| `Pages/Shared/_ImageGallery.cshtml` | Rendering. **Consumed, never edited**, so [[DOCS-011]]'s viewer is inherited rather than conflicted with |
| `Unidentified/Details.cshtml.cs` | The pattern for a non-intake page loading its origin receipt |

## Why not retain the images under the Triage

A Triage has **no evidence concept in the domain** — `TriageRecord`,
`TriageDetail` and all four Triage tables carry findings, response evidence and
history, and no assets. Retaining under the Triage would need a fifth table and
migration, a copy of a retention path driven by a case payload writing to a case
folder (a Triage has neither), and a Box location that cannot exist because a
Triage has no permanent reference — inventing one touches protected operator
notes. It would duplicate custody of the same bytes under two identities with no
rule saying which is authoritative, need an ADR, **and still need this UI on top**.

Nothing today says triage photographs must survive independently of the e-mail.

## Verified rather than assumed

**Neither production storage account carries a lifecycle management policy**
(`ManagementPolicyNotFound` on both). Intake assets do not age out, so a
read-only view over them is durable — that fact is what makes this approach safe
rather than merely cheap. Had a lifecycle rule existed, the plan would have been
wrong.

## Design

A heading and the gallery, nothing else. The section is **absent** when there is
nothing to show rather than rendering an empty-state panel, which the design
authority forbids in a read-only view — pinned by the second test. No new
operator-facing sentence; the necessary-copy list is closed.

## Simplification pass — 2026-08-24

Findings and dispositions are in the plan. **It found a real defect in my own
work.**

The `try/catch` around the receipt load caught `UnauthorizedAccessException`, but
rights denial throws `StaffAuthorizationException` — an unrelated type — so it
could never fire. It was unreachable twice over: `GetTriage`, two lines above,
already required the identical `PerformCasework` right from the identical actor.
Its comment claimed the page degrades gracefully for a reader without the right,
which was false on both counts. I had copied the pattern from the Unidentified
page without checking that its precondition holds here — it does not, because
that page loads its item through an unauthorised store call. Deleted, with a
comment saying why no guard is needed.

Also corrected: a fixture declaring `image/jpeg` over PNG bytes (green only
because nothing probes the bytes against the declared type); an asset-route
assertion that proved only that *some* asset URL rendered rather than this
receipt's; and a test comment giving the wrong reason for using two distinct
images — identical bytes would have failed the second filename assertion loudly,
not passed quietly.

**Declined, with reasons:** the projection stays in the view (it mirrors
`Cases/Details.cshtml` exactly and needs `Url`); `EvidenceImages` keeps exposing
`IntakeAssetRecord` rather than a purpose-built projection, because introducing a
second projection record for one caller trades a real rule against a cosmetic
one; and `CreateEmail` keeps its explicit MimeKit construction rather than
collapsing through `BodyBuilder`, which would also set `ContentType.Name` — a
harmless difference, but a difference.

## Verification

| Check | Result |
| --- | --- |
| `dotnet build --configuration Release` | green |
| Web test: photographs render, served from this receipt's asset route | passing |
| Web test: no section at all when the request carried none | passing |
| CI on the pushed SHA | three integration shards, unit, browser |

Local full-suite runs on this machine were contended and produced failures that
pass in isolation; CI on the exact SHA is the authority.
