# Document-extraction programme

## Authority

This document is the sole owner of programme dependencies, delivery gaps, activation conditions and exit gates for extraction of PDF, binary Word `.doc`, WordprocessingML `.docx`, Outlook `.msg` and RFC 5322/MIME `.eml`.

Canonical ownership is split as follows:

- [Requirements](../../../docs/requirements.md) own product obligations and exclusions.
- [Capabilities](../../../docs/capabilities.md) are the only current support and evidence-state matrix.
- [Open decisions](../../../docs/open-decisions.md) own unresolved choices.
- [Architecture](architecture.md) owns technical contracts and design rationale.
- [Operations](../../../docs/operations.md) owns executable operational procedures.
- [Engineering](../../../docs/engineering.md) owns repository-development and test practice.
- [Operator notes](../../../docs/operator-notes.md) own approved local-data handling.
- [Decision index](../../../docs/adr/README.md) owns ADRs and supersession.
- Git history owns accepted change records.
- [Design index](../../../design/README.md), [reference index](../../../docs/reference/README.md), [workspace index](../../README.md) and [documentation index](../../../docs/index.md) provide canonical navigation.

This document does not maintain a competing status ledger. Historical source-tree, test or research records below are evidence qualifications only. Intended, mapped, specified, source-present, locally verified, conformant, differentially verified, caller-proved, deployed and accepted are distinct states.

## Fixed programme boundary

| Area | Boundary |
|---|---|
| Inputs | Exactly five families: PDF 1.0–2.0, binary Word `.doc`, independent WordprocessingML `.docx`, Outlook `.msg`, and RFC 5322/MIME `.eml`. PDF 2.0 is not treated as the only PDF input. |
| Detection | One byte-level detector decides actual format without trusting filename, extension or media-type hints. It must preserve deterministic evidence for ambiguity, mislabelling, truncation and polyglots. |
| Public surface | One versioned managed-library request/result boundary and one thin, one-input, one-shot headless CLI. No second parser path or format-specific public entry point. |
| Runtime | Managed C# on .NET 10 or later. The ordinary package baseline is framework-dependent Windows/Linux library and CLI output. |
| Outputs | Ordered text, safely recoverable discrete images, control evidence, issues, measurements and explicit outcomes. Non-image binaries are never public assets. Nested output is also text-and-image-only. |
| Result invariants | Immutable requests/results; deterministic normalization, ordering, configuration identity, issue ordering, source locations, image identities and versioned JSON/evidence manifests. Identical input and configuration must produce byte-stable semantic output. |
| Active content | No macro, script, action, field, OLE, form, link, input-selected command, process, path or network execution. External content is never retrieved. XML entities are denied. HTML and RTF are interpreted inertly. |
| Containers | CFB, ZIP/ZIP64, OPC, XML and MIME traversal are read-only, bounded and hostile-input aware. Reject invalid ranges, cycles, cross-links, path traversal, uncontrolled recovery, expansion abuse and oversized counts. |
| Nesting | Supported PDF, DOC, DOCX, MSG and EML attachments may be recursively extracted under one cumulative budget. Preserve parent occurrence, original hash, nesting path and failure propagation. Unsupported embedded formats remain bounded hashed descriptors with issues; their bytes are not emitted. |
| Resource control | Checked arithmetic; per-operation and cumulative bounds for source bytes, decoded bytes, text, images, objects, parts, properties, nesting depth, time and concurrency; prompt cancellation/deadlines; no cross-operation mutable state. DOC additionally requires a temporary-file prohibition. |
| CLI | File or stdin input, caller-selected output directory, atomic deterministic bundles, stable image names, disciplined stdout/stderr, exit codes and Ctrl+C. No recursive input discovery. |
| Excluded product surfaces | No desktop or browser UI, web API, hosted service, daemon, watcher, mailbox client, Office/Outlook automation, arbitrary converter, document writer, OCR or general pixel rendering. |
| Pegasus boundary | The extractor remains engine-neutral and free of Pegasus business models or case-creation policy. Only the adjacent Infrastructure adapter may translate results. Real Web and Worker calls and caller-owned zero-false-case-creation gates are required before caller activation is claimed. |
| Caller input class | The currently declared Pegasus source class is at most 10 MiB per source. Decoded-output, object, image, memory, CPU, timeout and concurrency budgets still require measured justification. |
| Optional packaging | Self-contained, single-file and Native AOT outputs are deferred seams. Each authorised RID/variant requires separate evidence; framework-dependent success does not qualify them. |

The production DOC route reads CFB/FIB/CLX/property structures directly. It must never create DOCX, XML or another intermediary. DOCX remains a required independent handler.

## Evidence vocabulary

| Label | Meaning |
|---|---|
| `Unmapped` | Responsibility, specification or dependency evidence is missing. |
| `Mapped` | Responsibility, primary references, dependencies and security surface are recorded. |
| `Specified` | Managed behavior, outcomes and acceptance tests are reviewable. |
| `Implemented` | Managed source exists in the working tree; this alone proves neither a real caller nor acceptance. |
| `Locally verified` | Named checks pass on stated inputs and host. |
| `Conformant` | Declared specification tests pass for the stated subset. |
| `Differentially verified` | Semantic comparison against an exact-version independent oracle passes within declared tolerances. |
| `Called` | The intended real caller reaches the implementation. |
| `Accepted` | An authorised reviewer accepts the precisely stated evidence and scope. |

No format may be described as completely supported while any declared capability row is unsupported, partial or unverified. Raw source counts, readable happy-path text, a green build, fixture counts or aggregate test counts do not establish semantic support.

A unit may enter implementation only when its dependencies are sufficiently specified and its specification, provenance, licensing, security and resource boundaries are understood. Research and fixture preparation may run ahead; implementation may not bypass the shared detector, outcome model or cumulative resource controls. Tests and [capability entries](../../../docs/capabilities.md) change in the same implementation slice.

## Specification and provenance baseline

