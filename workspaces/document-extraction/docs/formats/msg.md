# Outlook `.msg` extraction plan

## Boundary

An Outlook `.msg` is a CFB-backed Outlook Item, not necessarily an email. It can represent messages, appointments/meetings, contacts/distribution lists, tasks, reports, notes, journals, posts, RSS/SMS/voice/fax items and custom Outlook forms.

The current primary baseline is `[MS-OXMSG]` revision 18.0 (2025-05-20) plus pinned MAPI property, message-object, RTF, TNEF and S/MIME specifications. Every property is retained generically before optional item-class semantics are projected.

Outlook/COM automation, mailbox access, OLE activation, rendering, decryption, online trust/revocation and external-path retrieval are excluded.

The `.msg` payload is decoded textual headers, properties and body evidence plus safely recoverable inline or attached images. Non-image attachments, raw RTF, OLE/custom storage, signatures and opaque MAPI values are inventory/control evidence and are not emitted as assets. Supported nested messages/documents may contribute only text and images.

## Complete feature surface

### Classification and CFB profile

- Structural Outlook Item identification inside CFB v3/v4, distinct from DOC, encrypted OOXML and arbitrary compound files.
- Root property stream, named-property mapping, recipient and attachment storage presence/invariants.
- Sparse/non-contiguous storage suffixes, count disagreement, duplicate/orphan storages and malformed names.
- Full CFB cycle/cross-link/truncation/resource controls inherited from Storage.

### Complete MAPI property substrate

- Root/recipient/attachment `__properties_version1.0` headers and 16-byte entries.
- Fixed property types: integer widths, floating values, Boolean, currency, floating time, FILETIME, error and GUID semantics.
- Variable and multivalued Unicode, String8, binary, GUID, object and array properties through `__substg1.0_*` streams.
- Required stream suffix/index rules, length/alignment checks and source spans.
- Unknown property identifiers/types retain bounded source location/hash evidence and issues; raw bytes are not emitted.
- Named-property GUID, entry and string streams, including mapping shared by embedded messages.
- Generated/pinned property catalogue linked to owning protocol semantics.

### Unicode, code pages and dates

- Unicode-state handling and deterministic String8 decoding using `PidTagMessageCodepage` and relevant fallbacks.
- Missing, zero, unknown and conflicting code pages produce explicit configuration-labelled issues; never use the machine default.
- FILETIME, floating-time, local/UTC and invalid/sentinel date handling with raw values retained.
- Time-zone and daylight-saving structures needed by calendar/task recurrence.

### Common item and mail evidence

- Message class, subject prefixes/normalised subject, sender and representing identities.
- To/Cc/Bcc recipient roles and complete recipient property rows.
- Creation, modification, submit, sent, delivery and client timestamps.
- Message, Internet, search, conversation and threading identifiers.
- Importance, priority, sensitivity, flags, categories, follow-up, reminders and voting state.
- Raw transport headers passed to the EML header parser; MAPI/transport conflicts retained rather than silently resolved.
- Generic property bag projection for all classes.

### Body representations and selection

Interpret separately for text selection and control evidence:

- `PidTagBody` plain text;
- decoded `PidTagBodyHtml` text and its code page;
- `PidTagRtfCompressed` as an internal decode source, never an emitted binary asset;
- native/best-body and representation metadata.

The deterministic canonical-body policy never destroys alternatives and records divergence. HTML is parsed inertly for text/links; it is never rendered and external resources are never loaded.

### Compressed RTF and passive RTF semantics

- `LZFu` and `MELA`, header lengths, CRC and raw-size validation.
- 4 KiB circular dictionary, checked back-references and bounded expansion.
- RTF groups, control words/symbols, destinations, Unicode fallback counts, fonts/code pages and binary data.
- Ignorable destinations, fields/objects and RTF-encapsulated HTML.
- Raw RTF bytes are not emitted when semantic parsing is partial; the omission and source hash/range are reported.

### Attachments and embedded messages

- By-value, external/path reference, reference-only, embedded-message, custom/OLE and web-reference attachment methods.
- Filename/display name/extension, media type, content ID/location, rendering position, hidden/inline state, timestamps and declared/actual size.
- Input-controlled names are metadata, never output paths.
- External/UNC/local/URL references reported without retrieval.
- Embedded messages recursively extracted under cumulative budgets with their own Unicode state and the root named-property map.
- OLE/custom storage remains passive inventory; never emit, instantiate or render its bytes.
- Supported nested PDF/DOC/DOCX/MSG/EML handoff emits only text/images; unsupported objects remain bounded hashed descriptors.

### Reports and protected content

