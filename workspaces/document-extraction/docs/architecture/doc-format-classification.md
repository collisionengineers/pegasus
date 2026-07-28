# DOC format and acquisition classification

Status: **Mapped and specified** for `DOC-R02` on 2026-07-24. This document defines detection behaviour; it does not claim the current detector implements it.

Owner: `EXT-DOC-001` and `EXT-DOC-012`. Primary authority is `[MS-DOC]` 12.5 sections 2.1, 2.5.1–2.5.15, 2.7.5 and 2.7.6 with `[MS-CFB]` 12.0 sections 2.2, 2.6 and 2.7 and `[MS-OFFCRYPTO]` 14.0 sections 2.3.4.4–2.3.4.6 and 2.3.4.10. Their exact hashes are frozen by `DOC-R00`. MSG precedence uses `[MS-OXMSG]` 18.0 (2025-05-20) sections 2.2.1, 2.2.2, 2.3, 2.4 and 2.4.1.1. That exact MSG revision is recorded in the repository baseline but no retained hash-pinned publication is in the DOC-R00 bundle; publication of derived MSG fixtures remains blocked on the owning MSG provenance gate. The intended caller is the CollisionSpike Infrastructure adapter through the one public extraction entry point.

The companion [machine-readable matrix](doc-format-classification.v1.json) is the normative repository decision table for generated classifier metadata and independent tests.

## Recognition algorithm

1. Apply cancellation, deadline and input/container budgets.
2. Inspect bytes; filename and declared media type are never routing inputs.
3. Validate a candidate container before interpreting its owned format profile. A signature alone is not a successful format match.
4. Retain every independently strong top-level match. Candidate order, confidence and hints never break a tie.
5. More than one strong match is `UnsupportedFeature` with stable `AMBIGUOUS_FORMAT` evidence and no parser invocation.
6. Dispatch exactly one strong match, then classify its family and acquisition subtype.
7. A valid unrelated container is `UnsupportedFormat`. Damage after a container or format profile is established is `Corrupt`.

The executable thresholds are in `profilePredicates` in the companion matrix. In particular, an exact eight-byte CFB signature identifies a damaged CFB; a valid CFB plus a root `WordDocument` stream of at least two bytes whose first little-endian value is `0xA5EC` identifies Word damage from byte 2 onward. The four unverified legacy markers require the same valid-CFB/root-stream/two-byte threshold but deliberately do not inherit modern FIB rules. MSG requires its bounded 32-byte root property header and count/profile invariants. Encrypted OOXML requires both root streams, a recognised Standard, Extensible or Agile `EncryptionInfo` grammar and an eight-byte-length-prefixed non-empty encrypted package; names alone never produce `Encrypted`, and extensible-provider URLs are never retrieved.

A mismatch between bytes and hints is informational acquisition evidence. It does not make otherwise complete extraction `Partial`. A malformed `.doc` does not become Word merely because of its name, and printable-byte recovery is prohibited.

## Word family decision

A supported Word candidate requires a valid CFB, a root `WordDocument` stream, `wIdent=0xA5EC`, a structurally coherent complete FIB, an effective `nFib` of C1, D9, 101, 10C or 112, and the root Table stream selected by `fWhichTblStm`. The exact FIB envelope comes from [DOC-R01](doc-binary-structure-atlas.md); detection and parsing must consume one generated family table rather than maintain separate numeric ranges.

`fDot` identifies a template subtype. `fGlsy` identifies an AutoText-only subtype. A nonzero `pnNext` is legal only on the specification-defined template branch and triggers bounded secondary-FIB validation. Templates and AutoText remain Word Binary inputs, not new top-level formats. Missing semantics can prevent a complete extraction, but the subtype alone is not corruption.

`fBulletProofed`, `fSeenRepairs` and `fLiveRecover` are passive repair/recovery state. They do not excuse invalid bytes and do not make a structurally valid document corrupt. An external claim that a file was repaired has no classification effect unless represented by specified bytes.

## Older Word decision