Exact published PDF or DOCX specification artifacts must be downloaded before implementation, retained only under ignored research artifacts unless redistribution is separately authorised, and recorded with retrieval date, source URL, revision and cryptographic hash. Online pages are navigation aids, not immutable implementation baselines.

| Citation | Revision/date to pin | Owned questions |
|---|---:|---|
| `[MS-DOC]` | 12.5 / 2026-02-17 | Word 97-family FIB, stories, text, properties, structures and host-specific OfficeArt integration |
| `[MS-CFB]` | 12.0 / 2024-04-23 | Compound storage and stream invariants |
| `[MS-ODRAW]` | 12.4 / 2025-08-19 | OfficeArt hierarchy, shapes, BLIPs and image encodings |
| `[MS-OLEDS]` | 13.0 / 2024-04-23 | Linked and embedded OLE structures |
| `[MS-OLEPS]` | 9.0 / 2024-04-23 | Property-set headers, sections, types, dictionaries and code pages |
| `[MS-OSHARED]` | 11.1 / 2025-11-13 | Shared Office properties, signatures and common objects |
| `[MS-OFFCRYPTO]` | 14.0 / 2026-02-17 | XOR, binary RC4 and CryptoAPI classification boundaries |
| `[MS-OVBA]` | 15.0 / 2026-05-19 | Passive VBA inventory only |
| `[MS-OFORMS]` | 9.1 / 2025-08-19 | Passive Office Forms inventory and safe textual values |
| PDF family specifications | Exact declared ISO 32000 revisions and applicable extensions/profiles | PDF 1.0–2.0 syntax, extraction features, profile claims and passive surfaces |
| ZIP and ECMA-376 Part 2 | Exact declared editions | ZIP/ZIP64, OPC content types and relationship graph |
| ECMA-376 WordprocessingML | Exact declared editions | Strict/Transitional semantics and Markup Compatibility |
| Published MSG/MAPI specifications | Exact declared revisions | CFB Outlook Item storage, property types, item classes and attachments |
| RFC 5322/MIME family | Exact declared RFC revisions and compatibility profiles | Message syntax, internationalised headers, MIME, transfer encodings, protected content and reports |

The provenance record must identify every normative reference reached from `[MS-DOC]`, each generated table’s exact source section, applicable copyright/patent notices, and whether each dependency, copied or derived artifact, fixture and oracle is redistributable. Mapping does not approve copied code, fixture publication or packaging. No upstream parser source is a design authority; source code must not be mechanically translated.

Source-recorded decisions include ADR-0002 and ADR-0003 as accepted on 2026-07-23 for the five-format and headless library/CLI boundaries, ADR-0004 for the text-and-image-only boundary, and ADR-0005 for internal specification-led DOC research. Product licensing, patent treatment, notices and distribution remain separate release gates. ADR-0006 was recorded as proposed: pre-97 parsing was not authorised, product-version attribution was prohibited, and a retained `[MS-OXMSG]` hash remained a fixture-publication provenance gate. Live decision state belongs in the [decision index](../../../docs/adr/README.md) and [open decisions](../../../docs/open-decisions.md).

## Dependency, gap, activation and exit plan

The order below favours useful vertical slices while preserving common hostile-input foundations. It is an implementation dependency order, not a historical progress ledger.

