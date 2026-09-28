---
id: ADR-0057
status: accepted
date: 2026-09-28
supersedes: []
superseded_by: []
related_capabilities: []
related_frd: [frd-12]
tags: [performance, web, security]
---

# ADR-0057: Compress staff HTML responses over HTTPS

## Status

Accepted, recording the operator's 28 September 2026 decision. It accepts the
BREACH risk stated below.

## Context

Pegasus sends its HTML pages uncompressed. The Case page and the Work Centre
are large, so every page costs more transfer time than it needs. Static files
are already precompressed at build time; pages are not.

Compressing a page that carries a secret beside text an attacker can choose
exposes the secret to BREACH. An attacker who can watch the encrypted traffic
and make the browser send many requests can learn the secret from the
compressed sizes. Pegasus pages carry antiforgery tokens and edit-lease tokens
next to text that staff, e-mail senders and providers supply.

## Decision

Web compresses `text/html` responses only, over HTTPS as well, with Brotli and
Gzip at their fastest level. JSON, files, images and downloads stay as they
are. Compression runs before routing, so every page response is eligible.

The operator accepts the BREACH risk. Pegasus is an internal staff
application. An attacker needs to observe staff traffic on the wire and drive a
signed-in browser at the same time. The antiforgery form token is encrypted
afresh for each response, and every command repeats Core authorization.

## Consequences

- Page transfer shrinks for every staff page, most for the Case page and the
  Work Centre.
- The server spends a little CPU per page. The fastest level keeps it small.
- A later change that puts a long-lived secret in page HTML must review this
  decision first.

## Links

- [FRD-12 — Operator experience](../frd/frd-12-operator-experience.md)
