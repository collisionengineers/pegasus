# Plan — DOCS-009

Same lane and branch as [[CASE-018]]: `task/qdos26011-regressions`.

## Steps

1. **Choose the role by media type.** Replace the hard-coded `DocumentSemanticRole.Instruction` at `EfQueuedCustodyProcessor.cs:306` with `InstructionEvidenceImages.IsImage(attachment.MediaType) ? DocumentSemanticRole.Image : DocumentSemanticRole.Instruction`.

   *Reuses:* `InstructionEvidenceImages.IsImage`, already called by this same file for the embedded-photograph pass. This makes the two loops agree instead of disagreeing.

2. **Migration to correct the recorded past.** `UPDATE DocumentOccurrences SET SemanticRole = 'Image'` joined to the occurrence's version row, where the media type is an image and the role is `Instruction`. Written with the same guarded shape the recent grant migrations use.

3. **Pin the new migration id** into `IntakePersistenceIntegrationTests`' applied-migration census. Skipping this fails CI on a collection mismatch, which has already cost one run on this repo.

4. **Test.** Extend `CustodyOutboxIntegrationTests` so the accepted receipt carries both a JPEG and a PDF attachment, and assert the roles they land with.

## Ordering inside the lane

This runs **before** [[CASE-019]] is verified: the export selects images by `SemanticRole == Image`, so until this lands an export of QDOS26011 would produce an archive with no photographs in it. It is not a code dependency — the two touch different files — but it is a verification dependency.

## Acceptance

- A newly accepted receipt files its image attachments as `Image` and everything else as `Instruction`.
- QDOS26011's eight photographs read as images after the migration.
- The instruction PDF and the `.eml` are unchanged.

## Simplification pass

Recorded after implementation, before the PR.