| Order | Port units and responsibility | Dependencies and activation condition | Required gap closure | Evidence and exit gate |
|---:|---|---|---|---|
| 0 | `EXT-GOV-001` five-input scope, source registry, compatibility and evidence governance.<br>`EXT-LIC-001` dependency, source, oracle, fixture, notice and distribution review. | `EXT-GOV-001`: none.<br>`EXT-LIC-001`: `EXT-GOV-001`.<br>Activate only after exact scope, primary revisions, status vocabulary, provenance schema, dependency policy and fixture rules are reviewable. | Freeze the five families, PDF version family, headless boundary, text/image payload, outcome vocabulary and ownership. Resolve contradictory active documentation. Record every active artifact’s owner and provenance route. | Accepted ADRs; every planned unit linked to a specification and evidence route; one capability vocabulary; safe machine-readable provenance/fixture manifests; authorised distribution decisions. Mapping alone is insufficient. |
| 1 | `EXT-FND-001` bounded random/sequential input, checked ranges, budgets, cancellation/deadlines, hashing, diagnostics and stable IDs.<br>`EXT-FND-002` checked binary/text primitives, endian values, offsets, dates, code pages and conversion.<br>`EXT-FND-003` deterministic normalization, ordering, registries and source locations.<br>`EXT-DET-001` structural five-format detector.<br>`EXT-STO-001` strict CFB v3/v4.<br>`EXT-STO-002` bounded ZIP/ZIP64 and OPC.<br>`EXT-STO-003` passive OLE property sets/descriptors.<br>`EXT-STO-004` bounded namespace-aware XML with entity denial and spans.<br>`EXT-MOD-001` immutable text/image/control/issue/outcome model. | `FND-001←GOV`; `FND-002←FND-001`; `FND-003←FND-001,002`; `DET←FND-002`; `STO-001/002/004←FND-001,002`; `STO-003←STO-001`; `MOD←FND-001,003`.<br>No handler may bypass these owners. | Complete strict CFB header, DIFAT/FAT, miniFAT, directory, stream and invariant validation; ZIP central/local records, ZIP64, supported compression, OPC graph; passive OLEPS; secure XML; ambiguity/polyglot/mislabelling classification; cumulative counters; ordered text segments, image assets, control evidence and text/image-only nested results. | Unit, property, conformance, corrupt-input, cancellation and exact resource-boundary tests for every bound and outcome; storage fuzzing; deterministic retry; no unbounded scans, expansion, cycles, cross-links or traversal; byte-stable semantic JSON for identical input/configuration. |
| 2.0 | DOC architecture for `EXT-DOC-001..013`; `DOC-R00`, `DOC-R01`, `DOC-R09`; then `DOC-I00`. | Depends on order 0–1. Research output may become `Mapped` or `Specified`, never `Implemented` merely by documentation.<br>`DOC-I00←DOC-R00,R01,R09`. | Pin sources/rights; catalogue all 183 cumulative FIB descriptors for C1, D9, 101, 10C and 112; map CFB ownership, secondary FIB, AutoText, quick-save and encryption; threat-model overflows, aliases, cycles, property indirection, OfficeArt depth, image expansion, encoding and overlapping anchors. `DOC-GAP-022`: decide by ADR whether to rename the historically named `CollisionDocNet.Writer` project; any rename is one bounded migration with no wrapper or parallel parser. Add typed descriptors, spans, coverage states, issue severities, semantic stories/anchors/images/passive descriptors and one owner for every DOC limit. | Approved source/provenance record with no unpinned normative dependency; every FIB-reachable byte has an owner and failure policy; measurable budget hypotheses; accepted model/generated-table provenance tests. This stage claims no parser behavior. |
| 2.1 | `EXT-DOC-001` actual-format/Word-family classifier.<br>`EXT-DOC-002` streams, FIB/version and encryption gate.<br>`EXT-DOC-012` pre-97 decision.<br>`DOC-R02`; then `DOC-I01`. | `DOC-001←DET,STO-001`; `DOC-002←DOC-001`; `DOC-012←DOC-001`.<br>`DOC-I01←DOC-I00,DOC-R02`. | `DOC-GAP-001`: define precedence for supported Word, MSG, encrypted OOXML, RTF, HTML/MHTML, text, PDF, OOXML, templates, corrupt CFB, repaired files and polyglots without trusting hints.<br>`DOC-GAP-002`: replace generic FIB ranges and the hard-coded index-87 exception with versioned field name/type/stream/version/invariant/payload ownership; retain unknown tails without treating them as Table ranges.<br>`DOC-GAP-003`: classify obfuscation, binary RC4 and CryptoAPI without protected-text reads; define malformed headers, `pnNext`, template/AutoText/glossary traversal and cycle/depth limits.<br>`DOC-GAP-021`: research Word 6/95, earlier and Macintosh variants independently; do not infer them from Word 97 semantics. | Executable detector decision table for every branch; supported versions and mislabeled, encrypted and pre-97 classes pass primitive, structure, semantic and public-boundary tests. Until an accepted `EXT-DOC-012` decision authorises more, Word 6/95 and earlier remain explicit `UnsupportedFeature`; filename never overrides bytes. |
| 2.2 | `EXT-DOC-003` CLX/Pcdt/PlcPcd, CP/FC mapping and direct text.<br>`DOC-R03`; then `DOC-I02`. | `DOC-003←DOC-002,FND-002`.<br>`DOC-I02←DOC-I01,DOC-R03`. | `DOC-GAP-004`: specify simple/complex-file algorithms, every CLX rule, PRC/complex PRM indexing, quick-save and legal trailing data; add a simple-file route only if required by the pinned specification.<br>`DOC-GAP-005`: resolve code pages from FIB/font/language/symbol properties; cover exact compressed-byte substitutions, DBCS, RTL/complex script, invalid bytes, surrogates and deterministic replacement. Preserve exact stream/byte/FC/global-CP spans and physical/logical ordering. | Executable specification tests for every supported `nFib`, piece encoding and story class; conformance and differential evidence including physically disordered pieces; deterministic diagnostics for malformed encoding. |
| 2.3 | `EXT-DOC-004` story graph, controls, anchors and locations; then `DOC-I03`. | `DOC-004←DOC-003`.<br>`DOC-I03←DOC-I02`; story semantics depend on the specified `DOC-R03` contract. | `DOC-GAP-006`: model all subdocuments, separators/continuations, headers by section/type, footnotes, endnotes, comments, textboxes, AutoText and secondary FIB. Decide whether macro-story text is payload or passive evidence.<br>`DOC-GAP-007`: replace generic control issues with paired/nested/overlap state machines for fields, notes, cells, pictures, objects and section/page markers; define current, stored, deleted and hidden projection without field evaluation. | Every story and anchor family passes isolation, cross-story, malformed, overlap, separator and quick-save tests; deterministic source locations; missing or unresolved payload-bearing branches force a visible non-complete outcome; no silent text loss. |
| 2.4 | `EXT-DOC-005` bounded PLC/FKP/PRM/SPRM property engine.<br>`DOC-R04`; then `DOC-I04`. | `DOC-005←DOC-003`.<br>`DOC-I04←DOC-I02,DOC-R04`. | `DOC-GAP-008`: complete SPRM catalogue by `sgc`, opcode, version, operand grammar and payload relevance, including special variable forms; independently prove skip length for unsupported opcodes.<br>`DOC-GAP-009`: define PRM, CHPX, PAPX, SEPX, large PAPX and `Data` indirection; apply defaults, styles and direct formatting in specified order with version precedence, cycles, depth and FC/CP normalization.<br>`DOC-GAP-010`: replace generic PLC/FIB range guessing with type-owned parsers and classify each branch as absent, semantic, safely passive, unsupported payload-bearing, corrupt or limited. | Property outputs are semantic rather than presence-only. Mutation and truncation tests cannot silently desynchronise a property stream. Exact count/depth/byte limits and cancellation checkpoints pass. |
| 2.5 | `EXT-DOC-006` styles, fonts, lists, paragraphs, tables and sections.<br>`DOC-R05`; then `DOC-I05`. | `DOC-006←DOC-005,MOD`.<br>`DOC-I05←DOC-I03,I04,DOC-R05`. | `DOC-GAP-011`: implement style defaults/inheritance/linked styles, font/code-page/language graphs, run/paragraph boundaries, list definitions/overrides/labels/restarts, nested tables/cells/merges and section/header associations; detect graph cycles and invalid references. No pagination, rendering or layout reproduction. | Feature-by-feature positive, malformed-graph, conformance and differential evidence for every declared `EXT-DOC-006` capability. |
| 2.6 | `EXT-DOC-007` fields, bookmarks, links, forms, SDTs and external references.<br>`DOC-R06`; then `DOC-I06`. | `DOC-007←DOC-004,DOC-005`.<br>`DOC-I06←DOC-I03,I04,DOC-R06`. | `DOC-GAP-012`: pair nested instruction/result ranges, bookmarks and permission ranges; decode supported hyperlink, reference, mail-merge, form and SDT text; record DDE, INCLUDE, template, link and external targets without execution or retrieval. | Range-joining, malformed-anchor, public-projection and hostile active/external-content tests pass. |
| 2.7 | `EXT-DOC-008` notes, comments and revisions; then `DOC-I07`. | `DOC-008←DOC-004,DOC-005`.<br>`DOC-I07←DOC-I03,I04,DOC-R06`. | `DOC-GAP-013`: join references to story ranges; decode comment authors, times and replies and insertion/deletion/move/property revisions; preserve current/deleted/hidden identity-critical text according to the declared policy. | Conformance and holdout-focused tests prove identity-critical review evidence is not silently omitted. |
| 2.8 | `EXT-DOC-009` textboxes, OfficeArt, pictures and equations.<br>`DOC-R07`; then `DOC-I08`. | `DOC-009←DOC-005,STO-003`.<br>`DOC-I08←DOC-I03,I04,DOC-R07`. | `DOC-GAP-014`: follow CHPX `sprmCPicLocation` to bounded PICF/OfficeArt; parse inline/floating anchors, BLIP stores and supported JPEG/PNG/TIFF/DIB/WMF/EMF paths; validate signatures, lengths, decompression and budgets before emission.<br>`DOC-GAP-015`: traverse OfficeArt with record/depth limits; associate shapes, alternate text and textbox stories; decide safe vector emission and equation text/image treatment. A raw `Data` stream is never an image. | Byte-accurate inline, floating, duplicate, malformed and expansion cases; exact media type, anchor, provenance and stable identity; hostile pseudo-images suppressed; raw drawing/data streams never mislabeled. |
| 2.9 | `EXT-DOC-010` OLE, embeddings, VBA and Office Forms.<br>`EXT-DOC-011` metadata, custom XML, settings and signatures.<br>`DOC-R08`; then `DOC-I09`. | `DOC-010←STO-001,STO-003,DOC-005`; `DOC-011←STO-003,DOC-002`.<br>`DOC-I09←DOC-I01,I04,DOC-R08`. | `DOC-GAP-016`: parse only bounded OLE identity/link/presentation evidence and exact supported child-source boundaries; do not expose arbitrary package, object, VBA or Form bytes.<br>`DOC-GAP-017`: complete OLEPS byte order, format IDs, multiple sections, dictionaries, code pages, vectors, user-defined properties, padding and relevant scalar types; add DOP/settings, custom XML and signature-presence semantics without trust claims. | Every optional stream/storage has a payload, nested or safely passive owner and defined completeness effect. Nested text/images and descriptors pass security, byte-suppression and no-activation tests. |
| 2.10 | `EXT-DOC-013` DOC projection, security, fuzz, conformance, differential, corpus, performance and acceptance evidence; then `DOC-I10`. | `DOC-013←DOC-001..012,MOD`.<br>`DOC-I10←DOC-I03..I09`. | `DOC-GAP-018`: project story, CP/FC/byte provenance, text state, anchors, image identities, passive descriptors and issue severity through the common model; retain global/story CP detail; no DOC-specific API.<br>`DOC-GAP-019`: map every public `ResourceLimits` field to one DOC owner, use checked cumulative counters, reconcile decoded bytes and actual parser work, and exercise exact-limit/one-over, cancellation, timeout and concurrency through API and CLI.<br>`DOC-GAP-020`: replace blanket warnings with observed-feature coverage and deterministic completeness rules. Informational passive evidence need not fail completeness; semantic omission must never be informational. | Library/CLI equivalence; accurate measurements; all outcomes; exact limits; cancellation/deadline; deterministic ordering; text/image-only payload. `Complete` requires every observed payload-bearing branch to be handled. |
| 2.11 | DOC evidence closure: `DOC-R10`; then `DOC-I11`, still owned by `EXT-DOC-013`. | `DOC-I11←DOC-I10,DOC-R10`. | Build independent specification-derived generators that do not share parser tables; approve only minimal provenance-safe fixtures; assess an exact Apache POI HWPF version as an isolated test-only candidate oracle; review any second oracle separately; define comparison normalization for story/category text, state, metadata, image hashes/types, anchors, outcomes and allowed deviations. No oracle is callable from production. | Complete `DOC-T01`–`DOC-T08`; isolate opt-in oracles; explain every deviation; retain minimized provenance-safe synthetic fuzz reproducers. Declared subset is conformant and differentially verified with no unresolved critical/high test-gap finding. |
| 2.12 | DOC genuine-data and performance closure: `DOC-R11`; then `DOC-I12`, owned by `EXT-DOC-013`. | `DOC-I12←DOC-I11,DOC-R11`. | Use only an explicit operator-approved local path and manifested hashes; never recurse through `sample-doc-files/`. Census features without logging text, filenames or operational identifiers. Select cohort and implementation-author-hidden holdout by content hash, stratified by `nFib`, stories, encodings, structures, images, active/passive content and damage. Measure the 10 MiB class, worst-case expansion, allocation, CPU, elapsed time, cancellation and concurrency. | Immutable pre/post snapshot manifests; zero silent truncation of identity-critical text/images; accepted budgets; stable retries; no corpus drift; independent review by someone other than implementation/test author; authorised acceptance for the precisely declared Word 97-family subset. |
| 3 | `EXT-API-001` one public request/result API and dispatch.<br>`EXT-API-002` deterministic versioned JSON/evidence manifest.<br>`EXT-API-003` enforce text/image-only output.<br>`EXT-CLI-001` one-input `detect`/`extract`, file/stdin, exit codes and Ctrl+C.<br>`EXT-CLI-002` atomic caller-owned bundle output and stable image names.<br>`EXT-CLI-003` baseline and optional publish variants. | `API-001←DET,MOD,active handlers`; `API-002←API-001`; `API-003←API-001,API-002,all handlers`; `CLI-001←API-001,API-002`; `CLI-002←CLI-001`; `CLI-003←CLI-001,PKG-001`.<br>The first useful public vertical slice routes DOC, but the same boundary ultimately serves all formats. | Implement stream/bytes API with untrusted hints, deterministic failure containment, file/stdin input, limits, process outcomes and no hidden fallback. Keep CLI thin; no recursive discovery or second parser. Separately gate self-contained, single-file and Native AOT RIDs. | API/CLI semantic equivalence; stdout/stderr leak tests; deterministic bundle hashes; atomic `result.json` plus image-only `assets/`; Windows/Linux framework-dependent smoke evidence. Optional variants require separate RID evidence. |
| 4 | `EXT-EML-001` scanner/detection/spans; `002` all modern/obsolete/trace/resent/unknown headers; `003` UTF-8, encoded words, parameters, addresses, dates and IDs; `004` MIME tree; `005` transfer/charset profiles; `006` disposition/images/CID identities; `007` body policy/flowed text/inert HTML; `008` nested/global/partial/external-body; `009` DSN/MDN/feedback/list/trace/authentication; `010` TNEF and selected legacy transports; `011` signatures/S/MIME/PGP-MIME; `012` projection and acceptance. | `001←DET,FND-001`; `002←001`; `003←002,FND-002`; `004←002`; `005←003,004`; `006←004,005,MOD`; `007←004,005`; `008←004,006`; `009←002,004`; `010←005,MSG-002`; `011←004,006`; `012←001..011,MOD`. | Bounded RFC 5322 current/obsolete syntax; malformed folding and ambiguity; ordered participants; raw-preserving provenance for lossy dates/charsets; strict multipart boundaries; bounded Base64/quoted-printable; documented plain/HTML choice; inline/attached images; text/image-only `message/rfc822` nesting; delivery/notification text; passive protected/external content. | RFC-derived conformance, malformed-tolerance policy, independent semantic comparison, recursion/expansion/resource limits, parser-smuggling, fuzz/security, deterministic and performance/concurrency evidence pass for the declared subset. |
| 5 | `EXT-DOCX-001` ZIP/CFB classifier; `002` ZIP/ZIP64 and OPC; `003` secure XML, Strict/Transitional and MCE; `004` stories/tokens; `005` properties/settings/styles/fonts/themes/numbering; `006` paragraphs/tables/sections/headers; `007` fields/bookmarks/controls/custom XML/mail merge; `008` notes/comments/revisions; `009` DrawingML/VML/images/charts/SmartArt/OMML; `010` `altChunk`, embeddings, OLE, VBA, ActiveX and external relationships; `011` protection/encryption/signatures/projection; `012` acceptance evidence. | `001←DET,STO-001,STO-002`; `002←STO-002`; `003←002,STO-004`; `004←003,MOD`; `005←003`; `006←004,005`; `007/008←004,005`; `009←003,MOD`; `010←002,STO-003`; `011←001..010,MOD`; `012←001..011`. | Complete secure OPC graph and MCE processing; identify Strict/Transitional; extract all supported stories, drawing/chart/diagram text and recoverable images; inspect packages, OLE, custom XML, macros, signatures and external relationships passively; detect encrypted OOXML in CFB. No ZIP-slip materialisation, entity resolution, layout recreation or DOC conversion. | ECMA-376 fixtures across declared editions/features; hostile ZIP/XML gates; deterministic text/images; semantic differential, fuzz and stated resource/performance evidence. Explicit outcomes for encrypted packages, invalid relationships, unsupported markup and damaged parts. |
| 6 | `EXT-MSG-001` CFB Outlook Item profile; `002` complete bounded MAPI types/streams; `003` named properties/catalogue/Unicode/code pages; `004` common metadata/recipients/transport/raw evidence; `005` body policy; `006` compressed/passive RTF and encapsulated HTML; `007` attachment methods/OLE/references; `008` embedded messages; `009` reports/S/MIME/protected state; `010` calendar/meeting; `011` contacts/lists; `012` tasks/other item classes; `013` projection and acceptance. | `001←DET,STO-001`; `002←001,FND-002`; `003←002`; `004←003,MOD`; `005←004`; `006←002,FND-002`; `007←002,STO-003`; `008←007`; `009←004,007`; `010..012←003,004`; `013←001..012,MOD`. | Treat the generic bounded MAPI property bag as the lossless base. Decode fixed, variable, multi-valued and named properties; recipients, dates, identifiers, bodies, compressed RTF, code pages and all declared item classes. Extract image attachments and embedded messages; inventory other attachment methods, TNEF, S/MIME, OLE and external content without execution, retrieval or byte emission. | Published-specification fixtures and real item-class cohorts; deterministic missing/duplicate/malformed-property handling; independent comparison; malformed RTF; recursive limits; fuzz/security; performance/concurrency evidence for declared properties, bodies, recurrence/time-zone data, methods and item classes. |
| 7 | `EXT-PDF-001` standards/version/extensions/profiles/detection; `002` bounded lexer/COS/spans; `003` core filters/predictors; `004` xref/object resolution, streams, revisions, hybrids and linearisation; `005` media filters and encryption classification; `006` Catalog/pages/resources/content; `007` fonts/encodings/CMaps/Unicode/positioned text. | `001←DET`; `002←FND-001,002`; `003←002`; `004←002,003`; `005←003,004`; `006←004`; `007←006`. | Implement PDF 1.0–2.0 lexical/object syntax, header/Catalog version rules, direct/indirect objects, classic and xref streams, trailers, hybrids, incremental revisions, linearisation, object streams, bounded non-image filters, page/resource trees, operators, graphics/text state, fonts and deterministic geometric/structure-aware order. Recovery must not guess beyond configured bounds. | Structural and text conformance covers every declared syntax/filter/font path; explicit unsupported-filter and malformed-stream issues; font/encoding uncertainty visible; encrypted, corrupt, partial, unsupported and resource-limit outcomes explicit. |
| 8 | `EXT-PDF-008` Information/XMP/IDs/profile claims; `009` images, files, collections and nesting handoff; `010` navigation/annotations/AcroForm/passive XFA; `011` tagged/logical/geometric/article order and optional content; `012` passive actions/JavaScript/multimedia/3D; `013` signatures and revision forensics; `014` projection, recovery and acceptance. | `008←PDF-004,STO-004`; `009←PDF-005,006,MOD`; `010←PDF-006,STO-004`; `011←PDF-006,007`; `012←PDF-006,009`; `013←PDF-004`; `014←PDF-001..013,MOD`. | Add metadata/XMP, tagged structure, marked/optional content, images/masks, outlines/page labels/name trees, annotation/form text, and passive inventory for attachments/portfolios, XFA, signatures, actions/scripts, multimedia/3D/rich media and encryption. Emit only text/images. Profile identification must not be presented as profile conformance validation. No rendering, OCR, writing, decryption or arbitrary attachment emission. | Specification-derived fixtures for every declared version, syntax/filter/font route, structural profile and passive clause family; independent semantic comparison; decompression, incremental-update, cancellation, fuzz/security and performance gates. Version/profile claims remain distinct from observed features. |
| 9 | `EXT-NEST-001` cross-format recursive extraction and identity. | `NEST←API-001,relevant handlers`. | Recursively extract supported formats under one cumulative budget; preserve occurrence identity, hashes, parent/child relationships, nesting path and deterministic failure propagation. Detect duplicate images, cycles, depth/expansion limits and cancellation. Unsupported formats remain hashed descriptors only. | Mixed-format nesting, duplicate, cycle, cancellation and cumulative-limit suites pass deterministically with no non-image byte emission. |
| 10 | `EXT-SEC-001` cross-format denial controls.<br>`EXT-QA-002` security, fuzz/property and hostile regression system. | `SEC←FND-001,active handlers`; `QA-002←active parser/decoder surfaces`. | Continuously fuzz/property-test tokenizers, binary tables, XML/MIME parsers and decoders. Prove denial of macros, scripts, actions, processes, field execution, external retrieval, input-selected paths, entities, ZIP traversal, bombs, oversized ranges/counts, algorithmic attacks and content-bearing logs. | Every parser has a maintained hostile regression corpus; structurally valid feature-bearing hostile files reach deep parsers; security review has no unresolved release-blocking finding. |
| 11 | `EXT-QA-003` performance, allocation, expansion, nesting, cancellation and concurrency evidence. | Depends on active handlers and stable measurement contracts. | Benchmark detection, extraction and cancellation on stated Windows/Linux hosts. Record allocations, working set, CPU, elapsed time, bytes read/decoded, object/part counts, text/image output, nesting and concurrent behavior. Tune only from measured evidence; retain safe managed bounds. | The declared 10 MiB Pegasus class and any larger future class need measured host/concurrency budgets; until then limits remain conservative and caller-controlled. |
| 12 | `EXT-QA-001` unit, conformance, differential and genuine-data harness.<br>`EXT-PKG-001` dependency review, SBOM, package/schema/version support, update and rollback. | `QA-001←active units`; `PKG←LIC-001,accepted release scope`. | Provide deterministic offline checks and separately authorised opt-in oracle, corpus and performance lanes; manifested fixtures with source hashes, licences, feature tags and expected outcomes; semantic comparators and deviations; host/toolchain/corpus manifests; content-free diagnostics; package, licence and security review; versioned schemas, notices, support and rollback policy. | Declared release subset passes unit, conformance, differential, security, fuzz, resource, performance, deterministic retry, operator cohort and implementation-author-hidden holdout gates. Framework-dependent packages smoke-test on Windows/Linux. No aggregate “all formats complete” claim while any required capability evidence is absent. |
| 13 | `EXT-INT-001`; DOC `DOC-I13`; Pegasus Infrastructure adapter and caller-owned evidence. | Depends on accepted package evidence, including DOC `DOC-I12` where DOC is activated. The adapter is implemented only in the adjacent Infrastructure boundary. | Prove Web and Worker reach the one public extractor and translate content, assets, issues and completeness without importing extractor internals or moving caller policy into this workspace. Run caller-owned cohort/holdout and zero-false-case-creation gates. | Package evidence does not imply Pegasus activation. |

