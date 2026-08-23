---
id: CASE-022
type: ticket
title: Make creating a public upload link findable
status: backlog
area: case-reference-workflow
assignee: ''
profile: fix
labels:
  - found-during-qa
  - ui
  - design
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-23T15:19:54.445Z'
updated: '2026-08-23T15:19:54.445Z'
---

## What the operator reported

> *"No method to create an upload link on frontend at all seemingly."*

## What is actually there

The control exists. `Pages/Cases/Shared/_CaseDocuments.cshtml:160-167` renders
a **"Create public upload request"** button, posting to
`/Cases/Custody?handler=CreateRequestUploadLink`, and Core's
`ICreateRequestUploadLink` is composed. Existing links are listed above it with
a revoke control.

The whole section is wrapped in `@if (mayEdit)`, and `mayEdit` is:

```csharp
var mayEdit = !string.IsNullOrWhiteSpace(leaseToken) && workflow.Archive is null;
```

So it appears **only while the operator holds an edit lease** — i.e. after
pressing *Edit case*. To someone reading the case it is not merely disabled, it
is absent. "Seemingly" is doing honest work in the report: the operator looked
and it was not there.

## The judgement to make

Requiring a lease is not obviously wrong — creating a public upload link is a
mutation that changes what the outside world can put into custody, and the lease
is how this codebase serialises case mutations. The defect is **discoverability**,
not authorisation.

Options, in the order I would try them:

1. Render the section for a reader with the button in the same `gated` /
   `is-disabled` shape the Export control already uses on `Details.cshtml`
   (`data-condition="Available in Review"`), so the capability is visible and
   its precondition is stated once. This is the existing convention for exactly
   this situation and needs no new pattern.
2. Move upload requests out of the custody panel entirely, onto the case action
   bar beside Export. [[DOCS-012]] is deleting most of that panel anyway, so
   this may fall out of that work.

Do **not** simply drop the lease requirement to make it visible; that trades a
findability problem for a concurrency one.

## Related

[[DOCS-012]] removes the surrounding custody table and explicitly flags that
this section needs rehoming rather than deleting. Plan the two together.
