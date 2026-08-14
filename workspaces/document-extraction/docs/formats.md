# Five-format extraction contract

This document is the sole intended-format contract for CollisionDocNetExtractor. It defines required behaviour for exactly five top-level input families:

1. PDF 1.0–2.0
2. legacy binary Word `.doc`
3. WordprocessingML `.docx`
4. Outlook `.msg`
5. RFC 5322/MIME `.eml`

It specifies intended scope and acceptance evidence; it does not by itself prove implementation, use by a real caller, deployment, or acceptance. Source-recorded implementation observations are evidence limitations, not accepted behaviour. Current support is reported only in the [feature matrix](compatibility/feature-matrix.md).

The wider product, architecture, repository-development, operational-state,
and decision context remains owned by the [documentation index](../../../docs/index.md),
[requirements](../../../docs/prd/README.md), [capabilities](../../../docs/capabilities.md),
[architecture](architecture.md), [engineering](../../../docs/engineering.md),
[operations](../../../docs/operations.md), [operator notes](../../../docs/operator-notes.md),
[open decisions](../../../docs/open-decisions.md), and the
[decision index](../../../docs/adr/README.md). Operational procedure belongs to
the [runbook](../../../docs/runbook.md).

## Common contract

### Delivery and payload boundary

The only delivery surfaces are the managed library and a thin headless CLI. The intended DOC caller is the Pegasus Infrastructure adapter through the single public extraction API; that statement identifies the intended integration and does not prove a caller, deployment, or acceptance.

Every format handler returns deterministic ordered text and discrete image assets through one common result model. The following remain control evidence and do not authorise another payload type:

- format and version evidence;
- metadata and participants;
- relationships and external targets;
- nested provenance and source locations;
- structured issues and outcome;
- hashes and stable identities;
- resource measurements;
- passive-content inventories.

The product does not emit converted documents, arbitrary attachment archives, non-image embedded-object bytes, fonts, scripts, signatures, certificates, ciphertext, or opaque application data.

A supported embedded PDF, DOC, DOCX, MSG, or EML may be processed recursively only when the caller enables nesting and supplies cumulative depth and resource budgets. Only its derived text and images cross the result boundary. Unsupported embedded content remains a bounded hashed descriptor with an explicit issue; its bytes are not copied to output.

### Common format map

| Family | Required detection evidence | Intended payload | Passive or protected surfaces |
|---|---|---|---|
| PDF 1.0–2.0 | `%PDF-` plus valid object, cross-reference, trailer, and revision evidence; reconcile header, Catalog version, extensions, and profile claims | Ordered text and recoverable embedded or inline images | Encryption, attachments, portfolios, JavaScript/actions, multimedia, 3D, signatures, XFA, and incremental shadowing remain control evidence unless supported nested content yields text or images |
| Legacy `.doc` | Actual-type probe, then CFB v3/v4 with a valid `WordDocument` FIB and selected Table stream | Direct binary text from supported stories and recoverable pictures | Encryption, VBA, OLE, packages, external fields/links, custom data, and pre-97 variants remain passive, protected, or explicitly unsupported |
| `.docx` | ZIP/OPC or encrypted CFB wrapper, content types, relationships, and a WordprocessingML main part | Strict/Transitional story text, drawing/chart/diagram text, and recoverable image parts | Macros, ActiveX, OLE, packages, external relationships, signatures, custom XML, and arbitrary non-image parts remain control evidence |
| `.msg` | CFB plus valid Outlook Item property streams and storages | Textual headers, properties, body evidence, and inline or attached images | Non-image attachments, protected content, raw RTF, OLE/custom storage, signatures, and opaque MAPI values remain control evidence |
| `.eml` | Bounded Internet Message Format header evidence and MIME structure where present | Decoded textual headers, bodies, reports, and MIME image parts | Non-image attachments, TNEF, signatures, certificates, ciphertext, and opaque MIME leaves remain control evidence |

### Determinism, completeness, and outcomes

For the same bytes and configuration, semantic ordering, identities, issues, and outcomes must be stable. Culture, current time, filesystem order, dictionary iteration, and machine-default character sets must not affect results.

Stable provenance must be retained at the finest format-appropriate level, including source byte spans and semantic occurrence paths. Original filenames, part names, attachment names, and object names are metadata and must never become output paths.

A handler must not return `Complete` after silently skipping an encountered unreadable, unsupported, ambiguous, or resource-breaching branch that may contain required text or images. Fully framed and validated non-payload structures may remain inventory-only.

Applicable outcomes and qualifications include:

- `Complete`: every observed payload-relevant feature has a declared and completed treatment.
- `Partial`: useful safe evidence was retained, but relevant semantics or branches remain unresolved.
- `UnsupportedFeature`: a valid feature has no safe useful payload projection.
- `Corrupt`: structural or semantic invariants are violated.
- `Encrypted`: required material is inaccessible because of encryption or protection.
- `ResourceLimitExceeded`: a finite traversal exceeded configured limits.
- Cancellation and deadline outcomes remain distinct.
- Earlier safe evidence is retained with non-complete outcomes.
- PDF recovery can never produce `Complete`.
- An image-only PDF may be complete for its declared subset with zero embedded text, extracted images, and an explicit `NoEmbeddedText` issue.

### Safety and excluded product scope

The extractor does not edit, render, paginate, print, export, or reproduce the applications that created the files. It has no desktop UI, browser interface, ASP.NET application, hosted service, directory watcher, mailbox client, mailbox-access capability, or caller business-rule engine.

It must never:

- execute macros, fields, formulas, scripts, actions, DDE, ActiveX, Office Forms, OLE, or application objects;
- launch processes or open input-controlled paths;
- retrieve templates, links, remote images, external parts, mail fragments, keys, certificates, revocation data, or network resources;
- perform password guessing, automatic decryption, online trust verification, or DNS-backed authentication verification;
- render HTML, PDF pages, office layout, media, 3D geometry, or application controls.

OCR, AI classification, mailbox access, and caller-specific business rules remain outside this repository.

Spreadsheet, presentation, drawing, formula, and database product families are not target input families. Their embedded files may be represented only by bounded descriptors, hashes, and explicit issues; no corresponding parser or application model is planned. Chart caches, equation semantics, or picture bullets do not expand top-level product scope.

### Common resource controls

Configuration owns cumulative limits and cancellation checkpoints for applicable:

- bytes, decoded bytes, and expansion ratios;
- tokens, nodes, attributes, objects, streams, records, and operators;
- graph, tree, style, property, message, and nesting depth;
- pages, revisions, attachments, image pixels, glyphs, CMap entries, and relationships;
- CPU, elapsed deadline, and cumulative nested extraction;
- format-specific indirections, offsets, pages, and property applications.

Cycles, overlaps, invalid references, arithmetic overflow, and boundary disagreement must be detected deterministically. Active or external behaviour is never a fallback for unsupported parsing.

---

# PDF 1.0–2.0

## Boundary and version model

PDF is one family covering versions 1.0 through 2.0 and published extensions. The extractor records separately:

- physical header version;
- current Catalog `/Version`;
- revision-local effective versions;
- `/Extensions`;
- XMP and profile claims;
- observed object features.

A declaration is evidence, not proof of the features actually present. PDF 2.0 was first published in July 2017; the current core edition is ISO 32000-2:2020 with its published errata. Real inputs may use older revisions, later extensions, legacy constructs, or mixtures of them. PDF/A, PDF/X, PDF/UA, PDF/E, PDF/R, PDF/VT, and PDF/VCR are constrained uses or profiles of the same object model, not separate handlers.

Rendering, OCR, decryption, action or script execution, media playback, 3D interpretation, and external retrieval are excluded.

## Detection, syntax, and revisions

The intended parser covers:

- `%PDF-1.0` through `%PDF-1.7` and `%PDF-2.0`;
- binary marker, bounded leading data, and final or incremental `%%EOF`;
- multiple headers, suspicious leading/trailing data, polyglots, and proprietary extensions;
- feature-based compatibility when declarations and observed objects disagree;
- whitespace, comments, and all permitted end-of-line forms;
- null, Boolean, checked integer/real, escaped name, literal and hexadecimal string, array, and dictionary syntax;
- PDFDocEncoding, UTF-16 text strings, and PDF 2.0 UTF-8 text strings with raw and decoded provenance;
- direct and indirect objects, generations, references, and streams with exact bounded spans;
- duplicate dictionary keys, invalid numerics, depth limits, cycle limits, and deterministic malformed-input policy.

Physical structure includes:

- classic cross-reference tables and trailers;
- cross-reference streams, object streams, and hybrid-reference files;
- free, in-use, and compressed entries;
- generations, `startxref`, `/Root`, `/Info`, `/ID`, `/Encrypt`, `/Prev`, and `/XRefStm`;
- incremental replacement and deletion, malicious shadowing, and current-versus-historical object state;
- linearisation dictionaries, first-page sections, and bounded hint-table validation without a separate parser path;
- page, name, and number trees with count, ordering, depth, and cycle validation.

