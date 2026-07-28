# P31 — DOC comprehension and completion programme

Status: specified investigation and implementation plan. No item in this document is evidence that the corresponding behaviour is implemented or supported.

This programme covers `EXT-DOC-001` through `EXT-DOC-013`. It turns the broad [legacy DOC extraction surface](../../formats/doc.md) into a source-led investigation, test and implementation sequence. It is deliberately separate from the earlier text and structured-parser evidence: a parser that returns readable text or inventories a structure is not semantically complete.

## Fixed authority and product boundary

| Item | Decision |
|---|---|
| Format | Word 97-family binary `.doc`, read directly from CFB and Word binary structures |
| Primary specification | `[MS-DOC]` revision 12.5, published 2026-02-17 |
| Intended caller | CollisionSpike Infrastructure source-reader adapter through the one public extraction API |
| Payload | Ordered document text and safely recoverable images only |
| Control evidence | Metadata, properties, anchors, object descriptors, active-content presence, issues and completeness |
| Excluded transformations | No DOCX/XML intermediary, rendering, layout reproduction, field execution or format conversion |
| Runtime | Managed C# on .NET 10 or later; library plus one-shot headless CLI |
| Production dependencies | No Office automation, external office-suite runtime, hosted converter or third-party format-extraction engine |
| Current input bound | 10 MiB per CollisionSpike source, with all decoded, object, text, image, depth, time and concurrency bounds still requiring measured justification |
| Older Word | Word 6/95 and earlier families remain explicitly identified `UnsupportedFeature` until `EXT-DOC-012` reaches an accepted specification/provenance decision |

The result contract is the repository-wide outcome model. An unread or ambiguously interpreted branch that can contain text or images prevents `Complete`. A safely identified non-payload branch can remain passive only when the specification map proves it cannot hide required payload. Encryption is classified without password guessing or decryption. Input-selected commands, code, fields, links, paths and network locations are never executed or retrieved.

## Specification and provenance baseline

The first research action must download the exact published PDF or DOCX specifications, retain their source URLs and hashes under ignored research artifacts, and record the retrieval date. Online pages are navigation aids, not the immutable implementation baseline.

| Source | Revision/date to pin | Owned questions |
|---|---|---|
| [`[MS-DOC]`](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-doc/ccd7b486-7881-484c-a137-51170af7cc22) | 12.5 / 2026-02-17 | Word 97-family FIB, stories, text, properties, document structures and host-specific OfficeArt integration |
| [`[MS-CFB]`](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-cfb/53989ce4-7b05-4f8d-829b-d08d6148375b) | 12.0 / 2024-04-23 | Compound storage and stream invariants |
| [`[MS-ODRAW]`](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-odraw/8560795e-7759-4745-838f-f7f2ef2f1872) | 12.4 / 2025-08-19 | OfficeArt record hierarchy, shapes, BLIPs and image encodings |
| [`[MS-OLEDS]`](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-oleds/85583d21-c1cf-4afe-a35f-d6701c5fbb6f) | 13.0 / 2024-04-23 | Linked and embedded OLE data structures |
| [`[MS-OLEPS]`](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-oleps/bf7aeae8-c47a-4939-9f45-700158dac3bc) | 9.0 / 2024-04-23 | Property-set headers, sections, types, dictionaries and code pages |
| [`[MS-OSHARED]`](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oshared/d93502fa-5b8f-4f47-a3fe-5574046f4b8d) | 11.1 / 2025-11-13 | Shared Office properties, signatures and common objects |
| [`[MS-OFFCRYPTO]`](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-offcrypto/3c34d72a-1a61-4b52-a893-196f9157f083) | 14.0 / 2026-02-17 | XOR, binary RC4 and CryptoAPI classification boundaries |
| [`[MS-OVBA]`](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-ovba/575462ba-bf67-4190-9fac-c275523c75fc) | 15.0 / 2026-05-19 | Passive VBA project inventory only |
| [`[MS-OFORMS]`](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/9c79701a-8c3e-4429-a139-b60ac3a1d50a) | 9.1 / 2025-08-19 | Passive Office Forms inventory and textual values where safe |

Before implementation, the source ledger must also identify every normative reference reached from `[MS-DOC]`, the exact section used by each generated table, the applicable intellectual-property notice, and whether any test fixture or oracle is redistributable. No upstream parser source is a design authority and no source code is mechanically translated.

## Current implementation map

The current managed route is:

```text
DocumentExtractor
  -> owned CFB reader and detector
  -> WordBinaryExtractor
     -> WordFibParser
     -> WordPieceTableParser
     -> story/control decoding
     -> WordStructuredEvidenceParser
        -> limited PRM/SPRM and CHPX/PAPX FKP evidence
        -> generic passive FIB-range records
        -> passive stream and limited property-set inventory
  -> common result projection
  -> public text/image payload filter
```

