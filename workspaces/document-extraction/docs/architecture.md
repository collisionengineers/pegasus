# Document-extraction architecture

## Authority and evidence boundaries

This document is the sole workspace architecture owner for CollisionDocNetExtractor. Product obligations are owned by [requirements](../../../docs/requirements.md), capability boundaries by [capabilities](../../../docs/capabilities.md), unresolved choices by [open decisions](../../../docs/open-decisions.md), accepted architectural decisions by the [decision index](../../../docs/decisions/README.md), and operational and engineering procedures by [operations](../../../docs/operations.md) and [engineering](../../../docs/engineering.md). See the [documentation index](../../../docs/index.md) for canonical navigation and the [workspace index](../../README.md) for repository boundaries.

Architecture statements use these evidence states distinctly:

- **Intended** describes a target contract or planned caller.
- **Implemented** requires source that performs the behavior.
- **Caller-proved** requires a real caller exercising the public boundary.
- **Deployed** requires release or runtime evidence.
- **Accepted** requires the applicable decision and evidence gates to be closed.

A specification, generated table, synthetic fixture, passing unit test, or local review does not by itself prove implementation, a caller, deployment, or acceptance. Exact row-level implementation support is owned only by `docs/compatibility/feature-matrix.md`; this document does not duplicate that matrix.
The retained source and clean-room boundary is
[ADR-0005](decisions/ADR-0005-doc-source-and-clean-room-boundary.md).


## Product boundary

CollisionDocNetExtractor is a safe, deterministic, managed extractor for exactly five top-level format families:

| Family | Required recognition | Extracted content | Passive or unsupported surfaces |
|---|---|---|---|
| PDF 1.0–2.0 | `%PDF-` plus valid object, cross-reference, trailer, Catalog/version, extension, and profile evidence | Ordered text and recoverable embedded or inline images | Encryption, attachments and portfolios, JavaScript/actions, multimedia/3D, and incremental shadowing are inventory-only unless a supported nested source yields text or images. PDF/A, PDF/X, PDF/UA, PDF/E, and related profiles are observed claims, not separate parsers. |
| Word Binary `.doc` | Actual-byte classification, CFB v3/v4, a valid root `WordDocument` FIB, and the selected Table stream | Direct binary text from supported stories and recoverable pictures | Encryption, VBA, OLE, embedded packages, external fields/links, unknown streams, and legacy families are passive or unsupported unless a supported nested source yields text or images. |
| WordprocessingML `.docx` | ZIP/OPC, content types, root office-document relationship, and a Strict or Transitional WordprocessingML main part; or a validated encrypted CFB wrapper | Story, drawing, chart, and diagram text plus recoverable image parts | Macros, ActiveX, OLE, embedded packages, external relationships, and signatures are passive. DOCX is an independent input handler and is never an intermediate representation for `.doc`. |
| Outlook `.msg` | CFB plus valid MSG property streams, storages, and profile invariants | Textual headers, properties, bodies, and inline or attached images | Non-image attachments, protected content, raw RTF, OLE, and opaque properties are passive unless supported nested extraction yields text or images. Typed projections cover mail, reports, meetings and appointments, contacts and lists, tasks, and other Outlook classes while retaining generic MAPI evidence. |
| RFC 5322/MIME `.eml` | Bounded Internet Message Format headers and MIME structure where present | Decoded textual headers, bodies, reports, and MIME image parts | Non-image attachments, TNEF, signatures, certificates, and ciphertext are passive unless supported nested extraction yields text or images. Modern and required obsolete syntax, internationalised headers, MIME entity trees, transfer and charset decoding, nested/report bodies, and passive protected content are in scope. |

Only logical text and independently validated images cross the payload boundary. The product does not produce a converted document or arbitrary attachment archive. Format evidence, properties and participants, relationship descriptors, nested provenance, source locations, structured issues, hashes, version identities, resource measures, and outcomes remain control evidence.

The product does not edit, render, paginate, print, export, or reproduce the originating applications. It has no desktop UI, browser interface, ASP.NET application, hosted service, directory watcher, mailbox client, Office/Outlook automation, external office-suite runtime, browser engine, or hidden fallback engine. OCR, AI classification, mailbox access, and caller business rules are outside this workspace.

Spreadsheet, presentation, drawing, formula, and database families are not target inputs and have no planned application model or parser. When encountered as embedded content, their bytes are not emitted. The extractor may retain a bounded descriptor, hash, and explicit unsupported issue.

## Source and evidence baseline

The extractor is specification-led. Primary specifications define required structures and semantics; owned conformance fixtures and tests convert those obligations into executable evidence. Independent tools and retained source trees are secondary behavioral oracles only. Every implementation unit must record an exact edition, revision, or downloaded artifact hash; “latest” is not reproducible evidence.

The research baseline was recorded on 2026-07-23, Europe/London:

| Area | Pinned baseline and qualification |
|---|---|
| PDF | ISO 32000-2:2020, Errata Collection 3, and archived PDF 1.0–1.7 specifications. |
| CFB | `[MS-CFB]` revision 12.0, 2024-04-23; read-only major versions 3 and 4 shared by `.doc`, `.msg`, and encrypted OOXML wrappers. |
| Word Binary | `[MS-DOC]` revision 12.5, 2026-02-17. The principal retained publication has SHA-256 `2e48b21886ebdd5dcc281c3d9baf1b7841c9f3d6881a153862069bbbc0608d7a`. Supporting baselines are `[MS-CFB]` 12.0, `[MS-ODRAW]` 12.4, `[MS-OLEDS]` 13.0, `[MS-OLEPS]` 9.0, `[MS-OSHARED]` 11.1, `[MS-OFFCRYPTO]` 14.0, `[MS-OVBA]` 15.0, and `[MS-OFORMS]` 9.1. |
| WordprocessingML | ECMA-376 fifth editions, `[MS-DOCX]` 22.1 dated 2025-11-13, and `[MS-OI29500]` 24.0 dated 2026-05-19. |
| Outlook MSG | `[MS-OXMSG]` revision 18.0 dated 2025-05-20, with `[MS-OXPROPS]`, `[MS-OXCMSG]`, and `[MS-OXRTFCP]` as owned supporting inputs. |
| Internet Message/MIME | RFC 5322, RFC 2045 through RFC 2049, and RFC 6532; each extension RFC is pinned by its owning unit. |
| Headless boundary | Microsoft unattended Office automation guidance supports a managed library and CLI without Office, desktop, browser, mailbox, web, or hosted-service dependencies. |

Unknown extensions, profile claims, optional structures, and application-specific properties are retained as bounded evidence. They prevent `Complete` whenever they can affect the declared supported subset; they are never silently ignored.

The installed and pinned development SDK is .NET `10.0.302`; .NET `10.0.300` is also installed.

### Research and corpus controls

- `sample-doc-files/` is not an approved fixture root. Immediate metadata suggests copied profile-style trees and potentially private material. Do not recurse into, read, move, rename, delete, or publish it without a separate provenance and recoverability audit.
- Tests use manifest-scoped fixtures and explicitly approved external corpus paths.
- Corpus tooling must reject profile, cache, application-data, and reparse-point roots before enumeration.
- The DOC specification bundle is retained only under ignored `artifacts/research/doc/2026-07-24/specifications/`.
- `scripts/Acquire-DocSpecifications.ps1` verifies pinned hashes during acquisition and is not part of the offline repository check.
- Distribution rights and external-fixture rights are separate from specification, implementation, and local-test evidence.

