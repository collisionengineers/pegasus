---
id: DOCS-011
type: ticket
title: 'Preview evidence images and documents in the case, with paging and a download'
status: backlog
area: documents-reports
assignee: ''
profile: feature
labels:
  - found-during-qa
  - ui
  - design
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-23T15:19:21.004Z'
updated: '2026-08-23T15:19:21.004Z'
---

## What the operator asked for

> *"Image viewing in-app should work similar to a gallery concept, forward +
> backward buttons on the left and right, and a download option. Clicking an
> image in evidence should not open a new tab for just the image, and it should
> not just download the image. This is the same for documents. Clicking should
> preview the document."*

## What it does today

`Pages/Cases/Shared/_ImageGallery.cshtml` is a thumbnail grid where each tile
is a bare link to the same authorised endpoint:

```html
<a href="@image.Href">
    <img src="@image.Href" alt="@image.FileName" loading="lazy" />
</a>
```

Clicking navigates to the raw file. The document route
(`/Cases/{caseId}/Documents/{occurrenceId}/Download`) returns
`File(content, mediaType, fileName)`, which sets a **download** disposition —
so a document link downloads and an image link opens a bare image. There is no
viewer, no paging, no download control.

The partial's own comment says this was deliberate: *"preview-and-expand works
with no script and stays keyboard accessible."* It is a reasonable no-script
starting point; the operator has now said it is not what the work needs.

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
  content, not a public URL.

## Boundaries worth holding

- The design authority's no-explanatory-copy rules bind: controls, labels and
  at most one consequence sentence. No hints, no empty-state prose.
- This is presentation only. It changes nothing about what is retained, what is
  eligible for EVA, or what custody records.
- [[DOCS-012]] removes the custody detail table from this same tab, so the two
  should be planned together — what survives there is the surface this viewer
  opens from.
