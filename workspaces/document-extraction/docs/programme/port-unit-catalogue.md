# Extraction port-unit catalogue

Scope decisions: [ADR-0002](../decisions/ADR-0002-five-format-extractor.md) and [ADR-0003](../decisions/ADR-0003-headless-library-cli.md), accepted 2026-07-23. The dependency-ordered delivery path is in the [implementation sequence](implementation-sequence.md); feature detail and evidence matrices are in the linked format plans.

The dependency rows and IDs in this catalogue remain authoritative. The status cells are the programme-entry baseline retained for traceability; they are not the live implementation ledger. Current per-capability status and evidence are maintained in the [compatibility matrix](../compatibility/feature-matrix.md), which takes precedence when the two differ. `Mapped` means the responsibility, inputs and dependency boundary are documented; it does not mean code exists.

## Governance and shared foundations

| ID | Unit | Primary inputs | Depends on | Current status |
|---|---|---|---|---|
| EXT-GOV-001 | Five-input scope, source registry, compatibility and evidence governance | accepted ADRs and specification registry | none | Specified |
| EXT-LIC-001 | Dependency, source, oracle, fixture, notice and distribution review | licences and per-artefact provenance | EXT-GOV-001 | Mapped; legal decision open |
| EXT-FND-001 | Bounded input, checked ranges, cancellation/deadlines, cumulative budgets, hashing, diagnostics and stable IDs | .NET BCL and extraction contract | EXT-GOV-001 | Specified |
| EXT-FND-002 | Checked binary/text primitives, endian values, offsets, dates, code pages and character conversion | format specifications | EXT-FND-001 | Specified |
| EXT-FND-003 | Deterministic normalisation, ordering, versioned policies, registries and source-location primitives | extraction contract | EXT-FND-001, EXT-FND-002 | Mapped |
| EXT-DET-001 | One byte-level detector with deterministic ambiguity, polyglot and mislabelling evidence | all five format plans | EXT-FND-002 | Specified |
| EXT-STO-001 | Strict read-only CFB v3/v4 header, DIFAT/FAT, miniFAT, directory, streams and invariants | `[MS-CFB]` 12.0 | EXT-FND-001, EXT-FND-002 | CFB v3 fixed header implemented and locally verified; remaining structures mapped |
| EXT-STO-002 | Strict bounded ZIP/ZIP64 and OPC content-type/relationship graph | ZIP and ECMA-376 Part 2 | EXT-FND-001, EXT-FND-002 | Mapped |
| EXT-STO-003 | Passive OLE property sets, embedded-object descriptors and shared compound metadata | `[MS-OLEPS]`, `[MS-OLEDS]` | EXT-STO-001 | Mapped |
| EXT-STO-004 | Bounded XML tokenisation with namespaces, entity denial, depth/count limits and source spans | XML and owning format rules | EXT-FND-001, EXT-FND-002 | Mapped |
| EXT-MOD-001 | Immutable request/result model for ordered text, image assets, control evidence, issues and outcomes | accepted ADRs | EXT-FND-001, EXT-FND-003 | Specified |

## PDF 1.0–2.0 family

Full surface: [PDF extraction plan](../formats/pdf.md).