## Delivery and caller boundary

A possible Pegasus application integration is a `Pegasus.Infrastructure` adapter calling the public extraction library directly. No such adapter or caller is currently proved; activation requires a separately accepted integration contract.

Current source evidence shows only a development-local Pegasus intake path: `Pegasus.Web` Razor Page `POST /Intake/Upload` calls `Pegasus.Core.Intake.ProcessIntake.ExecuteAsync`. It is enabled only when both the `DevelopmentOffline` runtime profile and `Features:LocalIntake` are active; otherwise `/Intake` returns `404`. This is not production, deployment, or extractor-caller evidence.

`Pegasus.Infrastructure` owns current intake registrations, while `Pegasus.Core` owns business policy and ports. The document-extraction workspace:

- is source-only and independently buildable;
- is not in `Pegasus.slnx`;
- is not referenced by a Pegasus application project;
- has no current application adapter, caller, production consumer, or deployment proof; and
- must remain free of Pegasus Core types.

A future `Pegasus.Infrastructure` adapter requires a separately accepted integration contract and caller-backed proof. `Pegasus.Core` must retain the policy deciding whether unsupported, encrypted, corrupt, or resource-breaching content can lead to case or reference creation.

The integration boundary includes a 10 MB source limit, deterministic reviewable text and images with control provenance, and visible non-complete outcomes.

## Managed target architecture

### Dependency direction

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

Format projects may share owned storage and decoding primitives. They must not call another format engine or depend on Pegasus; `CollisionSpike` is predecessor identity only. Any accepted future Pegasus integration must call the public extraction package through a `Pegasus.Infrastructure` adapter.

Projects are created only when an active `EXT-*` implementation unit owns source and tests. Empty reservation projects are prohibited. Names may change only through an accepted decision supported by implementation evidence demonstrating a more cohesive boundary.

### Logical-to-physical project map

| Project | Architectural responsibility |
|---|---|
| `CollisionDocNet.Core` | Bounded readers, checked offsets, resource budgets, hashing, cancellation, deadlines, stable identities, issues, and version/provenance primitives. |
| `CollisionDocNet.Storage` | Read-only CFB v3/v4, ZIP/OPC, safe decompression, and passive embedded-stream primitives. |
| `CollisionDocNet.Model` | Engine-neutral requests and results, ordered text, image assets, control evidence, source locations, and outcomes. |
| `CollisionDocNet.Pdf` | PDF object graph, streams, pages, text, images, and passive non-payload inventory. |
| `CollisionDocNet.WordBinary` | FIB, Table and Data streams, piece tables, stories, properties, fields, revisions, images, and passive embedded-content inventory. |
| `CollisionDocNet.OpenXml` | OPC part graph, WordprocessingML stories and properties, relationships, images, and embedded-content inventory. |
| `CollisionDocNet.Email` | RFC message/MIME and Outlook MAPI/CFB parsing, textual bodies and headers, images, and nested text/image extraction. |
| `CollisionDocNet.Extraction` | The single public entry point, byte-level detection, dispatch, nested extraction, and cumulative budgets for exactly the five top-level families. |
| `CollisionDocNet.Cli` | One-input headless adapter for arguments, caller-controlled I/O, serialization, evidence bundles, and exit status; it performs no detection or parsing. |

### Current implementation boundary

Current source proves only focused `CollisionDocNet.Storage` work for strict fixed-header parsing of CFB v3. FAT/DIFAT, MiniFAT, mini-stream, directory, and full stream traversal remain pending; therefore neither `.doc` nor `.msg` is supported end to end. Other target projects must not be inferred to exist or work merely because this architecture specifies them.

Current row-level support and test evidence remain exclusively in `docs/compatibility/feature-matrix.md`.

## Public extraction contract

The single public entry point accepts:

- caller-owned `ReadOnlyMemory<byte>` or a bounded readable stream;
- filename and declared media type as untrusted hints;
- immutable source identity;
- versioned configuration and resource budgets; and
- cancellation and deadline.

It returns:

- detected container and format with evidence;
- deterministic ordered text segments and source locations where available;
- image occurrences with stable IDs, hashes, media types, and provenance;
- textual document or message properties, headers, and participants where applicable;
- bounded nested text/image results for supported attachments or embedded messages;
- issues ordered by source location and stable code;
- source hash and extractor, specification, and configuration versions;
- bounded resource measures; and
- one explicit outcome.

Required outcomes are:

| Outcome | Meaning |
|---|---|
| `Complete` | Complete for the declared supported subset only. Every encountered relevant branch was successfully handled, and every ignored branch was validated and proven payload-neutral. |
| `Partial` | Safe useful text or images exist, but a valid unsupported semantic branch prevents completeness. |
| `Encrypted` | A conforming encrypted or protected representation was identified without decryption. |
| `Corrupt` | Bytes contradict the established container or format grammar, including invalid references, ranges, cycles, or exact-size invariants. |
| `UnsupportedFormat` | No supported top-level format profile was established, including valid unrelated containers. |
| `UnsupportedFeature` | The format is established but requires unsupported semantics, or classification is strongly ambiguous. |
| `ResourceLimitExceeded` | A configured item or cumulative budget was exceeded. |
| `Cancelled` | Caller cancellation was observed. |
| `TimedOut` | The deadline was exceeded. |
| `TechnicalFailure` | An internal or environmental failure occurred outside expected document outcomes. |

A handler must not report `Complete` after silently skipping unsupported, unreadable, truncated, ambiguous, or resource-breaching evidence.

## Detection and dispatch

One detector owns identification. Format handlers do not independently reinterpret routing.

The common algorithm is:

1. Apply cancellation, deadline, input, and container budgets.
2. Inspect bytes; filename and media type are hints only.
3. Validate a candidate container before applying an owned format profile. A signature alone is not a successful match.
4. Retain every independently strong top-level match.
5. Do not use candidate order, confidence, extension, media type, or other hints to break a tie.
6. More than one strong match returns `UnsupportedFeature` with stable `AMBIGUOUS_FORMAT` evidence and invokes no parser.
7. Dispatch exactly one strong match, then classify family and acquisition subtype.
8. A valid unrelated container is `UnsupportedFormat`.
9. Damage after a container or format profile is established is `Corrupt`.

A byte/hint mismatch is informational acquisition evidence and does not make an otherwise complete result `Partial`. Printable-byte recovery and raw-text salvage are prohibited.

Top-level recognition requires:

- PDF header plus structurally valid object and trailer evidence;
- CFB plus Word FIB validation for `.doc`, MSG property/profile validation for `.msg`, or validated encrypted-OOXML structures;
- ZIP/OPC plus content types, root office-document relationship, and a WordprocessingML main part for `.docx`; and
- RFC 5322 header grammar and MIME structure for `.eml`.

