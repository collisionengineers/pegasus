# Legacy binary Word `.doc` extraction plan

The dependency-ordered investigation, test-gap register and implementation work are tracked in [P31 — DOC comprehension and completion programme](../programme/tasks/P31-doc-comprehension-and-completion.md). This page defines the target feature surface; it does not imply that those behaviours are implemented.

## Boundary

The production path reads Word binary data directly. It never creates DOCX, XML, HTML or another intermediary:

```text
CFB -> WordDocument/FIB -> selected Table -> CLX/Pcdt/PlcPcd
    -> logical CP-to-FC mapping -> compressed or UTF-16LE text
    -> binary property/review/asset structures -> common extraction result
```

The current primary baseline is `[MS-DOC]` revision 12.5 (2026-02-17) plus its pinned supporting specifications. `[MS-DOC]` covers the Word 97-family binary format; the `.doc` filename extension can also contain older Word binaries, RTF, HTML or arbitrary renamed data. Each family needs byte-level classification and an explicit support decision.

Rendering, pagination, field execution, template/link retrieval, COM/OLE activation, macro execution and conversion to DOCX are excluded.

The `.doc` payload is ordered text from supported stories plus safely recoverable pictures only. OLE/native objects, VBA, forms, embedded packages, fonts, custom data and arbitrary streams are inventory/control evidence and are not emitted as assets. A supported nested document may contribute only its own text and images.

## Complete feature surface

### Actual-format classification and CFB profile

- CFB major versions 3 and 4; header, DIFAT/FAT, miniFAT, directory hierarchy, stream chains and checked sector arithmetic.
- Cycles, overlaps/cross-links, invalid tree structure, duplicate names, truncation and declared-size mismatch.
- Required `WordDocument`, selected `0Table`/`1Table`, optional `Data`, `ObjectPool`, property sets, VBA/Macro, custom XML and signature storages.
- Structural distinction between Word binary, MSG, encrypted OOXML and unrelated CFB.
- Detection of RTF, HTML/MHTML, plain text, OOXML, PDF and arbitrary renamed inputs before Word parsing.

### FIB, versions and encryption

- Variable-length FIB with unknown trailing fields retained.
- `wIdent`, `nFib`/`nFibNew`, `fWhichTblStm`, `fComplex`, `fHasPic`, template/AutoText and encryption flags.
- Word 97-family `nFib` values `0x00C1`, `0x00D9`, `0x0101`, `0x010C` and `0x0112`.
- Story lengths and complete bounded `fc/lcb` catalogue.
- Secondary FIB/AutoText data through `pnNext`.
- XOR obfuscation, binary RC4 and RC4 CryptoAPI classification with `Encrypted`; no password guessing/decryption.

### CLX and direct text retrieval

- CLX property records, Pcdt/PlcPcd, `Pcd.Prm` and ordered CP boundaries.
- CP-to-piece lookup and `FcCompressed` mapping into `WordDocument`.
- Mixed compressed code-page and two-byte Unicode pieces.
- Incremental/quick saves and physical piece order differing from logical CP order.
- Exact CFB stream, byte/FC, global CP and story-relative CP provenance.
- Tabs, paragraph/cell/row/section/field/object/note/comment markers, line/page/column breaks, soft/no-break hyphens and other text tokens.
- Symbol-font, language, RTL/bidirectional, hidden and deleted text flags.

A printable-byte scan is recovery diagnostics only and can never produce `Complete`.

### Stories and anchors

- Main body, footnotes, headers/footers, comments, endnotes, main textboxes and header/footer textboxes in the global CP order.
- All header/footer variants and section associations.
- Footnote/endnote separators and continuation stories.
- Comment text, anchors, authors, dates and reply relationships when present.
- AutoText/glossary entries and subdocuments.
- Typed story segments retained separately plus one versioned deterministic review-text projection.

### Property cascade and semantic formatting

- Character/paragraph bin tables and FKPs; section PLC/SEPX data.
- PRL/SPRM operand sizing, unknown-SPRM skipping and ordered application.
- Piece `PRM` contributions and large property records in `Data`, with depth/cycle limits.
- Style sheet defaults, inheritance/linked styles, direct formatting and style-cycle errors.
- Font table, character sets, language/complex-script and embedded-font metadata.
- Paragraph/run boundaries, semantic flags and hidden/deleted state.
- List definitions/overrides, levels, restarts, picture bullets and generated labels.
- Nested tables, cells, rows, grid spans/merges, header rows and table styles.
- Sections, columns, orientation/page metadata and header/footer linkage without layout reproduction.

### Fields, controls and review evidence

- Nested fields with exact instruction and stored-result ranges.
- Hyperlink/reference, TOC/index, mail merge, DDE, INCLUDE, template and external-data fields.
- Form fields and controls.
- Bookmarks, overlapping ranges, permissions and protection bookmarks.
- Structured document tags and binary custom-XML mappings.
- Footnote/endnote references.
- Tracked insertions, deletions, moves and property revisions with authors/timestamps/save IDs.
- Document variables, citations and mail-merge metadata.

No field is evaluated; DDE, INCLUDE, linked-template and external-data targets are passive evidence only.

### Pictures, drawings and equations

