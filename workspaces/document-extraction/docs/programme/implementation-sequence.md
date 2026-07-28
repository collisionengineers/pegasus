# Dependency-ordered implementation sequence

This is the start-to-finish delivery order. It favours useful vertical slices while preserving one detector, one result model and reusable hostile-input foundations. Research and fixture preparation may run ahead; implementation cannot bypass a dependency gate.

## Wave 0 — scope, specifications and evidence governance

Freeze the five input families, headless library/CLI boundary, primary specification revisions, status vocabulary, provenance schemas, dependency policy and fixture rules. Record the PDF 1.0–2.0 family and profiles rather than treating PDF 2.0 as the only input. Record `.docx` as a required handler and prohibit using it as an intermediate for `.doc`.

Exit: accepted ADRs, no contradictory active documentation, every planned unit linked to a specification and an evidence route.

## Wave 1 — hostile-input core and common result

Implement bounded random/sequential readers, checked offset/range arithmetic, counters and cumulative resource budgets, cancellation/deadlines, SHA-256, stable identities, issue ordering, encoding/date primitives and immutable request/result types. Define ordered text segments, image assets, control evidence, nested text/image results and all outcomes. Non-image binaries are never output assets.

Exit: property/unit tests cover every bound and outcome; identical inputs/configurations produce byte-stable semantic JSON fixtures.

## Wave 2 — shared containers and byte-level detection

Complete CFB v3/v4 header, DIFAT/FAT, miniFAT, directory, stream and invariant validation; implement ZIP central/local records, ZIP64, supported compression and OPC content types/relationships; add passive OLE property sets. Build structural probes for PDF, DOC, DOCX, MSG and EML plus encrypted OOXML, older/mislabeled Word and ambiguous/polyglot inputs.

Exit: conformance and hostile-container suites pass with no unbounded scan, expansion, cycle, cross-link or path traversal.

## Wave 3 — direct legacy DOC text vertical slice

Read the FIB and version/encryption flags, select `0Table`/`1Table`, validate stream ranges, parse CLX/PlcPcd, map CP to FC, decode compressed code-page and UTF-16 pieces, catalogue all stories and interpret mandatory text/control markers. Do not create DOCX or XML.

Before broadening this slice, complete the applicable source, version-atlas, classifier and text work in [P31 — DOC comprehension and completion programme](tasks/P31-doc-comprehension-and-completion.md). The current vertical slice is implementation evidence for a subset, not a complete model of every FIB field, encoding or story.

Exit: deterministic main and secondary story text with byte/CP provenance; missing or unsupported branches force visible non-complete outcomes; no silent truncation on the declared cohort.

## Wave 4 — structured DOC, text and images

Add PLCF/FKP/SPRM application, character/paragraph styles, fonts/languages, lists, tables, sections, fields/forms, bookmarks/hyperlinks, headers/footers, footnotes/endnotes, comments/revisions, textboxes, pictures/OfficeArt and passive OLE/VBA/custom-data/external-reference inventory. Extract textual properties and stable image assets; never emit non-image object bytes.

Execute Wave 4 through P31's `DOC-R04`–`DOC-R11` and `DOC-I04`–`DOC-I12` gates. Merely naming a FIB range, framing a generic PLC or retaining a raw stream is passive inventory and does not satisfy the semantic feature.

Exit: all declared DOC matrix rows have conformance, differential, security and corpus evidence; unsupported older binary variants are detected explicitly.

## Wave 5 — first public library and CLI vertical slice

Route DOC through the one public API and thin CLI. Implement file/stdin input, deterministic JSON/evidence bundles, exit codes, limits and Ctrl+C. Add no second parser path and no recursive directory processing.

Exit: library/CLI equivalence, stdout/stderr leak tests, deterministic bundle hashes and Windows/Linux framework-dependent smoke evidence.

## Wave 6 — EML parser and MIME tree

Implement bounded RFC 5322/current-plus-obsolete syntax, internationalised headers, MIME parameters and encoded words, multipart traversal, transfer decoding, charsets, body-selection policy, inline/attached images, nested text/image extraction, delivery/notification text and passive signed/encrypted inventory.

Exit: RFC-derived conformance, malformed-tolerance policy, recursion/expansion limits and independent semantic comparison pass.

