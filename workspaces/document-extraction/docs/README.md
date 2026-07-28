# CollisionDocNetExtractor documentation

This documentation is the execution map for the custom managed extractor for PDF, `.doc`, `.docx`, `.msg` and `.eml`. It separates product scope, specification, implementation and evidence; it does not track office-suite-wide parity.

## Start here

1. [Product scope and format map](architecture/format-scope-map.md)
2. [Source baseline and primary specifications](architecture/source-baseline.md)
3. [Managed target architecture](architecture/managed-target-architecture.md)
4. [Headless CLI contract](architecture/headless-cli-contract.md)
5. [Five-format extraction decision](decisions/ADR-0002-five-format-extractor.md)
6. [Headless library and CLI decision](decisions/ADR-0003-headless-library-cli.md)
7. [Text-and-image-only output decision](decisions/ADR-0004-text-and-image-output.md)
8. [DOC source and clean-room boundary](decisions/ADR-0005-doc-source-and-clean-room-boundary.md)
9. [DOC binary structure atlas](architecture/doc-binary-structure-atlas.md)
10. [DOC format-classification contract](architecture/doc-format-classification.md)
11. [DOC text, piece and story semantics](architecture/doc-text-story-semantics.md)
12. [Complete format-family plans](formats/README.md)
13. [Programme and task catalogue](programme/README.md)
14. [Compatibility matrix](compatibility/feature-matrix.md)
15. [Testing programme](testing/README.md)

Feature-level provenance and support are recorded incrementally by stable `EXT-*` port units. Passing tests prove only the documented boundary and input class; complete format support requires all declared compatibility entries and evidence gates.