| ID | Unit | Depends on | Current status |
|---|---|---|---|
| EXT-PDF-001 | Standards/version/extension/profile registry, detection and fixture provenance | EXT-DET-001 | Mapped |
| EXT-PDF-002 | Bounded lexer, COS object model, strings and exact source spans | EXT-FND-001, EXT-FND-002 | Mapped |
| EXT-PDF-003 | Core stream pipeline, ASCII/LZW/Flate/RunLength and predictors | EXT-PDF-002 | Mapped |
| EXT-PDF-004 | Cross-reference/object resolver, object streams, revisions, hybrids and linearisation | EXT-PDF-002, EXT-PDF-003 | Mapped |
| EXT-PDF-005 | Media filters and encryption/security classification | EXT-PDF-003, EXT-PDF-004 | Mapped |
| EXT-PDF-006 | Catalog, pages, trees, resources and content interpreter | EXT-PDF-004 | Mapped |
| EXT-PDF-007 | Fonts, encodings, CMaps, Unicode and positioned raw text | EXT-PDF-006 | Mapped |
| EXT-PDF-008 | Information/XMP metadata, IDs and profile/extension claims | EXT-PDF-004, EXT-STO-004 | Mapped |
| EXT-PDF-009 | Images, masks, embedded/associated files, collections and nesting handoff | EXT-PDF-005, EXT-PDF-006, EXT-MOD-001 | Mapped |
| EXT-PDF-010 | Outlines, destinations, annotations, AcroForm and passive XFA | EXT-PDF-006, EXT-STO-004 | Mapped |
| EXT-PDF-011 | Tagged/logical/geometric order, article order and optional content | EXT-PDF-006, EXT-PDF-007 | Mapped |
| EXT-PDF-012 | Actions, JavaScript, multimedia and 3D passive inspection | EXT-PDF-006, EXT-PDF-009 | Mapped |
| EXT-PDF-013 | Signatures, byte-range/digest inspection and revision forensics | EXT-PDF-004 | Mapped |
| EXT-PDF-014 | Projection/outcomes, recovery, security, fuzz, performance, differential and acceptance evidence | EXT-PDF-001..013, EXT-MOD-001 | Mapped |

## Legacy binary Word `.doc`

Full surface: [legacy DOC extraction plan](../formats/doc.md). Remaining-problem analysis and execution gates: [P31 — DOC comprehension and completion programme](tasks/P31-doc-comprehension-and-completion.md). The production parser reads the binary structures directly and never creates DOCX or XML.

| ID | Unit | Depends on | Current status |
|---|---|---|---|
| EXT-DOC-001 | Actual-format classifier, Word CFB/FIB family and pre-97/mislabeled classification | EXT-DET-001, EXT-STO-001 | Classification contract mapped/specified; production implementation subset remains non-conformant |
| EXT-DOC-002 | Required/optional streams, FIB/version map and encryption gate | EXT-DOC-001 | FIB/storage atlas mapped and specified; production implementation subset remains non-conformant |
| EXT-DOC-003 | CLX/Pcdt/PlcPcd, CP/FC mapping and direct binary text | EXT-DOC-002, EXT-FND-002 | R03 mapped/specified and independent oracle locally verified; production subset remains non-conformant |
| EXT-DOC-004 | Story segmentation, control tokens, anchors and source locations | EXT-DOC-003 | R03 mapped/specified and independent oracle locally verified; production subset remains non-conformant |
| EXT-DOC-005 | PLC/FKP/PRM/SPRM bounded property engine | EXT-DOC-003 | Implemented subset; semantic support partial |
| EXT-DOC-006 | Styles, fonts, lists, paragraphs, tables and sections | EXT-DOC-005, EXT-MOD-001 | Passive inventory only; semantic support partial |
| EXT-DOC-007 | Fields, bookmarks, hyperlinks, forms, SDTs and external references | EXT-DOC-004, EXT-DOC-005 | Passive inventory only; semantic support partial |
| EXT-DOC-008 | Notes, comments and tracked revisions | EXT-DOC-004, EXT-DOC-005 | Passive inventory only; semantic support partial |
| EXT-DOC-009 | Textboxes, OfficeArt, pictures and equation representations | EXT-DOC-005, EXT-STO-003 | Passive inventory only; no DOC image extraction |
| EXT-DOC-010 | OLE, embedded packages, VBA and Office Forms | EXT-STO-001, EXT-STO-003, EXT-DOC-005 | Passive stream inventory; nested semantics partial |
| EXT-DOC-011 | Metadata, custom XML, settings and signatures | EXT-STO-003, EXT-DOC-002 | Implemented scalar subset; semantic support partial |
| EXT-DOC-012 | Pre-97 Word research/parser decision | EXT-DOC-001 | Generic identification policy mapped/specified; ADR proposed and parser unsupported |
| EXT-DOC-013 | Projection, security, fuzz, conformance, differential, corpus, performance and acceptance evidence | EXT-DOC-001..012, EXT-MOD-001 | Implemented synthetic subset; acceptance evidence absent |

## WordprocessingML `.docx`

Full surface: [DOCX extraction plan](../formats/docx.md). DOCX is a required independent input handler, not a DOC conversion stage.

