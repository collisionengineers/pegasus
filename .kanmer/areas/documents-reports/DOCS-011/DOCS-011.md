---
id: DOCS-011
type: ticket
title: 'Preview evidence images and documents in the case, with paging and a download'
status: verifying
area: documents-reports
order: 30
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-24T11:13:35.573Z'
  review: '2026-08-24T11:43:48.351Z'
  verifying: '2026-08-24T14:57:27.374Z'
taken_at: '2026-08-24T11:16:17.083Z'
branch: task/docs-011-evidence-preview
worktree: ../pegasus-worktrees/docs-011-evidence-preview
labels:
  - found-during-qa
  - ui
  - design
links: []
refs:
  - docs/design/README.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/frd/frd-12-operator-experience.md
commits:
  - 992f5d42
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/535'
deployment: production
archived: false
created: '2026-08-23T15:19:21.004Z'
updated: '2026-08-25T00:49:44.415Z'
---

## What the operator asked for

> *"Image viewing in-app should work similar to a gallery concept, forward +
> backward buttons on the left and right, and a download option. Clicking an
> image in evidence should not open a new tab for just the image, and it should
> not just download the image. This is the same for documents. Clicking should
> preview the document."*

## What it does today

**Corrected 2026-08-24 — the path first recorded here was wrong.** The gallery
is `src/Pegasus.Web/Pages/Shared/_ImageGallery.cshtml`, a *global* shared
partial, not `Pages/Cases/Shared/`. It has three callers:
`Pages/Cases/Details.cshtml` twice (instruction photographs; vehicle images per
image-initiated record) and `Pages/ImageIntake/Details.cshtml`.

Each tile is a bare link to the same authorised endpoint:

```html
<a href="@image.Href">
    <img src="@image.Href" alt="@image.FileName" loading="lazy" />
</a>
```

**Also corrected: nothing opens a new tab.** There is no `target="_blank"` on
any evidence link. The observed behaviours are:

- The two receipt routes (`/Received/{id}/Image`, `/Received/{id}/Asset/{assetId}`)
  already set `Content-Disposition: inline` and navigate in the same tab — so a
  click replaces the case page with a bare image.
- `/Cases/{caseId}/Documents/{occurrenceId}/Download` returns
  `File(content, mediaType, fileName)`; supplying a filename forces
  `Content-Disposition: attachment`, so it **downloads**. Since DOCS-007 an
  evidence photograph registered as a case document takes that route, so the
  operator's "it should not just download" applies to photographs as well as to
  documents.

There is no viewer, no paging and no download control. There is no existing
lightbox, carousel or preview surface anywhere in the app.

## What this needs

- A viewer over the case's evidence images: open in place, previous/next, a
  download control, close. Keyboard and `Escape` must keep working — the reason
  the current version is script-free is accessibility, and that must not be
  traded away.
- The same click-to-preview for documents. PDFs are the common case and can be
  previewed inline; anything not previewable falls back to download rather than
  guessing.
- An **inline** disposition for preview, separate from the existing download
  disposition. The route currently only offers the latter. Both must stay
  authorised by the same case-document check — a preview is a read of custody
  content, not a public URL. The inline disposition must be **additive**:
  `QdosCustodialWebTests` asserts the attachment disposition on the plain route.

## Boundaries worth holding

- The design authority's no-explanatory-copy rules bind: controls, labels and
  at most one consequence sentence. No hints, no empty-state prose.
- `docs/design/README.md` already specifies this surface — the **Evidence image
  preview** contract row. This ticket implements an existing spec; it does not
  add one.
- The deployed CSP (`default-src 'self'; object-src 'none'`) forbids inline
  `<script>`, `blob:` URLs and `<embed>`/`<object>`. All script goes in
  `site.js`; PDF preview must be an `<iframe>` pointed at the same-origin route.
- This is presentation only. It changes nothing about what is retained, what is
  eligible for EVA, or what custody records.
- [[DOCS-012]] rewrites the custody detail table in this same tab and is open,
  unmerged, as PR #532. Keep the diff over that file minimal; a rebase is
  expected once #532 merges.