What exists today:

- CFB traversal, root `WordDocument`, selected `0Table`/`1Table`, five Word 97-family `nFib` values and a coarse encryption gate.
- CLX/Pcdt/PlcPcd parsing, logical CP-to-physical FC mapping, mixed compressed/UTF-16LE pieces and eight raw story extents.
- Exact piece and text-segment byte/CP evidence for the implemented text path.
- Recognition of common control characters, with non-complete outcomes when their semantics are unresolved.
- Basic PRM routing, generic SPRM framing, CHPX/PAPX BTE/FKP validation and eleven named SPRM meanings.
- A small typed index over selected FIB ranges, but every emitted `WordStructureRecord` is currently `SemanticallyDecoded == false`.
- Stable passive stream descriptors and a limited single-section OLE property-set reader.
- A public payload filter that ultimately emits only validated image encodings, although the DOC adapter first materialises every passive stream as an internal `ReviewAsset`.

Recorded local evidence is narrower than format support. The Writer test source contains 36 test methods, expanding to 44 cases through data rows. The latest recorded genuine compatibility check used one caller-selected local file and returned `Partial`, with readable text plus visible unimplemented-structure, property, anchor and non-image issues. There are no manifested DOC binary fixtures under `tests/fixtures/`, no DOC conformance or differential suite, no deep DOC parser security corpus, no DOC hidden holdout and no 10 MiB semantic performance evidence.

## Remaining problem register

