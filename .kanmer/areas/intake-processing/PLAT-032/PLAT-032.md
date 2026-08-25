---
id: PLAT-032
type: ticket
title: Unify EML and MSG inline-image classification
status: backlog
area: intake-processing
assignee: ''
profile: chore
labels:
  - simplification
links:
  - DOCS-007
  - INTK-030
  - PR-039
  - PR-043
  - PR-044
deployment: n/a
archived: false
created: '2026-08-21T18:17:19.735Z'
updated: '2026-08-25T06:47:12.129Z'
---

## What

Verify and remove drift between the EML and MSG inline-image classification paths in `MimeKitPdfPigOpenXmlIntakeSourceReader`.

## Why

The same business question—whether an embedded MIME/MSG part is an inline image—is implemented separately in the EML reader and the `.DocMsg` path. That is the remaining concrete duplicate from the earlier broad simplification sweep and is the class of drift that produced [[INTK-030]].

## Boundary

- Compare the two current classification rules and their real fixtures before changing either.
- If they express the same rule, give that rule one existing-layer owner and make both formats call it.
- If the formats require a genuine difference, keep the difference explicit and cover it with parity/contrast tests.
- Preserve extraction, custody, and supported format behavior.

Resolved custody routes, the now-consumed retained-mail move fields, speculative page splitting, and unrelated image-definition work are outside this ticket.

## Verification

- [ ] EML and MSG fixtures cover equivalent inline images and their supported format-specific differences.
- [ ] The classification rule exists in one place unless a proven format distinction requires otherwise.
- [ ] Focused extraction tests prove no attachment, custody, or document-routing regression.

## Outcome
