# PDF 1.0–2.0 extraction plan

## Boundary

PDF is one format family covering versions 1.0 through 2.0 and published extensions. The extractor reads observed structures and records the physical header version, Catalog `/Version`, revision-local effective versions, `/Extensions` and XMP/profile claims separately. A declared version is evidence, not proof of the features actually present.

PDF 2.0 was first published in July 2017; the current core edition is ISO 32000-2:2020 with the current published errata bundle. It is therefore nearly nine years old at this plan date, but real inputs can use any earlier revision or a mixture of later extensions and older constructs. Legacy Adobe/ISO references remain required. PDF/A, PDF/X, PDF/UA, PDF/E, PDF/R, PDF/VT and PDF/VCR are constrained uses or profiles of the same object model, not new top-level handlers.

Rendering, OCR, decryption, action/script execution, media playback, 3D interpretation and external retrieval are excluded. An image-only PDF can be complete for its declared subset with zero embedded text, extracted image assets and an explicit `NoEmbeddedText` issue.

The PDF payload is ordered text plus safely recoverable embedded or inline images only. Attachments, portfolios, fonts, scripts, signatures, multimedia, 3D data, XFA packets and arbitrary streams are not emitted; they are bounded inventory/control evidence unless a supported nested document yields text or images.

## Complete feature surface

### Detection, versions and extensions

- `%PDF-1.0` through `%PDF-1.7` and `%PDF-2.0`, binary marker, bounded leading data, final and incremental `%%EOF` markers.
- Physical header, current Catalog `/Version`, version per incremental revision, `/Extensions` and XMP declarations.
- Multiple headers, suspicious leading/trailing data, polyglots and unknown/proprietary extensions.
- Published ISO technical-specification extensions and clarifications, recorded by prefix/base version/extension level.
- Feature-based compatibility decisions when declared versions and actual objects disagree.

### Lexical and object syntax

- Whitespace, comments and all permitted end-of-line forms.
- Null, booleans, checked integers/reals, escaped names, literal/hexadecimal strings, arrays and dictionaries.
- PDFDocEncoding, UTF-16 text strings and PDF 2.0 UTF-8 text strings with raw/decoded provenance.
- Direct and indirect objects, generations, references and streams with exact bounded source spans.
- Duplicate dictionary keys, invalid numerics, depth/cycle limits and deterministic malformed-input policy.

### Physical structure and revision history

- Classic cross-reference tables and trailers.
- Cross-reference streams, object streams and hybrid-reference files.
- Free/in-use/compressed entries, generations, `startxref`, `/Root`, `/Info`, `/ID`, `/Encrypt`, `/Prev` and `/XRefStm`.
- Incremental chains, replacement/deletion, malicious shadowing and current-versus-historical object state.
- Linearisation dictionary, first-page section and bounded hint-table validation without a separate parser path.
- Page, name and number trees with ordering, count, depth and cycle validation.

### Stream filters

- `ASCIIHexDecode`, `ASCII85Decode`, `LZWDecode`, `FlateDecode` and `RunLengthDecode`.
- LZW `EarlyChange`, TIFF/PNG predictors, filter arrays, aligned `/DecodeParms` and chained filters.
- `CCITTFaxDecode`, `JBIG2Decode`, `DCTDecode`, `JPXDecode` and `Crypt` classification and payload preservation.
- Inline-image abbreviations and bounded termination parsing.
- Per-stage/per-stream/cumulative decoded-byte and expansion-ratio budgets.
- `/F`, `/FFilter` and `/FDecodeParms` recorded but never used to open external data.

Image codecs can remain codec-native assets with filter metadata; converting pixels to another image format is not required.

### Encryption and security handlers

- Standard security handler revisions 1–6, RC4, AES-128 and AES-256-CBC classification.
- Standard/public-key handlers, crypt filters, embedded-file-only encryption, `/EncryptMetadata`, permissions and owner/user modes.
- Custom/unknown handlers and PDF 2.0 unencrypted wrapper documents.
- Published AES-GCM and MAC-integrity extensions.
- `Encrypted` whenever material evidence cannot be read. Permissions are reported metadata, never an authorisation boundary.

No password prompt, guessing, key retrieval or decryption is implemented under the current contract.

### Catalog, pages, resources and content programs

- Catalog, page tree, inherited attributes, page ordering, boxes, rotation, user unit, thumbnails and content arrays.
- Resource dictionaries: fonts, XObjects, properties, colour spaces, patterns, shadings and extended graphics state.
- Content operand/operator syntax, graphics/text state and nested Form XObjects.
- Annotation appearances, Type 3 glyph programs and tiling-pattern programs as bounded auxiliary content programs.
- Viewer preferences, page mode/layout, requirements, permissions, output intents, page labels, destinations and article threads.
- Operator, resource, XObject, pattern and occurrence limits with cancellation inside interpretation loops.

