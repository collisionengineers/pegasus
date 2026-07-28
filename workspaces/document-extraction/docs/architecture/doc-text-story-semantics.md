# DOC text, piece and story semantics

Status: **Mapped and specified** for `DOC-R03` on 2026-07-24. This contract defines the managed behaviour and tests that must precede `DOC-I02`/`DOC-I03`; it is not a claim about current parser support.

Owners: `EXT-DOC-003` and `EXT-DOC-004`. Authority: pinned `[MS-DOC]` 12.5, SHA-256 `2e48b21886ebdd5dcc281c3d9baf1b7841c9f3d6881a153862069bbbc0608d7a`, principally sections 2.2.1, 2.2.2, 2.3, 2.4.1, 2.4.6.1–2.4.6.2, 2.5.2, 2.5.4, 2.5.6, 2.5.12–2.5.13, 2.8.25, 2.8.35, 2.9.38, 2.9.47, 2.9.73, 2.9.177–2.9.178, 2.9.194, 2.9.209–2.9.210, 2.9.214–2.9.216 and 2.9.286–2.9.287. The intended caller is the CollisionSpike Infrastructure adapter through the one public extraction API. Only text and image payload are authorised.

The companion [machine-readable contract](doc-text-story-contract.v1.json) freezes the executable decisions and independent fixture groups.

## Text retrieval

Every supported C1/D9/101/10C/112 document retrieves text through the selected Table stream's non-empty `Clx`. There is no modern “simple file” fallback and no `fcMin`/`fcMac` text route. `FibBase.fComplex` says only that the last save was incremental: both values require the current CLX. Missing or zero `lcbClx` is `Corrupt`.

A CLX is zero or more `Prc` records followed by exactly one final `Pcdt`. Each `Prc` begins with `0x01`, has a nonnegative signed length no greater than `0x3FA2`, and contains whole `Prl` records. The Pcdt begins with `0x02`; its bounded PlcPcd length is exactly `4 + 12n` for positive piece count `n`. No bytes may follow it in CLX.

The `n+1` CPs are unsigned 32-bit values below `0x7FFFFFFF`; they begin at zero and are unique and strictly ascending. Decode the `n` pieces in logical CP order, never physical FC order. Physical ranges can be discontiguous, descending, shared or overlapping after incremental saves; this is valid when each referenced range fits both the `WordDocument` stream and normative `FibRgLw97.cbMac`. Bytes at or after `cbMac` have no meaning.

For uncompressed pieces, byte address is `fc + 2 * cpDelta`; for compressed pieces it is `fc / 2 + cpDelta`. All arithmetic is checked. `FcCompressed.r1` and `Pcd.fDirty` must be zero. When `Pcd.fNoParaLast` is one, its referenced text must contain no U+000D paragraph mark. Preserve piece identity, global and part-relative CP ranges, and every exact WordDocument byte span, including a valid surrogate split across physically separate pieces.

Quick-save fields validate save history but never select stale text. C1 stores `0..15` in base `cQuickSaves`; D9 and later require base `0xF` and store `0..15` in `cQuickSavesNew`. The current FIB, selected Table and current CLX are authoritative. A `Prm0` applies one mapped property; a `Prm1` indexes a preceding `Prc`. Bad indices or malformed property records are `Corrupt`. A valid unimplemented property that can change text, visibility, revision state, symbol meaning or special-character interpretation prevents `Complete`.

## Encoding

`FcCompressed` defines the entire base decoder. Uncompressed text is UTF-16LE code units. Compressed text consumes exactly one byte per CP and maps each byte to the same-valued Unicode code point except the 24 substitutions listed in the machine contract. Bytes `0x80`, `0x8E` and `0x9E` map to U+0080, U+008E and U+009E—not Euro, Z-caron or z-caron.

No FIB language, `lidFE`, font charset, Windows code page or DBCS state selects another story decoder. East Asian byte pairs remain two CPs. RTL and complex-script properties never cause visual reordering. `sprmCSymbol` is a semantic override for special U+0028: emit `CSymbolOperand.xchar` and retain `ftc`; do not invoke a font engine or guess glyph mappings.

UTF-16 CP accounting remains by code unit. A valid surrogate pair consumes two CPs and four source bytes even across a logical piece boundary. `[MS-DOC]` gives no isolated-surrogate recovery rule, so the product policy is to emit U+FFFD per isolated unit with exact CP/byte evidence and return `Corrupt` while retaining other readable text.

