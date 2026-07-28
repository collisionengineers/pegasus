# P50 — Outlook `.msg` extraction

## Scope

Implement the complete declared [Outlook Item extraction surface](../../formats/msg.md) using the owned CFB reader and published MSG/MAPI structures. The payload is useful textual fields/bodies plus inline or attached images. A generic property bag preserves bounded control evidence for unknown/custom properties; typed projections include mail, reports, meetings/appointments, contacts/lists, tasks and remaining item classes. The product does not automate Outlook or open a mailbox.

## Owned units

- `EXT-MSG-001` CFB-based Outlook Item detection/storage profile.
- `EXT-MSG-002` complete MAPI property types and streams.
- `EXT-MSG-003` named properties, catalogue, Unicode and code pages.
- `EXT-MSG-004` common item/mail metadata, recipients and raw evidence.
- `EXT-MSG-005` plain/HTML bodies and body policy.
- `EXT-MSG-006` compressed/passive RTF and encapsulated HTML.
- `EXT-MSG-007` attachment methods and passive OLE/references.
- `EXT-MSG-008` embedded messages and recursion.
- `EXT-MSG-009` reports, S/MIME and protected states.
- `EXT-MSG-010` calendar and meeting semantics.
- `EXT-MSG-011` contacts and personal distribution lists.
- `EXT-MSG-012` tasks and remaining Outlook item classes.
- `EXT-MSG-013` projection and all MSG acceptance evidence.

## Required outputs

- Typed MAPI values with deterministic handling of missing, duplicate and malformed properties.
- Message class, subject, sender, recipients, dates, identifiers and body alternatives with provenance.
- Attachment classification and embedded-message extraction under cumulative budgets, emitting only text and images.
- Passive HTML/link/OLE evidence without rendering, execution or retrieval.

## Exit evidence

Published-specification fixtures, independent semantic comparisons, malformed-property/RTF tests, recursive-message limits, fuzz/security evidence and performance/concurrency bounds pass for the declared item classes and features.
