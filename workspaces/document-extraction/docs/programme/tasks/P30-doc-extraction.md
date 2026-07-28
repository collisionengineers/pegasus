# P30 — legacy Word `.doc` extraction

Detailed remaining-problem analysis and the source-led execution sequence are in [P31 — DOC comprehension and completion programme](P31-doc-comprehension-and-completion.md). P30 records the programme boundary; P31 owns the research, test and implementation gates required to close it.

## Scope

Implement the [legacy Word extraction plan](../../formats/doc.md) directly from binary CFB/FIB/CLX/property structures. The payload is ordered text and recoverable images only. The production path never creates DOCX, XML or another intermediary. Word 97-family data is the implementation target; pre-97 and mislabeled `.doc` families are classified explicitly. This is the first CollisionSpike production vertical slice.

## Owned units

- `EXT-DOC-001` actual-format, Word-family and pre-97/mislabeled classification.
- `EXT-DOC-002` streams, FIB/version map and encryption gate.
- `EXT-DOC-003` CLX/Pcdt/PlcPcd, CP/FC mapping and direct binary text.
- `EXT-DOC-004` stories, control tokens, anchors and source locations.
- `EXT-DOC-005` PLC/FKP/PRM/SPRM property engine.
- `EXT-DOC-006` styles, fonts, lists, paragraphs, tables and sections.
- `EXT-DOC-007` fields, bookmarks, links, forms, SDTs and external references.
- `EXT-DOC-008` notes, comments and tracked revisions.
- `EXT-DOC-009` textboxes, OfficeArt, pictures and equations.
- `EXT-DOC-010` OLE, embeddings, VBA and Office Forms.
- `EXT-DOC-011` metadata, custom XML, settings and signatures.
- `EXT-DOC-012` pre-97 research/parser decision.
- `EXT-DOC-013` projection and all DOC acceptance evidence.

## Required outputs

- Main and secondary stories in a documented deterministic order.
- Visible issues and non-complete outcomes for claimed-but-missing bytes or unsupported branches.
- Identity-critical text, textual properties and discrete images with source provenance; non-image embedded bytes are not emitted.
- No execution of VBA/OLE and no retrieval of linked or field-selected content.
- Compliance with current 10 MB caller input limit plus measured output, memory, CPU, timeout and concurrency bounds.

## Exit evidence

Unit and specification-derived conformance tests, semantic comparison with pinned independent oracles, corrupt/encrypted/security/fuzz gates and operator-reviewed genuine cohorts pass for the declared subset. An implementation-author-hidden holdout demonstrates zero silent truncation of identity-critical evidence. CollisionSpike caller evidence remains P80-owned.