`[MS-DOC]` 12.5 begins with the Word 97 family and requires `wIdent=0xA5EC`; it is not authority for Word 6/95, Word 2/earlier or Macintosh grammars. No pinned source in the approved ledger maps the existing `0xA59B`, `0xA59C`, `0xA5DB` and `0xA5DC` constants to product versions.

Under [ADR-0006](../decisions/ADR-0006-legacy-word-classification.md), those four values can identify only an `UnverifiedLegacyWordIdentifier` under a strong CFB/root-Word profile. They return `UnsupportedFeature` without payload parsing. Unknown flat or Macintosh candidates return `UnsupportedFormat`; the implementation must not apply modern FIB offsets or invent family names. This decision is reopened only after a licensed, hash-pinned authoritative grammar and independent fixtures/oracle are approved.

## Cross-format and acquisition variants

- Valid PDF, DOCX, MSG or RFC 5322/MIME bytes named `.doc` route to their actual handler and record an informational mismatch.
- A valid MIME `multipart/related` web archive is an Internet Message variant when the bytes satisfy the full RFC message/MIME grammar. `.mht` or `.doc` names alone have no effect.
- Top-level RTF, standalone HTML, plain text and arbitrary bytes are `UnsupportedFormat`; no raw-text salvage occurs.
- A valid CFB without a Word, MSG or validated encrypted-OOXML profile is `UnsupportedFormat`, not `Corrupt`.
- A valid ZIP/OPC package without a WordprocessingML profile is likewise `UnsupportedFormat`; malformed ZIP/OPC bytes or damage after a WordprocessingML profile is established are `Corrupt`.
- An encrypted OOXML wrapper requires a valid `EncryptionInfo` grammar and bounded `EncryptedPackage`; stream names alone are insufficient.
- Simultaneous Word/MSG/encrypted-OOXML profiles, or any multiple strong top-level matches, are rejected as ambiguous. A parser is never selected by a hint.

## Acceptance fixtures

`DOC-I01` must activate independently generated tests described by these groups:

| ID | Required coverage |
|---|---|
| `DOC-T01` | exact identifiers and five effective versions; every `fDot`/`fGlsy`/`pnNext` branch; repair flags; provenance-gated legacy markers |
| `DOC-T02` | CFB v3/v4 Word, MSG, encrypted OOXML and unrelated profiles; selected/missing Table; all FIB truncation boundaries; coherent unsupported versions; profile collisions |
| `DOC-T03` | ordinary document, template, AutoText-only and attached AutoText public semantics; repair flags do not change validity |
| `DOC-T04` | every supported format mislabeled as DOC; DOC-hinted RTF/HTML/plain/arbitrary/unrelated CFB; public outcome, stable candidates/issues and no sensitive diagnostics |

The fixture generator must not share an unchecked production constant table. Assert the full candidate set, evidence codes and offsets, container, family/variant, diagnostic, hint flags and public outcome. Required boundary companions include absent, exact-limit, one-over, every-byte truncation, malformed and resource-limited forms. Existing tests do not kill changes to the broad `nFib` range, encrypted-wrapper name-only matching, generic DOCX `application/xml` acceptance, template classification, CFB profile collisions or public DOC/MSG/encrypted/ambiguity dispatch.

## Known implementation differences

- `HasWordFib` currently accepts every base `nFib` from `0x0065` through `0x0112` and does not determine the effective version.
- Pre-97 markers never reach the public parser classification and become generic container corruption.
- Valid unrelated CFB and ZIP containers can become `Corrupt` rather than `UnsupportedFormat`.
- encrypted OOXML is accepted from two stream names, loses public format identity and produces a false DOCX hint mismatch.
- DOCX detection accepts generic `application/xml`, misses macro/template main types and does not require the package root office-document relationship.
- the EML probe counts repeated recognised names rather than distinct header evidence, so HTML-like bytes can false-positive.
- ambiguity has no public direct test and current mismatch warnings downgrade completeness.

These are requirements for `DOC-I01` and shared detector work, not accepted compatibility behaviour.