- Delivery/non-delivery, read/non-read and other report messages.
- Clear-signed and opaque S/MIME classification, exact signed bytes and clear content when available.
- CMS SignedData versus EnvelopedData structural state.
- Rights-managed/RPMSG classification.
- `Encrypted` for inaccessible material; no automatic decryption or online certificate/revocation activity.

### Calendar and meeting items

- Organiser, attendees, start/end, all-day, location and busy status.
- Recurrence patterns, exceptions/deletions, time zones/DST and global object IDs.
- Reminders and proposed times.
- Request/update/cancellation and accept/tentative/decline/counter-response semantics.
- Conflict/sequence/owner states required to explain a saved item.

### Contacts and distribution lists

- Structured names, companies/organisations, postal addresses, telephone/fax numbers.
- Email slots, address types, display and original addresses.
- Dates, photos, electronic business cards and user fields.
- Personal distribution-list member EntryIDs retained passively; no directory lookup.

### Tasks and remaining classes

- Task status, percent, owner/assignee, start/due/completion dates, recurrence and reminders.
- Task requests, accepts, declines and updates.
- Notes, journals, posts, RSS, document, SMS/MMS, voice/fax and sharing items.
- Custom forms/classes through textual properties and explicit inventory/partial/unsupported semantics until class-specific projection exists.

## Deterministic projection

- Property identity includes owner storage, property ID/type and multi-value index.
- Preserve decoded textual values plus bounded raw source/hash evidence and named-property identity/resolution source.
- Recipient/attachment order uses validated source/storage evidence, not filesystem order.
- All body variants remain addressable; canonical choice records policy/version.
- Embedded-message and image identity includes parent occurrence and content hash.
- Unknown properties/classes never disappear; unresolved evidence prevents `Complete` for class-semantic claims.

## Port units

| ID | Responsibility |
|---|---|
| `EXT-MSG-001` | CFB-based Outlook Item detection and storage profile |
| `EXT-MSG-002` | Complete bounded MAPI property-stream and type substrate |
| `EXT-MSG-003` | Named properties, property catalogue, Unicode state and code pages |
| `EXT-MSG-004` | Common item/mail metadata, recipients, transport headers and generic property evidence |
| `EXT-MSG-005` | Plain/HTML bodies and deterministic body policy |
| `EXT-MSG-006` | Compressed RTF, passive RTF semantics and encapsulated HTML |
| `EXT-MSG-007` | Attachment methods, metadata, inline relationships and passive OLE/references |
| `EXT-MSG-008` | Embedded messages and cumulative recursion |
| `EXT-MSG-009` | Reports, S/MIME and protected-message states |
| `EXT-MSG-010` | Calendar and meeting semantics |
| `EXT-MSG-011` | Contact and personal distribution-list semantics |
| `EXT-MSG-012` | Tasks and remaining Outlook item classes |
| `EXT-MSG-013` | Projection, conformance/malformed/fuzz/differential/performance/corpus acceptance |

## Evidence matrix

- CFB boundary/cycle/cross-link/truncation and DOC/MSG/encrypted-package ambiguity.
- Every MAPI fixed/variable/multivalue type, invalid length, duplicate/orphan and unknown property.
- Named-property indexes/GUID/string offsets and complete code-page states.
- Sparse/duplicate recipient and attachment storages.
- All body combinations, RTF CRC/back-reference/raw-size/expansion cases and encapsulated HTML.
- Every attachment method with proof that paths/URLs/OLE never activate.
- Deep embedded messages under cumulative budgets.
- Mail/report/calendar/meeting/contact/list/task and generic custom classes.
- Recurrence/time-zone/DST edge cases.
- Signed/encrypted/rights-managed outcomes without overstated trust.
- Determinism, cancellation/concurrency, independent comparison and genuine item-class cohorts.

## Primary sources

- [[MS-OXMSG] revision 18.0](https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxmsg/b046868c-9fbf-41ae-9ffb-8de2bd4eec82)
- [[MS-OXMSG] top-level storage](https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxmsg/1a69e000-f391-4c03-9d43-32d5f554bca7)
- [[MS-OXCMSG] Message and Attachment Object Protocol](https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxcmsg/7fd7ec40-deec-4c06-9493-1bc06b349682)
- [[MS-OXPROPS] property catalogue](https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxprops/f6ab1613-aefe-447d-a49c-18217230b148)
- [[MS-OXRTFCP] compressed RTF](https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxrtfcp/65dfe2df-1b69-43fc-8ebd-21819a7463fb)
- [[MS-OXRTFEX] RTF extensions](https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxrtfex/411d0d58-49f7-496c-b8c3-5859b045f6cf)
- [[MS-OXTNEF]](https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxtnef/1f0544d7-30b7-4194-b58f-adc82f3763bb)
- [[MS-OXOSMIME]](https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxosmime/bb17d126-d211-462c-8cd3-454ed33c8746)
