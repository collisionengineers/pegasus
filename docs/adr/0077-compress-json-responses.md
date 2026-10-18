---
id: ADR-0077
status: accepted
date: 2026-10-15
supersedes: [ADR-0057]
superseded_by: []
related_capabilities: []
related_frd: [frd-09, frd-10, frd-12]
tags: [performance, web, security]
---

# ADR-0077: Compress JSON responses over HTTPS

## Status

Accepted on 15 October 2026 by operator decision. The ruling on "Static
sprite vs design README; JSON compression vs ADR-0057" was: "Build both; a new
ADR supersedes ADR-0057's JSON clause; design README amended". This record is
the JSON half.

This record replaces, in part:

- [ADR-0057](0057-compress-staff-html-responses.md), Decision: the limit to
  `text/html` responses only, and the words that keep JSON as it is. The rest
  of ADR-0057 stands: pages are compressed over HTTPS with Brotli and Gzip at
  their fastest level, before routing; files, images and downloads stay as
  they are; and its BREACH acceptance for pages. The body of ADR-0057 stays as
  filed.

## Context

ADR-0057 limited compression to `text/html`, so Web sends JSON uncompressed.

Compressing a response that carries a secret beside text an attacker can
choose exposes the secret to BREACH (see ADR-0057). The JSON bodies Web sends,
at the time of this record:

- The bearer-only surfaces, each mapped only when its feature gate is on: the
  token endpoint `/connect/token`, `/mcp`, the discovery document
  `/.well-known/oauth-protected-resource/mcp`, and
  `/api/principal/v1/submissions`. They take a bearer or client credential and
  no staff cookie. Of their bodies, the token response and the results of
  `pegasus_edit_begin` and `pegasus_edit_renew` carry a secret: the access
  token, and the edit lease token.
- `/diagnostics/version` is anonymous and carries the product version and
  source SHA.
- The staff-cookie JSON from Razor Page handlers: the lease heartbeat, the
  repair-spec scale preview, the document-preview refusal, the valuation
  fetch, the Inbox preview and the Glass state. None of these bodies contains
  an antiforgery token or an edit lease token.
- Two bodies echo text that a sender or staff member supplies: the Inbox
  preview (sender, subject and excerpt) and the scale preview (the submitted
  rows).

## Decision

Web compresses `text/html`, `application/json`, `text/json` and
`application/problem+json` responses, over HTTPS as well, with Brotli and
Gzip at their fastest level. The list is explicit in `Program.cs`; every other
type stays as it is. Compression runs before routing, so every response of
those types is eligible.

## Consequences

- A client that offers `br` or `gzip` receives JSON encoded with it; a client
  that offers no encoding receives the body as before.
- `text/event-stream` is not in the list, so a streamed MCP answer is never
  held back by a compressor.
- Plain-text responses, such as the health endpoints, stay plain.

## Links

- [ADR-0057 — Compress staff HTML responses over HTTPS](0057-compress-staff-html-responses.md)
- [FRD-09 — Principal and intermediary routes](../frd/frd-09-principal-and-intermediary-routes.md)
- [FRD-10 — MCP automation and actor boundary](../frd/frd-10-mcp-automation-and-actor-boundary.md)
- [FRD-12 — Operator experience](../frd/frd-12-operator-experience.md)