Valid PDF, DOCX, MSG, or EML bytes named `.doc` route to the actual handler with informational mismatch evidence. A valid MIME `multipart/related` web archive is an Internet Message variant only when the bytes satisfy the complete message/MIME grammar. `.mht`, `.doc`, or a declared media type alone has no routing effect.

Top-level RTF, standalone HTML, plain text, arbitrary bytes, unknown flat Word candidates, and unsupported Macintosh/resource-fork candidates are `UnsupportedFormat`. A valid CFB without Word, MSG, or validated encrypted-OOXML evidence is `UnsupportedFormat`; a valid ZIP/OPC package without a WordprocessingML profile is likewise `UnsupportedFormat`.

## Nested extraction

Attachments and embedded messages are classified before output. When enabled by the caller, a supported embedded PDF, DOC, DOCX, MSG, or EML may receive a nested extraction result. Only nested text and images cross the result boundary.

Non-image source bytes remain within the authoritative parent input. Unsupported embedded content is represented by a bounded hashed descriptor and explicit issue; its bytes are not copied to output.

Nesting shares cumulative budgets for source and decoded bytes, object and part counts, depth, elapsed and CPU time, and memory. Every nested result retains its parent occurrence identity and source hash. A nested failure remains visible in the parent result.

## Security, resource, and determinism invariants

### Security

- Input bytes never select a local path, executable, command line, hostname, URL, output directory, or shared profile.
- Input-chosen filenames and content-disposition names are metadata only.
- Macros, scripts, JavaScript, launch actions, embedded programs, OLE code, and fields are never executed.
- External relationships, links, schemas, transforms, URLs, and UNC/network references may be described but are never followed.
- Unknown streams and storages are bounded passive descriptors, not returned assets.
- Non-image binary content is never returned as an asset.
- Parsers use checked arithmetic and explicit per-item and cumulative budgets.
- Traversal, decoding, and decompression loops observe cancellation and deadlines.
- Extracted content, sensitive source names, and attachment names are not logged.
- No hidden fallback engine is permitted.

### Determinism

For the same input bytes, extractor/configuration versions, and resource class, the result has the same:

- detected container and format;
- semantic text, asset, and issue ordering;
- identities and hashes; and
- outcome.

Duration and host-specific measures are recorded separately and are not hashed into semantic results.

### Generated data

Generation is justified only for large, stable tables such as PDF operators and encodings, OOXML names, MAPI properties, MIME registries, code-page mappings, DOC FIB descriptors, or DOC property opcodes. Inputs and outputs carry provenance and deterministic hashes. Generated files must not conceal unsupported definitions or replace independent expected-value tests.

## Headless CLI target contract

`CollisionDocNet.Cli` is intended as a machine-oriented, one-shot adapter over `CollisionDocNet.Extraction` for scripted extraction, isolated corpus evaluation, diagnostics, and operator verification. Any separately accepted Pegasus integration must use the library directly rather than spawning the CLI as an application runtime.

The CLI does not detect or parse formats. It owns only caller-controlled I/O, argument validation, cancellation, result serialization, safe image materialization, and process exit status.

### Commands

```text
collisiondocnet detect  --input <path|-> [--name <filename>] [--media-type <hint>]
collisiondocnet extract --input <path|-> --output <new-directory> [limits]
collisiondocnet version
collisiondocnet help
```

`-` means standard input. A standard-input invocation requires `--name` as an untrusted hint. Each invocation accepts exactly one source and never expands globs, enumerates directories, watches paths, opens mailboxes, or follows relationships found inside a document.

Paths are resolved before use. URI inputs are rejected. UNC/network input is rejected by default and requires a future explicit caller-policy decision. The output directory must not already exist; the CLI never recursively deletes or silently overwrites a destination.

### Extraction bundle

```text
<output>/
  result.json
  assets/
    <stable-image-id>.<safe-extension>
```

`result.json` is UTF-8 without a byte-order mark and conforms to a versioned schema. It contains ordered text, image descriptors, relative stable-ID image paths, SHA-256 values, and the evidence needed to explain outcome and provenance.

Images are written once, verified against their recorded hashes, and ordered in the result independently of filesystem enumeration. Original and content-disposition names never become paths. Non-image attachments and embedded-object bytes are never materialized.

The CLI creates only an extractor-owned staging directory beneath the caller-selected output parent. It atomically publishes the completed bundle where supported. On cancellation or technical failure it removes only the resolved staging path it created. A structured failure result is retained only when it can be published without misrepresenting an incomplete bundle.

### Standard streams

- `detect` and `version` write exactly one UTF-8 JSON document to standard output for a non-usage invocation.
- `extract` writes a small machine-readable completion envelope containing the outcome and result path.
- Extracted text and image bytes never go to standard output or logs.
- Progress and diagnostics go to standard error and contain stable issue codes, correlation identity, and bounded measures, never extracted content or sensitive source or attachment names.
- `--quiet` suppresses non-error diagnostics but not the completion envelope.

JSON property order, enum spelling, number representation, timestamps, and line endings are versioned and deterministic. Source-generated `System.Text.Json` metadata is the planned route; reflection-based serialization is not assumed.

### Exit codes

| Code | Result |
|---:|---|
| `0` | `Complete` |
| `10` | `Partial` |
| `20` | `UnsupportedFormat` |
| `21` | `UnsupportedFeature` |
| `22` | `Encrypted` |
| `23` | `Corrupt` |
| `24` | `ResourceLimitExceeded` |
| `25` | `Cancelled` |
| `26` | `TimedOut` |
| `70` | `TechnicalFailure` |
| `64` | Invalid usage or configuration; no extraction result exists |

Expected document outcomes are not converted into unstructured exceptions. A technical failure is contained at the process boundary and returns a safe issue or result where possible.

### Limits, cancellation, and packaging

CLI switches select a named, versioned resource class and may lower but not silently raise its limits. The intended surface includes input and decoded bytes, object/part/stream counts, text characters, image count/bytes/pixels, nesting depth, CPU and elapsed deadline, and working-memory ceilings.

`Ctrl+C` requests cancellation through the same `CancellationToken` used by library callers. A second interrupt may terminate immediately; tests must establish whether a valid result bundle can still be guaranteed in that case.

The first intended package is a framework-dependent `net10.0` console executable with managed dependencies and no desktop, web, or Office workload. Later candidates may add:

- RID-specific self-contained packages;
- RID-specific single-file packages after startup, signing, and temporary-extraction analysis; and
- Native AOT packages after trim/AOT analyzers and every format and encoding path pass for each target RID.

These are packaging variants, not alternative engines. Windows x64 and Linux x64 are the first planned host classes. Every additional RID requires separate release evidence.

CLI activation evidence must cover argument and usage snapshots, path containment, existing-output handling, symlink/reparse-point behavior, interruption, standard-input/file-input equivalence, stable JSON and image-bundle hashes, complete exit-code mapping, stdout/stderr leak checks, Windows/Linux framework-dependent smoke tests, and separate opt-in publish tests for every self-contained, single-file, or AOT RID.

# Word Binary architecture

## Ownership and evidence state

The Word Binary target is divided among stable implementation units:

| Owner | Responsibility |
|---|---|
| `EXT-DOC-001` | CFB/Word recognition and core format classification |
| `EXT-DOC-002` | FIB envelope, versions, table selection, encryption classification, and secondary FIB |
| `EXT-DOC-003` | CLX, pieces, low-level properties, and physical/logical mapping |
| `EXT-DOC-004` | Stories, parts, AutoText, controls, and review projection |
| `EXT-DOC-005` | Property framing, storage, effective state, and Data indirection |
| `EXT-DOC-006`–`EXT-DOC-009` | Downstream semantic, field, structure, picture, and image handling |
| `EXT-DOC-010` | Passive OLE, object, macro, and unknown-storage inventory |
| `EXT-DOC-011` | Metadata, custom XML, property sets, signatures, and protected-content descriptors |
| `EXT-DOC-012` | Acquisition evidence and hint handling |
| `EXT-DOC-013` | Public projection, stable evidence, and completeness accounting |

The workspace caller is the single extraction API. A possible future application caller is a separately accepted `Pegasus.Infrastructure` adapter; no real Pegasus application caller is currently proved.

Research state as of 2026-07-24 is narrower than implementation:

- `DOC-R01` binary layout mapping and `DOC-R02` classification behavior are mapped and specified.
- `DOC-R03` text, piece, and story semantics are mapped and specified.
- `DOC-R04` property framing and its specification overlay are mapped but not closed.
- These statements describe specifications and reviewed derived data, not parser support.
- Production source remains a limited framing subset and must not be treated as conforming to the target contracts below.

The repository companions are:

- `doc-fib-atlas.v1.json`: all 183 cumulative `FibRgFcLcb` descriptors.
- `doc-format-classification.v1.json`: the generated classifier decision matrix and profile predicates.
- `doc-text-story-contract.v1.json`: executable text/story decisions and fixture groups.
- `doc-sprm-catalogue.v1.json`: all 322 Word property names and opcodes with framing and ownership metadata.

## Word and acquisition classification

### Recognition thresholds

A supported Word candidate requires:

- a valid CFB v3 or v4 container;
- a root `WordDocument` stream;
- `wIdent=0xA5EC`;
- a structurally coherent complete FIB;
- effective `nFib` equal to `0x00C1`, `0x00D9`, `0x0101`, `0x010C`, or `0x0112`; and
- the root `0Table` or `1Table` selected by `FibBase.fWhichTblStm`.

Detection and parsing must consume one generated version-family table rather than maintain separate numeric ranges.

An exact eight-byte CFB signature establishes a damaged CFB candidate even when later structure is invalid. A valid CFB plus a root `WordDocument` stream of at least two bytes beginning with little-endian `0xA5EC` establishes Word damage from byte 2 onward. A signature alone does not establish a valid Word document.

`fDot` identifies a template subtype. `fGlsy` identifies an AutoText-only subtype. A legal nonzero `pnNext` identifies the specification-defined attached AutoText branch. These remain Word Binary variants, not separate top-level formats. Missing semantics may prevent completeness, but a valid subtype is not corruption.

`fBulletProofed`, `fSeenRepairs`, and `fLiveRecover` are passive repair or recovery state. They neither excuse invalid bytes nor make valid bytes corrupt. External claims that a file was repaired have no classification effect unless represented by specified bytes.

### Cross-format precedence

A valid CFB can independently satisfy Word, MSG, or encrypted-OOXML evidence. The detector retains all strong profiles and refuses parser dispatch when more than one survives.

MSG recognition requires the bounded 32-byte root property header and count/profile invariants from `[MS-OXMSG]` 18.0. That exact MSG revision is recorded in the repository baseline, but no retained hash-pinned publication is present in the DOC research bundle. Publication of MSG-derived fixtures remains blocked by the MSG provenance gate.

Encrypted OOXML requires:

- both required root streams;
- a recognized Standard, Extensible, or Agile `EncryptionInfo` grammar; and
- an eight-byte-length-prefixed, non-empty `EncryptedPackage`.

Stream names alone never establish encryption. Extensible-provider URLs are never retrieved.

### Legacy Word limitation

`[MS-DOC]` 12.5 begins with the Word 97 family and does not define Word 6/95, Word 2 or earlier, or Macintosh grammars. No approved pinned source maps the existing constants `0xA59B`, `0xA59C`, `0xA5DB`, and `0xA5DC` to product versions.

The legacy-classification ADR remains **proposed and unaccepted**. Consequently:

- pre-97 semantic support cannot exceed mapped research;
- no product or version names may be exposed for those constants;
- modern FIB offsets must never be applied to them;
- a stable public legacy classification cannot be claimed as accepted or activated; and
- fixtures derived from an unpinned legacy authority cannot be published as conformance evidence.

The pending proposal would, under the same valid-CFB/root-`WordDocument`/two-byte threshold, classify those values generically as `UnverifiedLegacyWordIdentifier`, return `UnsupportedFeature`, and perform no payload parsing. It would return `UnsupportedFormat` for unknown flat, pre-CFB, Macintosh, or resource-fork candidates and would preserve the common rules for unrelated CFB, identified damage, ambiguity, and hint mismatches.

That proposal may be reopened only after an exact licensed, hash-pinned authoritative grammar, encodings, packaging rules, fixture provenance, security model, and independent oracle are accepted. Its reversibility is the rationale for not delaying Word 97-family work or inventing unsupported product mappings.

## CFB physical ownership

Input-chosen names never become paths. Content is not executed or retrieved. Unknown streams are bounded passive descriptors and are not returned as assets.

| Storage or stream | Presence and authority | Owner and required behavior |
|---|---|---|
| Root CFB storage | Required | `EXT-STO-001`, `EXT-DOC-001`: bounded header, DIFAT/FAT/MiniFAT, directory, and stream traversal. Structural violations are `Corrupt`; an unambiguous valid non-Word CFB is `UnsupportedFormat`. |
| `WordDocument` | Required at root; primary FIB starts at offset zero | `EXT-DOC-002`: parse the selected FIB and referenced text/property pages. Missing, truncated, or invalid FIB is `Corrupt`; never scan for a replacement. |
| Selected `0Table` or `1Table` | Exactly the stream selected by `fWhichTblStm` is authoritative | `EXT-DOC-002`: a missing selected stream is `Corrupt`; ignore an unselected sibling and validate only references typed as belonging to the selected stream. |
| `Data` | Optional and reference-driven | `EXT-DOC-005`, `EXT-DOC-009`: read only bounded referenced operands, PICF, or OfficeArt data. Presence or `fHasPic` alone does not make it an image. Unresolved required payload is `UnsupportedFeature`. |
| `ObjectPool` child storages | Optional | `EXT-DOC-010`: parse passive identity, link, presentation descriptors, and bounded supported nested sources. Never execute or emit arbitrary object bytes. Unknown content that can hide text or images prevents `Complete`. |
| Child `\u0003ObjInfo` | Required in each conforming ObjectPool child | `EXT-DOC-010`: passive ODT parsing only; malformed referenced storage is `Corrupt`. |
| Child `\u0003PRINT` or `\u0003EPRINT` | Optional presentation streams | `EXT-DOC-009`, `EXT-DOC-010`: emit only a validated supported image representation; otherwise retain a descriptor and report unsupported image payload. |
| `MsoDataStore` | Optional custom-XML storage | `EXT-DOC-011`: bounded textual or control evidence only; never resolve schemas or transforms. |
| `\u0005SummaryInformation` and `\u0005DocumentSummaryInformation` | Optional | `EXT-DOC-011`: bounded property-set parsing. Unsupported property types are visible and prevent completeness when required text may be hidden. |
| `encryption` | Conditional on CryptoAPI encryption and flags | `EXT-DOC-002`: classify only; never decrypt. Inconsistent presence is `Corrupt`; a conforming supported encrypted form is `Encrypted`. |
| `Macros` | Optional VBA project root | `EXT-DOC-010`: passive inventory only; never decompress or execute modules for payload, and never emit module bytes. |
| `_xmlsignatures` and `_signatures` | Optional | `EXT-DOC-011`: bounded presence and identity only; no trust claim or signature validation. |
| `\u0006DataSpaces` with `\u0009DRMContent` | Paired protected-content representation | `EXT-DOC-002`: classify without reading protected payload. Broken pairing is `Corrupt`. |
| Unspecified streams or storages | Vendor-extensible | `EXT-DOC-010`, `EXT-DOC-013`: stable bounded descriptors only. Never guess a format from a substring or return raw bytes. Payload ambiguity is `UnsupportedFeature`. |