## Streams, filters, and encryption

Required filter handling includes:

- `ASCIIHexDecode`, `ASCII85Decode`, `LZWDecode`, `FlateDecode`, and `RunLengthDecode`;
- LZW `EarlyChange`;
- TIFF and PNG predictors;
- filter arrays, aligned `/DecodeParms`, and chained filters;
- classification and payload preservation for `CCITTFaxDecode`, `JBIG2Decode`, `DCTDecode`, `JPXDecode`, and `Crypt`;
- inline-image abbreviations and bounded termination parsing;
- per-stage, per-stream, and cumulative decoded-byte and expansion-ratio budgets.

`/F`, `/FFilter`, and `/FDecodeParms` are recorded but never used to open external data. Image codecs may remain codec-native assets with filter metadata; pixel conversion is not required.

Encryption handling classifies:

- Standard security handler revisions 1–6;
- RC4, AES-128, and AES-256-CBC;
- public-key handlers and crypt filters;
- embedded-file-only encryption;
- `/EncryptMetadata`, permissions, and owner/user modes;
- custom or unknown handlers;
- PDF 2.0 unencrypted wrapper documents;
- published AES-GCM and MAC-integrity extensions.

Unreadable required material produces `Encrypted`. Permissions are metadata, not an authorisation boundary. The current contract includes no password prompt, guessing, key retrieval, or decryption.

## Pages, content, fonts, and text

The object model covers:

- Catalog and page tree;
- inherited page attributes, ordering, boxes, rotation, user unit, thumbnails, and content arrays;
- resource dictionaries for fonts, XObjects, properties, colour spaces, patterns, shadings, and extended graphics state;
- content operands/operators, graphics and text state, and nested Form XObjects;
- bounded auxiliary interpretation of annotation appearances, Type 3 glyph programs, and tiling-pattern programs;
- viewer preferences, page mode/layout, requirements, permissions, output intents, page labels, destinations, and article threads;
- resource, operator, XObject, pattern, and occurrence limits with in-loop cancellation.

Text handling includes:

- all text objects and text-state, positioning, and showing operators;
- Type 1, Multiple Master Type 1, TrueType, Type 3, Type 0, CIDFontType0, and CIDFontType2;
- Standard, WinAnsi, MacRoman, MacExpert, Symbol, and ZapfDingbats encodings and Differences arrays;
- embedded and predefined CMaps, code-space ranges, horizontal/vertical writing, `CIDToGIDMap`, and `UseCMap`;
- `ToUnicode`, glyph-name mapping, known CID collections, and embedded-font character maps with mapping-source confidence;
- `ActualText`, alternate/expansion text, language, and multi-codepoint or ligature mappings;
- replacement or unknown segments with raw codes and explicit issues when mapping is absent or ambiguous;
- invisible OCR text, clipped/off-page text, and optional-content membership with visibility flags.

The result distinguishes content/paint order, valid tagged logical order, and a versioned deterministic geometric order. Columns, rotation, vertical writing, bidirectional text, superscripts, spacing, line reconstruction, and hyphenation require explicit heuristic version and confidence evidence.

## Tagged structure and optional content

The intended surface includes:

- `StructTreeRoot`, structure elements, ParentTree, IDTree, RoleMap, ClassMap, and namespaces;
- MCID, MCR, and OBJR linkage among logical structure, page content, and objects;
- PDF 1.7 and PDF 2.0 structure namespaces and coexistence rules;
- artifacts, headings, paragraphs, lists, tables, captions, figures, formulas, ruby, and warichu;
- structure attributes, alternate/replacement text, and language inheritance;
- optional-content groups, membership dictionaries, configurations, usage, and visibility expressions.

Hidden and default-off content remains evidence; default viewer visibility must not silently remove it.

## Metadata and profiles

The extractor records:

- Document Information;
- Catalog, page, and object XMP;
- language, dates, file IDs, piece/document-part information, and output intents;
- raw and parsed malformed dates or encodings;
- Info/XMP conflicts without silently resolving them;
- bounded XMP XML with DTD and external entities disabled;
- claims for PDF/A-1 through A-4, including A-4e/A-4f;
- PDF/X through X-6 variants;
- PDF/UA-1 and PDF/UA-2;
- PDF/E-1;
- optionally PDF/R, PDF/VT, and PDF/VCR.

Claim recognition is not profile-conformance validation. A validator remains a separately labelled future capability and must not block generic evidence extraction.

## Images, files, annotations, forms, and active content

Image handling covers:

- inline images and image XObjects;
- dimensions, colour spaces, bit depth, decode arrays, interpolation, and thumbnails;
- image masks, colour-key masks, soft masks, alternates, and original encoded/filter provenance;
- deduplication by underlying stream while retaining all relationships and occurrences.

Embedded-file streams are bounded and classified. Filespec names, description, media subtype, declared size/checksum/dates, EmbeddedFiles trees, FileAttachment annotations, Associated Files, and `AFRelationship` are control evidence. Collections and portfolios, including schema, item metadata, navigators, and folder hierarchy, are inventory-only. Non-image file bytes are not emitted.

Navigation and interactive evidence includes:

- outlines and bookmarks;
- explicit, named, and structure destinations;
- page labels, article threads, and beads;
- every specification-defined annotation subtype and common geometry, content, rich text, author, date, reply, review-state, and appearance fields;
- Link, FileAttachment, Widget, Redact, Sound, Movie, Screen, RichMedia, 3D, and Projection semantics;
- a warning when a Redact annotation exists but underlying content survives;
- AcroForm trees, qualified names, inheritance, values/defaults, options, selected indices/export values, and widgets;
- button, text, choice, and signature values even without appearance streams;
- bounded passive XFA packet and dataset inventory without emitting packet bytes, dynamic layout, or form logic.

Actions are traversed cycle-safely from document, page, annotation, outline, and field owners. Internal/remote go-to, URI, Launch, SubmitForm, ImportData, JavaScript, rendition, optional-content, 3D, and RichMedia targets are recorded without execution, submission, launch, or resolution. JavaScript is represented by presence and hash; script bytes are not emitted.

Sound, Movie, Screen, Rendition, alternate-presentation, RichMedia, U3D, PRC, STEP, glTF, and 3D JavaScript remain passive inventory without payload-byte emission or geometry interpretation.

## Signatures, recovery, and hostile input

Signature and revision evidence includes:

- signature fields and dictionaries;
- `ByteRange`, `Contents`, filters/subfilters, time, reason, location, and certificate material;
- approval, certification, usage-rights, and document-timestamp signatures;
- DocMDP, FieldMDP, locks, seed values, DSS/VRI, and embedded validation material;
- signature-to-revision linkage and later-modification reporting;
- offline byte-range structure and digest-coverage checks;
- modern signature and hash algorithms from published extensions.

Structural inspection, digest coverage, cryptographic verification, certificate trust, revocation, and trusted time are distinct claims. Structural parsing never implies trust.

Parsing is strict first. Optional bounded object/xref reconstruction is separately labelled and can never produce `Complete`. Recovered objects and source spans must remain explicit.

Hostile-input coverage includes:

- missing or bad header, EOF, `startxref`, xref/trailer, or `/Prev`;
- conflicting offsets and duplicate objects;
- truncated or mismatched streams;
- corrupt filters, operand errors, and inline-image ambiguity;
- reference, page, tree, XObject, action, and CMap cycles;
- pathological numeric and coordinate values;
- polyglots, object shadowing, and signature wrapping;
- limits for tokens, objects, trees, graphs, revisions, streams, decoded bytes, ratios, operators, CMaps, glyphs, image pixels, pages, attachments, and nesting.

## Deterministic PDF projection

- Object identity includes origin, revision, object number, and generation.
- Image identity includes source hash, semantic role, object identity, and occurrence path.
- Reused objects remain one object with multiple occurrences.
- Issues order by source offset, stable code, and object path.
- Text records page, object, content stream/operator, source mapping, ordering source, visibility, and policy version.
- Raw and decoded strings coexist when decoding is uncertain.

## PDF work decomposition and acceptance evidence

| ID | Responsibility |
|---|---|
| `EXT-PDF-001` | Standard/version/extension/profile registry, detection, and fixture provenance |
| `EXT-PDF-002` | Bounded lexer, object model, strings, and exact spans |
| `EXT-PDF-003` | Core filters and predictors |
| `EXT-PDF-004` | Xref/object resolution, object streams, revisions, hybrids, and linearisation |
| `EXT-PDF-005` | Media filters and encryption/security classification |
| `EXT-PDF-006` | Catalog, pages, trees, resources, and content interpretation |
| `EXT-PDF-007` | Fonts, encodings, CMaps, Unicode, and positioned raw text |
| `EXT-PDF-008` | Information/XMP metadata, IDs, profiles, and extension claims |
| `EXT-PDF-009` | Images, masks, embedded/associated files, collections, and nesting handoff |
| `EXT-PDF-010` | Navigation, annotations, AcroForm, and passive XFA |
| `EXT-PDF-011` | Tagged/logical/geometric order, article order, and optional content |
| `EXT-PDF-012` | Actions, JavaScript, multimedia, and 3D passive inspection |
| `EXT-PDF-013` | Signatures, digest coverage, and revision forensics |
| `EXT-PDF-014` | Projection, outcomes, recovery, security, fuzzing, performance, differential testing, and acceptance |

