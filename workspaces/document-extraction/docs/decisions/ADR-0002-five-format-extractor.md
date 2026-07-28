# ADR-0002: custom managed five-format extractor

Status: **Accepted**

Date: 2026-07-23

## Context

The required product is a completely custom, server-safe extractor for PDF, `.doc`, `.docx`, `.msg` and `.eml`. It excludes office-suite products, rendering, export, scripting and platform behaviours that do not contribute to evidence extraction.

The first production consumer remains CollisionSpike's Infrastructure intake path. It needs deterministic, reviewable evidence and visible incomplete outcomes without Microsoft Office automation or an external office-suite runtime.

## Decision

Build one managed .NET 10-or-later extraction library covering exactly the five named top-level formats.

- Own byte-level detection, container traversal, parsing, semantic interpretation and result projection.
- Use one request/result/outcome model and one format-detection owner.
- Share CFB between `.doc` and `.msg`, ZIP/OPC with `.docx`, and bounded decoding/nested-resource controls across all formats.
- Treat primary format specifications and owned tests as authoritative inputs.
- Return ordered text, discrete images, passive non-payload inventory, provenance, issues and an explicit outcome, as narrowed by [ADR-0004](ADR-0004-text-and-image-output.md).
- Extract supported nested attachments only under explicit cumulative depth and resource budgets, emitting only their text and images.
- Make partial, encrypted, corrupt, unsupported and resource-limited results visible.

The executable delivery boundary is defined separately by [ADR-0003](ADR-0003-headless-library-cli.md): a thin one-shot headless CLI over the same library API. `.docx` remains a required input handler; it is not an intermediate representation for `.doc`.

“Completely custom” prohibits delegating production extraction to Microsoft Office, Outlook, external office suites, paid or hosted services, or third-party PDF, Word or email parsing engines. Standard .NET facilities and approved foundational or test packages remain possible when they do not perform the product's format extraction and their licence/security purpose is recorded.

## Explicit exclusions

- Editing, layout reproduction, pagination, rendering, printing and thumbnail fidelity.
- Conversion or export to PDF, HTML, Office, OpenDocument or any other format.
- Calc, Impress, Draw, Math, Base, UNO, desktop UI, printer, extension or general macro-runtime parity.
- OCR, AI classification, mailbox access and CollisionSpike business policy.
- Execution of macros, scripts, JavaScript, launch actions, embedded programs or OLE objects.
- Retrieval of external relationships, URLs, network/UNC references or document-selected local paths.
- Emission of non-image attachments, embedded packages, native OLE streams, executable content, fonts, certificates, ciphertext or other arbitrary binary payloads.

Excluded features can still require passive detection and issue reporting when their presence affects extraction completeness.

## Consequences

- The existing strict CFB v3 header parser remains valid foundation work for `.doc` and `.msg`.
- Programme units and compatibility evidence use `EXT-*` identifiers and are organised by the five formats plus shared foundations and orchestration.
- Complete support is claimed per declared feature set and format, never by source-file count or a generic parity percentage.
- Differential tools are optional test oracles only; no external engine is a production fallback.
- Distribution still requires dependency, fixture, source provenance and licence review.
- Completeness is measured against text and image recovery; safely inventoried non-payload content is not silently treated as extracted.

## Acceptance gates

This ADR authorises planning and managed implementation. It does not by itself approve package distribution, CollisionSpike activation or a claim that any format is fully supported. Those require the compatibility, conformance, security, performance, genuine-data and caller evidence defined by the owning `EXT-*` units.