| Gap | Port unit | Current behaviour | Required comprehension and observable completion gate |
|---|---|---|---|
| `DOC-GAP-001` | EXT-DOC-001 | Detection recognises a valid Word FIB, several pre-97 identifiers and a few obvious non-CFB prefixes. | Specify structural precedence for Word, MSG, encrypted OOXML, RTF, HTML/MHTML, plain text, PDF, OOXML, templates, corrupt CFB and polyglots. Test renamed and ambiguous inputs without trusting filename/media type. |
| `DOC-GAP-002` | EXT-DOC-002 | The FIB reader consumes counted arrays but retains only a small field subset. It treats the catalogue generically, with one hard-coded non-`fc/lcb` exception at index 87. | Build a version-specific C1/D9/101/10C/112 FIB atlas: expected counts, every field name/type, owning stream, version introduction, invariants and payload relevance. Unknown trailing values must be retained without being misvalidated as Table ranges. |
| `DOC-GAP-003` | EXT-DOC-002 | Encryption is inferred from flags/key and returned as one generic encrypted result; `pnNext` is only warned about. | Distinguish obfuscation, binary RC4 and CryptoAPI headers without reading protected text; define malformed-header outcomes. Map secondary FIB, template/AutoText and glossary traversal with cycle/depth bounds. |
| `DOC-GAP-004` | EXT-DOC-003 | A CLX is mandatory. PRC records are initially labelled unapplied even when the later structured pass can reference them. | Resolve the exact simple/complex-file algorithms, all CLX record rules, complex PRM indexing, quick-save behaviour and legal trailing data. Add a positive simple-file route only if the pinned specification requires it. |
| `DOC-GAP-005` | EXT-DOC-003 | Compressed text is effectively Windows-1252 only when the FIB character-set field is zero; other non-ASCII bytes become replacement characters. | Define code-page selection from FIB, font table, language and symbol-font properties; cover DBCS, RTL/complex-script, invalid byte sequences, Unicode surrogate pairs and deterministic replacement policy. |
| `DOC-GAP-006` | EXT-DOC-004 | Eight story spans are decoded in fixed order. Every non-main story is unanchored; one specialised-story separator CP is handled. | Model all subdocuments, separator/continuation ranges, headers by section/type, footnote/endnote/comment anchors, textboxes, AutoText and secondary FIB content. Decide whether macro-story text is payload or passive control evidence. |
| `DOC-GAP-007` | EXT-DOC-004 | Control characters are tokenised, but fields, notes, cells, pictures, objects and section/page markers force a generic partial issue. | Specify a stack/range state machine for nested fields and all paired/overlapping anchors. Define stored/current/deleted/hidden projection and malformed-control recovery without evaluating fields. |
| `DOC-GAP-008` | EXT-DOC-005 | Generic `spra` sizing and eleven opcode meanings are implemented. Unknown operands are retained. | Produce the complete SPRM catalogue by `sgc`, opcode, version, operand grammar and payload relevance, including special variable-length forms. Prove skip length independently for every unsupported opcode so one unknown property cannot desynchronise the remainder. |
| `DOC-GAP-009` | EXT-DOC-005 | Piece PRM, CHPX and PAPX evidence exists. SEPX, complete property application and large PAPX indirection do not. | Specify the full property-application order, `Data`-stream indirection, style contributions, direct formatting, version precedence, cycle/depth handling and FC/CP boundary normalization. Add section property runs. |
| `DOC-GAP-010` | EXT-DOC-005..011 | Twenty-eight selected FIB indices receive names or generic PLC framing, but no structure is semantically decoded; all other non-empty ranges are blanket-unprocessed. | Catalogue every versioned FIB field and replace generic range guessing with type-owned parsers. Each observed branch must be classified as absent, semantically handled, safely passive, unsupported payload-bearing, corrupt or limited. |
| `DOC-GAP-011` | EXT-DOC-006 | Style, font, list and table ranges are merely present; paragraph runs expose only a style index and a few flags. | Implement style defaults/inheritance/linked styles, font/code-page/language resolution, list definitions/overrides/labels/restarts, paragraph boundaries, nested tables/cells/merges and section/header associations. Detect graph cycles and invalid references. |
| `DOC-GAP-012` | EXT-DOC-007 | Field, bookmark, form and external-reference PLCs are passive records. | Pair nested field instructions/results, bookmarks and permission ranges; decode hyperlinks/reference/mail-merge/form/SDT text. Record DDE, INCLUDE, template, link and external-data targets without retrieval. |
| `DOC-GAP-013` | EXT-DOC-008 | Note, comment and revision ranges are passive; secondary story text lacks anchors. | Join references to story ranges and decode comment authors/times/replies plus insertion/deletion/move/property revisions. Prove hidden/deleted identity-critical text is never silently omitted. |
| `DOC-GAP-014` | EXT-DOC-009 | A `Data` stream in a file with `fHasPic` is classified as one picture-data blob. Picture controls force `Partial`; no image is decoded. | Follow CHPX `sprmCPicLocation` to bounded PICF/OfficeArt records, parse inline and floating anchors, BLIP stores and supported JPEG/PNG/TIFF/DIB/WMF/EMF paths. Validate signatures, lengths, decompression and image budgets before emitting discrete images. |
| `DOC-GAP-015` | EXT-DOC-009 | Drawing/textbox/equation ranges are passive. | Traverse OfficeArt record trees with record/depth limits; associate shapes, alternate text and textbox stories. Define which vector representations can be safely emitted, which equations yield text/image evidence and which remain passive OLE descriptors. |
| `DOC-GAP-016` | EXT-DOC-010 | Individual streams are classified by names/ancestry and retained as raw bytes internally; an embedded storage is not reconstructed as one bounded source. | Parse only the OLE identity/link/presentation structures required for passive evidence and nested supported-source discovery. Build an exact child-source boundary before nested extraction; never publish arbitrary object/package/VBA/Form bytes. |
| `DOC-GAP-017` | EXT-DOC-011 | One property-set section and a few scalar types/names are decoded. | Implement byte order, format identifiers, multiple sections, dictionaries, code pages, vectors, user-defined properties, padding and all relevant scalar types with bounds. Add DOP/settings, custom XML and signature-presence semantics without trust claims. |
| `DOC-GAP-018` | EXT-DOC-013 | DOC property runs and structure records do not reach the common evidence model. Public text locations retain byte ranges but lose story/global CP detail. All DOC parser issues become warnings. | Define the versioned DOC semantic projection: story, CP/FC/byte provenance, current/deleted/hidden state, anchors, image identities, passive descriptors, issue severity and deterministic ordering. Add no DOC-specific public entry point. |
| `DOC-GAP-019` | EXT-DOC-013 | The public adapter propagates input and passive-asset-count limits but leaves DOC character, piece, property, structure and passive-byte limits at handler defaults. Measurements omit decoded bytes and undercount parser work. | Map every `ResourceLimits` field to owning DOC limits, use checked cumulative counters, and reconcile measurements to actual work. Exercise exact-limit/one-over, cancellation, timeout and concurrency through the public API and CLI. |
| `DOC-GAP-020` | EXT-DOC-013 | Any parser issue makes the result `Partial`, while a minimal synthetic file can be `Complete`. A blanket non-CLX FIB warning remains even for ranges partly inventoried. | Introduce an observed-feature coverage ledger and issue severity/completeness rules. `Complete` requires every present payload-bearing branch to be handled; informational passive evidence must not automatically fail completeness, and semantic omissions must never be informational. |
| `DOC-GAP-021` | EXT-DOC-012 | A few pre-97 identifiers are rejected explicitly; no accepted source route exists. | Inventory Word 6/95, Word 2/earlier and Macintosh variants by independent signatures and authoritative sources. Produce an ADR selecting parser work or precise permanent `UnsupportedFeature` classifications; do not infer semantics from the Word 97 specification. |
| `DOC-GAP-022` | All | The implementation lives in the historically named `CollisionDocNet.Writer` project although it is an extractor. | Decide by ADR whether to rename it to the documented WordBinary boundary. Any rename must be one bounded migration with no compatibility wrapper or parallel parser. This naming issue does not block semantic research. |

