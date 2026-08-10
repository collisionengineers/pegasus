# Extraction compatibility matrix

Status values are `not-started`, `mapped`, `specified`, `implemented`, `locally-verified`, `conformant`, `partial` and `blocked`. A status applies only to the capability named in that row. `Mapped` records completed source/feature research, not executable support; no aggregate percentage or format label overrides a row's evidence.

The authorised output contract is text and images only ([ADR-0004](../decisions/ADR-0004-text-and-image-output.md)). Rows that inspect attachments, embedded objects, scripts, signatures or opaque data require inventory/issues but do not authorise emitting their bytes. Existing evidence that mentions passive non-image assets describes internal parser evidence or pre-decision runs; the public `DocumentExtractor` projection and CLI materialise only signature-recognised image encodings.

## Shared foundations

| Capability | Port unit | Status | Evidence or next gate |
|---|---|---|---|
| Bounded input, checked ranges, cancellation/deadlines, budgets, hashing, diagnostics and stable IDs | EXT-FND-001 | implemented | Focused tests cover blocked-read cancellation/deadline interruption, checked ranges, cumulative counters, bounded-memory SHA-256 and filename-safe identities; independent re-review, parser fuzz and measured host memory/CPU enforcement remain |
| Binary/text values, offsets, dates, code pages and character conversion | EXT-FND-002 | implemented | Focused tests cover active UTF-8, UTF-16 LE/BE and Windows-1252 decoding with all invalid sequence offsets plus FILETIME; independent re-review and format-specific encodings/dates remain |
| Deterministic normalisation, ordering, registries and source locations | EXT-FND-003 | implemented | Focused tests cover total unique-asset/nested-result ordering, portable stable-ID tokens, duplicate-ID rejection, checked location ends and canonical JSON excluding volatile elapsed telemetry; cross-result bundle uniqueness belongs to the future orchestrator and independent re-review remains |
| One byte-level detector for all five families | EXT-DET-001 | partial | Root-aware PDF/DOC/DOCX/MSG/EML/encrypted-OOXML candidates, FIB/MSG profiles, mismatch and ambiguity pass synthetic tests; pre-97/tolerant-damage breadth and full format validation remain |
| CFB v3 fixed header | EXT-STO-001 | locally-verified | Synthetic v3/v4 version/sector/header cases pass; this is not independent specification conformance |
| CFB v3/v4 DIFAT/FAT, miniFAT, directories, streams and invariants | EXT-STO-001 | locally-verified | Synthetic allocation, streams, cycles/crosslinks/orphans and the exact MS-CFB 2.6.4 directory invariants pass; one caller-selected local DOC with a valid unequal-black-height sibling tree traverses successfully; official fixture breadth, fuzz, differential and independent acceptance remain |
| ZIP/ZIP64 and OPC graph | EXT-STO-002 | partial | Store/Deflate, ZIP64 EOCD/descriptors, CRC/local-central consistency, exact stream use, paths/overlap/limits and strict passive OPC graphs pass; other methods/disks/encodings and OPC signatures/interleaving remain |
| OLE property sets and embedded-object descriptors | EXT-STO-003 | partial | Common scalar/raw OLEPS and bounded ANSI Ole10Native with structural invariants pass; dictionaries/vectors/arrays/indirect values and broader OLEDS remain |
| Bounded XML with namespaces, spans and entity denial | EXT-STO-004 | partial | Namespace, input/depth/node/attribute/text/cancellation and UTF-8/16/32 DTD/entity/external denial tests pass; byte-exact spans and deterministic cancellation during a single blocked reader call remain |
| Shared immutable request/result/content/metadata/participant/asset/relation/issue/nesting/outcome model | EXT-MOD-001 | implemented | Focused tests cover immutable copies, invalid public states, all ten outcomes, result-local asset-ID uniqueness, total evidence ordering and source-generated canonical JSON; future orchestration must enforce the bundle-wide namespace and independent re-review remains |

## PDF 1.0–2.0 family