## DOC outcome and completion rules

For binary Word:

- The target is Word 97-family data under `[MS-DOC]` 12.5.
- An unread or ambiguously interpreted branch that can contain text or images prevents `Complete`.
- A non-payload branch may remain passive only when the specification map proves that it cannot hide required payload.
- Encryption is classified without password guessing or decryption.
- Unknown bounded data may be retained only when useful as control evidence.
- Every supported `nFib` and every reachable FIB field must have a reviewed owner and support classification.
- Every observed payload-bearing branch must either be extracted semantically or produce a deterministic non-complete outcome.
- All declared stories, current/deleted/hidden text, anchors and recoverable images retain exact provenance.
- Non-image and active content remains passive, unexecuted, unretrieved and absent from public asset bytes.
- Pre-97 files may remain explicitly unsupported after an accepted `EXT-DOC-012` decision. That does not enlarge the Word 97-family claim.
- Pegasus caller evidence remains separate from parser acceptance.

Each DOC implementation change must name one or more `DOC-I*` gates, cite exact specification sections and generated-table provenance, state the observed input and supported/unsupported branches, add independent positive/malformed/bound tests before claiming behavior, exercise the public path when output or outcomes change, update [capabilities](../../../docs/capabilities.md) and its evidence record with exact commands/results, and receive independent review before acceptance advances.

