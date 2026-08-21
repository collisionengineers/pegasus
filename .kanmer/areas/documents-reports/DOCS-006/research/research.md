# Research — DOCS-006

Premises verified read-only on origin/dev and the live estate:

- **Verified:** the reader already extracts embedded PDF images at intake —
  live QDOS26007 carries 17 `IntakeAssets` rows Kind=`embedded_image`
  (60–320 KB damage photos) plus repeated letterhead PNGs (234 B–28.7 KB) and
  Kind=`inline_image` signature images; the corpus `Images-V1.pdf` shape
  matches. Nothing promotes them: custody's
  `RetainInstructionAttachmentsAsync` filters Kind=`attachment` only, and
  `Cases/Details.EvidenceCount = Documents + ImageIntakes`.
- **Verified:** custody reads asset bytes by `StorageKey` through
  `IIntakeArtifactStore` (DOCS-005 attachment path);
  `RetainAcceptedIntakeAttachmentAsync` takes (receipt, file name, media
  type, hash, storage key, length) + ordinal + op key — embedded images fit
  the same contract unchanged.
- **Verified:** no per-asset web endpoint exists. `/Intake/Image/{id}` serves
  a receipt's *source* inline (image-only, nosniff, no-store);
  `DownloadIntakeSource` shows the pattern — receipt's `AssetRecords` +
  `artifactStore.ReadAsync` + hash verification. An asset variant reuses
  `IIntakeReceiptQueries` with no new store method.
- **Verified:** web role holds SELECT on `IntakeAssets`; worker holds
  SELECT, INSERT — no grant change needed.
- **Measured (corpus + live):** letterhead/logo embedded images ≤ 28.7 KB and
  repeat by identical `ContentHash` across pages; genuine photos ≥ 60.2 KB.
  Size floor for embedded promotion set at 40 KB; deliberately attached
  image files take no floor (the sender chose to attach them);
  `inline_image` (signature art) is never promoted.
- **Assumed:** photos belong beside the source in
  `Evidence/Original instruction` (same folder as the DOCS-005 attachments)
  rather than a new subfolder — one custody location per instruction.