## Document parts and guards

The modern layout has seven part counts in this order: main, footnote, header, comment, endnote, main textbox and header textbox. They are contiguous cumulative ranges. The field between `ccpHdd` and `ccpAtn` is `reserved3`; it must be zero and is never a macro story.

The main part must be non-empty and its last character is U+000D. When any specialised part is non-empty, exactly one additional U+000D follows the last non-empty part and is outside every part; it is validated and excluded from output. There is no gap before footnotes or another specialised part. With no specialised part, the final PlcPcd CP equals `ccpText` and no outside guard exists.

The header part is subdivided by `PlcfHdd`. Its first six stories are the footnote separator, footnote continuation separator, footnote continuation notice, endnote separator, endnote continuation separator and endnote continuation notice. Each main-document section then contributes even header, odd header, even footer, odd footer, first-page header and first-page footer. A non-empty header story has a final U+000D guard excluded from its content. Other specialised parts and textboxes are split and anchored by their owning PLCs.

A secondary AutoText FIB uses its own bounded CLX and the shared fields required by `[MS-DOC]`. Named `SttbfGlsy`/`PlcfGlsy` ranges follow primary evidence deterministically. Missing anchor/name/range semantics retain decoded text as partial evidence rather than dropping it.

## Safe review projection

Projection never executes fields, follows links, evaluates layout or retrieves content. It retains typed lossless tokens before normalising review text.

- Tabs emit tab; paragraph, line, column and resolved page/section boundaries emit newline. Header guards and the outside-part final mark emit nothing.
- Cell boundaries emit tab and row boundaries newline only after paragraph properties distinguish them.
- Picture, drawing, automatic note and comment anchors emit no literal text and hand off to their owning semantic unit.
- Fields require `sprmCFSpec`, valid `Plcfld` agreement and nesting. Emit stored result text, never evaluate instructions. Preserve instruction text as non-primary evidence. A missing result is not invented.
- Structured-document-tag markers emit nothing. En/em space specials emit their Unicode space. A symbol emits `xchar` with font provenance.
- Unknown special controls emit no raw control byte, remain typed evidence and prevent `Complete`. The current U+001E/U+001F hyphen assumptions remain unsupported pending conformance or differential evidence.

Canonical review order is main; anchored footnotes; section-associated headers/footers; anchored comments; anchored endnotes; main textboxes by anchor; header textboxes by owner/anchor; then named AutoText. Until anchors exist, stored-order decoded parts remain visible partial evidence and are never silently omitted.

## Independent fixture programme

| ID | Required coverage |
|---|---|
| `DOC-T01` | five exact version layouts; both `fComplex` values; quick-save boundaries; all CP and FC primitives |
| `DOC-T02` | zero/one/multiple PRCs; Prm0/Prm1; all CLX/Pcdt/PlcPcd boundaries; logical/physical piece permutations; exact/end+1 `cbMac`; malformed UTF-16 |
| `DOC-T03` | each of seven parts alone and combined; header kinds; secondary AutoText; every control/property combination; exact semantic projection and provenance |

Expected literals and version layouts must be generated independently from production tables. Each test asserts exact format/part, CP and byte spans, typed token, review text, issue code/severity/location/order and public outcome. The current suite's five-version rows reuse one invalid 34-range layout, its Main+Footnote test puts the final guard in the wrong place, and its fake character-set test enforces a nonexistent decoder.

## Known production differences

- reserved FibBase bytes are read as character-set/text-range fields; `cbMac` is not used for piece bounds;
- all five versions reuse a non-versioned minimal FIB shape;
- a reserved `FibRgLw97` value is exposed publicly as `Macro`;
- the outside-part U+000D is inserted before Footnote instead of after the last specialised part;
- compressed text uses a CP1252-like table and rejects bytes based on fabricated state;
- isolated UTF-16 surrogates pass silently;
- PRCs are always reported unapplied even when later referenced;
- raw character values determine controls without effective properties;
- public DOC locations lose structured CP/part identity and label every issue offset as the Table stream.

These are implementation requirements for `DOC-I01`–`DOC-I05` and `DOC-I10`, not accepted behaviour.