Do not merge placeholder semantic models, broad warning suppression, guessed opcodes, printable-byte recovery presented as success, or a second production DOC parser.

## Evidence programme

A higher evidence lane does not replace a lower one.

| Test ID | Lane | Required evidence |
|---|---|---|
| `DOC-T01` | Binary primitives | Independent table-driven tests for every integer, bit, count, offset, length, sentinel, version and stream owner |
| `DOC-T02` | Structure contracts | Positive, absent, exact-boundary, one-over, truncated-at-every-byte, invalid-reference, overflow, cycle and duplicate cases for every grammar |
| `DOC-T03` | Semantic conformance | Specification-derived examples for every supported story, property, field, revision, list/table/section and image behavior |
| `DOC-T04` | Public projection | Valid DOC bytes through the common API and CLI; exact text/image-only payload, locations, metadata, relationships, issues, outcomes and stable JSON/assets |
| `DOC-T05` | Differential | Exact-version test-only oracles, predeclared semantic normalization and explained deviations |
| `DOC-T06` | Security | Valid deep DOCs with macros, OLE, fields, links, paths and active markers; no execution/retrieval and content-free diagnostics |
| `DOC-T07` | Fuzz/property | In-process properties plus out-of-process coverage-guided fuzzing of FIB, CLX, PLC/FKP/SPRM, property sets, OfficeArt and image decoders |
| `DOC-T08` | Determinism | Retries, physical-layout permutations, duplicate content, stable identity, issue ordering and parallel extraction |
| `DOC-T09` | Resource/performance | Exact-limit/one-over for every counter; representative 10 MiB files; worst-case expansion; allocation, CPU, elapsed time, cancellation and supported concurrency |
| `DOC-T10` | Genuine data | Operator-reviewed cohort and implementation-author-hidden holdout with zero silent truncation of identity-critical text/images |
| `DOC-T11` | Caller acceptance | Pegasus-owned adapter and route tests after package acceptance; only the engine-neutral contract is supplied here |

