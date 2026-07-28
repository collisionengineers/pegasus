# DOC property-engine semantics

Status: **Mapped; specification overlay strengthened but not closed** for `DOC-R04` on 2026-07-24. Production remains an implemented framing subset. Authority is pinned `[MS-DOC]` 12.5, SHA-256 `2e48b21886ebdd5dcc281c3d9baf1b7841c9f3d6881a153862069bbbc0608d7a`, principally sections 2.2.5, 2.4.6, 2.6.1–2.6.5, 2.8.5–2.8.6, 2.8.26, 2.9.32–2.9.33, 2.9.113–2.9.114, 2.9.174–2.9.175, 2.9.209–2.9.216, 2.9.243, 2.9.245, 2.9.258–2.9.275 and 2.9.336–2.9.337.

Owner: `EXT-DOC-005`; inbound owners `EXT-DOC-002` and `EXT-DOC-003`; downstream semantic owners `EXT-DOC-006`–`EXT-DOC-009` and public projection owner `EXT-DOC-013`. The intended caller remains the single extraction API used by the CollisionSpike Infrastructure adapter. Only logical text and independently validated images are payload.

The generated [SPRM catalogue](doc-sprm-catalogue.v1.json) is derived from the hash-pinned publication by [its generator](../../scripts/Generate-DocSprmCatalogue.ps1). It freezes all 322 names/opcodes, decoded bit fields, operand framing, typed grammar and validator, legal property arrays, five supported `nFib` identities, application conditions, extraction relevance, concrete mutation family/state key, Data-stream targets, source paragraphs and definition hashes. Reviewed ownership is explicit per row; the generator does not infer relevance from names or assign style arrays merely from the SPRM group. The committed catalogue is a compact derived index, not redistributed specification prose.

## Catalogue and framing

The source contains 91 paragraph, 84 character, 8 picture, 59 section and 80 table SPRMs. `spra` counts are 25/80/59/41/26/9/75/7 for values zero through seven. Every 16-bit opcode must round-trip:

```text
ispmd = opcode & 0x01FF
fSpec = (opcode >> 9) & 1
sgc   = (opcode >> 10) & 7
spra  = (opcode >> 13) & 7
```

`spra` 0/1 consumes one byte; 2/4/5 consumes two; 3 consumes four; 7 consumes three. Ordinary `spra=6` consumes a one-byte `cb` plus exactly `cb` bytes. `sprmTDefTable` (`0xD608`) instead has UInt16 `cb` and total operand length `cb+1`. `sprmPChgTabs` (`0xC615`) uses ordinary framing below `0xFF`; at `0xFF`, checked deleted/added tab-array counts determine its length. Unknown opcodes may be retained only after their exact boundary is proven; their relevance is unknown, so an active-range occurrence prevents `Complete`.

Version-looking name suffixes do not select applicability. All five supported layouts are explicit. Six legacy table-shading SPRMs carry the source condition to ignore them above D9 when table styles are understood; conditional operations such as style permutation, list level, HugePapx placement, header-row continuity and section numbering carry row-level application conditions. Other definition restrictions remain validator obligations.

Style-owned arrays are narrower than direct formatting arrays. `UPX-CHPX` excludes the character-style-prohibited reset/style/conditional/bullet SPRMs from section 2.9.336. `UPX-PAPX` excludes paragraph-style-prohibited style selection, nesting, tab mutation, huge/Data indirection and conditional SPRMs from UpxPapx. `UPX-TAPX` applies the UpxTapx exclusion list, including direct table-definition, structural cell mutation and raw-shading records; its `sprmTIstd` is ignored and the built-in style-11 `sprmTWidthBefore` exception remains a typed application condition. A row absent from an array is corrupt if encountered there; it is not silently treated as direct formatting.

## Property storage

`Prm0` is the exact compact table in section 2.9.215; reserved `isprm` values are not invented. `isprm=0,val=0` has no effect. `Prm1` is a zero-based reference to an already preceding CLX `Prc`. `PrcData.cbGrpprl` is signed, `0..0x3FA2`, and its body must be wholly composed of valid `Prl` records. Paragraph application retains paragraph-group effects and character application retains character-group effects. Exact PCD/PRC provenance is retained and a referenced PRC is not simultaneously reported as unapplied.

`PlcBteChpx` and `PlcBtePapx` contain strictly ascending unique FC boundaries. Checked page number times 512 selects a complete WordDocument FKP page; BTE endpoints must agree with the referenced FKP. Aliased pages, shared property records and overlapping logical piece mappings are distinguished explicitly rather than resolved by first match.

- `ChpxFkp.crun` is `1..0x65`; it owns `crun+1` FCs and `crun` byte offsets multiplied by two. Zero means defaults. A `Chpx.cb` byte bounds a complete property array.
- `PapxFkp.cpara` is `1..0x1D`; it owns `cpara+1` FCs and complete 13-byte `BxPap` records. Zero offset means defaults. For nonzero first `cb`, `GrpPrlAndIstd` is `2*cb-1` bytes, leaving `2*cb-3` after two-byte `istd`. If first `cb` is zero, `cb' >= 1` owns exactly `2*cb'` bytes. Property heaps must not overlap run metadata or unrelated adjacent records.
- `PlcfSed` maps ordered section CPs. Each non-sentinel `Sed.fcSepx` selects a bounded WordDocument `Sepx`; its length and entire SPRM array must be valid.

