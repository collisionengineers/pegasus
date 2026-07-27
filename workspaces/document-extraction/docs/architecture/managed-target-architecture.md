# Managed target architecture

## Design objective

Build one safe, deterministic managed extraction library for PDF, `.doc`, `.docx`, `.msg` and `.eml`. The repository owns byte-level detection, container traversal, format parsing, semantic extraction and result projection. Projects are created only when an active `EXT-*` unit owns source and tests.

## Dependency direction

```text
CollisionDocNet.Cli
  -> CollisionDocNet.Extraction
       -> CollisionDocNet.Pdf
       -> CollisionDocNet.WordBinary
       -> CollisionDocNet.OpenXml
       -> CollisionDocNet.Email
            -> CollisionDocNet.Model
            -> CollisionDocNet.Storage
                 -> CollisionDocNet.Core
```

Format projects may share owned storage and decoding primitives but cannot call another extraction engine or depend on CollisionSpike. CollisionSpike calls the public extraction package through its own Infrastructure adapter.

## Planned cohesive components

| Managed area | Responsibility | Owned formats/capabilities |
|---|---|---|
| `CollisionDocNet.Core` | Bounded readers, checked offsets, resource budgets, hashing, cancellation, stable identities, issues and version/provenance | All formats |
| `CollisionDocNet.Storage` | Read-only CFB v3/v4, ZIP/OPC, safe decompression and passive embedded-stream primitives | `.doc`, `.docx`, `.msg`; reusable boundaries |
| `CollisionDocNet.Model` | Engine-neutral request/result, ordered text, image assets, control evidence, source locations and outcomes | All formats |
| `CollisionDocNet.Pdf` | PDF object graph, streams, pages, text, images and passive non-payload feature inventory | PDF |
| `CollisionDocNet.WordBinary` | FIB, table/data streams, piece tables, stories, properties, fields, revisions and passive embedded-content inventory | `.doc` |
| `CollisionDocNet.OpenXml` | OPC part graph plus WordprocessingML stories, properties, relationships and embedded-content inventory | `.docx` |
| `CollisionDocNet.Email` | Internet message/MIME parsing and Outlook MAPI compound-file parsing, textual bodies/headers, image parts and nested text/image extraction | `.eml`, `.msg` |
| `CollisionDocNet.Extraction` | One public entry point, byte-level detection, format dispatch, nested extraction and cumulative budgets | Exactly the five top-level formats |
| `CollisionDocNet.Cli` | Headless one-input console adapter, arguments, caller-owned I/O, JSON/evidence-bundle output and exit codes | Operational surface only; no parsing |

Names may change through an accepted ADR when implementation evidence demonstrates a more cohesive boundary. Empty reservation projects are prohibited.

## Public extraction contract

The single entry point accepts:

- caller-owned `ReadOnlyMemory<byte>` or a bounded readable stream;
- filename and declared media type as untrusted hints;
- immutable source identity;
- versioned configuration and resource budgets; and
- cancellation and deadline.

It returns a text-and-image payload plus control evidence:

- detected container and format with evidence;
- deterministic ordered text segments and source locations where available;
- image occurrences with stable IDs, hashes, media types and provenance;
- textual document/message properties, headers and participants where applicable;
- bounded nested text/image results for supported attachments or embedded messages;
- issues ordered by source location and stable code;
- source hash, extractor/specification/configuration versions;
- bounded resource measures; and
- one explicit outcome.

Required outcomes are `Complete`, `Partial`, `Encrypted`, `Corrupt`, `UnsupportedFormat`, `UnsupportedFeature`, `ResourceLimitExceeded`, `Cancelled`, `TimedOut` and `TechnicalFailure`.

`Complete` means complete only for the declared supported subset. Any encountered unreadable, truncated or unsupported evidence branch prevents a complete result.

## Detection and dispatch

One detector owns identification. It uses signatures and validated internal structures, not extensions alone:

- PDF header plus structurally valid object/trailer evidence;
- CFB followed by `WordDocument`/FIB validation for `.doc` or MSG property-storage validation for `.msg`;
- ZIP/OPC followed by content types and a WordprocessingML main part for `.docx`;
- RFC 5322 header syntax and MIME structure for `.eml`.

Ambiguous, polyglot or mislabeled inputs generate deterministic evidence and an explicit outcome. Format handlers do not independently reinterpret routing.

Non-image binary content is never returned as an asset. Descriptors, relationships and hashes may remain as control evidence when required to explain omissions or completeness.

## Nested extraction

Attachments and embedded messages are classified before output. A supported embedded PDF, DOC, DOCX, MSG or EML may receive a nested extraction result when enabled, but only its text and images are emitted. Non-image source bytes remain inside the authoritative parent input. Nesting shares a cumulative byte, decoded-output, object-count, depth, time and memory budget. Each result retains its parent occurrence identity and source hash; a nested failure is visible in the parent result.

## Security boundaries

- Input bytes never select a local path, executable, command line, hostname, URL, output directory or shared profile.
- Macros, scripts, JavaScript, launch actions and embedded programs are passive evidence only.
- External relationships and UNC/network references are recorded but never followed.
- Parsers and decoders use checked arithmetic and explicit per-item and cumulative budgets.
- Cancellation is checked inside traversal, decoding and decompression loops.
- Extracted content and sensitive source names are not logged.
- No hidden fallback engine is permitted.

## Determinism

For the same input bytes, extractor/configuration version and resource class, results have the same detected format, content/asset/issue order, identities and outcome. Duration and host measures are recorded separately and are not hashed into semantic results.

## Generated code

Managed generation is justified only for large stable tables such as PDF operators/encodings, OOXML names, MAPI properties, MIME registries or code-page mappings. Inputs and outputs carry provenance and deterministic hashes. Generated files cannot conceal unsupported source definitions.

## Current implementation boundary

Only `CollisionDocNet.Storage` and focused tests currently exist. They implement strict fixed-header parsing for CFB v3. FAT/DIFAT, mini-stream and directory traversal remain pending, so neither `.doc` nor `.msg` is supported end to end. Other projects appear only when their owning port units enter implementation.

## Runtime surface

The library and CLI are headless and cross-platform. No component references WindowsDesktop, ASP.NET hosting, Office/Outlook automation, an external office-suite runtime, a UI toolkit, a browser engine or a mailbox client. The [CLI contract](headless-cli-contract.md) defines process I/O and packaging without creating a second extraction path.
