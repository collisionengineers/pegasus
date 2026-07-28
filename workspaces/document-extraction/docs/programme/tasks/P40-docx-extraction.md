# P40 — WordprocessingML `.docx` extraction

## Scope

Implement the required independent [DOCX extraction plan](../../formats/docx.md). The handler owns ZIP/OPC/XML and WordprocessingML semantics, including Strict/Transitional and Markup Compatibility. It emits only ordered text and recoverable images. It is not used to parse `.doc`, does not recreate Word layout and does not export DOCX.

## Owned units

- `EXT-DOCX-001` ZIP/CFB wrapper and WordprocessingML classifier.
- `EXT-DOCX-002` bounded ZIP/ZIP64 and complete OPC graph.
- `EXT-DOCX-003` secure XML, Strict/Transitional normalisation and MCE.
- `EXT-DOCX-004` main/auxiliary stories and text tokens.
- `EXT-DOCX-005` properties, settings, styles, fonts, themes and numbering.
- `EXT-DOCX-006` paragraphs, tables, sections and headers/footers.
- `EXT-DOCX-007` fields, bookmarks, controls, custom XML and mail merge.
- `EXT-DOCX-008` notes, comments and revisions.
- `EXT-DOCX-009` DrawingML, VML, images, charts, SmartArt and OMML.
- `EXT-DOCX-010` `altChunk`, embeddings, OLE, VBA, ActiveX and external relationships.
- `EXT-DOCX-011` protection, encryption, signatures and projection.
- `EXT-DOCX-012` all DOCX acceptance evidence.

## Required outputs

- Strict bounded OPC traversal with no ZIP-slip path materialisation, entity resolution or uncontrolled expansion.
- Ordered main/header/footer/note/comment/text-box content and relevant document properties.
- Discrete images plus passive embedded-package/macro/external-relationship inventory; non-image part bytes are not emitted.
- Explicit outcomes for encrypted packages, invalid relationships, unsupported markup and damaged parts.

## Exit evidence

Declared ECMA-376 behaviours have conformance fixtures, semantic differential evidence, deterministic results, hostile XML/ZIP tests, fuzzing and stated resource/performance bounds.