Acceptance evidence must cover declared-version conflicts; all lexical, xref, object-stream, hybrid, revision, and linearisation forms; every standard filter alone and chained; corruption and expansion limits; encryption handlers with proof of no decryption attempt; every font/CMap route; tagged and untagged ordering; hidden and optional content; images and passive attachments; annotations, forms, actions, XFA, JavaScript, multimedia, and 3D; signed revisions and later modifications; recovery labelling; determinism, retry, concurrency, and nesting limits; and semantic comparison with at least two exact-version independent reference tools.

## PDF primary sources

- ISO 32000-2:2020 catalogue — `https://www.iso.org/standard/75839.html`
- Sponsored PDF 2.0 bundle and errata — `https://pdfa.org/sponsored-standards/`
- ISO 32000-2 errata index — `https://pdf-issues.pdfa.org/32000-2-2020/`
- PDF 1.0–1.7 archive — `https://pdfa.org/resource/pdf-specification-archive/`
- Adobe/ISO PDF 1.7 text — `https://opensource.adobe.com/dc-acrobat-sdk-docs/pdfstandards/PDF32000_2008.pdf`
- PDF version and Catalog override guidance — `https://pdfa.org/pdf-versions/`
- RFC 8118, `application/pdf` — `https://www.rfc-editor.org/rfc/rfc8118.html`
- Arlington machine-readable PDF model — `https://github.com/pdf-association/arlington-pdf-model`
- Official PDF 2.0 examples — `https://github.com/pdf-association/pdf20examples`

---

# Legacy binary Word `.doc`

## Boundary and authority

DOC is parsed directly and must never be converted to DOCX, XML, HTML, or another intermediary:

```text
CFB -> WordDocument/FIB -> selected Table -> CLX/Pcdt/PlcPcd
    -> logical CP-to-FC mapping -> compressed or UTF-16LE text
    -> binary property/review/asset structures -> common extraction result
```

The baseline is `[MS-DOC]` revision 12.5, published 2026-02-17, plus pinned supporting specifications. The text/story and property-engine mappings use SHA-256:

```text
2e48b21886ebdd5dcc281c3d9baf1b7841c9f3d6881a153862069bbbc0608d7a
```

The mapped text/story contract was specified for `DOC-R03` on 2026-07-24. The property overlay was strengthened for `DOC-R04` on that date but was not closed; the source evidence describes only an implemented framing subset and does not prove a real caller, deployment, or accepted behaviour.

`[MS-DOC]` covers the Word 97-family binary format. A `.doc` extension may instead contain pre-97 Word, RTF, HTML, MHTML, plain text, OOXML, PDF, or arbitrary renamed data. Byte-level actual-format classification and an explicit support decision are required before Word parsing.

Rendering, pagination, field execution, template or link retrieval, COM/OLE activation, macro execution, and conversion are excluded.

## Container, FIB, versions, and encryption

The intended container profile covers:

- CFB major versions 3 and 4;
- header, DIFAT/FAT, miniFAT, directory hierarchy, stream chains, and checked sector arithmetic;
- cycles, overlaps, cross-links, invalid trees, duplicate names, truncation, and size disagreement;
- required `WordDocument` and selected `0Table` or `1Table`;
- optional `Data`, `ObjectPool`, property sets, VBA/Macro, custom XML, and signature storages;
- structural distinction among Word binary, MSG, encrypted OOXML, and unrelated CFB;
- detection of RTF, HTML/MHTML, plain text, OOXML, PDF, and renamed inputs before DOC parsing.

The FIB contract includes:

- variable-length FIBs with unknown trailing fields retained;
- `wIdent`, `nFib`, `nFibNew`, `fWhichTblStm`, `fComplex`, `fHasPic`, template/AutoText, and encryption flags;
- exact supported Word 97-family identities `0x00C1`, `0x00D9`, `0x0101`, `0x010C`, and `0x0112`;
- story lengths and a complete bounded `fc/lcb` catalogue;
- secondary FIB and AutoText through `pnNext`;
- XOR obfuscation, binary RC4, and RC4 CryptoAPI classification as `Encrypted`;
- no password guessing or decryption.

Pre-97 Word 6/95, Word 2 or earlier, and Macintosh/resource-fork variants require separate sources and decisions. `[MS-DOC]` does not justify claiming them. Each must be parsed from authoritative or clean-room evidence or returned as a specifically identified unsupported variant. `.dot`, mislabeled RTF/HTML/MHTML/plain text, OOXML/PDF, and damaged-FIB recovery remain detector cases.

## Exact CLX and text retrieval contract

Every supported C1/D9/101/10C/112 document obtains text through the selected Table stream’s non-empty CLX. There is no modern simple-file fallback and no `fcMin`/`fcMac` text route. `FibBase.fComplex` records whether the last save was incremental; both values still require the current CLX. Missing or zero `lcbClx` is `Corrupt`.

A CLX contains zero or more `Prc` records followed by exactly one final `Pcdt`:

- each `Prc` begins with `0x01`;
- its signed length is nonnegative and no greater than `0x3FA2`;
- its body contains only whole `Prl` records;
- `Pcdt` begins with `0x02`;
- its bounded `PlcPcd` length is exactly `4 + 12n` for positive piece count `n`;
- no bytes may follow the final `Pcdt` within CLX.

The `n+1` CP boundaries are unsigned 32-bit values below `0x7FFFFFFF`. They begin at zero and are unique and strictly ascending.

Pieces are decoded in logical CP order, never physical FC order. Incremental saves may produce discontiguous, descending, shared, or overlapping physical ranges. Those layouts are valid when each referenced range fits both the `WordDocument` stream and normative `FibRgLw97.cbMac`; bytes at or after `cbMac` have no meaning.

Addressing is:

```text
uncompressed byte address = fc + 2 * cpDelta
compressed byte address   = fc / 2 + cpDelta
```

All arithmetic is checked. `FcCompressed.r1` and `Pcd.fDirty` must be zero. If `Pcd.fNoParaLast` is one, the piece’s text must contain no U+000D paragraph mark.

Provenance retains:

- piece identity;
- exact `WordDocument` byte spans;
- global and part-relative CP ranges;
- compressed/uncompressed mode;
- valid surrogate pairs split across physically separate pieces.

Quick-save fields validate save history but never select stale text:

- C1 stores `0..15` in base `cQuickSaves`;
- D9 and later require base `0xF` and store `0..15` in `cQuickSavesNew`;
- the current FIB, selected Table stream, and current CLX are authoritative.

`Prm0` applies one mapped property. `Prm1` indexes a preceding CLX `Prc`. Invalid indices or malformed property records are `Corrupt`. A valid unimplemented property that may alter text, visibility, revision state, symbol meaning, or special-character interpretation prevents `Complete`.

A printable-byte scan is diagnostic recovery only and can never produce `Complete`.

## Exact DOC encoding contract

`FcCompressed` selects the complete base decoder:

- uncompressed text is UTF-16LE code units;
- compressed text consumes exactly one byte per CP;
- each compressed byte maps to the same-valued Unicode code point except for the 24 substitutions frozen in `doc-text-story-contract.v1.json`;
- bytes `0x80`, `0x8E`, and `0x9E` map to U+0080, U+008E, and U+009E, not Euro, Z-caron, or z-caron.

No FIB language, `lidFE`, font charset, Windows code page, or DBCS state selects another story decoder. East Asian byte pairs remain two CPs. RTL and complex-script properties do not reorder text visually.

`sprmCSymbol` is a semantic override for special U+0028: emit `CSymbolOperand.xchar`, retain `ftc`, and do not invoke a font engine or guess glyph mappings.

CP accounting remains by UTF-16 code unit. A valid surrogate pair consumes two CPs and four source bytes, including across a piece boundary. `[MS-DOC]` defines no isolated-surrogate recovery rule; product policy is to emit U+FFFD for each isolated unit with exact CP/byte evidence, retain other readable text, and return `Corrupt`.

## Exact story and guard contract

Modern layouts contain seven contiguous part counts in this order:

1. main;
2. footnote;
3. header;
4. comment;
5. endnote;
6. main textbox;
7. header textbox.

The field between `ccpHdd` and `ccpAtn` is `reserved3`. It must be zero and is never a macro story.

The main part must be non-empty and end in U+000D. If any specialised part is non-empty, exactly one additional U+000D follows the last non-empty part, lies outside all parts, is validated, and is omitted from output. There is no gap before footnotes or another specialised part. If all specialised parts are empty, the final `PlcPcd` CP equals `ccpText` and no outside guard exists.

The header part is subdivided by `PlcfHdd`. Its first six stories are:

1. footnote separator;
2. footnote continuation separator;
3. footnote continuation notice;
4. endnote separator;
5. endnote continuation separator;
6. endnote continuation notice.

Each main-document section then contributes, in order:

1. even header;
2. odd header;
3. even footer;
4. odd footer;
5. first-page header;
6. first-page footer.

A non-empty header story ends in a U+000D guard excluded from content. Other specialised parts and textboxes are split and anchored by their owning PLCs.

A secondary AutoText FIB has its own bounded CLX and the shared fields required by `[MS-DOC]`. Named `SttbfGlsy` and `PlcfGlsy` ranges follow primary evidence deterministically. If anchor, name, or range semantics are missing, decoded text remains visible as partial evidence rather than being dropped.

## Safe DOC review projection

Lossless typed tokens are retained before review-text normalisation. Projection never executes fields, follows links, evaluates layout, or retrieves content.

- Tabs emit tab.
- Paragraph, line, column, and resolved page or section boundaries emit newline.
- Header guards and the outside-part final mark emit nothing.
- Cell boundaries emit tab and row boundaries emit newline only after paragraph properties distinguish them.
- Picture, drawing, automatic-note, and comment anchors emit no literal text and hand off to their owning semantic unit.
- Fields require `sprmCFSpec`, valid `Plcfld` agreement, and valid nesting. Emit stored result text, never evaluate instructions. Preserve instruction text as non-primary evidence. Do not invent a missing result.
- Structured-document-tag markers emit nothing.
- En and em space specials emit their Unicode space.
- Symbols emit `xchar` with font provenance.
- Unknown special controls emit no raw control byte, remain typed evidence, and prevent `Complete`.
- U+001E/U+001F hyphen assumptions remain unsupported pending conformance or differential evidence.

Canonical review order is:

1. main;
2. anchored footnotes;
3. section-associated headers and footers;
4. anchored comments;
5. anchored endnotes;
6. main textboxes by anchor;
7. header textboxes by owner and anchor;
8. named AutoText.

Until anchors exist, stored-order decoded parts remain visible as partial evidence and are never silently omitted.

## Exact property-engine contract

### Catalogue, framing, and array membership

`EXT-DOC-005` owns bounded property parsing; inbound owners are `EXT-DOC-002` and `EXT-DOC-003`, downstream semantics are `EXT-DOC-006` through `EXT-DOC-009`, and public projection is `EXT-DOC-013`.

The generated `doc-sprm-catalogue.v1.json` is derived from the hash-pinned publication by:

```text
scripts/Generate-DocSprmCatalogue.ps1
```

The committed catalogue freezes all 322 names and opcodes, decoded fields, operand framing, typed grammar and validator, legal property arrays, five supported `nFib` identities, application conditions, extraction relevance, concrete mutation family and state key, Data-stream targets, source paragraphs, and definition hashes. Ownership is reviewed per row; the generator must not infer relevance from names or infer style arrays from the SPRM group. The catalogue is a compact derived index, not redistributed specification prose.

Catalogue counts are:

- 91 paragraph SPRMs;
- 84 character SPRMs;
- 8 picture SPRMs;
- 59 section SPRMs;
- 80 table SPRMs.

For `spra` values zero through seven, counts are `25/80/59/41/26/9/75/7`.

Every 16-bit opcode must round-trip:

```text
ispmd = opcode & 0x01FF
fSpec = (opcode >> 9) & 1
sgc   = (opcode >> 10) & 7
spra  = (opcode >> 13) & 7
```

Operand framing is exact:

- `spra` 0 or 1: one byte;
- `spra` 2, 4, or 5: two bytes;
- `spra` 3: four bytes;
- `spra` 7: three bytes;
- ordinary `spra=6`: one-byte `cb` followed by exactly `cb` bytes;
- `sprmTDefTable` (`0xD608`): UInt16 `cb`, total operand length `cb+1`;
- `sprmPChgTabs` (`0xC615`): ordinary framing below `0xFF`; at `0xFF`, checked deleted/added tab counts determine length.

Unknown opcodes may be retained only after their exact boundary is proved. Their relevance remains unknown, so an occurrence in an active range prevents `Complete`.

Version-looking name suffixes do not select applicability. Applicability is explicit for all five layouts. Six legacy table-shading SPRMs carry the source rule to ignore them above D9 when table styles are understood. Style permutation, list level, HugePapx placement, header-row continuity, section numbering, and other conditional operations retain row-level application conditions and validator obligations.

Style-owned arrays are narrower than direct-formatting arrays:

- `UPX-CHPX` excludes reset, style, conditional, and bullet SPRMs prohibited by `[MS-DOC]` section 2.9.336.
- `UPX-PAPX` excludes prohibited style selection, nesting, tab mutation, huge/Data indirection, and conditional SPRMs.
- `UPX-TAPX` applies the UpxTapx exclusions, including direct table definition, structural cell mutation, and raw shading.
- `UPX-TAPX` ignores `sprmTIstd`.
- The built-in style-11 `sprmTWidthBefore` exception remains a typed application condition.

A row absent from an array is `Corrupt` if encountered there; it must not be silently treated as direct formatting.

### Property storage and provenance

`Prm0` is the exact compact table in `[MS-DOC]` section 2.9.215. Reserved `isprm` values are not invented. `isprm=0,val=0` has no effect.

`Prm1` is a zero-based reference to an already preceding CLX `Prc`. `PrcData.cbGrpprl` is signed, lies in `0..0x3FA2`, and contains only valid whole `Prl` records. Paragraph application retains paragraph-group effects; character application retains character-group effects. Exact PCD/PRC provenance remains, and a referenced PRC is not also reported as unapplied.

`PlcBteChpx` and `PlcBtePapx` contain strictly ascending unique FC boundaries. Checked page number multiplied by 512 selects a complete `WordDocument` FKP page. BTE endpoints must agree with the selected FKP. Aliased pages, shared property records, and overlapping logical piece mappings are represented explicitly rather than resolved by first match.

`ChpxFkp` rules:

- `crun` is `1..0x65`;
- it owns `crun+1` FCs and `crun` byte offsets multiplied by two;
- zero offset means defaults;
- `Chpx.cb` bounds one complete property array.

`PapxFkp` rules:

- `cpara` is `1..0x1D`;
- it owns `cpara+1` FCs and complete 13-byte `BxPap` records;
- zero offset means defaults;
- for a nonzero first `cb`, `GrpPrlAndIstd` is `2*cb-1` bytes, leaving `2*cb-3` after the two-byte `istd`;
- if the first `cb` is zero, `cb' >= 1` owns exactly `2*cb'` bytes;
- property heaps must not overlap run metadata or unrelated adjacent records.

`PlcfSed` maps ordered section CPs. Each non-sentinel `Sed.fcSepx` selects a bounded `WordDocument` `Sepx`; its length and complete SPRM array must be valid.

Each physical property interval is normalised across every intersecting logical piece, story, and semantic boundary. Endpoint ownership is half-open. Exact FC, global/story CP, stream, FKP page, record, and property-byte provenance remains stable.

### Effective state and styles

SPRM arrays are ordered transitions. Later applicable entries win unless an individual grammar specifies otherwise. State snapshots retain both the winning value and source.

Paragraph state applies, in order:

1. specification and stylesheet defaults;
2. table-style paragraph properties and table conditional state;
3. base paragraph styles parent-first;
4. current paragraph style;
5. direct PAPX;
6. paragraph-group piece PRM;
7. list-derived paragraph state.

Character state applies:

1. stylesheet font defaults;
2. table-style character properties;
3. matching table conditional character formatting;
4. paragraph-derived character style;
5. current character style, including valid `sprmCIstd` transitions;
6. direct CHPX;
7. character-group piece PRM.

Table conditional order is horizontal bands, vertical bands, first/last column, first/last row, then corners. Section defaults and ordered SEPX form a separate state.

An `istd` is `0x0000..0x0FFD` and selects a nonempty style. `istdBase=0x0FFF` means no parent. Otherwise parent, next, and link references must select valid nonempty styles. Self-reference and cycles are `Corrupt`.

`cupx` and revision forms must match exact style-kind counts. Typed UPX members appear in required order, even-size padding bytes are zero, and array group/opcode exclusions are enforced.

Relevance is conservative:

- visibility, revision, field hiding, special/symbol/font/language/script state is text-critical;
- paragraph, list, table, cell, row, section, and story linkage is structure-critical;
- picture, Data, and OLE discriminator state is image-critical;
- decoration, borders, shading, and page geometry are rendering-only only when they cannot change logical text, image identity, or ordering;
- all eight picture-group SPRMs are border properties and payload-neutral after validation;
- proofing, UI, printing, and session properties remain passive compatibility evidence.

### Data indirection, limits, and failures

Only these SPRMs directly identify Data-stream state:

- `sprmCPicLocation` (`0x6A03`);
- `sprmPHugePapx` (`0x6646`);
- `sprmPTableProps` (`0x646B`).

