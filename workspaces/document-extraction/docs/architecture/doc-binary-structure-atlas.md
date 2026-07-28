# DOC binary structure atlas

Status: **Mapped and specified** for `DOC-R01` on 2026-07-24; this is a specification and ownership map, not parser support. The source-backed verifier and independent review cover all 183 descriptors across five layouts.

Owner: `EXT-DOC-002`. Primary inputs: `[MS-DOC]` revision 12.5, sections 2.1 and 2.5.1–2.5.15, and `[MS-CFB]` revision 12.0. The intended caller is the CollisionSpike Infrastructure adapter through the common extraction API. The accepted input subset is the Word 97-family FIB versions `0x00C1`, `0x00D9`, `0x0101`, `0x010C` and `0x0112`; older families remain identified but unsupported.

The machine-readable companion is [doc-fib-atlas.v1.json](doc-fib-atlas.v1.json). It contains all 183 cumulative `FibRgFcLcb` slots, including the non-range `FILETIME` at ordinal 87. [Generate-DocFibAtlas.ps1](../../scripts/Generate-DocFibAtlas.ps1) derives it only from the pinned MS-DOC bytes; [Test-DocFibAtlas.ps1](../../scripts/Test-DocFibAtlas.ps1) provides the offline invariant check.

## CFB storage ownership

Input-chosen names never become paths and no content is executed or retrieved. Unknown streams are bounded passive descriptors; they are not returned as assets.

| Storage or stream | Presence and selection | Specification | Owner | Extraction/failure policy |
|---|---|---|---|---|
| Root CFB storage | Required | MS-DOC 2.1; MS-CFB 2.2–2.7 and 2.9 | `EXT-STO-001`, `EXT-DOC-001` | Bounded CFB header, DIFAT/FAT/MiniFAT, directory and stream traversal. Structural violation is `Corrupt`; an ambiguous non-Word CFB is `UnsupportedFormat`. |
| `WordDocument` | Required at root; FIB begins at offset zero | MS-DOC 2.1 | `EXT-DOC-002` | Parse the selected FIB and referenced text/property pages. Missing/truncated/invalid FIB is `Corrupt`. Never scan for a replacement FIB. |
| selected `0Table` or `1Table` | Exactly the stream selected by `FibBase.fWhichTblStm` is authoritative | MS-DOC 2.1 | `EXT-DOC-002` | Missing selected stream is `Corrupt`. Ignore an unselected sibling. Validate only typed references assigned to this stream. |
| `Data` | Optional and reference-driven | MS-DOC 2.1 | `EXT-DOC-005`, `EXT-DOC-009` | Read only bounded referenced operands/PICF/OfficeArt data. Presence or `fHasPic` alone does not make the stream an image. Unresolved referenced payload is `UnsupportedFeature`. |
| `ObjectPool` child storages | Optional | MS-DOC 2.1; MS-OLEDS | `EXT-DOC-010` | Parse passive identity/link/presentation descriptors and bounded supported nested sources. Never execute or emit arbitrary object bytes. Unknown content that can hide text/images prevents `Complete`. |
| child `\u0003ObjInfo` | Required within each conforming ObjectPool child | MS-DOC 2.1 | `EXT-DOC-010` | Passive ODT parsing only; malformed referenced storage is `Corrupt`. |
| child `\u0003PRINT` / `\u0003EPRINT` | Optional presentation streams | MS-DOC 2.1; MS-WMF/MS-EMF | `EXT-DOC-009`, `EXT-DOC-010` | Emit only a validated supported image representation; otherwise retain a descriptor and report unsupported image payload. |
| `MsoDataStore` | Optional custom-XML storage | MS-DOC 2.1; MS-OSHARED 2.3.6 | `EXT-DOC-011` | Passive bounded textual/control evidence only; never resolve schemas or transforms. |
| `\u0005SummaryInformation` / `\u0005DocumentSummaryInformation` | Optional | MS-DOC 2.1; MS-OSHARED 2.3.3.2 | `EXT-DOC-011` | Bounded property-set parsing. Unsupported property types are visible and prevent completeness only when required text is at risk. |
| `encryption` | Conditional on CryptoAPI encryption and header flags | MS-DOC 2.1, 2.2.6 | `EXT-DOC-002` | Classify only; no decryption. Inconsistent presence is `Corrupt`, supported encrypted form is `Encrypted`. |
| `Macros` | Optional VBA project root | MS-DOC 2.1; MS-OVBA | `EXT-DOC-010` | Passive inventory only; never decompress/execute modules for payload. Presence is reported without emitting bytes. |
| `_xmlsignatures` / `_signatures` | Optional | MS-DOC 2.1; MS-OFFCRYPTO | `EXT-DOC-011` | Presence and bounded identity only; no trust claim or signature validation. |
| `\u0006DataSpaces` plus `\u0009DRMContent` | Paired protected-content representation | MS-DOC 2.1; MS-OFFCRYPTO | `EXT-DOC-002` | Classify as protected/encrypted without reading protected payload. Broken pairing is `Corrupt`. |
| unspecified streams/storages | Vendor-extensible | MS-DOC 1.7 and 2.1 | `EXT-DOC-010`, `EXT-DOC-013` | Stable bounded descriptors only. Never guess a format from a substring or return complete raw bytes. Payload ambiguity is `UnsupportedFeature`. |

