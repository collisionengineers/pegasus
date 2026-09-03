---
id: PLAT-038
type: ticket
title: Serve intake-retained document content in the local profile
status: backlog
area: platform-operations
order: 730
assignee: ''
profile: fix
labels:
  - found-during-qa
  - developer-experience
links:
  - CASE-019
  - DOCS-009
deployment: n/a
archived: false
created: '2026-08-22T23:56:16.433Z'
updated: '2026-09-03T15:15:28.372Z'
---

## The gap

`IDocumentContentStore.OpenReadVersionAsync` is a **default interface method** that delegates to the `versionId` overload:

```csharp
Task<Stream> OpenReadVersionAsync(ManagedDocumentContentAddress address, …) =>
    OpenReadAsync(address.CaseId, address.CaseReference, address.VersionId, …);
```

`BoxDocumentContentStore` **overrides** it and resolves the full occurrence address — the flat ordinal name custody wrote into the case folder. Its `versionId` overload deliberately throws:

> "Managed Box reads require the persisted business occurrence and revision address."

`LocalDocumentContentStore` does **not** override it. It resolves `<caseReference>/<versionId>` on disk, and intake's local custody adapter never writes that layout — it writes its own attachments tree. So a locally accepted case has `DocumentVersions` rows whose content the store cannot find, and any read throws:

```
System.IO.FileNotFoundException : The document content is unavailable.
```

## Why this is dev-only

Production is unaffected: the Box override resolves the address, and Box already holds the file custody uploaded (DOCS-007 registers records without re-sending content). The asymmetry is that one implementation overrides the default and the other relies on it while storing under a different key.

## What it costs

- The case Evidence gallery cannot serve an intake-retained image under `DevelopmentOffline`, so local visual QA of that surface is impossible.
- The same is true of the case-document download route and of [[CASE-019]]'s export.
- Found while writing `ExportingACaseProducesTheEvaFormatArchive`, which had to seed the content into the local store to run at all. That seeding is a stand-in for what Box already holds and is commented as such — it should be deleted when this is fixed.

## Scope

Give `LocalDocumentContentStore` an `OpenReadVersionAsync` that resolves the same occurrence address the local custody adapter writes, so the two agree. Nothing about the Box path or the interface default changes.

## How to verify

A case accepted through intake under `DevelopmentOffline` serves its retained photographs on the Evidence tab and exports them, with no test-only content seeding.