## FIB envelope

All integer fields are little-endian. Counted arrays use checked arithmetic. Unknown trailing values are bounded and retained with their observed identity; they are never silently reinterpreted as an older known layout. Fields are validated according to their declared type rather than treating every eight-byte slot as an `fc/lcb` range.

| Effective `nFib` | `cbRgFcLcb` | `cswNew` | Complete FIB bytes | Effective-version source | Cumulative layout |
|---:|---:|---:|---:|---|---|
| `0x00C1` | 93 | 0 | 900 | `FibBase.nFib` | `FibRgFcLcb97` |
| `0x00D9` | 108 | 2 | 1,024 | `FibRgCswNew.nFibNew` | through `FibRgFcLcb2000` |
| `0x0101` | 136 | 2 | 1,248 | `FibRgCswNew.nFibNew` | through `FibRgFcLcb2002` |
| `0x010C` | 164 | 2 | 1,472 | `FibRgCswNew.nFibNew` | through `FibRgFcLcb2003` |
| `0x0112` | 183 | 5 | 1,630 | `FibRgCswNew.nFibNew` | through `FibRgFcLcb2007` |

For this family, `csw=14` and `cslw=22`.

| Region | Shape | Required policy |
|---|---|---|
| `FibBase` | 32 bytes | Type every field and bit. Validate `wIdent`, flags, reserved requirements, table selection, encryption, and the secondary-FIB branch. |
| `csw` plus `FibRgW97` | 2 + 28 bytes | Require count 14 and retain every word. `lidFE` is encoding research evidence only; reserved fields follow `[MS-DOC]` section 2.5.3. |
| `cslw` plus `FibRgLw97` | 2 + 88 bytes | Require count 22. Own `cbMac`, all story counts, and reserved/version fields. The checked sum of story CP extents is mandatory. |
| `cbRgFcLcb` plus blob | 2 + 744, 864, 1,088, 1,312, or 1,464 bytes | Select the exact cumulative layout. Ordinal 87 is a FIB-resident `FILETIME`, never a stream range. |
| `cswNew` plus `FibRgCswNew` | 2 + 0, 4, 4, 4, or 10 bytes | Validate the exact family count, determine effective `nFib`, and retain version-specific data. |

`FibBase` owns `wIdent`, `nFib`, `unused`, `lid`, unsigned `pnNext`, document/template/glossary/quick-save/encryption/table flags, `nFibBack`, `lKey`, `envr`, the second flag byte, and reserved fields through byte 31.

The current `WordFibParser` interprets reserved bytes at offsets 20, 24, and 28 as `characterSet`, `fcMin`, and `fcMac`, and reads `pnNext` as signed. This is an implementation defect, not an accepted compatibility convention. `DOC-I01` must remove those interpretations and replace affected synthetic fixtures with specification-shaped layouts.

### Effective version

Read and bound the complete FIB using `[MS-DOC]` section 2.5.15. Use `FibBase.nFib` when `cswNew=0`; otherwise use `FibRgCswNew.nFibNew`. The five-value table controls expected counts and field availability.

A recognized other Word-family value is `UnsupportedFeature`. Contradictory versions, impossible counts, or inconsistent layouts are `Corrupt`.

### Secondary FIB and AutoText

`pnNext` is an unsigned page number. Zero means no attached AutoText FIB. Otherwise calculate `pnNext * 512` with checked arithmetic and use the resulting `WordDocument` offset.

`pnNext` must be zero when `fGlsy` is set or `fDot` is clear. Primary and secondary FIBs must share the CHPX/PAPX BTE ranges and `cbMac` required by the specification.

Traversal uses:

- visited offsets;
- a maximum secondary-FIB count; and
- cumulative byte and work budgets.

A cycle, contradictory alias, or out-of-range FIB is `Corrupt`. A finite traversal exceeding configuration is `ResourceLimitExceeded`.

An AutoText-only FIB owns `SttbfGlsy`, `PlcfGlsy`, and `SttbGlsyStyle`. Their text is required payload under `EXT-DOC-004`. A non-empty unimplemented AutoText range is `UnsupportedFeature`, not an informational warning.

### Quick-save

For effective versions below `0x00D9`, `FibBase.cQuickSaves` is the consecutive incremental-save count. At `0x00D9` and later, the base field must be `0xF`; `FibRgCswNewData2000.cQuickSavesNew` carries the extended count for D9/101/10C and is embedded in `FibRgCswNewData2007` for 112.

`fComplex` means that the last save was incremental. It does not authorize stale or overlapping structures to be ignored. The current FIB, selected Table stream, and current CLX remain authoritative. An unimplemented required branch returns `UnsupportedFeature`, retaining earlier text only as partial evidence.

### Encryption

When `fEncrypted` and `fObfuscated` are set, the form is XOR obfuscation. When `fEncrypted` is clear, `fObfuscated` is ignored. Encrypted, non-obfuscated input requires the bounded Table-stream header that distinguishes binary RC4 from CryptoAPI.

`lKey` is an XOR verifier or, for the other branch, the encryption-header byte count. The initial 68 bytes of `WordDocument` and the required header region remain clear as specified. The extractor reads classification bytes only and never decrypts.

CryptoAPI:

- forbids `ObjectPool`;
- places OLE objects in `Data` behind `FOBJH`; and
- uses `fDocProps` to control `encryption` and summary-property-stream rules.

A conforming protected document is `Encrypted`. Contradictory lengths, header versions, stream rules, flags, or protected-content pairing are `Corrupt`.

## FIB descriptor policy and evidence

`doc-fib-atlas.v1.json` contains all 183 cumulative `FibRgFcLcb` slots, including the ordinal-87 `FILETIME`. Every descriptor records its ordinal, byte offset, introducing layout, minimum `nFib`, field names and value kind, owning stream, grammar and specification section, text/image/control relevance, active-content risk, one `EXT-DOC-*` owner, and support/failure policy.

Policies mean:

