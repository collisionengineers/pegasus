---
id: ADR-0083
status: accepted
date: 2026-10-23
supersedes: []
superseded_by: []
related_capabilities: [MCP-04]
related_frd: [frd-10]
tags: [mcp, automation, documents, custody]
---

# ADR-0083: Stage large Automation documents in parts

## Status

Accepted (operator ruling R203, 22 October 2026, round 41, answering OQ-133
and OQ-202): "Staged parts through MCP tools (`pegasus_upload_begin`,
`pegasus_upload_part`, then an `uploadId` on document-add and market-research
complete; custody written only by the consuming call; a damaged part refused
by its own hash before storing); the two tools wait for the server split".
R301 places them after the split, and R448 accepts the 49,152-byte part, the
24-hour staging life, the scopes and no `uploadId` on
`pegasus_intake_submit`. This record refines ADR-0080 (the two tools are on
the documents server) and leaves ADR-0059 as filed.

## Context

Issue #1474: `pegasus_document_add` and
`pegasus_ai_job_complete_market_research` take the whole document as one
base64 argument of at most 10 MiB. The two PDFs the issue measured, 342,499
and 1,459,873 bytes, are 456,668 and 1,946,500 base64 characters, which the
client cannot place in one argument. ADR-0059 records a model corrupting a
re-typed base64 PDF, and neither tool checked a declared SHA-256.

## Decision

- `pegasus_upload_begin` declares a document of 1 to 10,485,760 bytes and its
  SHA-256 under an `mcp:` operation key. It returns the `uploadId`, the part
  size (49,152 bytes, 65,536 base64 characters), the part count and when the
  upload expires. The same key and declaration return the same upload and
  the parts already received; another declaration under the key is refused.
- `pegasus_upload_part` sends one part with its own SHA-256, and a part whose
  bytes do not match it is refused before anything is stored. Every part but
  the last is 49,152 bytes. The same bytes sent again change nothing; other
  bytes under a stored part number are refused.
- The consuming call takes exactly one of `contentBase64` and `uploadId`. It
  opens the upload whole before any lease or transaction, refuses a missing
  part or a size or SHA-256 that differs from the declaration, and only then
  calls the unchanged custody use case, which writes custody in one
  transaction. Opening deletes and marks nothing, so a replay of the
  consuming call returns the original record while the upload lives.
- Begin, part, open and the sweep write no `DocumentVersions`,
  `DocumentOccurrences`, `CaseDocuments` or Case history row. A refused
  begin or part appends its refusal to history, as other adapter mechanics
  do.
- The staged objects are under `uploads/` in the `transient-intake`
  container, or a folder of the local artifact root offline, each written
  once. The Worker's existing minute timer deletes, about hourly, every
  staged object older than 24 hours, R100's figure.

## Consequences

- The inventory is 67 tools; the documents server serves 12.
- A 10 MiB document takes 214 part calls against the 120 requests a minute
  that every server shares.
- A consuming call replayed after its upload is deleted is refused rather
  than answered with the original record.
- `pegasus_intake_submit` keeps its one argument.

## Links

- [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md)
- [ADR-0059](0059-native-mcp-file-content-and-consolidated-tool-inventory.md)
- [ADR-0087](0087-one-automation-mcp-server-with-per-scope-consent.md)
- Issue [#1474](https://github.com/collisionengineers/pegasus/issues/1474)