Huge-PAPX and table-property offsets select bounded `PrcData` with `cbGrpprl >= 10`. A processed huge property terminates its containing array as specified. HugePapx must be first and has stricter `GrpPrlAndIstd` constraints.

Chains use checked offsets, visited sets, and cumulative depth, count, and byte budgets. Cycles are `Corrupt`; finite traversal beyond configuration is `ResourceLimitExceeded`.

DOC property configuration owns cumulative property bytes, PRC bytes, FKP/PLC pages and records, property applications, style depth, Data offsets and dereferences, image/object references, CPU/deadline, and cancellation checkpoints.

No property may cause process, link, path, network, OLE, macro, or field execution.

The following are `Corrupt`:

- truncation or invalid exact sizes;
- descending or duplicate ranges;
- property/table overlap;
- invalid references;
- prohibited array membership;
- style cycles;
- Data cycles.

Valid unsupported relevant semantics with useful evidence produce `Partial`; without safe useful projection they produce `UnsupportedFeature`. Bounds, cancellation, and deadlines retain distinct outcomes. `Complete` requires every observed relevant property to be applied and every ignored property to be fully framed, validated, and proved payload-neutral.

### Property executable evidence and unresolved closure

`DocR04ExecutableSpecificationTests` must be independent and must not call production property parsers or constants. It covers:

- all eight framing forms and both exceptions;
- PRM/PRC group filtering;
- PLC/BTE/FKP layouts;
- both PAPX forms;
- SEPX;
- literal cascade snapshots;
- styles and cycles;
- Data indirection and cycles;
- exact limits and deterministic outcomes.

Generated row tests use the committed catalogue, while expected framing and transitions are independently encoded.

The catalogue supplies deterministic validator-dispatch and mutation-family identities for every row, but it does not yet encode every definition-specific numeric domain, cross-field precondition, index range, default, relative/additive operation, or legacy replacement interaction. Named complex operands still depend on typed validator contracts. Generic last-applicable-wins mutation families still require a reviewed per-SPRM exception audit.

Until that executable overlay and independent row tests exist, the `DOC-R04` exit condition that implementation can proceed solely from tables and state transitions is not met.

## Remaining DOC semantic surface

Beyond the exact text, story, and property contracts above, the intended DOC surface includes:

- all headers/footers, notes, comments, endnotes, textboxes, AutoText, and subdocuments;
- character and paragraph bin tables, FKPs, section PLC/SEPX, styles, fonts, lists, tables, and sections;
- hidden and deleted text, language/script state, revision state, and semantic run/paragraph boundaries;
- list definitions, overrides, levels, restarts, picture bullets, and generated labels;
- nested tables, cells, rows, grids, merges, header rows, and table styles;
- fields with instruction/result separation, bookmarks, permissions, protection ranges, forms, SDTs, custom-XML mappings, citations, variables, and mail-merge metadata;
- comments, authors, dates, replies, tracked insertions/deletions/moves/property revisions, timestamps, and save IDs;
- inline `PICFAndOfficeArtData`, floating OfficeArt/BLIPs, anchors, alternate text, textboxes, VML, and safely recoverable native image representations;
- equation text and image representations without emitting equation/OLE object bytes;
- linked-picture targets without retrieval;
- OLE1/OLE2, `ObjectPool`, `ObjInfo`, `CompObj`, class/ProgID, presentations, and native streams as passive inventory;
- VBA and Office Forms inventory, with any passive VBA source extraction deferred to a separate security and licence decision;
- custom XML with DTD and external entities disabled;
- legacy/XML signature presence and bounded offline metadata or coverage inspection;
- Summary Information, Document Summary Information, user-defined properties, title, subject, author, keywords, comments, company, manager, application identity, timestamps, cached statistics labelled unverified, DOP settings, protection, templates, sources, object/image links, and hyperlinks.

Only text and images are payload. OLE/native objects, VBA, forms, packages, fonts, custom data, arbitrary streams, and linked targets remain control evidence.

## DOC deterministic projection

Preserve:

- story type, global/story CP, FC/byte range, CFB stream, and originating record;
- stored/current/deleted/hidden text and field instruction/result distinctions;
- stable identities and anchors for lists, tables, fields, bookmarks, comments, revisions, shapes, and objects;
- original object and attachment names only as metadata.

Any unread branch that may contain required text or images prevents `Complete`.

## DOC evidence limitations recorded by the source mapping

The source mapping recorded these production differences; they are requirements for `DOC-I01` through `DOC-I05` and `DOC-I10`, not accepted behaviour and not proof of current state:

- reserved FibBase bytes were read as character-set or text-range fields;
- `cbMac` was not used for piece bounds;
- all five versions reused a non-versioned minimal FIB shape;
- a reserved `FibRgLw97` value was exposed as `Macro`;
- the outside-part U+000D was inserted before Footnote instead of after the last specialised part;
- compressed text used a CP1252-like table and rejected bytes using fabricated state;
- isolated UTF-16 surrogates passed silently;
- PRCs were reported unapplied even when referenced;
- raw character values selected controls without effective properties;
- DOC public locations lost structured CP/part identity and labelled issue offsets as the Table stream;
- only twelve opcodes across ten semantic categories and seven compact PRM mappings were recognised;
- generic `spra=6` handling missed both framing exceptions;
- PAPX over-read one byte and omitted the `cb=0` form;
- ambiguous CHPX/PAPX mapping chose a first physical piece;
- SEPX, effective state, styles, Data indirection, and cumulative property budgets were absent;
- public extraction discarded property runs, labelled every issue as a warning, and could describe processed ranges as unprocessed.

## DOC work decomposition and acceptance evidence

| ID | Responsibility |
|---|---|
| `EXT-DOC-001` | Actual-format classifier, Word CFB/FIB family, pre-97 and mislabeled classification |
| `EXT-DOC-002` | Streams, FIB/version map, and encryption gate |
| `EXT-DOC-003` | CLX/Pcdt/PlcPcd, CP/FC mapping, and direct text |
| `EXT-DOC-004` | Stories, controls, anchors, and source locations |
| `EXT-DOC-005` | PLC/FKP/PRM/SPRM property engine |
| `EXT-DOC-006` | Styles, fonts, lists, paragraphs, tables, and sections |
| `EXT-DOC-007` | Fields, bookmarks, hyperlinks, forms, SDTs, and external references |
| `EXT-DOC-008` | Notes, comments, and tracked revisions |
| `EXT-DOC-009` | Textboxes, OfficeArt, pictures, and equation representations |
| `EXT-DOC-010` | OLE, packages, VBA, and Office Forms |
| `EXT-DOC-011` | Metadata, custom XML, settings, and signatures |
| `EXT-DOC-012` | Pre-97 research/parser decision |
| `EXT-DOC-013` | Projection, security, fuzzing, conformance, differential, corpus, performance, and acceptance |

Independent fixture groups are:

| ID | Required coverage |
|---|---|
| `DOC-T01` | Five exact version layouts, both `fComplex` values, quick-save boundaries, and all CP/FC primitives |
| `DOC-T02` | Zero/one/multiple PRCs, Prm0/Prm1, CLX/Pcdt/PlcPcd boundaries, logical/physical permutations, exact and end+1 `cbMac`, and malformed UTF-16 |
| `DOC-T03` | Each of seven parts alone and combined, header kinds, secondary AutoText, every control/property combination, exact projection, and provenance |

Expected literals and version layouts must be generated independently from production tables. Each test asserts exact format/part, CP and byte spans, typed token, review text, issue code, severity, location, order, and public outcome.

The source evidence also records invalid test assumptions that must not define the contract: five-version rows reused one invalid 34-range layout; a Main+Footnote test placed the final guard incorrectly; and a fake character-set test enforced a nonexistent decoder.

Acceptance also requires all `nFib` families and Table streams, secondary FIB, encryption variants, mixed and physically disordered pieces, every story/control family, malformed ranges and cycles, style/list/table/section graphs, nested fields/bookmarks/tables, hidden/deleted/revision evidence, passive pictures/OLE/VBA/Forms/custom XML/targets, mislabeled and pre-97 classification, determinism, cancellation, concurrency, genuine cohorts, and hidden holdouts.

## DOC primary sources

- `[MS-DOC]` revision 12.5 — `https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-doc/ccd7b486-7881-484c-a137-51170af7cc22`
- `[MS-CFB]` revision 12.0 — `https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-cfb/53989ce4-7b05-4f8d-829b-d08d6148375b`
- `[MS-DOC]` retrieving text — `https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-doc/01d5d8c4-cf9c-4ef9-80fd-439e763cfe01`
- `[MS-DOC]` FIB versions — `https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-doc/175d2fe1-92dd-45d2-b091-1fe8a0c0d40a`
- `[MS-DOC]` property storage — `https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-doc/9ac56e29-8488-4b0a-a009-86a26e2f175e`
- `[MS-OFFCRYPTO]` — `https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-offcrypto/3c34d72a-1a61-4b52-a893-196f9157f083`
- `[MS-ODRAW]` — `https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-odraw/8560795e-7759-4745-838f-f7f2ef2f1872`
- `[MS-OLEDS]` — `https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-oleds/85583d21-c1cf-4afe-a35f-d6701c5fbb6f`
- `[MS-OVBA]` — `https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-ovba/575462ba-bf67-4190-9fac-c275523c75fc`