## Test-strength audit

The existing tests are valuable regression protection for the current vertical slice. They do not establish broad semantic support. The following pseudo-mutations describe changes that current tests are likely not to kill and therefore define mandatory new tests.

| Risk | Example mutation that can survive current evidence | Missing test |
|---|---|---|
| Critical | Change an untested FIB catalogue index, type or owning stream. | Table-driven cases for every index in every supported `nFib`, including non-offset fields and zero/unknown tails. |
| Critical | Leave every structure `SemanticallyDecoded == false`, or drop an unprojected structure/property family entirely. | Positive semantic assertions per structure family through the public result, not presence-only assertions. |
| Critical | Treat the entire `Data` stream as an image or change its public kind while emitting no actual picture. | Inline and floating picture fixtures asserting exact discrete image bytes, media type, anchor and stable identity; hostile pseudo-images must be suppressed. |
| Critical | Ignore caller `MaxAssetBytes`, `MaxTextCharacters` or object limits inside the DOC adapter. | Public-boundary exact-limit and one-over tests for each resource dimension. |
| High | Change an unsupported SPRM's operand length so all following properties are misaligned. | Opcode/operand grammar tests generated independently from the pinned SPRM catalogue, including special variable forms and truncation at every byte. |
| High | Change supported compressed-codepage mapping outside the single Windows-1252-like case. | Font/FIB/language code-page matrix, DBCS boundaries, symbol fonts, RTL and invalid-sequence cases. |
| High | Drop or mis-anchor a secondary story while preserving main text. | Each story/anchor family in isolation and cross-product, with malformed, overlap, separator and quick-save variants. |
| High | Change DOC passive stream classification or nested-source boundaries without a public valid-DOC test failing. | Full valid DOC through `DocumentExtractor` and CLI, proving text/image-only bytes, metadata, relationships, outcomes and no sensitive diagnostics. |
| High | Remove cancellation checks from long PLC/FKP/SPRM/OfficeArt/property loops. | Deterministic mid-loop cancellation/deadline tests on valid deep structures. |
| Medium | Reorder issues, structures or assets when directory or physical piece order changes. | Whole-result byte-stability, duplicate-content identity and concurrent-retry tests using semantically equivalent physical layouts. |

Current hostile-DOC mutation and active-content tests begin with incomplete CFB signatures and mostly exercise detection/container failure. They do not reach deep DOC parsing. The current performance input is a small, simple main-story document. Both lanes need structurally valid, feature-bearing DOC generators and manifested binary cases.

## Full-comprehension research sequence

Every research unit ends with a reviewed note under `docs/architecture/` or `docs/decisions/`, an update to the compatibility matrix, fixture IDs, and acceptance tests written before production implementation. Research output is `Mapped` or `Specified`, never `Implemented`.

### `DOC-R00` — freeze sources, rights and revisions

Status on 2026-07-24: **complete for internal implementation; release review open**. The nine date-stamped publications and current PDFs are retained under ignored research artifacts and verify against the committed [provenance ledger](../../licensing/doc-source-provenance.json). The repository owner's direction to proceed through `DOC-I13` accepts [ADR-0005](../../decisions/ADR-0005-doc-source-and-clean-room-boundary.md) and the [rights record](../../licensing/doc-source-rights.md) for internal specification-led research, implementation and testing. Product licensing, patent treatment and distribution remain separate release gates.

- Acquire and hash the specification bundle listed above; record retrieval commands, URLs, revisions, file hashes and change histories.
- Record the Open Specifications copyright/patent notice and obtain a repository licensing decision before deriving generated tables or redistributing excerpts.
- Inventory any retained research source by exact revision and licence. Keep downloaded documents and tools ignored unless separately authorised.

Exit: one approved source/provenance ledger and no unpinned normative dependency.

### `DOC-R01` — build the binary structure atlas

