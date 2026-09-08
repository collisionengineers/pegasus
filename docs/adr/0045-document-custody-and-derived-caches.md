---
id: ADR-0045
status: accepted
date: 2026-09-08
supersedes: [ADR-0029]
superseded_by: []
related_capabilities: []
related_frd: [FRD-02, FRD-05]
tags: [identity, persistence]
---
# ADR-0045: Document custody and derived caches

## Status

Accepted. Partially supersedes ADR-0029's image-reference custody exclusion;
the separate image-origin projection and identity remain accepted.

## Context

Provider transport identities and physical copies must not compete with the
business identity and custody contract.

## Decision

Box owns durable document custody, including image-reference holding folders
and their verified merge into Case custody under FRD-05. Existing intake staging keeps
original bytes until verified handoff; SQL keeps logical identity, version and
provenance. Image/cache copies are derived, rebuildable read/processing data.
Read through the existing logical occurrence/version boundary and authorize
before fetching bytes. Do not add a parallel archive or physical-path bypass.

## Consequences

Preserve attributable occurrences and exact-version authorization. Use existing
Core ports and persistence; this decision requires no new store or runtime.
Implementation and deployed conformance require their own evidence.

## Links

- [Intake and identity](../frd/frd-02-intake-and-source-identity.md)
- [Custody](../frd/frd-05-documents-extraction-and-custody.md)
- [Email](../frd/frd-08-email-mailbox-and-background-processing.md)
