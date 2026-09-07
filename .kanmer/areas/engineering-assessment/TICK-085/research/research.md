# Research — TICK-085: genuine Glass's PDF import

## Question

How can all five supplied calculation PDFs enter the existing canonical import,
including YL69YFO's unusable font map, without duplicate import policy or an
unbounded OCR fallback?

Evidence base: origin/dev 783b537f189ead88553f940d03df0d1f9558ef75.
Root owns all builds, tests and cloud writes. This research ran read-only Git,
PDF extraction/font-resource inspection and local page rendering only. Poppler
is not installed; available PyMuPDF rendered the previews, pypdf inspected font
resources. No supplied PDF was modified and no Azure/Glass call occurred.

## Findings

1. All five genuine files are present in
   pegasus_pack/glasses-integration/glass_ref_docs. Current hash/length/page
   census and visually reviewed section totals are in
   pegasus_pack/current/glass-pdf-research.json. Files are 10–242 KB, 3–6 pages.
   The adjacent pegasus-work-pack and its artifact-manifest.yml/full line
   oracles cited by the historical ticket are absent. Astra's five text
   extractions exist; four readable, one gibberish. Its 15/15 Glass offline
   result explicitly excludes EvidenceParserTests and is NOT PDF import proof.
2. YL69YFO renders a normal calculation, but every page has Type3 fonts with
   no ToUnicode and anonymous numeric Encoding/Differences names. Page 1 has
   104 numeric names out of 106 named entries; the other two are .notdef.
   All six pages have the same class. Replacement-character count is zero.
   The other four have Verdana TrueType/WinAnsiEncoding, ALSO no ToUnicode.
   Therefore neither missing ToUnicode nor replacement counts nor parser
   failure is a correct OCR trigger. Use actually selected/drawn Type3
   anonymous-map fonts, not unused resources or all Type3 fonts.
3. PdfPig 0.1.15 is installed and locked. Public APIs confirmed from local
   package XML plus read-only reflection: Page.Dictionary;
   PdfDocument.Structure.GetObject/TokenScanner; Page.Operations;
   Graphics.Operations.TextState.SetFontAndSize.Font is NameToken. Letter.Font
   is FontDetails, NOT IFont; do not type-test it as IType3Font or use private
   reflection. Root TICK-041 owns the shared page qualification helper.
4. PDF Labour time is ALREADY NET, unlike XML Time-minus-OverlapTime.
   ML23OXR's first row prints .50 h, .10 overlap and GBP40 at GBP80/h.
   Subtracting the overlap again corrupts the estimate. Body/Auxiliary/Paint
   totals independently reconcile. Zero source labour rates in VX/LT are real.
5. Tables continue across pages; summary, Part no. and Position no. tables
   repeat evidence. They must not generate extra chargeable rows. Nested
   guide-only operations and annotation lines belong to their parent. Equal
   descriptions can identify distinct left/right positions, so description
   alone cannot join a part number. Keep raw guide/section/row evidence.
   LT's PR means Partial repair (page 6 legend), mapping to existing Repair;
   RP=Replace, R=Repair, UI=Remove/refit, EC=Surcharge. R/L direction, paint
   type 200 and paint level B/I/II/III/K1R/K2 are different columns/vocabularies.
6. Canonical Core ImportRawEstimate already proves retained metadata, logical
   document identity/length/hash, records line provenance, and replays by
   Case+source hash. Its parsers are selected only by filename/media type;
   adding another PDF registration would make every PDF ambiguous. The current
   Audatex parser accepts every PDF container before its strict footer check.
7. The actual Details.OnPostImportEstimateAsync bypasses canonical import:
   selects Audatex/JSON itself, parses first, retains, re-acquires the lease and
   calls ISaveEstimate directly with fresh mutable Case version. That loses
   per-row canonical source provenance and source-hash replay behavior. The
   production importer registration cannot prove this Web caller works.
8. The existing parser port is synchronous. TICK-041 will extend the existing
   OCR request/operation/envelope to exact Case/occurrence/document version,
   source hash/length and qualified pages; BeginDocumentAsync is deterministic
   and returns existing work. The Worker retains completed evidence without
   performing intake analysis or making an estimate. TICK-085 should consume
   that result through ImportRawEstimate, with no provider calls/wait loop,
   second dispatcher, automatic lease renewal or background human mutation.
9. ParsedEstimate.SourceTotals is currently dropped by canonical import.
   CaseRepairSpecificationEntity has no source-evidence JSON. The existing
   CalculationBreakdownJson stores and repeatedly recomputes Pegasus totals;
   OriginalValuesJson is per-line origin and CurrentValuesJson anomalies.
   None is an appropriate substitute for provider header evidence. Original
   retained PDF is already durable authority for raw identity/rates/totals;
   structured source persistence is only needed for a concrete consumer.
10. MCP already calls IImportRawEstimate but that command's unconditional
    RequireEngineer rejects Automation. SaveEstimate also restricts Automation
    to AiDraft plus a held job. Current FRD-10 line 121 explicitly owns raw
    retained import under automation.assessment; this is caller/policy drift.
    Extend the existing Core authorization only for validated document imports;
    keep AiDraft job rules and Engineer-only acceptance/Current unchanged.

## Implications

One registered PDF container reader must classify Glass/Audatex using positive
format markers AFTER embedded text or retained OCR is usable. Zero or multiple
formats refuse whole. Reuse Audatex's coordinate table reader, add one Glass
reader over the same normalized placed words. Infrastructure only interprets
format; Core owns queue/pending/identity/replay/mutation semantics.

The canonical result must distinguish imported ID from pending/unknown OCR.
A short reachable completion/retry action must use the exact retained source,
a newly submitted current version and lease, then the SAME command; do not
pretend pending imported, silently resubmit unknown work, or re-upload bytes.

No provider rate or printed VAT implies a chosen rate-card or repairer VAT
status. Parsing reconciles source arithmetic; Pegasus pricing remains its
existing Core owner. Missing or ambiguous input rejects whole, never partial.

## Open questions

See open-questions for the remaining TICK-041 exact seam, fresh full-line
oracles/recorded OCR evidence, and source-header persistence disposition. Root
approved the typed pending/unknown approach in principle; execution is not
assigned and this ticket remains untaken in Preparing.