Every positive feature also needs absence, malformed representation and resource exhaustion cases where meaningful. Handcrafted fixtures derive from named specification rules, not production constants. Binary fixtures use the repository manifest schema and contain no private operational data.

Mandatory mutation-oriented coverage includes:

- every FIB index, type, stream owner, non-offset field and unknown/zero tail for every supported `nFib`;
- positive semantic assertions through the public result, not presence-only structure assertions;
- exact discrete image bytes, media type, anchor and identity, with hostile pseudo-images suppressed;
- public exact-limit/one-over tests for text, assets and object dimensions;
- independently generated SPRM operand/skip tests, special variable forms and truncation at every byte;
- font/FIB/language code-page matrices, DBCS boundaries, symbol fonts, RTL and invalid sequences;
- every story/anchor family, including malformed, overlap, separator and quick-save variants;
- full valid DOCs through API and CLI proving text/image-only bytes and content-free diagnostics;
- deterministic mid-loop cancellation/deadline tests for PLC, FKP, SPRM, OfficeArt and property loops;
- whole-result byte stability, duplicate identity and concurrent retries across equivalent physical layouts.

Detection-only hostile files with incomplete CFB signatures do not exercise deep DOC parsing. Performance evidence from a small simple main-story file does not qualify the 10 MiB semantic input class.