## FIB envelope and version layouts

All integer fields are little-endian. The parser reads each counted array with checked arithmetic, retains unknown trailing values, and validates fields according to their declared type rather than treating every eight-byte slot as a Table range.

| Effective `nFib` | `cbRgFcLcb` | `cswNew` | Complete FIB bytes | Effective version source | Cumulative layout |
|---:|---:|---:|---:|---|---|
| `0x00C1` | 93 | 0 | 900 | `FibBase.nFib` | `FibRgFcLcb97` |
| `0x00D9` | 108 | 2 | 1,024 | `FibRgCswNew.nFibNew` | through `FibRgFcLcb2000` |
| `0x0101` | 136 | 2 | 1,248 | `FibRgCswNew.nFibNew` | through `FibRgFcLcb2002` |
| `0x010C` | 164 | 2 | 1,472 | `FibRgCswNew.nFibNew` | through `FibRgFcLcb2003` |
| `0x0112` | 183 | 5 | 1,630 | `FibRgCswNew.nFibNew` | through `FibRgFcLcb2007` |

`csw` is 14 and `cslw` is 22 for this family. A reader may skip an unknown counted tail only after bounding it and preserving its observed identity; it must not silently reinterpret it as a known older layout.

| FIB region | Width/shape | Owner | Required policy |
|---|---|---|---|
| `FibBase` | 32 bytes | `EXT-DOC-002` | Type every field and bit; validate `wIdent`, flags, reserved requirements, table selection, encryption and secondary-FIB branch. |
| `csw` + `FibRgW97` | 2 + 28 bytes | `EXT-DOC-002`, `EXT-DOC-003` | Require count 14; preserve all words. `lidFE` contributes to encoding research; reserved fields follow section 2.5.3 policy. |
| `cslw` + `FibRgLw97` | 2 + 88 bytes | `EXT-DOC-002`, `EXT-DOC-004` | Require count 22; own `cbMac`, eight story counts and all reserved/version fields. Checked sum of story CP extents is mandatory. |
| `cbRgFcLcb` + blob | 2 + 744/864/1,088/1,312/1,464 bytes | `EXT-DOC-002..011` | Select an exact cumulative layout and use the companion atlas. Ordinal 87 is `FILETIME`, never an `fc/lcb` range. |
| `cswNew` + `FibRgCswNew` | 2 + 0/4/4/4/10 bytes | `EXT-DOC-002` | Apply sections 2.5.11–2.5.14, validate exact family count and determine effective `nFib`; retain version-specific data. |

### FibBase ownership and current incompatibility

MS-DOC 12.5 section 2.5.2 owns `wIdent`, `nFib`, `unused`, `lid`, unsigned `pnNext`, document/template/glossary/quick-save/encryption/table flags, `nFibBack`, `lKey`, `envr`, the second flag byte and reserved fields through byte 31.

The current `WordFibParser` incorrectly interprets section-2.5.2 reserved bytes at offsets 20, 24 and 28 as `characterSet`, `fcMin` and `fcMac`, and reads `pnNext` as signed. This is now a recorded implementation defect, not an accepted compatibility convention. `DOC-I01` must remove those interpretations and replace affected synthetic fixtures with specification-shaped layouts.

## Branch algorithms

### Effective version

Read and bound the FIB using section 2.5.15. When `cswNew` is zero, use `FibBase.nFib`; otherwise use `FibRgCswNew.nFibNew`. The accepted five-value table above controls expected counts and field availability. A recognised other Word-family value is `UnsupportedFeature`; inconsistent counts or contradictory version fields are `Corrupt`.

### Secondary FIB and AutoText

