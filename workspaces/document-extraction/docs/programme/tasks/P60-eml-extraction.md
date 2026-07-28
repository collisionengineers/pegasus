# P60 — RFC 5322/MIME `.eml` extraction

## Scope

Implement the complete declared [RFC 5322/MIME extraction surface](../../formats/eml.md). Emit decoded textual headers/bodies and MIME image parts only. Preserve bounded source evidence, the full message/MIME structure and ambiguity rather than relying on a mailbox client or silently repairing hostile syntax.

## Owned units

- `EXT-EML-001` detection, line scanner, raw spans and limits.
- `EXT-EML-002` modern/obsolete/trace/resent/unknown headers.
- `EXT-EML-003` UTF-8, encoded values, addresses, dates and IDs.
- `EXT-EML-004` MIME tree, defaults, boundaries and multipart semantics.
- `EXT-EML-005` transfer and charset decoding profiles.
- `EXT-EML-006` disposition, assets, CID/related graph and identities.
- `EXT-EML-007` alternative-body policy, flowed text and inert HTML.
- `EXT-EML-008` nested/global/partial/external-body handling.
- `EXT-EML-009` DSN, MDN, feedback, list, trace and reported authentication.
- `EXT-EML-010` TNEF and selected legacy transport encodings.
- `EXT-EML-011` multipart signatures, S/MIME and PGP/MIME.
- `EXT-EML-012` projection and all EML acceptance evidence.

## Required outputs

- Bounded line/header parsing with exact issues for malformed folding and ambiguous fields.
- Ordered address/participant values and raw-preserving provenance for lossy date/charset cases.
- Strict multipart traversal and bounded Base64/quoted-printable decoding.
- Documented plain/HTML body policy, discrete images and text/image-only `message/rfc822` nesting under cumulative budgets.
- Passive HTML/link evidence without rendering or retrieval.

## Exit evidence

RFC-derived conformance cases, independent semantic comparisons, malformed boundary/encoding tests, recursive MIME/resource limits, fuzz/security evidence and performance/concurrency bounds pass for the declared subset.