## Wave 7 — DOCX OPC and WordprocessingML

Finish secure OPC/XML foundations, Markup Compatibility processing and Strict/Transitional identification. Extract every supported text story and drawing/chart/diagram text plus recoverable images. Inspect embedded packages/OLE, custom XML, macros, signatures and external relationships without emitting their bytes. Detect encrypted OOXML carried in CFB.

Exit: ECMA-376 conformance across declared editions/features, hostile ZIP/XML gates, deterministic text/images and semantic differential evidence pass.

## Wave 8 — MSG properties, bodies and item classes

Build on CFB to decode property streams, fixed/variable/multi-valued and named properties, recipients, body variants, compressed RTF, code pages, attachments and embedded messages. Project useful textual properties/bodies and image attachments. Inspect other attachment methods, S/MIME/TNEF/OLE/external content without emitting their bytes.

Exit: published-specification fixtures and real item-class cohorts cover declared properties, bodies, recurrence/time-zone data, attachment methods and recursive limits.

## Wave 9 — PDF grammar, streams and page text

Implement PDF 1.0–2.0 lexical/object syntax, header and Catalog version rules, direct/indirect objects, classic/xref streams, trailers, hybrid/incremental revisions, linearisation, object streams, standard non-image filters, page/resource trees, content operators, graphics/text state, fonts/encodings/CMaps/ToUnicode and deterministic geometric/structure-aware text ordering.

Exit: structural and text conformance covers every declared syntax/filter/font path; recovery never guesses beyond configured bounds.

## Wave 10 — complete PDF evidence surface

Add textual metadata/XMP, tagged structure, marked/optional content, images and masks, outlines/page labels/name trees, annotation/form text, and inventory for attachments/portfolios, XFA, signatures, actions/JavaScript, multimedia/3D/rich media and encryption. Emit only text and images. Identify PDF profiles without falsely asserting full profile validation.

Exit: passive/security policy and explicit support matrix cover every ISO 32000 clause family used by extraction; profile/version claims are distinct from observed features.

## Wave 11 — cross-format nesting and identity

Extract supported PDF, DOC, DOCX, MSG and EML attachments recursively under one cumulative budget, emitting only derived text and images. Preserve parent occurrence identity, original hashes, nesting path and failure propagation. Unsupported embedded formats remain bounded hashed descriptors with issues; their bytes are not emitted.

Exit: mixed-format nesting, duplicate images, cycles/recursion, cancellation and cumulative-limit suites pass deterministically with no non-image byte emission.

## Wave 12 — security and robustness closure

Run continuous fuzzing/property tests across every tokenizer, binary table, XML/MIME parser and decoder. Prove denial of macro/script/action execution, external retrieval, document-selected paths, XML entities, ZIP traversal, decompression bombs, oversized counts/ranges, algorithmic complexity attacks and content-bearing logs.

Exit: security review has no unresolved release-blocking finding and every parser has a maintained hostile regression corpus.

## Wave 13 — performance and concurrency closure

Benchmark detection, extraction and cancellation on stated Windows/Linux host classes. Record allocations, working set, CPU, elapsed time, bytes read/decoded, objects/parts, text/image output, nesting and concurrent-operation behaviour. Tune only with measured evidence; retain safe managed bounds.

Exit: declared 10 MB CollisionSpike class and larger non-caller evaluation classes meet accepted budgets with no cross-operation state or nondeterminism.

## Wave 14 — packaging and independent acceptance

Produce dependency/licence review, SBOM, versioned schemas, framework-dependent CLI/library packages, update/rollback notes and support policy. Test any optional self-contained, single-file or Native AOT RID separately. Run implementation-author-hidden holdouts and independent review.

Exit: authorised acceptance for a precisely declared format/feature set; no aggregate “all formats complete” claim while any matrix row lacks its required evidence.

## Wave 15 — CollisionSpike caller activation

Implement only the adjacent Infrastructure adapter, map engine-neutral results, prove Web and Worker calls, then run caller-owned zero-false-case-creation gates. This repository remains free of CollisionSpike business models and policy.

Exit: `Called` and later `Accepted` evidence from the real caller. Library or CLI success alone does not satisfy this wave.