`pnNext` is an unsigned page number. Zero means no attached AutoText FIB; otherwise the candidate offset is `pnNext * 512` using checked arithmetic in `WordDocument`. It must be zero when `fGlsy` is set or `fDot` is clear. The primary and secondary FIBs must share the CHPX/PAPX BTE ranges and `cbMac` required by section 2.5.2. Traversal uses visited offsets, maximum-secondary-FIB count and cumulative budgets. A cycle, alias with contradictory identity or out-of-range FIB is `Corrupt`; a budget breach is `ResourceLimitExceeded`.

An AutoText-only FIB owns `SttbfGlsy`, `PlcfGlsy` and `SttbGlsyStyle`. Their text is required payload under `EXT-DOC-004`; unimplemented non-empty AutoText ranges are `UnsupportedFeature`, never an informational warning.

### Quick-save

For `nFib < 0x00D9`, `FibBase.cQuickSaves` records the consecutive incremental-save count. At `0x00D9` and later the base field must be `0xF`, while `FibRgCswNewData2000.cQuickSavesNew` in section 2.5.12 carries the extended count for D9/101/10C and is embedded by `FibRgCswNewData2007` in section 2.5.13 for 112. `fComplex` identifies an incremental last save; it does not authorize ignoring stale or overlapping structures. CLX/PRC/simple-file decisions remain owned by `DOC-R03`; until resolved, an input requiring an unimplemented branch returns `UnsupportedFeature` with any earlier text marked partial evidence.

### Encryption

`fEncrypted` plus `fObfuscated` selects XOR obfuscation; when `fEncrypted` is clear, `fObfuscated` is ignored. `fEncrypted` without `fObfuscated` requires the bounded Table-stream encryption header used to distinguish binary RC4 from CryptoAPI. `lKey` is a verifier for XOR and an encryption-header byte count otherwise. The initial 68 bytes of `WordDocument` and the required header region remain clear as specified; the extractor reads only those classification bytes and never decrypts. CryptoAPI forbids `ObjectPool`, places OLE objects in `Data` behind `FOBJH`, and uses `fDocProps` to control the `encryption` and summary-property-stream rules. A conforming protected document is `Encrypted`; contradictory lengths, header versions, stream rules or protected-content pairing are `Corrupt`.

## Fc/Lcb slot policy

The JSON atlas assigns every cumulative slot:

- its ordinal, byte offset, introducing layout and minimum `nFib`;
- the two 32-bit field names and value kind;
- owning stream, record-grammar name and specification section;
- text/image/control relevance and active-content risk;
- one `EXT-DOC-*` parser owner; and
- an explicit support/failure policy.

`ValidateAndIgnore` is permitted only for specification-defined unused or deprecated cache fields after their invariants are checked. `RequiredSemanticExtraction` means a non-empty unimplemented value prevents `Complete`. `PassiveInspectOrUnsupported` means bounded passive classification may become complete only when the owning later research unit proves the structure cannot hide required text or images. The conservative default is `UnsupportedFeatureIfPresent`; no slot is unassigned.

Physical storage ownership is independent of semantic disposition. The reviewed atlas contains 143 Table-stream descriptors, four WordDocument-stream descriptors, 35 intrinsically no-stream descriptors and one FIB-resident `FILETIME`. An ignored cache therefore retains its Table ownership and must still undergo checked range validation before it can be omitted without a completeness penalty.

The committed generator verifies the pinned `[MS-DOC]` publication hash before producing the atlas. The offline verifier freezes the complete descriptor sequence and policy mapping; its optional `-SpecificationPath` mode independently re-reads the five source paragraph bands and verifies the exact field sequence. The 2026-07-24 independent closure review found zero source/atlas stream-ownership mismatches. See [DOC-R01 evidence](../testing/evidence/EV-2026-07-24-doc-r01-binary-atlas.md).

## Pre-implementation acceptance tests

`DOC-I01` cannot claim this atlas implemented until independent fixtures establish:

1. exact 900/1,024/1,248/1,472/1,630-byte FIB boundaries and exact counted-array values;
2. every descriptor ordinal, including the ordinal-87 `FILETIME`, without generic range guessing;
3. truncation at every FIB boundary, unknown bounded tails and contradictory version/count cases;
4. unsigned `pnNext` at zero, maximum, out-of-range, repeated and cyclic offsets;
5. AutoText shared-range invariants and glossary range routing;
6. pre-D9 and D9+ quick-save invariants; and
7. XOR, binary RC4, CryptoAPI and malformed encryption-header classification without decryption.

The descriptor generator and fixture generator must not share one unchecked handwritten table. An independent review must compare ordinals against the pinned specification before implementation metadata is accepted.
