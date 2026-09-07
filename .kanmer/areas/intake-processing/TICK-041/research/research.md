# Research — TICK-041: Qualified Azure OCR

## Question

What remains between the existing intake OCR implementation and the operator's
required scan-like instructions and unusable-text-map estimate PDFs?

## Findings

- Source base is dev 783b537f189ead88553f940d03df0d1f9558ef75, including
  INTK-061's retained-result retry fix. WorkerDependencyInjection already
  registers AzureDocumentIntelligenceOcr only when DocumentIntelligence:Endpoint
  is configured. It uses existing HttpClient/Azure.Identity, prebuilt-layout
  and GA 2024-11-30; no SDK dependency is needed.
- IntakeOcr.cs owns operation identity, provider port, page/word/table output,
  validation, retry and provider-result replay. EfIntakeOcrOperationStore pairs
  one operation with the existing ExternalWorkItem and stores result/hash.
- The foundation schema already supports exactly one IntakeAssetId OR
  DocumentVersionId. However ProcessIntakeOcr always loads an intake receipt
  and asset, so the document-version form has no working post-Case caller.
  Request.Validate also always requires an intake receipt. Extend this
  existing source-context boundary rather than add a second OCR store/worker.
- The operation's QualifiedPagesJson already contains its context envelope;
  source Case/occurrence/length can be bound there with the existing immutable
  document-version FK, avoiding a new storage unit. Exact Case metadata and
  ReadLogicalDocumentVersion remain the only source reader/authorization path.
- ImportRawEstimate is the Core import used by the Glass's XML gateway and
  automation. The Web OnPostImportEstimate still parses first and calls the
  custody/save operations itself. It offers only JSON/Audatex; TICK-085 owns
  genuine Glass's PDF parsing and must wire the same canonical import.
- Existing PdfPig intake extraction qualifies insufficient embedded text plus
  a dominant raster. Unusable text maps are NOT implemented: the supplied
  YL69YFO extracted text is nonlinguistic but printable, so counting Unicode
  replacement/control characters would miss it. Do not substitute a generic
  ambiguous-parse-to-OCR fallback or write a glyph decoder. Qualification needs
  positive PDF/font evidence or explicit retained evidence of a text-map fault.
- Genuine five-PDF Glass's fixtures exist under
  pegasus_pack/glasses-integration/glass_ref_docs; TICK-085 owns their reviewed
  parser oracles. Six MP scan samples previously lacked real Azure output.
- Last read-only Azure census found no Cognitive Services account. PLAT-065
  owns provisioning and worker-only managed-identity activation. The current
  operator explicitly authorizes that service; no cloud write has occurred.
- Microsoft Learn confirms the selected GA API and that Entra authentication
  needs the account's custom subdomain. References:
  https://learn.microsoft.com/azure/ai-services/document-intelligence/prebuilt/layout?view=doc-intel-4.0.0
  https://learn.microsoft.com/azure/ai-services/document-intelligence/quickstarts/get-started-sdks-rest-api?view=doc-intel-4.0.0

## Implications

ADR-0040 supersedes ADR-0001's scan-only/prebuilt-read decision, retaining
ADR-0003's PdfPig selection and ADR-0005's ordinary intake limits. FRD-05 owns
qualification, source attribution and fail-closed behavior. TICK-085 supplies
the second production caller; do not mark a post-Case OCR port complete before
that retained-document import can actually reach it. Local tests remain
offline; genuine Azure activation evidence belongs to PLAT-065.

## Open questions

Positive generalizable text-map fault detection and the minimal UI resumption
of a queued estimate import need to be settled in the concrete plan. These are
implementation research, not permission to send every failed parser to OCR.
