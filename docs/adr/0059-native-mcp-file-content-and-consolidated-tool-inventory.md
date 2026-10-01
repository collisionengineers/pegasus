---
id: ADR-0059
status: accepted
date: 2026-10-01
supersedes: []
superseded_by: []
related_capabilities: [MCP-01, MCP-02, MCP-03, MCP-04, MCP-06]
related_frd: [frd-10, frd-14]
tags: [mcp, automation, documents]
---

# ADR-0059: Native MCP file content and a consolidated tool inventory

## Status

Accepted. Refines the Automation Actor contract of ADR-0011, ADR-0021 and
ADR-0031; the tool count ADR-0031 names is history.

## Context

Claude Desktop reaches Pegasus through the Automation MCP ingress. The three
download tools returned file bytes as a base64 string inside the structured
result. An MCP client cannot render base64 text; Claude re-typed the string
into its sandbox and corrupted an estimate PDF, and every photograph would
have gone the same way. Its two suggestions were native image content or a
URL its sandbox could fetch.

Observed 1 October 2026: Claude's hosted connector accepts `image` content
blocks but refuses a binary embedded resource (`blob`, so `application/pdf`)
with `-32602` (anthropics/claude-ai-mcp #1086, csharp-sdk #1261), and caps a
tool result at roughly 150k characters. A sandbox-fetchable URL would be a
public capability link, which FRD-10 forbids, and the sandbox's egress is
allowlisted anyway.

The inventory had grown to 47 tools. Twenty-six were named in no document,
thirteen had no description text, twelve had never been exercised by a test,
and every write needed three calls (begin lease, write, end lease).

## Decision

1. **Native content.** A download returns a `CallToolResult` whose content
   blocks the client shows its model and whose structured half keeps the
   file's identity, size, media type, SHA-256 and an authenticated content
   URL. An image is one JPEG image block re-encoded to a byte budget (100 KiB
   by default, longest edge 1568 px) through the existing SkiaSharp decode; a
   PDF is its page text through the existing PdfPig extraction (operator
   ruling: text, not page images); a text file that fits the budget is its
   text; anything else is metadata only. Base64 output is gone. Originals stay
   in custody behind `/automation/documents/...` and the new
   `/automation/intake-sources/{receipt}`, each under its scope's bearer
   policy. No public links.
2. **One-command lease.** Every write tool takes an optional `editLeaseToken`.
   Given none, it claims the record's edit lease through the same Core port
   as staff, runs the one command and releases the lease; the claim is
   refused exactly as a staff claim is while another editor holds the record.
   The explicit lease tools remain for multi-step work.
3. **Consolidation, 47 to 36.** `pegasus_edit_begin/renew/end` with a
   `recordKind` replace the Case and Triage lease trios;
   `pegasus_ai_job_transition` with an `action` replaces take, progress,
   complete, fail and release; `pegasus_triage_record_finding` supersedes when
   it names `supersedesFindingId`; `pegasus_triage_response_evidence` and
   `pegasus_triage_case_link` take Link or Unlink. `pegasus_document_export`
   is removed with its ticket endpoint and the `IExportCaseDocuments` port: a
   ZIP is as unusable to the client as base64, and no other caller existed.
4. Every tool and parameter carries description text; FRD-10 lists the whole
   inventory.

## Consequences

- Claude Desktop sees a photograph and reads an estimate without retyping
  bytes. A PDF's layout is not conveyed; a scanned page is named as needing
  OCR rather than shown.
- Scripted callers read identity, hash and URL from the structured result and
  fetch originals with their bearer token, as before.
- `IExportCaseDocuments`, its Review-only export rule and the ZIP builder
  leave Core and Infrastructure. The staff EVA export (FRD-07,
  `IExportCaseBundle`) is unaffected.
- The Administrator consent descriptions for the Documents and Intake scopes
  now read "Add and download case documents." and "List the intake queue,
  submit intake, and work Unidentified and Triage records." (operator-approved
  wording, 1 October 2026).
- The hosted-connector limitations above are observations of 1 October 2026,
  not Pegasus rules; when the connector accepts binary resources, PDFs may
  travel as resources under a new decision.

## Links

- [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md)
- [FRD-14](../frd/frd-14-record-edit-leases.md)
- [ADR-0011](0011-restrict-mcp-to-automation-actor.md),
  [ADR-0021](0021-automation-actor-direct-write-assessment-contract.md),
  [ADR-0031](0031-automation-actor-contract-without-eva-export-tools.md),
  [ADR-0035](0035-ai-job-ledger.md)