---

# WordprocessingML `.docx`

## Boundary and package model

DOCX is an independent input format, normally a ZIP/Open Packaging Conventions package containing WordprocessingML and related parts. It is never an intermediate representation for DOC.

The baseline is ECMA-376 fifth edition: Part 1 (2016), Part 2 OPC (2021), Part 3 Markup Compatibility (2015), and Part 4 Transitional features (2016), plus pinned Microsoft extensions and implementation information. Strict and Transitional documents are both in scope.

Rendering, layout reproduction, field evaluation, macro or ActiveX execution, external relationship retrieval, and export are excluded.

The payload is ordered story and drawing-related text plus safely recoverable image relationship targets. Packages, workbooks, OLE, VBA, ActiveX, fonts, custom XML, signatures, and arbitrary non-image parts are control evidence. Renaming `.docx` to `.zip` is unnecessary because detection and OPC traversal operate on the same bytes.

## ZIP, OPC, and actual-format classification

The intended surface includes:

- bounded ZIP end, central, and local record traversal;
- stored and deflate entries;
- an explicit ZIP64 support decision;
- duplicate names, central/local disagreement, overlap, truncation, unsupported compression or encryption, and expansion abuse;
- no filesystem extraction and therefore no ZIP-slip materialisation;
- OPC part-name and URI normalisation;
- `[Content_Types].xml` defaults and overrides;
- package and part relationships;
- root `officeDocument` relationship and main-part content-type validation;
- core, extended, and custom properties;
- signature origin and signature parts;
- internal/external targets, duplicate relationship IDs, cycles, unreachable parts, and orphans;
- byte-level distinction among Strict/Transitional DOCX, DOCM, DOTX, DOTM, other OPC packages, and malformed ZIP;
- classification of CFB `EncryptionInfo` and `EncryptedPackage` wrappers.

A macro-enabled package mislabeled as DOCX is a variant mismatch; active parts remain passive.

## Secure XML and markup compatibility

XML processing is bounded and streaming, with DTDs, entities, and external resolvers prohibited. Limits cover depth, nodes, attributes, namespaces, text, and decoded output.

The parser normalises Strict/Transitional namespaces and relationship URIs and implements ECMA-376 Part 3:

- `Ignorable`;
- `ProcessContent`;
- `PreserveElements`;
- `PreserveAttributes`;
- `AlternateContent`;
- `Choice`;
- `Fallback`.

A versioned known-namespace and capability registry governs Microsoft extensions. Unknown non-ignorable or evidence-bearing unprocessed markup produces stable issues. Skipping markup that may contain evidence prevents `Complete`.

## Parts, stories, and text

Parts are discovered through relationships, never assumed filenames. Required discovery includes:

- main document;
- every header and footer;
- footnotes and endnotes;
- legacy, modern, and threaded comments plus people data;
- glossary/building blocks and subdocuments;
- frames and textboxes;
- text in drawings, charts, and diagrams.

Text interpretation covers paragraphs, runs, preserved whitespace, tabs, line/carriage/page breaks, soft and non-breaking hyphens, symbols, special characters, hidden/deleted/instruction text, hyperlinks, bookmarks, permissions, nested structured/custom-XML content, and section boundaries.

Typed stories and part/XML-node provenance are retained separately from a versioned deterministic review projection.

## Properties, styles, numbering, tables, and sections

The intended semantic surface includes:

- core, extended, and custom properties, including malformed/raw values;
- settings, compatibility modes, protection, tracked-change settings, and attached templates;
- document defaults and latent styles;
- character, paragraph, table, and numbering styles;
- `basedOn`, linked, and next-style graphs with cycle handling;
- direct formatting;
- font tables, embedded-font metadata, themes, font schemes, language, bidi, and complex-script properties;
- abstract numbering, instances, overrides, restarts, levels, picture bullets, and generated labels;
- semantic paragraph/run boundaries;
- nested tables, grids, spans, horizontal/vertical merges, header rows, and table styles;
- sections, headers/footers, columns, orientation, and page metadata without pagination;
- frames, textboxes, and anchor relationships.

## Fields, controls, review evidence, and drawings

The contract includes:

- simple, complex, and nested fields with instruction/result separation;
- hyperlinks, bookmarks, permissions, proofing, and smart-tag wrappers;
- controls including repeating sections, checkboxes, dates, lists, and placeholders;
- custom XML parts, data binding, and mapping metadata;
- mail merge, attached templates, bibliography/citations, and document variables;
- footnotes and endnotes;
- comments, replies, durable IDs, authors, and people records;
- insertions, deletions, moves, property changes, and deleted field instructions.

Fields and data bindings are never evaluated; external sources are never fetched.

Drawing and formula handling includes:

- DrawingML inline and anchored objects;
- relationships, crop data, alternate text, and titles;
- native images, including SVG/content parts;
- VML fallbacks, legacy controls, WordArt, shapes, groups, canvases, and textboxes;
- chart titles, labels, and cached categories/series without emitting workbook bytes;
- SmartArt/diagram semantic-model text, relationships, and alternate text without layout;
- OMML equations as structured formula trees with source provenance;
- recoverable images as discrete assets;
- ink, media, and unsupported non-image graphics as inventory-only;
- extension/fallback agreement and duplicate-evidence suppression.

## Embedded, active, encrypted, and signed content

Embedded packages and OLE objects may recurse only for PDF, DOC, DOCX, MSG, or EML. Unsupported XLSX, PPTX, and other packages remain bounded hashed descriptors.

For internal `altChunk`, safely identified text and supported nested formats may contribute text and images. RTF, HTML, XHTML, XML, and other source bytes are not emitted. External `altChunk`, templates, linked images, OLE links, and hyperlinks are recorded without retrieval.

VBA, ActiveX, Office Forms, CustomUI, web extensions, and scripts remain passive inventory without byte emission.

Encryption handling classifies standard, agile, extensible, and rights-managed wrappers without password prompting or decryption.

Signature handling separates:

- structural recognition;
- digest coverage;
- cryptographic validity;
- certificate trust;
- revocation assurance.

No online trust or revocation retrieval occurs, and structural parsing must not produce a generic “valid signature” claim.

## DOCX deterministic projection

- Stable part identities use normalised OPC names and content hashes.
- Relationship, story, XML-node, content, review, and image-occurrence identities are stable.
- Original part and attachment names remain metadata.
- Current, deleted, moved, hidden, instruction, alternative, and fallback evidence remains distinguishable.
- An unresolved relationship, namespace, or unreadable part prevents `Complete` if it may contain text/images or cannot be classified safely.
- A fully retained passive hyperlink alone does not prevent `Complete`.

## DOCX work decomposition and acceptance evidence

| ID | Responsibility |
|---|---|
| `EXT-DOCX-001` | ZIP/CFB wrapper and WordprocessingML-family classification |
| `EXT-DOCX-002` | Bounded ZIP and complete OPC graph |
| `EXT-DOCX-003` | Secure XML, Strict/Transitional normalisation, and MCE |
| `EXT-DOCX-004` | Main and auxiliary stories and text tokens |
| `EXT-DOCX-005` | Properties, settings, styles, fonts, themes, and numbering |
| `EXT-DOCX-006` | Paragraphs, tables, sections, and headers/footers |
| `EXT-DOCX-007` | Fields, bookmarks, controls, custom XML, and mail merge |
| `EXT-DOCX-008` | Notes, comments, and revisions |
| `EXT-DOCX-009` | DrawingML, VML, images, charts, SmartArt, and OMML |
| `EXT-DOCX-010` | `altChunk`, embeddings, OLE, VBA, ActiveX, and external relationships |
| `EXT-DOCX-011` | Protection, encryption, signatures, and deterministic projection |
| `EXT-DOCX-012` | Security, fuzzing, conformance, differential, corpus, performance, and acceptance |

Acceptance evidence covers ZIP forms and limits; Strict/Transitional equivalents; Microsoft extensions; all MCE paths; every story, token, style, numbering, table, and section graph; fields, controls, custom XML, comments, and revisions; drawings, charts, diagrams, equations, and fallbacks; `altChunk`, embeddings, active content, and zero-execution/retrieval proof; encrypted wrappers and signature assurance levels; determinism, cancellation, concurrency, genuine cohorts, and semantic differential testing.

## DOCX primary sources