## Tool and delegated-skill policy

Skill invocation is conditional on its trigger. Listing a skill does not show that its trigger occurred or that a product gate passed.

| Plugin | Skills | Required use and exclusions |
|---|---|---|
| `dotnet` | `setup-local-sdk` | Use only for bootstrap when the pinned .NET 10 SDK is absent or isolation is required. A workstation already containing the required SDK does not ordinarily reinstall it. |
| `dotnet-diag` | `analyzing-dotnet-performance`, `android-tombstone-symbolication`, `apple-crash-symbolication`, `clr-activation-debugging`, `dotnet-trace-collect`, `dump-collect`, `microbenchmarking` | Apply static performance analysis to parser/decoder hot paths. Establish repeatable extraction baselines during performance closure. Collect traces or dumps only for measured performance, hang or crash problems. Mobile symbolication and .NET Framework CLR activation do not apply to the headless .NET 10 library/CLI target. |
| `dotnet-msbuild` | `binlog-failure-analysis`, `binlog-generation`, `build-parallelism`, `build-perf-baseline`, `build-perf-diagnostics`, `check-bin-obj-clash`, `copy-to-output-directory`, `directory-build-organization`, `eval-performance`, `extension-points`, `including-generated-files`, `incremental-build`, `item-management`, `msbuild-antipatterns`, `msbuild-modernization`, `msbuild-server`, `property-patterns`, `resolve-project-references`, `target-authoring` | Use organization, property and anti-pattern reviews when repository-wide policy changes. Use output, item, extension, generated-file and target-authoring skills only when that behavior is introduced. Establish build-performance/parallelism evidence during performance and release closure. Generate/analyse binlogs only when ordinary output cannot explain a failure or measured bottleneck. Legacy modernization is not currently applicable to SDK-style projects. |
| `dotnet-test` | `assertion-quality`, `code-testing-agent`, `code-testing-extensions`, `coverage-analysis`, `crap-score`, `detect-static-dependencies`, `filter-syntax`, `find-untested-sources`, `generate-testability-wrappers`, `grade-tests`, `migrate-static-to-wrapper`, `mtp-hot-reload`, `platform-detection`, `run-tests`, `test-analysis-extensions`, `test-anti-patterns`, `test-gap-analysis`, `test-smell-detection`, `test-tagging`, `writing-mstest-tests` | For each new or unfamiliar test project, begin with platform detection, add focused MSTest/code-agent tests and end with test execution. Apply assertion, gap, tagging, smell, anti-pattern, untested-source and coverage analysis at feature and acceptance gates. Static-dependency detection is mandatory at public orchestration/CLI boundaries and useful around time, files, environment and processes. Create/migrate wrappers only for real static boundaries. Filtering, grading, CRAP scoring and MTP hot reload are problem-specific, not unconditional gates. |

Routing by delivery activity:

| Activity | Required | Conditional |
|---|---|---|
| Governance/bootstrap | MSBuild organization/property/anti-pattern review; test-tagging policy | Local SDK setup |
| Foundations and format parsers | Platform detection; MSTest/focused authoring; test execution; assertion/gap review; parser performance scan | Coverage/CRAP; static wrappers; binlog diagnostics; fuzz-related analysis; hot reload |
| Public API/CLI | API/CLI tests; test execution; mandatory static-dependency detection | Output/item/target/generated-file skills where packaging requires them |
| Performance closure | Microbenchmarking; parser performance scan; build-performance baseline | Traces, dumps and build-performance diagnostics only after measured evidence identifies a problem |
| Packaging/acceptance | Build anti-pattern/output/clash review; test quality, gap, coverage and acceptance analysis | Publish target authoring; binlog failure analysis |
| Caller activation | Caller test execution, tagging, gap and coverage review | Diagnostics driven by real caller evidence |

Delegated agents must name the skills used, record exact commands and exits, and distinguish a skill review from a passed product gate.

## Historical evidence qualifications

These records preserve evidence limitations without defining current capability:

1. An earlier foundation note reported 30 focused tests for the fixed CFB v3 header and explicitly said FAT/DIFAT, mini-stream, directory and stream traversal were absent. It also reported local source/test evidence for bounded reads, counters, cancellation/deadlines, SHA-256 and length-prefixed identities, selected Unicode/Windows-1252/FILETIME primitives, NFC/LF normalization, immutable models, deterministic ordering and source-generated JSON. It left detection, complete containers, XML, additional encodings/dates, fuzz/property campaigns and independent API review open.

2. A later programme-entry snapshot described this local route:

   ```text
   shared bounded Core / immutable Model / byte-level detection
     -> CFB v3/v4, ZIP/ZIP64, OPC, OLEPS and secure XML storage primitives
     -> managed PDF / binary DOC / DOCX / MSG / EML handlers
     -> one five-format extraction API with cumulative nesting budgets
     -> deterministic JSON and atomic evidence bundles
     -> headless Windows framework-dependent CLI
     -> hostile-input, deterministic fuzz, performance and packaging checks
     -> historical opt-in predecessor CollisionSpike Web adapter; default legacy path remained enabled
   ```

   That snapshot reported 523 tests: 522 passed, one intentionally skipped opt-in opaque EML cohort test and zero failures, plus deterministic two-run completion for twelve authorised local PDF/EML/MSG samples. It expressly limited the evidence to local slices, reported no capability row as `Conformant`, and did not prove real Web and Worker caller activation.

3. A 2026-07-24 DOC planning record described a source-tree route through an owned CFB reader/detector, `WordBinaryExtractor`, FIB and piece-table parsing, story/control decoding, limited structured evidence, and common projection. Because it supplied no real-caller proof, this document does not use that record to claim caller-backed implementation.

   The same record reported narrowly scoped behavior: selected Word 97-family FIB values and streams; coarse encryption classification; CLX/Pcdt/PlcPcd and CP/FC text; mixed compressed/UTF-16LE pieces; eight raw story extents; piece/text byte and CP evidence; common control recognition; limited PRM/SPRM and CHPX/PAPX evidence; eleven named SPRMs; passive FIB/stream/property evidence; and an internal `ReviewAsset` path later filtered to validated image encodings. Every emitted generic Word structure was recorded as not semantically decoded, and no DOC image extraction was established.

   Its test inventory was 36 methods expanding to 44 cases. One caller-selected local DOC returned `Partial` with readable text and visible unimplemented-structure, property, anchor and non-image issues. It recorded no manifested DOC binary fixtures, conformance/differential suite, deep security corpus, hidden holdout or 10 MiB semantic performance evidence.

4. The 2026-07-24 research record reported:
   - `DOC-R00` sufficient for internal specification-led work, while product licensing, patent and distribution review remained open;
   - `DOC-R01` mapped/specifed all 183 cumulative FIB descriptors and five accepted layouts, while production semantic coverage remained partial;
   - `DOC-R02` froze five classifier predicates and 26 outcome cases, while pre-97 parsing remained unauthorised and fixture-publication provenance remained open;
   - `DOC-R03` froze 39 text/story outcome cases, 24 compressed-byte substitutions, CLX/PRC/PRM and CP/FC rules, seven document parts, header/footer and AutoText rules, with 92 independent test-oracle cases that did not invoke production DOC parsers.

   These are research and local-verification records, not current support claims.

5. The planning validation command was:

   ```powershell
   pwsh -NoProfile -File .\scripts\Invoke-RepoCheck.ps1
   ```

   Recorded exit: `0`.

   Locked restore, formatting, Release build, JSON parsing and local Markdown-link validation passed with zero build warnings/errors. Microsoft.Testing.Platform reported 534 tests: 533 succeeded, zero failed and one opt-in EML local-cohort test skipped. The input class was repository-owned source, synthetic tests and documentation.

   The command did **not** inspect `sample-doc-files/`, run DOC conformance, differential, genuine-data or fuzz lanes, evaluate a hidden holdout, establish DOC performance/resource acceptance, prove a deployed package, or prove the intended caller. It validated the repository after a planning change, not the unimplemented DOC semantics.

The differing test totals above are separate recorded snapshots and must not be combined into a current status claim.

## Release and activation rule

A release candidate exits this programme only for a precisely declared capability set when:

- scope, specifications, provenance, licensing, security and distribution decisions are authorised;
- all required dependency rows have passed their own exits;
- capabilities are supported by unit, conformance, differential, hostile-input, fuzz, determinism, resource, performance and CLI evidence as applicable;
- genuine cohorts and implementation-author-hidden holdouts show zero silent truncation of identity-critical text/images;
- package, SBOM, schema/version, support, update and rollback evidence is accepted;
- optional packaging variants are separately qualified;
- independent review is performed by someone other than the implementation/test author; and
- no aggregate completeness statement exceeds the evidence in [capabilities](../../../docs/capabilities.md).

Pegasus activation is later and separate:

1. accepted engine-neutral package evidence;
2. adjacent Infrastructure adapter;
3. real Web and Worker calls;
4. caller-owned cohort/holdout and zero-false-case-creation gates;
5. `Called` evidence;
6. deployment evidence, if applicable;
7. authorised caller acceptance.

Library or CLI success, local test success, package production, or an opt-in adapter does not satisfy `Called`, deployment or acceptance.