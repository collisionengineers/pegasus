# ADR-0004: text-and-image-only extraction payload

Status: **Accepted**

Date: 2026-07-24

## Context

CollisionSpike needs readable text and image evidence from PDF, `.doc`, `.docx`, `.msg` and `.eml`. Emitting every attachment, embedded package, native OLE stream, script, certificate, font or opaque format payload expands the public contract, attack surface and custody burden without serving that need.

The extractor still needs enough structural evidence to detect omissions, explain incomplete results, preserve provenance and enforce resource/security limits.

## Decision

The public extracted payload has exactly two classes:

1. deterministic ordered Unicode text segments; and
2. discrete image byte assets in a safely recoverable source encoding.

Text includes body and story text, captions, alternate text, comments or annotations, and textual message headers, addresses and subject fields where these form part of the useful evidence. Images include embedded or attached raster images and independently recoverable image representations. The extractor does not render pages or document objects to manufacture images, and it does not perform OCR.

The JSON result may additionally contain control evidence: detected format, source locations, stable identities, media types, image properties, source hashes, parent/nesting provenance, structured issues, outcome, version/configuration identity and bounded resource measurements. This information explains and governs the text/images; it is not a third extracted payload class.

Non-image attachments and embedded objects are not written to the output bundle. Supported embedded PDF, DOC, DOCX, MSG or EML content may be parsed recursively, but only resulting text and images cross the public output boundary. Unsupported or prohibited objects are reported by bounded descriptors, hashes where safely available and explicit issues without returning their bytes.

## Per-format application

| Input | Text payload | Image payload | Not emitted |
|---|---|---|---|
| PDF | ordered page/structure text, annotations, form values and textual metadata selected by contract | embedded/inline image streams and thumbnails when safely recoverable | file attachments, portfolios, fonts, JavaScript, multimedia, 3D, signatures and arbitrary streams |
| `.doc` | all supported Word stories, tables, fields, comments, revisions and textual properties | recoverable embedded pictures and image previews | OLE/native objects, VBA, forms, embedded packages, fonts and arbitrary data streams |
| `.docx` | all supported WordprocessingML stories plus drawing/chart/diagram text and textual properties | image relationship targets and recoverable image fallbacks | embedded packages/workbooks, OLE, VBA, ActiveX, fonts, custom XML and signatures |
| `.msg` | subject, address/header fields, selected textual properties and decoded body representations | by-value or inline image attachments | non-image attachments, raw RTF, OLE/custom storage, signatures and opaque MAPI bytes |
| `.eml` | decoded headers, addresses, body alternatives, reports and supported nested-message text | MIME image parts, including inline and attached images | non-image MIME attachments, signatures, certificates, ciphertext, TNEF and opaque leaf bytes |

## Completeness

`Complete` is judged against this text-and-image contract. A safely recognised non-image object does not become an output asset. It prevents `Complete` only when it may contain required text or images that were not extracted, or when its structure cannot be classified safely. A passive external hyperlink that is fully retained as control evidence does not by itself mean text or image extraction was incomplete.

## Consequences

- CLI bundles contain `result.json` and zero or more stable-ID image files only.
- “Asset” in the public payload means an image asset; arbitrary binary evidence is inventory-only.
- Nested extraction is a transformation boundary, not a mechanism for copying attachments.
- Compatibility rows and tests must distinguish text/image loss from safely inventoried non-payload features.
- Existing model fields may remain for schema compatibility, but producers must not populate asset bytes for non-image content after this decision is implemented.