- ECMA-376 fifth edition — `https://ecma-international.org/publications-and-standards/standards/ecma-376/`
- `[MS-DOCX]` — `https://learn.microsoft.com/en-us/openspecs/office_standards/ms-docx/b839fe1f-e1ca-4fa6-8c26-5954d0abbccd`
- `[MS-OI29500]` — `https://learn.microsoft.com/en-us/openspecs/office_standards/ms-oi29500/1fd4a662-8623-49c0-82f0-18fa91b413b8`
- `[MS-ODRAWXML]` — `https://learn.microsoft.com/en-us/openspecs/office_standards/ms-odrawxml/a807ad3a-1f35-4540-9237-353ed61c93ea`
- `[MS-OFFMACRO2]` — `https://learn.microsoft.com/en-us/openspecs/office_standards/ms-offmacro2/802a7c98-c802-41c6-8a13-987457098d8f`
- `[MS-OFFCRYPTO]` — `https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-offcrypto/3c34d72a-1a61-4b52-a893-196f9157f083`

---

# Outlook `.msg`

## Boundary and item model

An MSG is a CFB-backed Outlook Item, not necessarily email. It may represent messages, appointments, meetings, contacts, distribution lists, tasks, reports, notes, journals, posts, RSS, SMS, voice/fax items, document items, sharing items, or custom Outlook forms.

The baseline is `[MS-OXMSG]` revision 18.0, dated 2025-05-20, with pinned MAPI property, message-object, RTF, TNEF, and S/MIME specifications. Every property is retained generically before optional item-class semantics are projected.

Outlook/COM automation, mailbox access, OLE activation, rendering, decryption, online trust/revocation, and external-path retrieval are excluded.

## Classification and MAPI substrate

MSG classification requires:

- structural Outlook Item evidence in CFB v3/v4;
- distinction from DOC, encrypted OOXML, and arbitrary CFB;
- root property stream;
- named-property mapping;
- recipient and attachment storage invariants;
- sparse or non-contiguous suffixes;
- count disagreements, duplicate/orphan storages, and malformed names;
- complete CFB cycle, cross-link, truncation, and resource controls.

The property substrate covers:

- root, recipient, and attachment `__properties_version1.0` headers and 16-byte entries;
- integer widths, floating values, Boolean, currency, floating time, FILETIME, error, and GUID;
- variable and multivalued Unicode, String8, binary, GUID, object, and arrays through `__substg1.0_*`;
- stream suffix/index rules, length/alignment checks, and exact spans;
- unknown property IDs/types retained as bounded location/hash evidence with issues, without emitting raw bytes;
- named-property GUID, entry, and string streams;
- a root mapping shared with embedded messages;
- a generated and pinned property catalogue linked to owning protocol semantics.

## Text, code pages, dates, and common evidence

String8 decoding uses `PidTagMessageCodepage` and relevant deterministic fallbacks. Missing, zero, unknown, or conflicting code pages produce explicit configuration-labelled issues; machine defaults are forbidden.

FILETIME, floating-time, local/UTC, invalid, and sentinel dates retain raw values. Calendar and task handling includes the time-zone and daylight-saving structures needed for recurrence.

Common evidence includes:

- message class;
- subject prefixes and normalised subject;
- sender and representing identities;
- complete To/Cc/Bcc recipient rows;
- creation, modification, submit, sent, delivery, and client timestamps;
- message, Internet, search, conversation, and threading identifiers;
- importance, priority, sensitivity, flags, categories, follow-up, reminders, and voting state;
- raw transport headers passed to the EML header parser;
- MAPI/transport conflicts retained rather than silently resolved;
- a generic property-bag projection for every class.

## Bodies and passive RTF

Body representations remain separate:

- `PidTagBody`;
- decoded `PidTagBodyHtml` and its code page;
- `PidTagRtfCompressed` as an internal decode source, never an emitted binary asset;
- native/best-body and representation metadata.

A versioned canonical-body policy records its choice and divergence without destroying alternatives. HTML is parsed inertly for text and links; it is not rendered and external resources are not loaded.

Compressed RTF handling covers:

- `LZFu` and `MELA`;
- header lengths, CRC, and raw-size validation;
- 4 KiB circular dictionary;
- checked back-references and bounded expansion;
- RTF groups, controls, symbols, destinations, Unicode fallback counts, fonts/code pages, and binary data;
- ignorable destinations, fields, objects, and encapsulated HTML.

If semantic RTF parsing is partial, raw RTF bytes still are not emitted; omission and source hash/range are reported.

## Attachments, nesting, reports, and protected content

Attachment methods include:

- by-value;
- external/path reference;
- reference-only;
- embedded message;
- custom/OLE;
- web reference.

Metadata includes filename/display name/extension, media type, content ID/location, rendering position, hidden/inline state, timestamps, and declared/actual size. Names are metadata, never paths. External, UNC, local, and URL targets are not retrieved.

Embedded messages recurse under cumulative budgets with their own Unicode state and the root named-property map. OLE/custom storage remains passive and is never emitted, instantiated, or rendered. Supported nested PDF, DOC, DOCX, MSG, or EML contributes only text and images; unsupported objects remain bounded hashed descriptors.

Report and protected-content handling includes:

- delivery, non-delivery, read, non-read, and other reports;
- clear-signed and opaque S/MIME;
- exact signed bytes and clear content when available;
- CMS SignedData versus EnvelopedData;
- rights-managed and RPMSG classification;
- `Encrypted` for inaccessible material;
- no automatic decryption or online certificate/revocation activity.

## Outlook item-class semantics

Calendar and meeting semantics include organisers, attendees, start/end, all-day, location, busy status, recurrence, exceptions, deletions, time zones/DST, global object IDs, reminders, proposed times, request/update/cancellation, accept/tentative/decline/counter responses, and conflict/sequence/owner state.

Contacts and personal distribution lists include names, organisations, postal addresses, telephone/fax numbers, email slots and address types, display and original addresses, dates, photos, electronic business cards, user fields, and passive member EntryIDs without directory lookup.

Tasks include status, percentage, owner/assignee, start/due/completion dates, recurrence, reminders, and request/accept/decline/update semantics.

Notes, journals, posts, RSS, document, SMS/MMS, voice/fax, sharing, and custom forms remain accessible through textual properties and explicit inventory, partial, or unsupported class-specific semantics until specialised projection exists.

## MSG deterministic projection

- Property identity includes owner storage, property ID/type, and multivalue index.
- Decoded text coexists with bounded raw source/hash evidence and named-property resolution provenance.
- Recipient and attachment ordering uses validated source/storage evidence, not filesystem order.
- All body variants remain addressable; canonical choice records policy and version.
- Embedded-message and image identities include parent occurrence and content hash.
- Unknown properties and classes never disappear.
- Unresolved evidence prevents `Complete` for class-semantic claims.

## MSG work decomposition and acceptance evidence

| ID | Responsibility |
|---|---|
| `EXT-MSG-001` | CFB Outlook Item detection and storage profile |
| `EXT-MSG-002` | Bounded MAPI property-stream and type substrate |
| `EXT-MSG-003` | Named properties, catalogue, Unicode state, and code pages |
| `EXT-MSG-004` | Common item/mail metadata, recipients, transport headers, and generic properties |
| `EXT-MSG-005` | Plain/HTML bodies and canonical-body policy |
| `EXT-MSG-006` | Compressed RTF, passive RTF, and encapsulated HTML |
| `EXT-MSG-007` | Attachment methods, inline relationships, passive OLE, and references |
| `EXT-MSG-008` | Embedded messages and cumulative recursion |
| `EXT-MSG-009` | Reports, S/MIME, and protected-message states |
| `EXT-MSG-010` | Calendar and meeting semantics |
| `EXT-MSG-011` | Contacts and personal distribution lists |
| `EXT-MSG-012` | Tasks and remaining item classes |
| `EXT-MSG-013` | Projection, conformance, malformed input, fuzzing, differential, performance, corpus, and acceptance |

Acceptance evidence covers CFB ambiguity and corruption; every MAPI type and malformed layout; named-property and code-page states; sparse/duplicate recipients and attachments; every body combination and RTF failure mode; all attachment methods with proof of no path/URL/OLE activation; recursive messages; mail/report/calendar/contact/task/custom classes; recurrence and time-zone edges; protected outcomes without overstated trust; determinism, cancellation, concurrency, independent comparison, and genuine item-class cohorts.

## MSG primary sources

- `[MS-OXMSG]` revision 18.0 — `https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxmsg/b046868c-9fbf-41ae-9ffb-8de2bd4eec82`
- `[MS-OXMSG]` top-level storage — `https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxmsg/1a69e000-f391-4c03-9d43-32d5f554bca7`
- `[MS-OXCMSG]` — `https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxcmsg/7fd7ec40-deec-4c06-9493-1bc06b349682`
- `[MS-OXPROPS]` — `https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxprops/f6ab1613-aefe-447d-a49c-18217230b148`
- `[MS-OXRTFCP]` — `https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxrtfcp/65dfe2df-1b69-43fc-8ebd-21819a7463fb`
- `[MS-OXRTFEX]` — `https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxrtfex/411d0d58-49f7-496c-b8c3-5859b045f6cf`
- `[MS-OXTNEF]` — `https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxtnef/1f0544d7-30b7-4194-b58f-adc82f3763bb`
- `[MS-OXOSMIME]` — `https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxosmime/bb17d126-d211-462c-8cd3-454ed33c8746`