Each physical property interval is normalized across every intersecting logical piece, story and semantic boundary. Endpoint ownership is half-open. Exact FC, global/story CP, stream, FKP page, record and property-byte provenance remains stable.

## Effective-state transitions

SPRM arrays are ordered transitions; later applicable entries win unless their individual grammar says otherwise. State snapshots retain the winning value and source.

Paragraph state applies specification and stylesheet defaults, table-style paragraph properties and table conditional state, then base paragraph styles parent-first, the current paragraph style, direct PAPX, paragraph-group piece PRM and list-derived paragraph state. Character state applies stylesheet font defaults, table-style character properties and matching table conditional character formatting before the paragraph-derived character style; it then applies the current character style (including valid `sprmCIstd` transitions), direct CHPX and character-group piece PRM. Table conditional order is horizontal bands, vertical bands, first/last column, first/last row, then corners. Section defaults and ordered SEPX form a separate state.

An `istd` is `0x0000..0x0FFD` and selects a nonempty style. `istdBase=0x0FFF` means no parent; otherwise parent, next and link references must select valid nonempty styles. Self-reference and cycles are `Corrupt`. `cupx` and revision forms must match the exact style-kind counts, typed UPX members occur in required order, and even-size padding bytes are zero. Property arrays enforce their group/opcode exclusions.

The product relevance classification is conservative. Visibility/revision, field hiding, special/symbol/font/language/script state is text-critical. Paragraph/list/table/cell/row and section/story linkage is structure-critical. Picture/data/OLE discriminator state is image-critical. Visual decoration, borders, shading and page geometry are rendering-only only when they cannot change logical text, image identity or ordering. All eight picture-group SPRMs are border properties and are payload-neutral after validation. Proofing/UI/printing/session properties remain passive compatibility evidence.

## Data indirection and bounds

Only `sprmCPicLocation` (`0x6A03`), `sprmPHugePapx` (`0x6646`) and `sprmPTableProps` (`0x646B`) directly identify Data-stream state. Huge-PAPX/table-property offsets select bounded `PrcData` with `cbGrpprl >= 10`. A processed huge property terminates the containing array as specified; HugePapx must be first and has stricter `GrpPrlAndIstd` constraints. Chains use checked offsets, visited sets and cumulative depth/count/byte budgets. Cycles are `Corrupt`; a finite traversal exceeding configuration is `ResourceLimitExceeded`.

Configuration owns cumulative property bytes, PRC bytes, FKP/PLC pages and records, property applications, style depth, Data offsets/dereferences, image/object references, CPU/deadline and cancellation checkpoints. Earlier safe evidence is retained with the non-complete outcome. No property can trigger process, link, path, network, OLE, macro or field execution.

## Failure contract and executable evidence

Truncation, invalid exact sizes, descending/duplicate ranges, property/table overlap, invalid references, prohibited array membership, style cycles and Data cycles are `Corrupt`. Valid unsupported relevant semantics with useful evidence are `Partial`; if no safe useful projection exists they are `UnsupportedFeature`. Bounds, cancellation and deadlines retain their distinct public outcomes. `Complete` requires every observed relevant property to be applied and every ignored property to be fully framed, validated and proven payload-neutral.

The independent `DocR04ExecutableSpecificationTests` oracle must not call production property parsers or constants. It covers all eight framing forms and both exceptions, PRM/PRC group filtering, PLC/BTE/FKP layouts, both PAPX forms, SEPX, literal cascade snapshots, styles/cycles, Data indirection/cycles, exact bounds and deterministic outcomes. Generated row tests use the committed catalogue, while expected framing and transitions are independently encoded.

## Remaining `DOC-R04` closure

The catalogue now provides a deterministic validator dispatch identity and mutation family for every row, but this does not yet encode every definition-specific numeric domain, cross-field precondition, index range, default value, relative/additive operation and legacy replacement interaction as executable data. Named complex operands still depend on their typed validator contracts, and generic last-applicable-wins mutation families still require a reviewed per-SPRM exception audit. Until that overlay and its independent row tests exist, the `DOC-R04` exit statement that implementation can proceed solely from tables and state transitions is not met.

## Known production differences

Production recognizes twelve opcodes across only ten semantic categories and seven compact PRM mappings. Its generic `spra=6` decoder misses both exceptions. PAPX over-reads one byte, omits the `cb=0` form, and CHPX/PAPX mapping chooses a first physical piece at ambiguous boundaries. SEPX, effective state, styles, Data indirection and cumulative property budgets do not exist. Public extraction discards property runs, labels all issues as warnings and can describe processed ranges as unprocessed. These are requirements for `DOC-I04`, not accepted behaviour.