| ID | Unit | Depends on | Current status |
|---|---|---|---|
| EXT-DOCX-001 | ZIP/CFB wrapper and WordprocessingML-family classifier | EXT-DET-001, EXT-STO-001, EXT-STO-002 | Mapped |
| EXT-DOCX-002 | Bounded ZIP and complete OPC graph | EXT-STO-002 | Mapped |
| EXT-DOCX-003 | Secure XML, Strict/Transitional normalisation and Markup Compatibility | EXT-DOCX-002, EXT-STO-004 | Mapped |
| EXT-DOCX-004 | Main/auxiliary stories and text tokens | EXT-DOCX-003, EXT-MOD-001 | Mapped |
| EXT-DOCX-005 | Properties, settings, styles, fonts, themes and numbering | EXT-DOCX-003 | Mapped |
| EXT-DOCX-006 | Paragraphs, tables, sections and headers/footers | EXT-DOCX-004, EXT-DOCX-005 | Mapped |
| EXT-DOCX-007 | Fields, bookmarks, controls, custom XML and mail merge | EXT-DOCX-004, EXT-DOCX-005 | Mapped |
| EXT-DOCX-008 | Notes, comments and revisions | EXT-DOCX-004, EXT-DOCX-005 | Mapped |
| EXT-DOCX-009 | DrawingML, VML, images, charts, SmartArt and OMML | EXT-DOCX-003, EXT-MOD-001 | Mapped |
| EXT-DOCX-010 | `altChunk`, embeddings, OLE, VBA, ActiveX and external relationships | EXT-DOCX-002, EXT-STO-003 | Mapped |
| EXT-DOCX-011 | Protection, encryption, signatures and deterministic projection | EXT-DOCX-001..010, EXT-MOD-001 | Mapped |
| EXT-DOCX-012 | Security, fuzz, conformance, differential, corpus, performance and acceptance evidence | EXT-DOCX-001..011 | Mapped |

## Outlook `.msg`

Full surface: [MSG extraction plan](../formats/msg.md). The generic MAPI property bag is the lossless base; typed projections cover more than mail messages.

| ID | Unit | Depends on | Current status |
|---|---|---|---|
| EXT-MSG-001 | CFB-based Outlook Item detection and storage profile | EXT-DET-001, EXT-STO-001 | Mapped |
| EXT-MSG-002 | Complete bounded MAPI property-stream and type substrate | EXT-MSG-001, EXT-FND-002 | Mapped |
| EXT-MSG-003 | Named properties, property catalogue, Unicode state and code pages | EXT-MSG-002 | Mapped |
| EXT-MSG-004 | Common item/mail metadata, recipients, transport headers and generic property evidence | EXT-MSG-003, EXT-MOD-001 | Mapped |
| EXT-MSG-005 | Plain/HTML bodies and deterministic body policy | EXT-MSG-004 | Mapped |
| EXT-MSG-006 | Compressed RTF, passive RTF semantics and encapsulated HTML | EXT-MSG-002, EXT-FND-002 | Mapped |
| EXT-MSG-007 | Attachment methods, metadata, inline relationships and passive OLE/references | EXT-MSG-002, EXT-STO-003 | Mapped |
| EXT-MSG-008 | Embedded messages and cumulative recursion | EXT-MSG-007 | Mapped |
| EXT-MSG-009 | Reports, S/MIME and protected-message states | EXT-MSG-004, EXT-MSG-007 | Mapped |
| EXT-MSG-010 | Calendar and meeting semantics | EXT-MSG-003, EXT-MSG-004 | Mapped |
| EXT-MSG-011 | Contact and personal distribution-list semantics | EXT-MSG-003, EXT-MSG-004 | Mapped |
| EXT-MSG-012 | Tasks and remaining Outlook item classes | EXT-MSG-003, EXT-MSG-004 | Mapped |
| EXT-MSG-013 | Projection, conformance, malformed, fuzz, differential, performance, corpus and acceptance evidence | EXT-MSG-001..012, EXT-MOD-001 | Mapped |

## RFC 5322/MIME `.eml`

Full surface: [EML extraction plan](../formats/eml.md).