Status on 2026-07-24: **complete as mapped and specified; production implementation remains partial**. The [binary structure atlas](../../architecture/doc-binary-structure-atlas.md) and [machine-readable descriptor table](../../architecture/doc-fib-atlas.v1.json) cover all 183 cumulative FIB descriptors, all five accepted layouts, CFB storage ownership, secondary FIB/AutoText, quick-save and encryption branches. Source-backed verification and independent review passed; [evidence](../../testing/evidence/EV-2026-07-24-doc-r01-binary-atlas.md) records the exact boundary and current parser defects.

- Map CFB storage/stream ownership and all FIB sections for C1, D9, 101, 10C and 112.
- Catalogue every `FibRgFcLcb` entry with field name, type, owning stream, version, record grammar, payload relevance and parser owner.
- Map `pnNext`, AutoText/glossary, quick-save and encryption branches.
- Produce machine-readable, hand-reviewed tables suitable for generated C# metadata and independent test generation.

Exit: every byte reachable from the FIB has an owner and explicit support/failure policy.

### `DOC-R02` — classify Word families and acquisition variants

Status on 2026-07-24: **complete as mapped, specified and locally verified; production implementation remains partial**. The [classification contract](../../architecture/doc-format-classification.md) defines five executable profile predicates and 26 frozen outcome cases across Word, legacy markers, MSG, encrypted OOXML, other supported formats, unrelated/damaged containers, repaired state, hints, ambiguity and interruption/resource gates. Its [offline verifier](../../../scripts/Test-DocFormatClassification.ps1) and independent closure review pass. [ADR-0006](../../decisions/ADR-0006-legacy-word-classification.md) remains proposed: pre-97 parsing is not authorised, product-version attribution is prohibited, and the missing retained `[MS-OXMSG]` hash remains a fixture-publication provenance gate. See [evidence](../../testing/evidence/EV-2026-07-24-doc-r02-format-classification.md).

- Define byte-level recognition precedence for supported Word, pre-97, MSG, encrypted OOXML and non-Word/mislabeled families.
- Research Word 6/95 and earlier signatures/sources separately; identify where authoritative semantics are unavailable.
- Define template, repaired, truncated and polyglot classifications.

Exit: decision table covers every detector branch and `EXT-DOC-012` has an ADR-ready recommendation.

### `DOC-R03` — prove text, encoding and story algorithms

Status on 2026-07-24: **complete as mapped, specified and locally verified; production implementation remains partial**. The [text/story semantics](../../architecture/doc-text-story-semantics.md) and [machine contract](../../architecture/doc-text-story-contract.v1.json) freeze 39 outcome cases, the exact 24 compressed-byte substitutions, CLX/PRC/PRM and CP/FC invariants, all seven document parts, header/footer and AutoText rules, and passive control projection. An independent test-only oracle executes 92 cases without calling production DOC parsers; the offline verifier and independent closure review pass. See [evidence](../../testing/evidence/EV-2026-07-24-doc-r03-text-story.md).

- Trace FIB to CLX/Pcdt/PlcPcd and any specified simple-file route; write invariants and corruption rules for each boundary.
- Map PRC/PRM interaction, logical versus physical order, all story lengths/separators and exact CP/FC conversion.
- Specify encoding selection, symbol fonts, DBCS/complex scripts and malformed Unicode policy.
- Define control-token and review-text projection without evaluating fields or layout.

Exit: executable specification tests cover every `nFib`, piece encoding and story class.

### `DOC-R04` — specify the property engine

- Catalogue all SPRMs by group, version and operand grammar; separate structural/payload-relevant properties from safely ignorable rendering-only properties.
- Specify PRM, CHPX, PAPX, SEPX, style/default/direct formatting order and `Data`-stream indirection.
- Define cycle, depth, record-count and cumulative-byte bounds.

Exit: property application can be implemented from tables and state transitions without consulting an upstream parser.

### `DOC-R05` — specify semantic document structures

- Map styles, fonts, paragraph/run boundaries, lists, tables, sections and header/footer association.
- Define deterministic list-label and table-cell text projection without pagination or rendering.
- Specify malformed references, overlapping ranges, graph cycles and unsupported semantics.

Exit: `EXT-DOC-006` has feature-by-feature acceptance examples and failure contracts.

### `DOC-R06` — specify fields and review evidence

- Map fields, bookmarks, forms, permissions, SDTs/custom mappings and every external-reference surface.
- Map footnotes, endnotes, comments, authors, replies and revision families to story ranges.
- Define current/stored/deleted/hidden text policy and exact no-execution/no-retrieval behaviour.

Exit: `EXT-DOC-007` and `EXT-DOC-008` have range-joining algorithms and hostile cases.

### `DOC-R07` — specify image and drawing extraction