- Inline `PICFAndOfficeArtData`, floating OfficeArt shapes/BLIPs, anchors, alternate text and textboxes.
- VML/legacy drawing text and image references where present; non-image drawing payloads are inventory-only.
- Raster images and safely recoverable image representations preserved with native type/provenance.
- Equation text and image representations; embedded equation/OLE object bytes are not emitted.
- Linked-picture targets recorded without retrieval.

### Embedded, active and custom content

- OLE1/OLE2 embedded and linked objects; `ObjectPool`, `ObjInfo`, `CompObj`, class/ProgID, presentation and native streams.
- Supported nested PDF/DOC/DOCX/MSG/EML through cumulative nested extraction, emitting only text and images.
- Spreadsheet, presentation and other unsupported embedded objects as bounded hashed descriptors without byte emission.
- VBA project inventory and optional passive module-source extraction under a separate security/licence decision.
- Office Forms/ActiveX passive inventory.
- Custom XML Data with DTD/external entities disabled.
- Legacy/XML signature presence and bounded offline metadata/coverage inspection.

Nothing is instantiated, rendered or executed.

### Metadata, settings and external surfaces

- Summary Information, Document Summary Information and user-defined OLE property sets.
- Title, subject, author, keywords, comments, company, manager and application identity.
- Creation, modification, last-print and save timestamps.
- Cached statistics clearly marked unverified.
- DOP compatibility, protection, revision and document settings.
- Template, mail-merge source, linked object/image and hyperlink targets.

### Pre-97 and mislabeled families

Separate decisions and provenance are required for Word 6/95, Word 2/earlier and Macintosh/resource-fork variants. `[MS-DOC]` does not justify claiming them. Each is either implemented from an authoritative source/clean-room evidence route or returned as a specifically identified unsupported variant.

`.dot` templates, RTF/HTML/MHTML/plain text, OOXML/PDF and damaged-FIB recovery cases remain detector cases even when the filename is `.doc`.

## Deterministic projection

- Preserve story type, global/story CP, FC/byte range, CFB stream and originating record.
- Separate stored/current/deleted/hidden text and field instructions/results.
- Stable list/table/field/bookmark/comment/revision/shape/object identities and anchors.
- Original object and attachment names are metadata, never output paths.
- Any unread branch that may contain required text or images prevents `Complete`; safely classified non-payload structures may remain inventory-only.

## Port units

| ID | Responsibility |
|---|---|
| `EXT-DOC-001` | Actual-format classifier, Word CFB/FIB family and pre-97/mislabeled classification |
| `EXT-DOC-002` | Required/optional streams, FIB/version map and encryption gate |
| `EXT-DOC-003` | CLX/Pcdt/PlcPcd, CP/FC mapping and direct binary text |
| `EXT-DOC-004` | Story segmentation, control tokens, anchors and source locations |
| `EXT-DOC-005` | PLC/FKP/PRM/SPRM bounded property engine |
| `EXT-DOC-006` | Styles, fonts, lists, paragraphs, tables and sections |
| `EXT-DOC-007` | Fields, bookmarks, hyperlinks, forms, SDTs and external references |
| `EXT-DOC-008` | Notes, comments and tracked revisions |
| `EXT-DOC-009` | Textboxes, OfficeArt, pictures and equation representations |
| `EXT-DOC-010` | OLE, embedded packages, VBA and Office Forms |
| `EXT-DOC-011` | Metadata, custom XML, settings and signatures |
| `EXT-DOC-012` | Pre-97 Word research/parser decision |
| `EXT-DOC-013` | Projection, security/fuzz/conformance/differential/corpus/performance acceptance |

## Evidence matrix

- All declared `nFib` families, both table streams, secondary FIB and encryption variants.
- Mixed/disordered compressed/UTF-16 pieces proving logical rather than physical order.
- Every story and control-token family with exact source ranges.
- Malformed `fc/lcb`, CLX, PLC, FKP, property and SPRM ranges/cycles/overflows.
- Style/list/table/section graphs, nested fields, overlapping bookmarks and nested tables.
- Hidden/deleted/revision/comment/note evidence proving no silent omission.
- Pictures, drawings, OLE/VBA/Forms/custom XML/external targets proving passive handling.
- Pre-97, RTF/HTML/OOXML/PDF/mislabeled and corrupt detection.
- Determinism, resource/cancellation/concurrency, genuine cohort and hidden holdout evidence.

## Primary sources

- [[MS-DOC] revision 12.5](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-doc/ccd7b486-7881-484c-a137-51170af7cc22)
- [[MS-CFB] revision 12.0](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-cfb/53989ce4-7b05-4f8d-829b-d08d6148375b)
- [[MS-DOC] retrieving text](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-doc/01d5d8c4-cf9c-4ef9-80fd-439e763cfe01)
- [[MS-DOC] FIB version table](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-doc/175d2fe1-92dd-45d2-b091-1fe8a0c0d40a)
- [[MS-DOC] property storage](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-doc/9ac56e29-8488-4b0a-a009-86a26e2f175e)
- [[MS-OFFCRYPTO]](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-offcrypto/3c34d72a-1a61-4b52-a893-196f9157f083)
- [[MS-ODRAW]](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-odraw/8560795e-7759-4745-838f-f7f2ef2f1872)
- [[MS-OLEDS]](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-oleds/85583d21-c1cf-4afe-a35f-d6701c5fbb6f)
- [[MS-OVBA]](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-ovba/575462ba-bf67-4190-9fac-c275523c75fc)
