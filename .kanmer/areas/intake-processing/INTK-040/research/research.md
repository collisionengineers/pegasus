# Research — INTK-040: mailbox image attachments bypass Image Intake

## Question

Why did production Unidentified reference U35 not create or match an image-based Case, and how can future mail use the existing grouped manual-upload lifecycle without duplicating image policy?

## Findings

- Production SQL for U35 (UnidentifiedWork.Id = dd25ae8a-9607-444a-8ab6-1c781693ebc4) shows a completed mailbox receipt with decision needs_sorting, reason NoUsableIdentification, no failed processing attempt, no ImageVrmSuggestions, no ImageIntakes, and no case association.
- The same receipt retained one message/rfc822 source, three direct attachment image/jpeg assets totalling 10,964,058 bytes, and four inline_image image/png assets. The failure was therefore routing, not absence of vehicle photographs.
- ImageIntakeLifecycleRules.IsImageOnlyMaterial requires every retained asset to be an image. A mailbox receipt containing its EML source can never satisfy that rule, so ApplyAutomaticImageIntake never scans U35's JPEG attachments.
- EfQueuedCustodyProcessor.LoadImageAssetsAsync loads each registered receipt's source asset. Registering the parent EML receipt as Image Intake would custody the EML rather than the photographs; each selected attachment must therefore become a normal child receipt.
- SubmitGroupedIntake already stages one receipt per file, persists one idempotent group, and feeds the same group reconciliation used by manual uploads. It currently hard-codes ManualUpload; preserving the supplied source channel lets mailbox attachments reuse it without a second business implementation.
- ProcessIntake currently creates the parent Unidentified item before queued post-processing. The mailbox-image candidate must be deferred before that registration, and the child group must be durably submitted before the work item is completed.
- [[INTK-039]] merged PR #545 into dev; its ticket remains claimed only for post-merge verification. This task builds from that merged code and does not touch its branch or worktree.
- The operator chose two scope decisions in chat on 2026-08-25: successful child image routing replaces the parent email-level Unidentified outcome, and U35 itself is not replayed or repaired.

## Implications

Use one mailbox attachment selector for direct attachment assets whose media type is image/*; exclude the EML source, inline signature/logo images, and derived embedded images. Apply it only to otherwise-Unidentified mailbox receipts without instruction content, Case routing, or Triage routing. Submit those assets as one source-preserving grouped request with a nullable parent receipt relationship. Existing group reconciliation remains responsible for one registration, matched/no-match Case behavior, one group Unidentified result for unreadable/conflicting registrations, and image custody. Submission retries must reuse a stable token, while a terminal child-submission failure creates a technical-failure Unidentified result.

## Open questions

None. The replacement behavior and future-mail-only boundary were explicitly resolved by the operator.