- `ValidateAndIgnore` is allowed only for specification-defined unused or deprecated cache fields after their invariants and stream ranges are checked.
- `RequiredSemanticExtraction` means a non-empty unimplemented value prevents `Complete`.
- `PassiveInspectOrUnsupported` can become complete only when later owned research proves the structure cannot hide required text or images.
- The conservative default is `UnsupportedFeatureIfPresent`.
- No slot may be unassigned.

Physical storage ownership is independent of semantic disposition. The reviewed atlas has 143 Table-stream descriptors, four `WordDocument` descriptors, 35 intrinsically no-stream descriptors, and one FIB-resident `FILETIME`. An ignored cache retains its physical stream ownership and range-validation obligation.

`scripts/Generate-DocFibAtlas.ps1` derives the atlas only from pinned `[MS-DOC]` bytes and verifies the publication hash. `scripts/Test-DocFibAtlas.ps1` freezes the descriptor sequence and policy mapping for offline checks. Its optional `-SpecificationPath` mode independently rereads the five source paragraph bands and verifies the exact field sequence.

The independent closure review dated 2026-07-24 covered all 183 descriptors across five layouts and found zero source/atlas stream-ownership mismatches. This proves the reviewed specification map, not parser support.

## Text, pieces, and stories

### CLX and piece-table retrieval

Every supported C1/D9/101/10C/112 document retrieves text through the selected Table stream’s non-empty `Clx`. There is no modern simple-file fallback and no `fcMin`/`fcMac` text route. Both values of `fComplex` require the current CLX. Missing or zero `lcbClx` is `Corrupt`.

A CLX consists of zero or more `Prc` records followed by exactly one final `Pcdt`:

- each `Prc` begins with `0x01`;
- its signed length is nonnegative and no greater than `0x3FA2`;
- its body consists entirely of complete `Prl` records;
- `Pcdt` begins with `0x02`;
- its bounded `PlcPcd` length is exactly `4 + 12n` for positive piece count `n`; and
- no bytes follow the final `Pcdt` inside the CLX.

The `n+1` CP values are unsigned 32-bit integers below `0x7FFFFFFF`, begin at zero, and are unique and strictly ascending. Pieces are decoded in logical CP order, never physical FC order.

Physical source ranges may be discontiguous, descending, shared, or overlapping after incremental saves. This is valid only when every referenced range fits both the `WordDocument` stream and normative `FibRgLw97.cbMac`. Bytes at or after `cbMac` have no semantic meaning.

For a piece-relative `cpDelta`:

```text
uncompressed byte address = fc + 2 * cpDelta
compressed byte address   = fc / 2 + cpDelta
```

All arithmetic is checked. `FcCompressed.r1` and `Pcd.fDirty` must be zero. If `Pcd.fNoParaLast=1`, the referenced text must contain no U+000D paragraph mark.

The model retains piece identity, global and part-relative CP ranges, and exact `WordDocument` byte spans, including a valid surrogate pair split across physically separate pieces.

`Prm0` applies one compact mapped property. `Prm1` is a zero-based index into a preceding CLX `Prc`. Invalid indices or malformed records are `Corrupt`. A valid unimplemented property that can change text, visibility, revision state, symbol meaning, or special-character interpretation prevents `Complete`.

### Encoding

Uncompressed text is UTF-16LE code units. Compressed text consumes exactly one byte per CP and maps each byte to the same-valued Unicode code point except for the 24 substitutions fixed by `doc-text-story-contract.v1.json`.

Bytes `0x80`, `0x8E`, and `0x9E` map to U+0080, U+008E, and U+009E, not Euro, Z-caron, or z-caron.

FIB language, `lidFE`, font charset, Windows code page, and DBCS state never select a different story decoder. East Asian byte pairs remain two CPs. RTL and complex-script properties do not cause visual reordering.

`sprmCSymbol` is a semantic override for special U+0028. Emit `CSymbolOperand.xchar`, retain `ftc`, and do not invoke a font engine or guess glyph mappings.

CP accounting is by UTF-16 code unit. A valid surrogate pair consumes two CPs and four bytes even across a logical piece boundary. For an isolated surrogate, emit U+FFFD for each isolated unit with exact CP and byte evidence, retain other safe text, and return `Corrupt`; `[MS-DOC]` defines no recovery rule that would justify silent acceptance.

### Document parts and guards

Modern Word Binary has seven part counts in this order:

1. main;
2. footnote;
3. header;
4. comment;
5. endnote;
6. main textbox;
7. header textbox.

They form contiguous cumulative ranges. The field between `ccpHdd` and `ccpAtn` is `reserved3`; it must be zero and is never a macro story.

The main part is non-empty and ends with U+000D. If any specialized part is non-empty, exactly one additional U+000D follows the last non-empty part outside every part; it is validated and omitted from output. There is no gap before footnotes or another specialized part. With no specialized part, the final `PlcPcd` CP equals `ccpText` and no outside guard exists.

`PlcfHdd` subdivides the header part. Its first six stories are:

1. footnote separator;
2. footnote continuation separator;
3. footnote continuation notice;
4. endnote separator;
5. endnote continuation separator;
6. endnote continuation notice.

Each main-document section then contributes even header, odd header, even footer, odd footer, first-page header, and first-page footer. A non-empty header story ends in a U+000D guard excluded from its content. Other specialized parts and textboxes are split and anchored by their owning PLCs.

A secondary AutoText FIB has its own bounded CLX and the required shared fields. Named `SttbfGlsy`/`PlcfGlsy` ranges follow primary evidence deterministically. When anchor, name, or range semantics are unavailable, decoded text remains visible as partial evidence rather than being dropped.

### Safe review projection

Projection retains typed lossless tokens before normalizing review text. It never executes fields, follows links, evaluates layout, or retrieves content.

- Tabs emit a tab.
- Paragraph, line, column, and resolved page/section boundaries emit newline.
- Header guards and the outside-part final mark emit nothing.
- Cell boundaries emit tab and row boundaries emit newline only after paragraph properties identify them.
- Picture, drawing, automatic-note, and comment anchors emit no literal text and hand off to their semantic owners.
- Fields require `sprmCFSpec`, valid `Plcfld` agreement, and valid nesting. Emit stored result text, never evaluate instructions. Preserve instruction text as non-primary evidence; do not invent a missing result.
- Structured-document-tag markers emit nothing.
- En and em space specials emit their Unicode space.
- A symbol emits `xchar` with font provenance.
- Unknown special controls emit no raw control byte, remain typed evidence, and prevent `Complete`.
- Existing U+001E/U+001F hyphen assumptions remain unsupported pending conformance or differential evidence.

Canonical review order is:

1. main;
2. anchored footnotes;
3. section-associated headers and footers;
4. anchored comments;
5. anchored endnotes;
6. main textboxes by anchor;
7. header textboxes by owner and anchor;
8. named AutoText.

Until anchor semantics exist, stored-order decoded parts remain visible partial evidence and are never silently omitted.

## Property engine

### Catalogue and opcode framing