- Map inline picture controls through CHPX and PICF into `Data`; map floating anchors through host PLCs into OfficeArt.
- Catalogue OfficeArt record/container invariants and BLIP formats, including metafile decompression and raster validation limits.
- Define textboxes, alternate text, linked images and equation representations.

Exit: each claimed image encoding has a byte-accurate extraction algorithm, signature validator, provenance model and resource budget.

### `DOC-R08` — specify passive, nested and metadata surfaces

- Map OLE linked/embedded identity and child-source boundaries, VBA/Forms presence, custom XML, settings and signatures.
- Define which descriptors require hashes only, which supported embedded sources can be reconstructed for nested extraction and which bytes must never reach the public payload.
- Specify complete OLE property-set and document-settings handling.

Exit: every optional stream/storage has a passive, nested or payload owner and a completeness effect.

### `DOC-R09` — threat model and resource model

- Enumerate integer/range overflow, CFB aliases, PLC/FKP cycles, huge property indirection, deep OfficeArt, image expansion, malformed encodings and overlapping anchors.
- Establish per-structure and cumulative budgets derived from the 10 MiB input class.
- Define cancellation checkpoints, elapsed-time behaviour, concurrency assumptions and temporary-file prohibition.

Exit: security cases and measurable budget hypotheses exist before complex parsers are written.

### `DOC-R10` — approve fixtures, oracles and comparators

- Create specification-derived synthetic generators that do not share parser code or lookup tables with production.
- Select only minimal redistributable binary fixtures with manifest provenance.
- Evaluate a pinned Apache POI HWPF release as a test-only candidate oracle; record its exact version, licence, known unsupported areas and sandbox command. Select a second independent oracle only after the same review.
- Define semantic normalization before comparing: story/category text, current/deleted/hidden state, metadata, image hashes/types, anchors, outcomes and allowed deviations.

Exit: accepted oracle ADR, comparator schema and fixture manifest. No oracle is callable from production code.

### `DOC-R11` — feature census and hidden holdout design

- Run only against an explicit, operator-approved local path and manifested hashes; never recurse through `sample-doc-files/`.
- Inventory feature presence without logging text, filenames or operational identifiers.
- Choose a reviewed cohort and implementation-author-hidden holdout by content hash, stratified by observed `nFib`, stories, encodings, structures, images, active/passive content and damage.
- Record snapshot and manifest hashes before and after evaluation.

Exit: the cohort tests the declared surface rather than merely supplying a file count.

## Test programme

Each parser slice uses the following evidence ladder. A higher lane does not replace a lower one.

| Test ID | Lane | Required evidence |
|---|---|---|
| `DOC-T01` | Binary primitives | Independent table-driven tests for every integer, bit field, count, offset, length, sentinel, version and stream owner |
| `DOC-T02` | Structure contracts | Positive, absent, exact-boundary, one-over, truncated-at-every-byte, invalid-reference, overflow, cycle and duplicate cases for every record grammar |
| `DOC-T03` | Semantic conformance | Specification-derived examples for every supported story, property, field, revision, list/table/section and image behaviour |
| `DOC-T04` | Public projection | Valid DOC bytes through `DocumentExtractor` and CLI; exact text/image-only payload, source locations, metadata, relationships, issues, outcomes and stable JSON/assets |
| `DOC-T05` | Differential | Exact-version test-only oracles compared with a predeclared semantic normalizer and explained deviations |
| `DOC-T06` | Security | Valid deep DOCs containing macros, OLE, fields, links, paths and active markers; prove no execution/retrieval and content-free diagnostics |
| `DOC-T07` | Fuzz/property | In-process property tests for primitives plus out-of-process coverage-guided fuzzing of FIB, CLX, PLC/FKP/SPRM, property sets, OfficeArt and image decoders |
| `DOC-T08` | Determinism | Retry, physical-layout permutations, duplicate content, stable identity, issue order and parallel extraction |
| `DOC-T09` | Resource/performance | Exact limit/one-over for every counter; 10 MiB representative files; worst-case expansion; allocation, CPU, elapsed time, cancellation and supported concurrency |
| `DOC-T10` | Genuine data | Operator-reviewed cohort plus implementation-author-hidden holdout, with zero silent truncation of identity-critical text/images |
| `DOC-T11` | Caller acceptance | CollisionSpike-owned adapter and route tests after package acceptance; this repository supplies only the engine-neutral contract |

For every positive feature, fixtures also cover absence, malformed representation and resource exhaustion where meaningful. Handcrafted fixtures must be generated from named specification rules, not copied from production constants. Binary fixtures use the repository manifest schema and contain no private operational data.

## Dependency-ordered implementation plan

Implementation begins only as the owning `DOC-R*` unit reaches `Specified`. Every step adds failing tests first, production code through the one parser route, public-boundary coverage, an evidence record and compatibility-matrix updates.

