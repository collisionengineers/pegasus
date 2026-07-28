# WordprocessingML `.docx` extraction plan

## Boundary

`.docx` is a required independent input format. It is normally a ZIP/Open Packaging Conventions package containing WordprocessingML and related parts. It is never used as an intermediate representation for `.doc`.

The primary baseline is ECMA-376 fifth edition: Part 1 (2016), Part 2 OPC (2021), Part 3 Markup Compatibility (2015) and Part 4 Transitional features (2016), plus pinned Microsoft implementation/extension specifications. Strict and Transitional documents are both in scope.

Rendering, layout reproduction, field execution, macro/ActiveX execution, external relationship retrieval and document export are excluded.

The `.docx` payload is ordered text from supported stories and drawing-related text plus safely recoverable image relationship targets. Embedded packages/workbooks, OLE, VBA, ActiveX, fonts, custom XML, signatures and arbitrary non-image parts are inventory/control evidence and are not emitted. Renaming `.docx` to `.zip` is unnecessary because detection and OPC traversal operate on the same package bytes.

## Complete feature surface

### Package and actual-format classification

- Bounded ZIP end/central/local record traversal, stored/deflate entries and an explicit ZIP64 support decision.
- Duplicate names, central/local disagreement, overlaps, truncation, unsupported compression/encryption and expansion abuse.
- No filesystem extraction and therefore no ZIP-slip materialisation.
- OPC part-name/URI normalisation, `[Content_Types].xml` defaults/overrides and package/part relationships.
- Root `officeDocument` relationship and main-part content-type validation.
- Core, extended and custom properties; signature origin/signature parts.
- Internal/external target classification, duplicate relationship IDs, cycles, unreachable/orphaned parts.
- Byte-level distinction among Strict/Transitional `.docx`, `.docm`, `.dotx`, `.dotm`, other OPC packages and malformed ZIP.
- CFB `EncryptionInfo`/`EncryptedPackage` wrapper classification for password-encrypted OOXML.

A macro-enabled package mislabeled `.docx` is reported as a variant mismatch; active parts remain passive.

### Secure XML and markup compatibility

- Custom bounded streaming XML with DTD, entity and external resolver prohibition.
- Depth, node, attribute, namespace, text and decoded-output budgets.
- Strict/Transitional namespace and relationship-URI normalisation.
- ECMA-376 Part 3 `Ignorable`, `ProcessContent`, `PreserveElements`, `PreserveAttributes`, `AlternateContent`, `Choice` and `Fallback`.
- Versioned known-namespace/capability registry for Microsoft extensions.
- Stable issues for unknown non-ignorable or evidence-bearing unprocessed markup.
- No `Complete` result when skipped markup can contain evidence.

### Parts, stories and raw text tokens

Discover parts through relationships, not assumed filenames:

- main document;
- every header/footer;
- footnotes/endnotes;
- legacy and modern/threaded comments plus people data;
- glossary/building blocks and subdocuments;
- frames and textbox stories;
- text stored in drawings, charts and diagrams.

Interpret paragraphs, runs, preserved whitespace, tabs, line/carriage/page breaks, soft/no-break hyphens, symbols/special characters, hidden/deleted/instruction text, hyperlinks, bookmarks/permissions, nested structured/custom-XML content and section boundaries.

Return typed stories and part/XML-node provenance plus a separate versioned deterministic review projection.

### Properties, settings, styles, fonts and numbering

- Core/extended/custom properties and malformed/raw values.
- Document settings, compatibility modes, protection, tracked-change settings and attached-template relationship.
- Document defaults, latent styles, character/paragraph/table/numbering styles.
- `basedOn`, linked and next-style graphs with cycle handling; direct formatting.
- Font table, embedded-font metadata, themes/font schemes, language, bidi and complex-script properties.
- Abstract numbering, concrete instances, overrides, restarts, levels, picture bullets and generated labels.

### Paragraphs, tables and sections

- Semantic paragraph/run boundaries and formatting relevant to evidence.
- Tables/nested tables, grids, spans, horizontal/vertical merges, header rows and table styles.
- Sections, header/footer references, columns, orientation and page metadata without pagination/layout.
- Frames, textboxes and anchor relationships.

### Fields, controls, custom XML and review evidence

- Simple/complex/nested fields with instruction/result separation.
- Hyperlinks, bookmarks, permission and proofing/smart-tag wrappers.
- Content controls including repeating sections, checkboxes, dates, lists and placeholders.
- Custom XML parts, data binding and mapping metadata.
- Mail merge, attached templates, bibliography/citations and document variables.
- Footnotes/endnotes.
- Comments, replies, durable IDs and author/person records.
- Insertions, deletions, moves and property-change revisions, including deleted field instructions.

Fields and data bindings are never evaluated; external sources are never fetched.

### Drawings, images, charts, diagrams and equations