---

# RFC 5322/MIME `.eml`

## Boundary and detection

EML has no reliable magic number. Detection requires bounded Internet Message Format header evidence with plausible ASCII field-name/colon syntax and structure. Filename extension and media type are untrusted hints.

The parser preserves raw source positions, field and part order, and semantic values. Every leaf is classified as text, image, nested supported content, protected content, or non-payload content.

HTML rendering, scripts, remote retrieval, automatic decryption, DNS/key lookup, and online signature or authentication verification are excluded.

## Octets, lines, headers, and internationalisation

The line scanner retains exact raw offsets and handles canonical CRLF plus explicitly labelled compatibility cases for:

- LF-only or CR-only input;
- optional BOM;
- missing terminal newline;
- one leading mbox `From_` separator.

It enforces header/body separation, line limits, control handling, truncation/overlong-line reporting, and cancellation.

RFC 5322 parsing includes:

- folding and unfolding;
- comments and folding whitespace;
- quoted strings, atoms, dot-atoms, domain literals, groups, and mailbox lists;
- modern and required obsolete receiving syntax with raw-preserving issues;
- original order, duplicates, casing, spans, unknown fields, and X-fields;
- Date, From, Sender, Reply-To, To, Cc, and Bcc;
- Message-ID, In-Reply-To, References, Subject, Comments, and Keywords;
- resent blocks, Return-Path, and ordered Received fields;
- no silent winner for conflicting or duplicate singleton fields.

Internationalisation includes:

- RFC 6532 UTF-8 values and internationalised addresses while field names remain ASCII;
- RFC 2047 B/Q encoded words with context, adjacency, and whitespace rules;
- RFC 2231 charset, language, percent-encoding, and continuations;
- deterministic address, date, and message-ID parsing;
- raw values retained on ambiguity;
- no machine-default charset.

## MIME tree and decoding

MIME handling includes:

- MIME-Version;
- Content-Type, Transfer-Encoding, Disposition, ID, Location, Language, and Description;
- defaults and duplicate/conflict policy;
- recursive source-order part paths;
- preamble and epilogue;
- multipart `mixed`, `alternative`, `digest`, `parallel`, `related`, `report`, `signed`, and `encrypted`;
- unknown multipart subtypes treated conservatively as mixed with an issue;
- correct recognition of an outer boundary when inner parts are truncated;
- collision, missing close, and parser-differential cases.

Transfer decoding includes:

- `7bit`, `8bit`, `binary`, quoted-printable, and Base64;
- strict and compatibility states;
- bounded decoded output and cumulative expansion;
- `UnsupportedFeature` or partial handling for unknown transfer encodings;
- retention of bounded source evidence without emitting undecoded bytes;
- explicit future compatibility seams for uuencode, BinHex, and AppleDouble.

Charset handling includes declared mappings, BOM conflicts, and invalid-sequence policy. `text/plain` without a charset defaults strictly to US-ASCII. Any UTF-8 or Windows-1252 recovery must be configuration-labelled and issue-producing.

## Bodies, alternatives, images, and relationships

Every `multipart/alternative` candidate is retained. A versioned policy selects canonical representation without discarding alternatives or hiding completeness failures.

Body handling covers:

- `text/plain`;
- `format=flowed` and `delsp`;
- inert HTML-to-text;
- character references;
- meaningful alt/title text;
- passive links;
- exclusion of script/style text from canonical content;
- no render, execution, refresh, redirect, or remote loading;
- representation divergence and source paths.

Attachment and image handling includes:

- Content-Disposition and media-type semantics without trusting names;
- RFC 2231 and encoded-word filename variants, conflicts, and raw values;
- image identity from source part path plus decoded-content hash;
- filenames never used as output paths;
- `multipart/related`, Content-ID, and `cid:` resolution only within the parsed message;
- Content-Location, remote images, and URLs as passive relationships;
- inline/attachment ambiguity and duplicate Content-ID issues.

## Nested, special, operational, and protected bodies

Nested handling includes:

- recursive `message/rfc822` and `message/global` under cumulative budgets;
- `message/partial` as a fragment with `Partial`, without searching for sibling fragments;
- `message/external-body` metadata without retrieval;
- delivery-status and disposition-notification bodies;
- internationalised DSN/MDN;
- abuse-feedback reports;
- TNEF/`winmail.dat` classification.

Future shared TNEF primitives may extract contained text/images, but TNEF bytes remain non-payload.

Operational headers include:

- mailing-list fields, List-ID, and one-click unsubscribe metadata;
- trace, resent, delivery, and report relationships;
- Authentication-Results, DKIM, SPF, ARC, and DMARC as reported assertions only.

No verification claim is permitted without separately supplied trust boundaries and DNS/key evidence.

Signed and encrypted handling includes:

- clear-signed MIME with exact canonical signed octets;
- CMS/S/MIME and PGP/MIME structural recognition;
- extraction of clear text/images when present;
- signatures, certificates, and ciphertext as inventory-only;
- distinction between signed and encrypted/protected states;
- `Encrypted` for inaccessible payloads;
- no automatic decryption, key lookup, revocation, or trust-chain claim.

## Malformed and hostile EML

Required controls cover:

- extreme comment, group, and address nesting;
- overlong fields and controls;
- duplicate/conflicting Content-Type, transfer encoding, disposition, and boundary fields;
- invalid encoded-word placement and RFC 2231 continuations;
- missing or colliding boundaries and outer-boundary recovery;
- malformed Base64 and quoted-printable;
- decoded-output bombs;
- unknown/conflicting charsets without platform fallback;
- recursive messages, fragments, external bodies, and TNEF property bombs;
- scripts, meta refresh, remote resources, and hostile URLs;
- parser-smuggling and differential fixtures;
- exact strict-versus-compatibility status.

## EML deterministic projection

- Stable field occurrences and MIME part paths preserve source order.
- Raw octets/spans coexist with decoded values.
- Every text/image occurrence, relationship, nested result, and protected/non-payload part remains addressable without emitting non-image bytes.
- Canonical-body policy and version are recorded.
- Alternative selection never changes completeness by hiding another candidate.
- Unknown fields and media types remain retained.
- Unknown decoders never silently discard bytes.

## EML work decomposition and acceptance evidence

| ID | Responsibility |
|---|---|
| `EXT-EML-001` | Detection, line scanning, raw spans, and syntax limits |
| `EXT-EML-002` | Modern, obsolete, trace, resent, and unknown headers |
| `EXT-EML-003` | UTF-8, encoded words, parameters, addresses, dates, and identifiers |
| `EXT-EML-004` | MIME tree, defaults, boundaries, and multipart semantics |
| `EXT-EML-005` | Transfer and charset decoding with explicit compatibility profiles |
| `EXT-EML-006` | Disposition, images, CID/related graph, and identities |
| `EXT-EML-007` | Alternative-body policy, flowed text, and inert HTML |
| `EXT-EML-008` | Nested/global/partial/external-body handling and recursion |
| `EXT-EML-009` | DSN, MDN, feedback, list, trace, and reported authentication |
| `EXT-EML-010` | TNEF and explicitly selected legacy encodings |
| `EXT-EML-011` | Multipart signatures, S/MIME, PGP/MIME, and protected content |
| `EXT-EML-012` | Projection, conformance, recovery, smuggling, fuzzing, differential, performance, corpus, and acceptance |

Acceptance evidence covers line-ending and compatibility forms; conflicting headers; all address/date/ID forms; encoded words and parameters; every multipart subtype and boundary failure; every transfer and charset state; divergent alternatives and HTML threats; CID graphs; nested and special messages; TNEF; signed-octet preservation and encrypted outcomes; spoofed authentication assertions; cancellation, concurrency, repeat determinism, and independent parser comparison.

## EML primary sources

- RFC 5322 — `https://www.rfc-editor.org/rfc/rfc5322.html`
- RFC 2045 — `https://www.rfc-editor.org/rfc/rfc2045.html`
- RFC 2046 — `https://www.rfc-editor.org/rfc/rfc2046.html`
- RFC 2047 — `https://www.rfc-editor.org/rfc/rfc2047.html`
- RFC 2183 — `https://www.rfc-editor.org/rfc/rfc2183.html`
- RFC 2231 — `https://www.rfc-editor.org/rfc/rfc2231.html`
- RFC 2387 — `https://www.rfc-editor.org/rfc/rfc2387.html`
- RFC 2392 — `https://www.rfc-editor.org/rfc/rfc2392.html`
- RFC 3676 — `https://www.rfc-editor.org/rfc/rfc3676.html`
- RFC 6532 — `https://www.rfc-editor.org/rfc/rfc6532.html`
- RFC 8551 — `https://www.rfc-editor.org/rfc/rfc8551.html`
- `[MS-OXTNEF]` — `https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxtnef/1f0544d7-30b7-4194-b58f-adc82f3763bb`