### Fonts, Unicode and raw text

- Text objects and all text-state, positioning and showing operators.
- Type 1, Multiple Master Type 1, TrueType, Type 3, Type 0, CIDFontType0 and CIDFontType2.
- Standard, WinAnsi, MacRoman, MacExpert, Symbol and ZapfDingbats encodings plus Differences arrays.
- Embedded/predefined CMaps, codespace ranges, horizontal/vertical writing, `CIDToGIDMap` and `UseCMap`.
- `ToUnicode`, glyph-name mapping, known CID collections and embedded-font character maps with mapping-source confidence.
- `ActualText`, alternate/expansion text, language and multi-codepoint/ligature mappings.
- Missing/ambiguous maps as replacement or unknown segments with raw codes and explicit issues.
- Invisible OCR text, clipped/off-page text and optional-content membership retained with visibility flags.

The result distinguishes content/paint order, valid tagged logical order and versioned deterministic geometric order. Columns, rotations, vertical writing, bidirectional text, superscripts, spacing, line reconstruction and hyphenation require explicit heuristic/version/confidence evidence.

### Tagged PDF and optional content

- `StructTreeRoot`, structure elements, ParentTree, IDTree, RoleMap, ClassMap and namespaces.
- MCID, MCR and OBJR linkage between logical structure, page content and objects.
- PDF 1.7/PDF 2.0 structure namespaces and namespace coexistence rules.
- Artifacts, headings, paragraphs, lists, tables, captions, figures, formulas, ruby and warichu structures.
- Structure attributes, alternate/replacement text and language inheritance.
- Optional-content groups, membership dictionaries, configurations, usage and visibility expressions.

Hidden/default-off content remains evidence; default viewer visibility never silently removes it.

### Metadata and profile claims

- Document Information dictionary; Catalog/page/object XMP; language, dates, file IDs, piece/document-part information and output intents.
- Raw and parsed values for malformed dates/encodings; Info/XMP conflicts retained rather than silently resolved.
- Bounded XMP XML parsing with DTD and external entities disabled.
- PDF/A-1 through A-4 including A-4e/A-4f, PDF/X through X-6 variants, PDF/UA-1/2 and PDF/E-1 claims.
- Optional recognition of PDF/R, PDF/VT and PDF/VCR claims.

Claim recognition is not full profile conformance validation. Any validator is a separately labelled future capability and never blocks generic evidence extraction.

### Images and non-payload embedded content

- Inline images and image XObjects, dimensions, colour space, bit depth, decode arrays, interpolation and thumbnails.
- Image masks, colour-key masks, soft masks, alternates and original encoded payload/filter provenance.
- Embedded-file streams are bounded and classified; Filespec names, description, media subtype, size/checksum/dates are control evidence, but non-image file bytes are not emitted.
- EmbeddedFiles name tree, FileAttachment annotations and Associated Files with `AFRelationship`.
- Collections/portfolios, schema/item metadata, navigators and folder hierarchy as inventory only; contained file bytes are not emitted.
- Deduplication by underlying stream while retaining every relationship and occurrence.
- Handoff of supported embedded formats to the common nested extractor under cumulative budgets, emitting only derived text/images.

### Navigation, annotations and forms

- Outlines/bookmarks, explicit/named/structure destinations, page labels, article threads and beads.
- Every specification-defined annotation subtype and common fields: geometry, content/rich text, author, dates, replies, review state and appearance.
- Link, FileAttachment, Widget, Redact, Sound, Movie, Screen, RichMedia, 3D and Projection semantics.
- Warning when a Redact annotation exists but underlying content survives.
- AcroForm field trees, fully qualified names, inheritance, values/defaults, options, selected indices/export values and widget occurrences.
- Button, text, choice and signature fields, including values without appearance streams.
- XFA packet arrays/datasets as bounded passive inventory; no packet bytes, dynamic layout or form logic are emitted.

### Actions, JavaScript, multimedia and 3D

- Internal/remote go-to, URI, Launch, SubmitForm, ImportData, JavaScript, rendition, optional-content, 3D and RichMedia actions.
- Action chains and triggers traversed cycle-safely from document, page, annotation, outline and field owners.
- Target values and source locations recorded; no execution, submission, process launch or external resolution.
- JavaScript presence/hash as passive inventory; script bytes are not emitted.
- Sound, Movie, Screen, Rendition, alternate-presentation and RichMedia payload inventory without byte emission.
- U3D, PRC, STEP and glTF streams/resources and 3D JavaScript as passive inventory without byte emission or geometry interpretation.

### Digital signatures and revision forensics

- Signature fields/dictionaries, `ByteRange`, `Contents`, filters/subfilters, time, reason, location and certificate material.
- Approval, certification, usage-rights and document-timestamp signatures.
- DocMDP, FieldMDP, locks, seed values, DSS/VRI and embedded validation material.
- Signature-to-incremental-revision linkage and later-modification reporting.
- Offline byte-range structure and digest-coverage checks.
- Modern signature/hash algorithms from published extensions.

