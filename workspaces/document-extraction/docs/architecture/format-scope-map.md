# Product scope and format map

## Authoritative scope

CollisionDocNetExtractor accepts exactly five top-level format families: PDF, `.doc`, `.docx`, `.msg` and `.eml`. The common extracted payload is [text and images only](../decisions/ADR-0004-text-and-image-output.md), not a converted document or arbitrary attachment archive. The only delivery surfaces are the managed library and a thin [headless CLI](headless-cli-contract.md).

| Format | Detection and container | Principal extraction | Passive/security surfaces | Shared foundations |
|---|---|---|---|---|
| [PDF 1.0–2.0](../formats/pdf.md) | `%PDF-` plus valid object, cross-reference and trailer evidence; reconcile header, Catalog version, extensions and profile claims | ordered text plus recoverable embedded/inline images | encryption, attachments/portfolios, JavaScript/actions, multimedia/3D and incremental shadowing are inventory-only unless they yield supported nested text/images | bounded object/stream parsing, filters/fonts/CMaps, images and nested extraction |
| [Legacy `.doc`](../formats/doc.md) | actual-type probe, then CFB v3/v4 plus valid `WordDocument` FIB and selected table stream | direct binary text from every supported story plus recoverable pictures | encryption, VBA, OLE, embedded packages, external fields/links and pre-97 variants are inventory-only unless they yield supported nested text/images | CFB, code pages, binary property records, images and nested extraction |
| [WordprocessingML `.docx`](../formats/docx.md) | ZIP/OPC or encrypted CFB wrapper, content types, relationships and WordprocessingML main part | Strict/Transitional story text, drawing/chart/diagram text and recoverable image parts | macros/ActiveX/OLE, embedded packages, external relationships and signatures are inventory-only unless they yield supported nested text/images | ZIP/OPC, bounded XML, images and nested extraction |
| [Outlook `.msg`](../formats/msg.md) | CFB plus valid MSG property streams/storages | textual headers/properties/bodies plus inline or attached images | non-image attachments, protected content, raw RTF, OLE and opaque properties are inventory-only unless they yield supported nested text/images | CFB, MAPI values, RTF/code pages, images and nested extraction |
| [RFC 5322/MIME `.eml`](../formats/eml.md) | bounded Internet Message Format headers plus MIME structure where present | decoded textual headers/bodies/reports plus MIME image parts | non-image attachments, TNEF, signatures, certificates and ciphertext are inventory-only unless they yield supported nested text/images | bounded line parsing, transfer decoding, charsets, images and nested extraction |

## Common result

Every handler projects deterministic ordered text and discrete image assets into one result model. Format evidence, metadata/participants, relationship descriptors, nested provenance, source locations, structured issues, hashes, version identities, resource measures and outcome remain control evidence. They do not authorise another extracted payload type.

The same byte sequence and configuration must produce the same semantic ordering and identities. A handler cannot report `Complete` after silently skipping an encountered unsupported, unreadable or resource-breaching branch.

## Scope boundaries

The product does not edit, render, paginate, print or export. It does not reproduce the applications that created the files. It has no desktop UI, browser interface, ASP.NET application, hosted service, directory watcher or mailbox client. Active content is never executed, external content is never retrieved and input-controlled paths are never opened. OCR, AI classification, mailbox access and caller business rules live outside this repository.

Spreadsheet, presentation, drawing, formula and database product families are not target input families. If their files appear embedded, their bytes are not emitted; a bounded descriptor/hash and explicit unsupported issue may be recorded. No corresponding application model or parser is planned.

Supported embedded files may be extracted recursively only when the caller enables nesting and supplies cumulative depth and resource budgets. Only their text and images cross the result boundary. Unsupported embedded content remains a bounded hashed descriptor with an explicit issue; its bytes are not copied to output.