### `DOC-I00` — architecture, generated tables and semantic model

Owners: EXT-DOC-001..013; depends on `DOC-R00`, `DOC-R01`, `DOC-R09`.

- Record the project-boundary/naming ADR and avoid a second DOC parser.
- Add typed FIB descriptors, source spans, observed-feature coverage states and an issue registry with severity/completeness effects.
- Extend the internal semantic model for stories, stateful text, anchors, property runs, images and passive descriptors.
- Extend DOC limits/counters so every public resource budget has one owner.

Exit: models, generated-table provenance and contract tests are accepted; no parser behaviour is claimed yet.

### `DOC-I01` — classification, complete FIB and encryption gate

Owners: EXT-DOC-001, EXT-DOC-002, EXT-DOC-012; depends on `DOC-I00`, `DOC-R02`.

- Implement classifier precedence and version-specific FIB readers using the reviewed atlas.
- Validate counts and stream ownership without rejecting unknown trailing fields.
- Classify encryption/obfuscation and secondary FIB branches; implement the accepted older-family decision.

Exit: all supported versions and mislabeled/encrypted/pre-97 classes pass `DOC-T01` through `DOC-T04`.

### `DOC-I02` — complete direct text and encoding engine

Owners: EXT-DOC-003; depends on `DOC-I01`, `DOC-R03`.

- Implement all specified CLX/simple/complex routes, PRC/PRM indexing, logical CP/FC lookup and quick-save cases.
- Add code-page/font/language/symbol and Unicode handling.
- Preserve exact stream/byte/FC/global-CP source spans and deterministic replacement diagnostics.

Exit: text conformance and differential results pass for every version/encoding class, including physically disordered pieces.

### `DOC-I03` — story graph and control anchors

Owners: EXT-DOC-004; depends on `DOC-I02`.

- Build typed subdocuments, separator ranges, headers/footers by section and AutoText/secondary-FIB traversal.
- Implement paired/nested control state machines and deterministic current/stored/deleted/hidden projection.

Exit: every story and anchor family passes cross-story, malformed and source-location tests with no silent text loss.

### `DOC-I04` — complete bounded property engine

Owners: EXT-DOC-005; depends on `DOC-I02`, `DOC-R04`.

- Implement generated SPRM operand grammars, safe unknown skipping, PRM, CHPX/PAPX/SEPX and large-property indirection.
- Apply defaults, styles and direct properties in specified order with cycle/depth/cumulative limits.

Exit: property results are semantic rather than passive, and mutation tests cannot desynchronise the property stream silently.

### `DOC-I05` — styles, fonts, lists, tables and sections

Owners: EXT-DOC-006; depends on `DOC-I03`, `DOC-I04`, `DOC-R05`.

- Resolve style/font/language graphs and run/paragraph boundaries.
- Project deterministic list labels, nested table/cell text and section/header associations without layout reproduction.

Exit: all `EXT-DOC-006` matrix rows have conformance, differential and malformed-graph evidence.

### `DOC-I06` — fields, bookmarks, forms and external references

Owners: EXT-DOC-007; depends on `DOC-I03`, `DOC-I04`, `DOC-R06`.

- Decode nested field instruction/result ranges, bookmarks, permissions, forms/controls and supported textual values.
- Inventory all active/external targets and prove no evaluation or retrieval.

Exit: range pairing and passive security tests pass through the public result.

### `DOC-I07` — notes, comments and revisions

Owners: EXT-DOC-008; depends on `DOC-I03`, `DOC-I04`, `DOC-R06`.

- Join references to note/comment stories and author/time/reply data.
- Decode insertion, deletion, move and property revisions under the declared review-text policy.

Exit: current/deleted/hidden identity-critical evidence passes conformance and holdout-focused tests.

### `DOC-I08` — pictures, drawings, textboxes and equations

Owners: EXT-DOC-009; depends on `DOC-I03`, `DOC-I04`, `DOC-R07`.

- Implement PICF, OfficeArt containers, BLIP stores and host anchors with strict lengths/depth/counts.
- Emit only validated discrete image encodings with stable identities; project textbox/alternate/equation text where specified.

Exit: inline/floating/duplicate/malformed/expansion cases pass, and raw `Data` or drawing streams are never mislabeled as images.

### `DOC-I09` — embedded, active, custom and metadata surfaces

Owners: EXT-DOC-010, EXT-DOC-011; depends on `DOC-I01`, `DOC-I04`, `DOC-R08`.