| ID | Unit | Depends on | Current status |
|---|---|---|---|
| EXT-EML-001 | Detection, line scanner, raw spans and syntax limits | EXT-DET-001, EXT-FND-001 | Mapped |
| EXT-EML-002 | RFC 5322 modern/obsolete/trace/resent/unknown headers | EXT-EML-001 | Mapped |
| EXT-EML-003 | UTF-8, encoded words, parameters, addresses, dates and identifiers | EXT-EML-002, EXT-FND-002 | Mapped |
| EXT-EML-004 | MIME entity tree, defaults, boundaries and multipart semantics | EXT-EML-002 | Mapped |
| EXT-EML-005 | Transfer and charset decoding with explicit compatibility profiles | EXT-EML-003, EXT-EML-004 | Mapped |
| EXT-EML-006 | Disposition, images, CID/related graph and stable identities | EXT-EML-004, EXT-EML-005, EXT-MOD-001 | Mapped |
| EXT-EML-007 | Alternative-body policy, flowed text and inert HTML extraction | EXT-EML-004, EXT-EML-005 | Mapped |
| EXT-EML-008 | Nested/global/partial/external-body handling and recursion | EXT-EML-004, EXT-EML-006 | Mapped |
| EXT-EML-009 | DSN, MDN, feedback, list, trace and reported-authentication semantics | EXT-EML-002, EXT-EML-004 | Mapped |
| EXT-EML-010 | TNEF and explicitly selected legacy transport encodings | EXT-EML-005, EXT-MSG-002 | Mapped |
| EXT-EML-011 | Multipart signatures, S/MIME and PGP/MIME protected content | EXT-EML-004, EXT-EML-006 | Mapped |
| EXT-EML-012 | Projection, conformance, recovery, parser-smuggling, fuzz, differential, performance and corpus acceptance | EXT-EML-001..011, EXT-MOD-001 | Mapped |

## Orchestration, headless CLI, security, QA and integration

| ID | Unit | Primary inputs | Depends on | Current status |
|---|---|---|---|---|
| EXT-API-001 | One public request/result API, detection/dispatch, versioning and failure boundary | ADR-0002 and EXT-MOD-001 | EXT-DET-001, EXT-MOD-001, active handlers | Implemented |
| EXT-API-002 | Versioned deterministic JSON result schema and evidence-bundle manifest | public contract | EXT-API-001 | Specified |
| EXT-API-003 | Enforce the text-and-image-only output boundary and prohibit non-image asset materialisation | ADR-0004 | EXT-API-001, EXT-API-002, all format handlers | Implemented |
| EXT-CLI-001 | Headless one-input `detect`/`extract` process, stdin/file input, stdout/stderr discipline, cancellation and exit codes | [CLI contract](../architecture/headless-cli-contract.md) | EXT-API-001, EXT-API-002 | Specified |
| EXT-CLI-002 | Caller-selected output directory, atomic bundle writing, stable image names and no recursive input discovery | CLI contract | EXT-CLI-001 | Specified |
| EXT-CLI-003 | Framework-dependent baseline and separately evidenced self-contained, single-file and Native AOT variants | .NET 10 publishing guidance | EXT-CLI-001, EXT-PKG-001 | Mapped |
| EXT-NEST-001 | Recursive supported-attachment text/image extraction with parent identity and cumulative budgets | extraction contract | EXT-API-001, relevant handlers | Specified |
| EXT-SEC-001 | Cross-format active-content, external-reference, path/process and log-content denial | security contract | EXT-FND-001, active handlers | Specified |
| EXT-QA-001 | Unit, specification conformance, semantic differential and genuine-data harness | owned tests and pinned oracles | active units | Specified |
| EXT-QA-002 | Security, fuzz/property and hostile-input regression system | parser/decoder attack surfaces | active parsers | Specified |
| EXT-QA-003 | Performance, allocation, expansion, nesting, cancellation and concurrency evidence | benchmark and host manifests | active handlers | Specified |
| EXT-PKG-001 | Dependency review, SBOM, package/version/schema support, update and rollback evidence | implemented product | EXT-LIC-001, accepted release scope | Mapped |
| EXT-INT-001 | CollisionSpike Infrastructure adapter and caller-backed cohort/holdout evidence | adjacent caller contract | accepted DOC/API/CLI/security/QA units | Specified |