`doc-sprm-catalogue.v1.json` is derived from the hash-pinned `[MS-DOC]` publication by `scripts/Generate-DocSprmCatalogue.ps1`. It contains all 322 names and opcodes, decoded bit fields, operand framing, typed grammar and validator identity, legal arrays, five supported `nFib` identities, application conditions, extraction relevance, mutation family and state key, Data-stream targets, source paragraphs, definition hashes, and reviewed owner per row.

The source contains:

- 91 paragraph SPRMs;
- 84 character SPRMs;
- 8 picture SPRMs;
- 59 section SPRMs; and
- 80 table SPRMs.

For `spra` values zero through seven, counts are `25/80/59/41/26/9/75/7`.

Every opcode must round-trip:

```text
ispmd = opcode & 0x01FF
fSpec = (opcode >> 9) & 1
sgc   = (opcode >> 10) & 7
spra  = (opcode >> 13) & 7
```

Operand lengths are:

- `spra=0` or `1`: one byte;
- `spra=2`, `4`, or `5`: two bytes;
- `spra=3`: four bytes;
- `spra=7`: three bytes;
- ordinary `spra=6`: one-byte `cb` plus exactly `cb` bytes;
- `sprmTDefTable` (`0xD608`): UInt16 `cb`, with total operand length `cb+1`; and
- `sprmPChgTabs` (`0xC615`): ordinary framing below `0xFF`, but at `0xFF` checked deleted/added tab-array counts determine the length.

Unknown opcodes may be retained only after their exact boundary is proven. Their relevance remains unknown, so an occurrence in an active range prevents `Complete`.

Version-looking suffixes do not select applicability. Applicability is explicit for all five versions. Six legacy table-shading SPRMs carry the condition that they are ignored above D9 only when table styles are understood. Style permutation, list level, HugePapx placement, header-row continuity, section numbering, and other conditional operations retain row-specific application conditions and validators.

### Legal property arrays

Style arrays are narrower than direct-formatting arrays:

- `UPX-CHPX` excludes the character-style-prohibited reset, style, conditional, and bullet SPRMs identified by the specification.
- `UPX-PAPX` excludes paragraph-style-prohibited style selection, nesting, tab mutation, huge/Data indirection, and conditional SPRMs.
- `UPX-TAPX` applies its exclusion list, including direct table definition, structural cell mutation, and raw-shading records.
- `sprmTIstd` in `UPX-TAPX` is ignored.
- The built-in style-11 `sprmTWidthBefore` exception remains a typed application condition.

A row absent from an array is `Corrupt` if encountered there; it is not treated as direct formatting.

### PRM, PRC, BTE, FKP, and section storage

`Prm0` is exactly the compact table in `[MS-DOC]` section 2.9.215. Reserved `isprm` values are not invented. `isprm=0,val=0` has no effect.

`Prm1` references an already preceding CLX `Prc`. `PrcData.cbGrpprl` is signed, lies in `0..0x3FA2`, and bounds a body wholly composed of valid `Prl` records. Paragraph application retains paragraph-group effects; character application retains character-group effects. Exact PCD/PRC provenance is retained. A referenced PRC is not also reported as unapplied.

`PlcBteChpx` and `PlcBtePapx` have strictly ascending unique FC boundaries. Checked page number multiplied by 512 selects a complete `WordDocument` FKP page. BTE endpoints must agree with the referenced FKP. Aliased pages, shared records, and overlapping logical piece mappings remain explicit rather than being resolved by first match.

`ChpxFkp` rules:

- `crun` is `1..0x65`;
- it owns `crun+1` FCs and `crun` offsets multiplied by two;
- zero offset means defaults; and
- a `Chpx.cb` byte bounds a complete property array.

`PapxFkp` rules:

- `cpara` is `1..0x1D`;
- it owns `cpara+1` FCs and complete 13-byte `BxPap` records;
- zero offset means defaults;
- for nonzero first `cb`, `GrpPrlAndIstd` is `2*cb-1` bytes, leaving `2*cb-3` after two-byte `istd`;
- when the first `cb` is zero, `cb' >= 1` owns exactly `2*cb'` bytes; and
- property heaps do not overlap run metadata or unrelated adjacent records.

`PlcfSed` maps ordered section CPs. Each non-sentinel `Sed.fcSepx` selects a bounded `WordDocument` `Sepx`; its length and complete SPRM array must be valid.

Every physical property interval is normalized across intersecting logical pieces, stories, and semantic boundaries using half-open endpoint ownership. Exact FC, global/story CP, stream, FKP page, record, and property-byte provenance remains stable.

### Effective state

SPRM arrays are ordered transitions. Later applicable entries win unless their grammar defines a different operation. State snapshots retain both the winning value and source.

Paragraph state applies, in order:

1. specification and stylesheet defaults;
2. table-style paragraph properties and conditional state;
3. base paragraph styles, parent first;
4. the current paragraph style;
5. direct PAPX;
6. paragraph-group piece PRM; and
7. list-derived paragraph state.

Character state applies:

1. stylesheet font defaults;
2. table-style character properties and matching conditional character formatting;
3. paragraph-derived character style;
4. the current character style, including valid `sprmCIstd` transitions;
5. direct CHPX; and
6. character-group piece PRM.

Table conditional precedence is horizontal bands, vertical bands, first/last column, first/last row, then corners. Section defaults and ordered SEPX form a separate state.

An `istd` lies in `0x0000..0x0FFD` and selects a non-empty style. `istdBase=0x0FFF` means no parent. Other parent, next, and link references must select valid non-empty styles. Self-reference and cycles are `Corrupt`.

`cupx` and revision forms must match exact style-kind counts. Typed UPX members occur in required order. Even-size padding bytes are zero. Every property array enforces group and opcode exclusions.

### Relevance

Relevance is conservative:

- visibility, revision, field hiding, special/symbol, font, language, and script state are text-critical;
- paragraph, list, table, cell, row, section, and story linkage are structure-critical;
- picture, Data, and OLE discriminator state are image-critical;
- visual decoration, borders, shading, and page geometry are rendering-only only when they cannot change logical text, image identity, or ordering;
- all eight picture-group SPRMs are border properties and are payload-neutral after validation; and
- proofing, UI, printing, and session properties are passive compatibility evidence.

### Data indirection and budgets

Only these SPRMs directly identify Data-stream state:

- `sprmCPicLocation` (`0x6A03`);
- `sprmPHugePapx` (`0x6646`);
- `sprmPTableProps` (`0x646B`).

Huge-PAPX and table-property offsets select bounded `PrcData` with `cbGrpprl >= 10`. A processed huge property terminates the containing array as specified. HugePapx must be first and obeys stricter `GrpPrlAndIstd` constraints.

Chains use checked offsets, visited sets, and cumulative depth, count, and byte budgets. Cycles are `Corrupt`; finite traversal exceeding configuration is `ResourceLimitExceeded`.

Configuration owns cumulative limits for property bytes, PRC bytes, FKP/PLC pages and records, property applications, style depth, Data offsets and dereferences, image/object references, CPU/deadline, and cancellation checkpoints. Earlier safe evidence remains available with the non-complete outcome.

No property can trigger a process, link, path, network request, OLE execution, macro execution, or field evaluation.

### Property failure contract

The following are `Corrupt`:

- truncation or invalid exact sizes;
- descending or duplicate ranges;
- property/table overlap;
- invalid references;
- prohibited array membership;
- style cycles; and
- Data cycles.

