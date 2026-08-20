# Research — PR-018

Searchability currently collapses projection rows to filenames. Both retained attachments and search documents have stable ordinals within their owning message/receipt, but the search document row does not persist attachment ordinal. Add nullable attachment ordinal to the one projection and correlate by ordinal; null continues to identify the message body. This requires updating the existing unmerged migration, not a second migration. Source: `IntakeSearchProjection.cs`, `PegasusDbContext.cs`, `EfRetainedMailboxMessageStore.cs`.

## Re-review refresh — 2026-08-20

The persisted ordinal is exact only if the display and canonical readers retain the same non-inline attachment occurrence domain. `LocalEmailDisplayReader.Attachments` currently skips a nameless MIME attachment while the canonical reader infers a filename and advances its descriptor ordinal, shifting every later display attachment. Preserve the existing ordinal design by retaining nameless display attachments with a deterministic operator label instead of dropping them. The retained attachment table also omits `IsSearchable`, so `Message.cshtml` must render the already-derived property per row. Sources: `LocalEmailDisplayReader.cs`, `MimeKitPdfPigOpenXmlIntakeSourceReader.cs`, `Message.cshtml`.

## Final re-review refresh — 2026-08-20

Verified at current branch after merging `origin/dev`: `ReadMimeEntityAsync` returns for every `TextPart` before it creates an `IntakeAttachmentDescriptor`. MimeKit includes an attached `text/plain` part in `message.Attachments`, so the display reader retains it while the canonical reader does not advance its ordinal; every later descriptor shifts. Treat an attached text part as an unsupported-but-described attachment before returning. Non-attachment body text remains root content. Source: `MimeKitPdfPigOpenXmlIntakeSourceReader.ReadMimeEntityAsync`; existing ordinal persistence remains correct.