- Implement bounded OLE identity/link/presentation descriptors and exact nested-source reconstruction for supported embedded formats.
- Add VBA/Forms/custom/signature passive evidence and complete property-set/settings metadata.
- Keep arbitrary non-image bytes out of the public payload.

Exit: nested text/images and passive descriptors pass security and byte-suppression tests without activation.

### `DOC-I10` — public projection, completeness and resource reconciliation

Owners: EXT-DOC-013; depends on `DOC-I03` through `DOC-I09`.

- Project the semantic model through the one common result and keep the CLI thin.
- Replace blanket issues with observed-feature coverage and deterministic severity/outcome rules.
- Propagate all limits, report accurate bounded measurements and enforce cumulative nested budgets.

Exit: library/CLI equivalence, all outcomes, exact limits, cancellation/deadline and text/image-only payload tests pass.

### `DOC-I11` — conformance, differential, security and fuzz closure

Owners: EXT-DOC-013; depends on `DOC-I10`, `DOC-R10`.

- Complete `DOC-T01` through `DOC-T08` for every declared supported feature.
- Run pinned oracles only in isolated opt-in test lanes and triage every semantic deviation.
- Maintain minimized, provenance-safe synthetic reproducers for fixed fuzz defects.

Exit: declared subset is conformant and differentially verified; no unresolved critical/high test-gap finding remains.

### `DOC-I12` — genuine-data, performance and acceptance closure

Owners: EXT-DOC-013; depends on `DOC-I11`, `DOC-R11`.

- Run the reviewed cohort and hidden holdout with immutable manifests.
- Measure the 10 MiB input class, worst-case decoded/property/image expansion, memory, CPU, timeout and concurrency.
- Obtain independent review from someone other than the implementation and test author.

Exit: zero silent truncation of identity-critical text/images, accepted budgets, stable retries, no corpus drift, and an authorised `Accepted` decision for the declared Word 97-family subset.

### `DOC-I13` — caller-backed release evidence

Owner: EXT-INT-001 in the caller repository; depends on accepted package evidence from `DOC-I12`.

- Prove the CollisionSpike Infrastructure adapter reaches the public extractor and translates content/assets/issues/completeness without importing extractor internals.
- Run caller-owned cohort/holdout and zero-false-case-creation gates.

Exit: `Called` and caller acceptance are evidenced separately from parser acceptance.

## Slice discipline

Each implementation pull request or bounded work item must name one or more `DOC-I*` steps and:

1. cite exact specification sections and generated-table provenance;
2. state observed inputs, supported subset and explicit unsupported branches;
3. add independent positive, malformed and bound tests before claiming implementation;
4. exercise the public extraction path when output or outcome changes;
5. preserve unknown data only when bounded and useful for control evidence;
6. update the feature matrix and evidence record with exact commands/results;
7. receive review from someone other than the implementation/test author before an acceptance label advances.

Do not merge placeholder semantic models, broad warning suppression, guessed opcodes, printable-byte recovery presented as success, or a second production parser. A fixture count, green solution build or readable happy-path text cannot close a port unit.

## Completion gate

The declared Word 97-family subset is complete only when:

- every supported `nFib` and every reachable FIB field has a reviewed owner and support classification;
- every present payload-bearing branch is either semantically extracted or produces a deterministic non-complete outcome;
- all declared stories, current/deleted/hidden text, anchors and recoverable images retain exact provenance;
- non-image and active content remains passive, unexecuted, unretrieved and absent from public asset bytes;
- conformance, semantic differential, hostile-input, fuzz, determinism, resource, performance and CLI gates pass;
- the operator cohort and hidden holdout show zero silent truncation of identity-critical text/images;
- the dependency/licence/security review and independent acceptance are recorded; and
- CollisionSpike caller evidence is reported as a separate `Called`/accepted gate.

Pre-97 files may remain explicitly unsupported after an accepted `EXT-DOC-012` decision. That does not expand the Word 97-family claim, and a `.doc` filename never overrides byte-level format detection.

## Plan validation record

Validation date: 2026-07-24. Command:

```powershell
pwsh -NoProfile -File .\scripts\Invoke-RepoCheck.ps1
```

Exit result: `0`. Locked restore, formatting and the Release build passed with zero build warnings/errors; the Microsoft.Testing.Platform solution run reported 534 tests, 533 succeeded, zero failed and one explicitly opt-in EML local-cohort test skipped; JSON parsing and local Markdown-link validation passed.

The input class was repository-owned source, synthetic tests and documentation. The command did not inspect `sample-doc-files/`, run a DOC conformance/differential/genuine-data/fuzz lane, evaluate a hidden holdout or establish DOC performance/resource acceptance. It validates the repository after this planning change, not the unimplemented DOC semantics described above.