Intended surface (current support remains this row): [PDF extraction plan](../formats.md#pdf-1020).

| Capability | Port unit | Status | Evidence or next gate |
|---|---|---|---|
| Version/extension/profile registry and feature-based detection | EXT-PDF-001 | partial | Header/Catalog versions are parsed and conflicts visible; extension/profile registry and XMP claim policy remain |
| Lexical grammar, COS values, strings, objects and exact spans | EXT-PDF-002 | locally-verified | Corrected synthetic COS lexer/span and strict object/stream-boundary tests pass; ISO conformance breadth remains |
| ASCIIHex/ASCII85/LZW/Flate/RunLength filters and predictors | EXT-PDF-003 | locally-verified | Corrected synthetic five-filter/predictor terminal, strict ASCII85, expansion/overflow/cancellation outcome cases pass; independent chained conformance breadth remains |
| Classic/xref-stream/hybrid/object-stream resolution, revisions and linearisation | EXT-PDF-004 | partial | Authoritative newest-first classic/xref-stream free/generation/Prev/hybrid state and compressed ownership exist; synthetic Prev-cycle/hybrid breadth, public revision history and linearisation remain |
| Media filters and standard/public-key encryption classification | EXT-PDF-005 | partial | Encrypted/media-filter states are explicit passive issues; handler revision breadth and native media assets remain |
| Catalog, pages, trees, resources and content programs | EXT-PDF-006 | partial | Trailer/root/page trees and core content text operators pass; inherited resources, XObjects, inline images and full operator surface remain |
| Fonts, encodings, CMaps, Unicode and positioned raw text | EXT-PDF-007 | partial | Corrected WinAnsi/partial Standard and variable-width/array-bfrange ToUnicode pass; Differences/usecmap/vertical/metrics/bidi remain and positions are explicitly approximate without CTM/rotation |
| Information/XMP, IDs and extension/profile claims | EXT-PDF-008 | partial | Bounded Info/XMP claims are projected with validation explicitly not performed; full raw/decoded conflict and profile registries remain |
| Images/masks, embedded/associated files and portfolios | EXT-PDF-009 | partial | Stable bounded image/mask/file/portfolio inspection exists; the public projection removes non-image bytes, while complete inline-image decoding, specialised codecs and text/image-only nested handoff breadth remain |
| Outlines/destinations, annotations, AcroForm and passive XFA | EXT-PDF-010 | partial | Passive navigation/annotation/form/XFA inventories exist without execution; complete subtype/value/semantic expansion remains |
| Tagged/logical/geometric order, articles and optional content | EXT-PDF-011 | partial | MCID/ActualText/marked and optional-content evidence exists; complete structure order, property lists, articles/hidden policy and accurate geometry remain |
| Actions, JavaScript, multimedia and 3D passive evidence | EXT-PDF-012 | partial | Passive actions/URI/launch/script/media/3D evidence with execution/retrieval disabled passes synthetic tests; full traversal/subtype breadth remains |
| Signatures, byte ranges, digest coverage and revision forensics | EXT-PDF-013 | partial | ByteRange/signature structure is projected with trust=false; digest/trust/revocation and complete revision forensics remain unsupported |
| Projection, bounded recovery, hostile input, performance and acceptance | EXT-PDF-014 | partial | 46 corrected synthetic tests cover structured failures, strict recovery, Form cycles/cumulative budgets and complete-result determinism; fuzz/differential/corpus/measured performance/acceptance remain |

## Legacy binary Word `.doc`

Intended surface (current support remains this row): [legacy DOC extraction plan](../formats.md#legacy-binary-word-doc) and [DOC comprehension/completion programme](../programme.md). DOC text is read directly from CFB/FIB/CLX binary structures; DOCX/XML is not an intermediate.

| Capability | Port unit | Status | Evidence or next gate |
|---|---|---|---|
| Actual-format classification, CFB/FIB family and mislabeled input | EXT-DOC-001, EXT-DOC-012 | mapped/specified; implemented subset | Reviewed contract freezes five executable profile predicates and 26 Word/legacy/cross-format/damage/hint/ambiguity/interruption cases. Production still uses a broad `nFib` range, cannot publicly identify legacy markers, name-matches encrypted OOXML, misclassifies unrelated containers, and lacks required public/fixture evidence. |
| Required/optional streams, FIB versions/ranges and encryption gate | EXT-DOC-002 | mapped/specified; implemented subset | Reviewed atlas covers all 183 C1/D9/101/10C/112 descriptors plus CFB ownership, secondary FIB/AutoText, quick-save and encryption branches. Production still retains only a small subset, misreads reserved FibBase bytes and signed `pnNext`, and lacks branch conformance evidence. |
| CLX/Pcdt/PlcPcd, logical CP-to-FC mapping and direct binary text | EXT-DOC-003 | mapped/specified; implemented subset | Reviewed R03 contract and independent 92-case oracle cover exact version layouts, CLX/PRC/PRM, both piece encodings, all 256 compressed bytes, malformed UTF-16, quick saves, `cbMac` and CP/FC boundaries. Production still misreads FIB state, uses a false CP1252 route, omits `cbMac` and surrogate enforcement, and lacks conformance/differential evidence. |
| Stories, control tokens, anchors and source locations | EXT-DOC-004 | mapped/specified; implemented subset | Reviewed R03 contract covers seven cumulative parts, outside/header guards, all header kinds, AutoText, 18 typed controls and deterministic projection/provenance. Production still exposes reserved3 as Macro, misplaces the outside guard, lacks effective-property/anchor graphs and loses structured public provenance. |
| PLC/FKP/PRM/SPRM bounded property engine | EXT-DOC-005 | partial | Synthetic SPRM/unknowns, PRM and CHPX/PAPX BTE/FKP order/alias/extent validation pass; only eleven SPRM meanings exist, every generic structure record remains semantically undecoded, and complex PRC/SEPX/cascade semantics remain |
| Styles, fonts, lists, paragraphs, tables and sections | EXT-DOC-006 | partial | Style/font/list/table/section indices and passive range evidence exist; cascade, labels, reconstruction and section semantics remain |
| Fields, bookmarks, hyperlinks, forms, SDTs and external references | EXT-DOC-007 | partial | Typed passive ranges and external non-retrieval evidence exist; nested fields, pairing and semantic forms remain |
| Notes, comments and tracked revisions | EXT-DOC-008 | partial | Typed passive note/comment/revision ranges exist; author/reply/time/hidden/deleted semantics remain |
| Textboxes, OfficeArt, pictures and equation representations | EXT-DOC-009 | partial | Drawing/textbox ranges and a raw `Data`-stream descriptor exist, but no PICF/OfficeArt/BLIP image is decoded or anchored; only independently validated discrete image encodings may ultimately be emitted |
| OLE, embedded packages, VBA and Office Forms | EXT-DOC-010 | partial | OLE/VBA/forms/package streams receive stable passive descriptors without activation; the public projection retains descriptors but removes their bytes |
| Metadata, custom XML, settings and signatures | EXT-DOC-011 | partial | Bounded common SummaryInformation scalars and passive custom/settings/signature ranges exist; vector/codepage/multisection and assurance semantics remain |
| Pre-97 Word family | EXT-DOC-012 | mapped | Corpus/specification research followed by an explicit parser/support decision |
| Projection, security, fuzz, differential, corpus and performance acceptance | EXT-DOC-013 | partial | The latest recorded Writer run expanded 36 source test methods to 44 passing cases, plus a positive raw-CFB integration and whole-result retry; no manifested DOC fixtures, conformance/differential suite, deep valid-DOC fuzz/security lane, hidden holdout or 10 MiB semantic performance acceptance exists |

## WordprocessingML `.docx` source-workspace architecture

Workspace extraction surface: [DOCX extraction plan](../formats.md#wordprocessingml-docx). This row describes the independently tested source library; it is not a Pegasus integration contract.

| Capability | Port unit | Status | Evidence or next gate |
|---|---|---|---|
| ZIP/CFB wrapper and WordprocessingML-family classification | EXT-DOCX-001 | partial | OPC/encrypted-wrapper and exact Strict relationship/content-type classification pass focused tests; spoof resistance and other OOXML/mislabelling breadth remain |
| Bounded ZIP/ZIP64 and complete OPC graph | EXT-DOCX-002 | partial | Shared bounded ZIP/OPC plus reachable story/dependency ordering and orphan evidence handling pass focused tests; complete OPC/WordprocessingML relationship breadth remains |
| Secure XML, Strict/Transitional normalisation and Markup Compatibility | EXT-DOCX-003 | partial | Strict/Transitional namespaces and DTD/entity denial pass; full MCE Choice/Fallback/process/preserve semantics force Partial |
| Main and auxiliary stories plus text tokens | EXT-DOCX-004 | locally-verified | Exact allowlisted reachable graph, main-first relationship order, recognised current/field/deleted token source order and unknown-markup partials pass synthetic tests; glossary/subdocument breadth remains |
| Properties, settings, styles, fonts, themes and numbering | EXT-DOCX-005 | partial | Core/app/custom properties and dependency inventories exist; cascade/resolution semantics remain |
| Paragraphs, tables, sections and headers/footers | EXT-DOCX-006 | partial | Core paragraph/token/table/section story projection passes synthetic cases; full nesting/merge/inheritance semantics remain |
| Fields, bookmarks, controls, custom XML and mail merge | EXT-DOCX-007 | partial | Fields/bookmarks/hyperlinks/custom XML are inspectable; control binding, mail merge and altChunk interpretation remain explicit Partial |
| Notes, comments and revisions | EXT-DOCX-008 | partial | Classic note/comment stories and deleted revisions are projected; threaded/extended comments, moves and full range policy remain |
| DrawingML, VML, images, charts, SmartArt and OMML | EXT-DOCX-009 | partial | Related image parts are deterministic assets and drawing text is retained; non-image related parts must remain inventory-only, while graphical/equation semantics remain incomplete |
| `altChunk`, embeddings, OLE, VBA, ActiveX and external relationships | EXT-DOCX-010 | partial | Passive issues, external non-retrieval and public non-image byte suppression pass; text/image-only nested interpretation remains incomplete |
| Protection, encryption, signatures and deterministic projection | EXT-DOCX-011 | partial | Encrypted wrapper and passive signatures/protection are explicit; decryption/trust assurance remains unsupported |
| Security, fuzz, conformance, differential, corpus and performance acceptance | EXT-DOCX-012 | partial | 35 corrected synthetic tests cover allowlists, ordering, MCE/unknowns, orphan/reachability, provenance, cancellation/deadline and cumulative limits; two nested DOCX samples complete deterministically, while formal conformance/fuzz/differential/corpus/performance acceptance remain |

## Outlook `.msg`

Intended surface (current support remains this row): [MSG extraction plan](../formats.md#outlook-msg). The base contract preserves a generic MAPI property bag; it is not restricted to mail messages.

| Capability | Port unit | Status | Evidence or next gate |
|---|---|---|---|
| CFB-based Outlook Item detection and storage profile | EXT-MSG-001 | implemented | Root profile and parser exist; shared detector property-header correction, ambiguity breadth and hostile CFB acceptance remain |
| Fixed/variable/multi-valued MAPI property substrate | EXT-MSG-002 | locally-verified | Synthetic contextual 32/24/8 headers, fixed/variable/4-byte-table multivalue values, malformed raw retention, duplicates and cumulative bounds pass; complete published type fixtures/source spans remain |
| Named properties, property catalogue, Unicode state and code pages | EXT-MSG-003 | partial | GUID/numeric/string named mappings and selected deterministic code pages pass; full catalogue/encoding states remain |
| Common item/mail metadata, recipients, transport headers and raw properties | EXT-MSG-004 | partial | Recipients/common metadata/raw bag pass synthetic cases; transport-header delegation and full property breadth remain |
| Plain/HTML bodies and deterministic representation policy | EXT-MSG-005 | locally-verified | Synthetic plain/HTML/RTF-only, divergence, transport and canonical selection cases pass; complete HTML/RTF semantics remain |
| Compressed RTF, passive RTF semantics and encapsulated HTML | EXT-MSG-006 | partial | Bounded MELA/LZFu validation and shallow passive RTF text pass; full RTF/encapsulated-HTML fidelity remains |
| Attachment methods, inline relationships, OLE and references | EXT-MSG-007 | partial | By-value/reference/OLE/embedded methods are classified with no activation; the public projection emits only signature-recognised image attachments |
| Embedded messages and cumulative recursion | EXT-MSG-008 | partial | Embedded parsing, stable identity, depth, shared cumulative budgets and nested text/image projection pass focused tests; complete cycle, timing and item-class breadth remain |
| Reports, S/MIME and protected-message states | EXT-MSG-009 | partial | Report/protected classes are recognised without false trust/decryption; CMS/TNEF semantics remain unsupported |
| Calendar and meeting semantics | EXT-MSG-010 | partial | Selected appointment/meeting projections exist over raw properties; recurrence/time-zone/DST semantics remain incomplete |
| Contact and personal distribution-list semantics | EXT-MSG-011 | partial | Selected contact/list projections exist over the generic bag; complete members/relationships remain |
| Tasks and remaining Outlook item classes | EXT-MSG-012 | partial | Selected task/note/generic projections exist; journal/post/RSS/SMS/voice/fax breadth remains raw/unsupported |
| Projection, conformance, malformed, fuzz, differential, performance and corpus acceptance | EXT-MSG-013 | partial | 45 corrected synthetic tests and the four-file opaque cohort twice pass `Complete`, including two nested DOCX documents; conformance/fuzz/differential/broader genuine corpus/performance/acceptance remain |

## RFC 5322/MIME `.eml`

Intended surface (current support remains this row): [EML extraction plan](../formats.md#rfc-5322mime-eml).

| Capability | Port unit | Status | Evidence or next gate |
|---|---|---|---|
| Detection, bounded line scanner, raw spans and syntax limits | EXT-EML-001 | locally-verified | Synthetic CRLF/LF compatibility, hostile lengths, periodic cancellation and absolute raw-span cases pass; standalone shared detector is covered under EXT-DET-001 |
| Modern/obsolete/trace/resent/unknown RFC 5322 headers | EXT-EML-002 | partial | Ordered folded/duplicate/unknown headers are retained with bounded unfolding; complete obsolete and trace/resent grammar remains |
| UTF-8, encoded words/parameters, addresses, dates and identifiers | EXT-EML-003 | partial | Bounded quoted/group address, encoded-word mode/adjacency, quoted parameter and RFC2231 UTF-8 cases pass; uncommon/obsolete grammar and extended charsets remain |
| MIME entity tree, defaults, boundaries and multipart semantics | EXT-EML-004 | partial | Bounded multipart/nested traversal, framing, path and cumulative limits pass; full subtype semantics remain explicit unsupported/partial |
| Transfer and charset decoding with compatibility profiles | EXT-EML-005 | locally-verified | Incremental bounded Base64/QP plus basic 7bit/8bit/binary and selected charsets pass decoded limits/cancellation; extended transfer/charset profiles remain |
| Disposition, images, CID/related graph and stable identities | EXT-EML-006 | partial | Stable attachment/CID classification, exact encoded raw spans, no retrieval and public signature-recognised image-only projection pass; full image validation and related-graph semantics remain |
| Alternative-body policy, flowed text and inert HTML | EXT-EML-007 | partial | Plain/inert HTML evidence and explicit unsupported flowed handling pass; complete alternative selection and flowed reconstruction remain |
| Nested/global/partial/external-body handling | EXT-EML-008 | partial | Nested message identity, terminal propagation, recursion/cumulative limits and no external retrieval pass; partial-fragment semantics remain |
| DSN, MDN, feedback, list, trace and reported authentication | EXT-EML-009 | partial | Structures are recognised and forced non-complete; semantic extraction remains unimplemented |
| TNEF and selected legacy transport encodings | EXT-EML-010 | partial | TNEF is passively retained and forces visible non-complete status; semantic decoding remains unimplemented |
| Multipart signatures, S/MIME and PGP/MIME protected content | EXT-EML-011 | partial | Protected/signed structures are recognised passively without trust/decryption; exact signed-octet and cryptographic semantics remain |
| Projection, recovery, parser-smuggling, fuzz, differential, performance and corpus acceptance | EXT-EML-012 | partial | 35 ordinary focused tests pass, the opt-in cohort gate is isolated, and four opaque files complete deterministically across retries; formal conformance, maintained fuzzing, differential, benchmark, hidden-holdout and independent acceptance remain |

## Public API, headless CLI and cross-format gates

| Capability | Port unit | Status | Evidence or next gate |
|---|---|---|---|
| One public request/result API and deterministic dispatch/failure boundary | EXT-API-001 | partial | Five-format byte-first dispatch, explicit failures/cancellation and one outer cumulative operation context pass focused tests; handler entry points are internal and broader caller acceptance remains |
| Versioned deterministic JSON result and evidence-bundle manifest | EXT-API-002 | locally-verified | Source-generated canonical JSON, policy identity, stable ordering and retry tests pass; schema migration/compatibility review remains |
| Text-and-image-only public payload with no non-image asset materialisation | EXT-API-003 | implemented | The public projection recognises PNG/JPEG/GIF/TIFF/BMP/WebP/ICO/WMF/EMF signatures, normalises image identity/type, converts other binaries to bounded hash descriptors and issues, recursively applies the rule to nested results, and the CLI rejects any non-image asset defensively; full per-codec structural validation, conformance, fuzz and genuine-data breadth remain |
| One-input `detect`/`extract` CLI, file/stdin, exit codes and Ctrl+C | EXT-CLI-001 | locally-verified | 28 focused Windows-host tests cover documented commands/arguments, 0/10/20-26/70 codes, stdin/file, quiet/envelopes, image-only bundles and cancellation paths; Linux/process-host/second-Ctrl+C evidence remains |
| Atomic caller-owned output bundle and stable image names | EXT-CLI-002 | partial | Collision/staging cleanup, safe paths, image-only enforcement, hash verification and URI/UNC/device/reparse denial pass; output-parent reparse race/post-write corruption tests remain |
| Framework-dependent and separately tested optional publish variants | EXT-CLI-003 | mapped | Windows/Linux framework smoke; per-RID self-contained/single/AOT evidence |
| Recursive supported-format attachments under cumulative budgets | EXT-NEST-001 | partial | Focused tests cover mixed nested inputs, identities, cumulative limits and removal of parent source bytes after derived text/image extraction; broader ancestor-cycle/mid-recursion timing evidence remains |
| No macro/script/action/OLE execution or external/path/process access | EXT-SEC-001 | partial | 21 cross-format hostile tests cover passive actions/macros/links, DTD/entities, traversal/expansion, cancellation/deadline and content-free issues; valid structured hostile DOC/MSG and socket instrumentation remain |
| Unit, conformance, semantic differential and genuine-data harness | EXT-QA-001 | partial | Deterministic unit/security/performance and opaque local cohort harnesses exist; exact-version semantic comparators and hidden holdout acceptance remain |
| Security fuzz/property and hostile regression system | EXT-QA-002 | partial | 21 security tests include 80 deterministic format mutations and 64 arbitrary seeds; maintained continuous fuzzing and valid structured DOC/MSG hostile fixtures remain |
| Allocation, throughput, expansion, cancellation and concurrency evidence | EXT-QA-003 | partial | BDN Dry 10/10, representative Short measurements, 4-way deterministic concurrency and ~30 ms blocked cancellation exist; 10 MiB/Linux/sustained/nested/accepted budgets remain |
| Dependency/licence/SBOM/package/schema/update/rollback evidence | EXT-PKG-001 | partial | Central package/version metadata, versioned result/bundle schemas, local framework-dependent library/CLI candidate script, deterministic dependency/package manifests and update/rollback/support reviews exist; authorised product licence, standard SBOM, Linux/RID variants, signing, holdout and independent acceptance remain |
| Pegasus adapter and caller-backed cohort/holdout evidence | EXT-INT-001 | partial | Dated 2026-07-24 predecessor CollisionSpike evidence covered an adjacent opt-in Web adapter and real `/Intake/Qdos` synthetic tests with the default legacy path preserved; no Pegasus adapter or caller is proved, Worker had no authorised Qdos caller, sibling project references were non-portable, and broader caller text/image/outcome/cohort/holdout evidence remains |