Cryptographic signature verification, certificate trust, revocation and trusted time are distinct claims. The extractor never implies trust from structural inspection alone.

### Strict parsing, bounded recovery and hostile input

- Strict parser first; optional separately labelled bounded object/xref reconstruction.
- Recovery can never produce `Complete`; reconstructed evidence and source spans are explicit.
- Missing/bad header, EOF, `startxref`, xref/trailer and `/Prev`; wrong/conflicting offsets and duplicate objects.
- Truncated/mismatched streams, corrupt filters, content operand errors and inline-image ambiguity.
- Reference/page/tree/XObject/action/CMap cycles and pathological numeric/coordinate values.
- Polyglot, shadowing and signature-wrapping cases.
- Budgets for tokens, objects, graph/tree depth, revisions, streams/decoded bytes/ratio, operators, CMap entries, glyphs, image pixels, pages, attachments and nesting.

## Deterministic projection

- Object identity includes PDF origin, revision, object number and generation.
- Image identity includes source hash, semantic role, object identity and occurrence path.
- Reused objects remain one object with multiple occurrences.
- Issues order by source offset, stable code and object path.
- Text records page, object, content stream/operator, source mapping, order source, visibility and policy version.
- Raw and decoded strings coexist when decoding is uncertain.
- Culture, current time, filesystem order and dictionary iteration cannot affect results.

## Port units

| ID | Responsibility |
|---|---|
| `EXT-PDF-001` | Standard/version/extension/profile registry, detection and fixture provenance |
| `EXT-PDF-002` | Bounded lexer, object model, strings and exact source spans |
| `EXT-PDF-003` | Core stream pipeline, ASCII/LZW/Flate/RunLength and predictors |
| `EXT-PDF-004` | Xref/object resolver, object streams, revisions, hybrids and linearisation |
| `EXT-PDF-005` | Media filters and encryption/security classification |
| `EXT-PDF-006` | Catalog, pages, trees, resources and content interpreter |
| `EXT-PDF-007` | Fonts, encodings, CMaps, Unicode and positioned raw text |
| `EXT-PDF-008` | Information/XMP metadata, IDs and profile/extension claims |
| `EXT-PDF-009` | Images, masks, embedded/associated files, collections and nesting handoff |
| `EXT-PDF-010` | Outlines, destinations, annotations, AcroForm and passive XFA |
| `EXT-PDF-011` | Tagged/logical/geometric order, article order and optional content |
| `EXT-PDF-012` | Actions, JavaScript, multimedia and 3D passive inspection |
| `EXT-PDF-013` | Signatures, byte-range/digest inspection and revision forensics |
| `EXT-PDF-014` | Projection/outcomes, recovery, security/fuzz/performance/differential and acceptance evidence |

## Evidence matrix

- Positive/negative cases for versions 1.0–2.0, header/Catalog conflicts, extensions and profile conflicts.
- Every lexical type, xref/object-stream/hybrid/revision/linearisation form.
- Every standard filter alone/chained, parameters/predictors, corruption and expansion limits.
- Encryption revisions/handlers and wrapper cases, proving no decryption attempt.
- Every font/encoding/CMap route, missing maps, vertical text, ligatures and `ActualText`.
- Tagged/untagged, columns, rotation, bidi, hidden OCR, optional content and ambiguous order.
- Images/masks plus inventory-only attachments, portfolios, Associated Files and duplicate relationships.
- Annotation/form/action/XFA/JavaScript/multimedia/3D passive cases.
- Signed revisions, partial byte ranges and later modifications.
- Strict-versus-recovery cases, fuzz/property tests, determinism/retry/concurrency and nesting limits.
- Semantic comparisons with at least two exact-version independent reference tools.

## Primary sources

- [ISO 32000-2:2020 catalogue](https://www.iso.org/standard/75839.html)
- [Current sponsored PDF 2.0 bundle and errata](https://pdfa.org/sponsored-standards/)
- [Current ISO 32000-2 errata index](https://pdf-issues.pdfa.org/32000-2-2020/)
- [PDF 1.0–1.7 specification archive](https://pdfa.org/resource/pdf-specification-archive/)
- [Adobe/ISO PDF 1.7 text](https://opensource.adobe.com/dc-acrobat-sdk-docs/pdfstandards/PDF32000_2008.pdf)
- [PDF version and Catalog override guidance](https://pdfa.org/pdf-versions/)
- [RFC 8118 `application/pdf`](https://www.rfc-editor.org/rfc/rfc8118.html)
- [Arlington machine-readable PDF model](https://github.com/pdf-association/arlington-pdf-model)
- [Official PDF 2.0 examples](https://github.com/pdf-association/pdf20examples)