- DrawingML inline/anchored objects, relationships, crop data, alternate text and titles.
- Images including modern SVG/content parts and native payload/provenance.
- VML fallbacks, legacy controls, WordArt, shapes, groups, canvases and textboxes.
- Charts: titles, labels and cached categories/series; embedded workbook bytes are not emitted.
- SmartArt/diagram semantic data-model text, relationships and alternate text; no layout.
- OMML equations as structured formula trees plus source provenance.
- Recoverable images as discrete assets; ink, media and unsupported non-image graphical payloads are inventory-only.
- Extension/fallback agreement and duplicate-evidence suppression.

Chart/workbook evidence does not introduce Calc or SpreadsheetML scope.

### Embedded, active and external content

- Embedded packages and OLE objects, with recursive extraction only for PDF/DOC/DOCX/MSG/EML.
- Unsupported XLSX/PPTX/other packages retained only as bounded hashed descriptors.
- Internal `altChunk` content: safely identified text and supported nested types may contribute text/images; RTF/HTML/XHTML/XML/other source bytes are not emitted.
- External `altChunk`, templates, linked images, OLE links and hyperlinks recorded without retrieval.
- VBA projects in macro-enabled/mislabeled packages.
- ActiveX, Office Forms, CustomUI, web extensions and scripts as passive inventory without byte emission.

### Encryption and digital signatures

- Standard, agile, extensible and rights-managed encryption wrapper classification; no password prompt/decryption.
- OPC signature origin/signature parts, signed-part references, certificates and signature metadata.
- Separate structural, digest-coverage, cryptographic-validity and trust/revocation assurance labels.
- No online revocation/trust retrieval and no generic “valid signature” from structural parsing.

## Deterministic projection

- Stable part identities from normalised OPC names and content hashes.
- Stable relationship, story, XML-node, content, review and image occurrence identities.
- Original part/attachment names remain metadata, not output paths.
- Preserve current/deleted/moved/hidden/instruction text and alternative/fallback provenance.
- An unresolved relationship, namespace or unreadable part prevents `Complete` when it may contain required text/images or cannot be safely classified. A fully retained passive hyperlink alone does not.

## Port units

| ID | Responsibility |
|---|---|
| `EXT-DOCX-001` | ZIP/CFB wrapper and WordprocessingML-family classifier |
| `EXT-DOCX-002` | Bounded ZIP and complete OPC graph |
| `EXT-DOCX-003` | Secure XML, Strict/Transitional normalisation and MCE |
| `EXT-DOCX-004` | Main/auxiliary stories and text tokens |
| `EXT-DOCX-005` | Properties, settings, styles, fonts, themes and numbering |
| `EXT-DOCX-006` | Paragraphs, tables, sections and headers/footers |
| `EXT-DOCX-007` | Fields, bookmarks, controls, custom XML and mail merge |
| `EXT-DOCX-008` | Notes, comments and revisions |
| `EXT-DOCX-009` | DrawingML, VML, images, charts, SmartArt and OMML |
| `EXT-DOCX-010` | `altChunk`, embeddings, OLE, VBA, ActiveX and external relationships |
| `EXT-DOCX-011` | Protection, encryption, signatures and deterministic projection |
| `EXT-DOCX-012` | Security/fuzz/conformance/differential/corpus/performance acceptance |

## Evidence matrix

- ZIP stored/deflate/ZIP64 decision, duplicates, overlaps, traversal, truncation and expansion limits.
- Strict/Transitional equivalents and Microsoft extension namespaces.
- MCE `Choice`/`Fallback`, process/preserve rules and unknown non-ignorable content.
- Every story, token, property/style/numbering graph and nested table/section relationship.
- Nested fields, controls, custom XML, comments/replies and current/deleted/moved revisions.
- DrawingML/VML/images/charts/SmartArt/OMML and duplicate fallback evidence.
- Internal/external `altChunk`, embeddings, VBA/ActiveX/forms and zero-retrieval/zero-execution proof.
- Encrypted wrappers and signature assurance levels.
- Determinism, resource/cancellation/concurrency, genuine cohorts and semantic differential evidence.

## Primary sources

- [ECMA-376 fifth edition](https://ecma-international.org/publications-and-standards/standards/ecma-376/)
- [[MS-DOCX] Word OOXML extensions](https://learn.microsoft.com/en-us/openspecs/office_standards/ms-docx/b839fe1f-e1ca-4fa6-8c26-5954d0abbccd)
- [[MS-OI29500] implementation information](https://learn.microsoft.com/en-us/openspecs/office_standards/ms-oi29500/1fd4a662-8623-49c0-82f0-18fa91b413b8)
- [[MS-ODRAWXML] DrawingML extensions](https://learn.microsoft.com/en-us/openspecs/office_standards/ms-odrawxml/a807ad3a-1f35-4540-9237-353ed61c93ea)
- [[MS-OFFMACRO2] macro-enabled variants](https://learn.microsoft.com/en-us/openspecs/office_standards/ms-offmacro2/802a7c98-c802-41c6-8a13-987457098d8f)
- [[MS-OFFCRYPTO] encrypted packages](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-offcrypto/3c34d72a-1a61-4b52-a893-196f9157f083)