Valid unsupported relevant semantics produce `Partial` when useful safe evidence exists, otherwise `UnsupportedFeature`. Bounds, cancellation, and deadlines retain their distinct public outcomes.

`Complete` requires every observed relevant property to be applied and every ignored property to be fully framed, validated, and proven payload-neutral.

`DOC-R04` is not closed. The catalogue supplies deterministic validator and mutation identities, but it does not yet encode every definition-specific numeric domain, cross-field precondition, index range, default, relative/additive operation, or legacy replacement interaction. Named complex operands still depend on typed validators, and generic last-applicable-wins families require a reviewed per-SPRM exception audit. Implementation cannot yet proceed solely from generated tables and state transitions.

## Known source limitations

These are implementation defects or evidence gaps, not accepted compatibility behavior:

### Shared detection

- `HasWordFib` accepts every base `nFib` from `0x0065` through `0x0112` and does not determine effective version.
- Pre-97 markers do not reach a stable public classification and can become generic container corruption.
- Valid unrelated CFB and ZIP containers can become `Corrupt` instead of `UnsupportedFormat`.
- Encrypted OOXML can be accepted from two stream names, lose public format identity, and produce a false DOCX hint mismatch.
- DOCX detection accepts generic `application/xml`, misses macro/template main types, and does not require the package-root office-document relationship.
- The EML probe counts repeated recognized names instead of distinct header evidence, allowing HTML-like false positives.
- Ambiguity lacks direct public tests.
- Current mismatch warnings downgrade completeness.

### FIB and text

- Reserved `FibBase` bytes are exposed as character-set/text-range fields.
- `pnNext` is read as signed.
- All five versions reuse a non-versioned minimal FIB shape.
- `cbMac` is not used for piece bounds.
- A reserved `FibRgLw97` value is exposed publicly as `Macro`.
- The outside-part U+000D is inserted before Footnote instead of after the last specialized part.
- Compressed text uses a CP1252-like table and rejects bytes using fabricated state.
- Isolated UTF-16 surrogates pass silently.
- PRCs are reported as unapplied even when referenced later.
- Raw character values determine controls without effective properties.
- Public DOC locations lose structured CP and part identity and label every issue offset as belonging to the Table stream.

### Property engine

- Production recognizes twelve opcodes across ten semantic categories and seven compact PRM mappings.
- The generic `spra=6` decoder misses both length exceptions.
- PAPX over-reads one byte and omits the `cb=0` form.
- CHPX/PAPX mapping chooses a first physical piece at ambiguous boundaries.
- SEPX, effective state, styles, Data indirection, and cumulative property budgets do not exist.
- Public extraction discards property runs, labels every issue as a warning, and can describe processed ranges as unprocessed.

These limitations are requirements for the applicable `DOC-I01` through `DOC-I05` and `DOC-I10` implementation units, not proof that those units are active or complete.

## Activation and acceptance gates

### FIB and classification

`DOC-I01` cannot claim implementation of the FIB atlas until independent fixtures prove:

1. exact 900, 1,024, 1,248, 1,472, and 1,630-byte FIB boundaries and counted-array values;
2. every descriptor ordinal, including ordinal-87 `FILETIME`, without generic range guessing;
3. truncation at every boundary, bounded unknown tails, and contradictory version/count cases;
4. unsigned `pnNext` at zero, maximum, out-of-range, repeated, and cyclic offsets;
5. AutoText shared-range invariants and glossary routing;
6. pre-D9 and D9+ quick-save invariants; and
7. XOR, binary RC4, CryptoAPI, and malformed-header classification without decryption.

The descriptor generator and fixture generator must not share one unchecked handwritten table. An independent review must compare ordinals with the pinned specification before implementation metadata is accepted.

Classifier fixture groups must cover:

| Group | Required evidence |
|---|---|
| `DOC-T01` | Exact identifiers and five effective versions; every `fDot`, `fGlsy`, and `pnNext` branch; repair flags; and legacy markers only after their provenance and decision gates close. |
| `DOC-T02` | CFB v3/v4 Word, MSG, encrypted OOXML, and unrelated profiles; selected or missing Table; all FIB truncation boundaries; coherent unsupported versions; and profile collisions. |
| `DOC-T03` | Ordinary document, template, AutoText-only, and attached AutoText semantics; repair flags do not change validity. |
| `DOC-T04` | Every supported format mislabeled as DOC; DOC-hinted RTF, HTML, plain, arbitrary, and unrelated CFB input; exact public outcome, candidate and issue evidence, and content-safe diagnostics. |

Tests assert the complete candidate set, evidence codes and offsets, container, family and variant, diagnostic, hint flags, and public outcome. Boundary companions include absent, exact-limit, one-over, every-byte truncation, malformed, and resource-limited forms. Fixture generation must not share unchecked production constants.

Existing tests do not kill changes to the broad `nFib` range, name-only encrypted-wrapper matching, generic DOCX `application/xml` acceptance, template classification, CFB profile collisions, or public DOC/MSG/encrypted/ambiguity dispatch. Those gaps must close before activation claims.

### Text and stories

Independent fixtures must cover:

| Group | Required evidence |
|---|---|
| `DOC-T01` | Five exact layouts, both `fComplex` values, quick-save boundaries, and every CP/FC primitive. |
| `DOC-T02` | Zero, one, and multiple PRCs; Prm0/Prm1; every CLX/Pcdt/PlcPcd boundary; logical/physical permutations; exact and end-plus-one `cbMac`; malformed UTF-16. |
| `DOC-T03` | Each of seven parts alone and combined; all header kinds; secondary AutoText; every control/property combination; exact projection and provenance. |

Expected literals and layouts must be independent of production tables. Each test asserts exact format and part, CP and byte spans, typed token, review text, issue code, severity, location, order, and public outcome.

The existing five-version rows reuse one invalid 34-range layout, the Main+Footnote fixture places the final guard incorrectly, and the character-set fixture enforces a nonexistent decoder. They are not acceptance evidence for the target semantics.

### Property engine

`DocR04ExecutableSpecificationTests` must not call production property parsers or constants. The independent oracle covers:

- all eight framing forms and both exceptions;
- PRM/PRC group filtering;
- PLC/BTE/FKP layouts;
- both PAPX forms;
- SEPX;
- literal cascade snapshots;
- styles and cycles;
- Data indirection and cycles;
- exact budgets; and
- deterministic outcomes.

Generated row tests may consume the committed catalogue, but expected framing and transitions must be independently encoded. `DOC-R04` closes only after the missing executable overlay and independent row tests cover every definition-specific domain and reviewed exception.

## Runtime and release boundary

The target library and CLI are headless and cross-platform. No component may reference WindowsDesktop, ASP.NET hosting, Office or Outlook automation, an external office-suite runtime, a UI toolkit, browser engine, or mailbox client.

Specification mapping, implementation, caller proof, deployment, packaging, and accepted support remain separate gates. Release and operational evidence are governed by [operations](../../../docs/operations.md), implementation practice by [engineering](../../../docs/engineering.md), and operator-facing cautions by [operator notes](../../../docs/operator-notes.md).