# Plan — BUG-001

## Chosen approach

Decouple QDOS principal identification from instruction-document field extraction.

For a QDOS email, establish the principal from the email body within an accepted QDOS mail route. Keep mail classification as its own required decision. Once those gates establish the QDOS instruction context, extract the case fields from the appropriate readable instruction documents without requiring those documents to contain a QDOS marker. Build the draft with principal `QDOS`, then retain the existing completeness, case-match, allocation, replay, and custody gates.

Do not add `OfQDOS` recognition. That string is incidental attachment extraction output and was never a product criterion.

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: authorised definitive intake and fail-closed allocation.
- `docs/frd/frd-05-documents-extraction-and-custody.md`: extract required instruction data; custody follows allocation.
- `docs/frd/frd-09-provider-and-intermediary-routes.md`: route, provider/principal identity, classification, and association remain distinct.
- No new PRD/FRD/ADR is required for this defect correction.

## Ordered implementation steps

1. Create the dedicated BUG-001 branch/worktree from fresh `origin/dev`, take the ticket into Implementing, and record the source head.
2. Add a failing Core regression representing the observed structure:
   - accepted QDOS direct route;
   - QDOS identified in the email body;
   - valid QDOS Audit classification;
   - instruction fields located in separate attachment content;
   - expected result is an applicable QDOS draft and `CaseCreated`.
3. Add fail-closed tests:
   - route accepted but no QDOS body evidence;
   - QDOS body evidence but rejected/ambiguous route;
   - QDOS body and route but insufficient/ambiguous classification or mandatory instruction evidence;
   - QDOS appears only in an attachment;
   - unrelated content and replay cases.
4. Refactor the smallest Core boundary so the established mail context is explicit:
   - do not let `QdosInstructionExtractionPolicy` rediscover principal identity from arbitrary content fragments;
   - consume email-body identity separately from document field extraction;
   - require the accepted route and applicable classification before automatic-case eligibility;
   - populate the draft principal as `QDOS` from the established QDOS context;
   - keep provider-neutral interfaces where practical and bump any changed policy version.
5. Preserve extraction rules for actual instruction fields and missing/ambiguous values. Do not require a QDOS token in an attachment and do not add a document-principal fallback.
6. Add focused processing/allocation integration coverage proving:
   - the regression allocates exactly one Case/PO and enqueues exactly one custody item;
   - reevaluation/replay is idempotent;
   - every negative prerequisite produces no allocation or custody work.
7. Run the QDOS route/classification/extraction and ProcessIntake unit suites, focused allocation/custody integration suites, Worker composition tests, Release build, and `git diff --check`. Record exact results in the post-implementation report.
8. Obtain independent review and green CI before merge to `dev`. The reviewer must specifically verify that attachment extraction no longer identifies the QDOS principal and that all prerequisite gates fail closed.
9. Deployment remains separately approval-gated. With exact-target approval, deploy the immutable reviewed revision, read back Web/Worker identity and health, and update `docs/current-architecture.md` and `docs/operations.md`.
10. With separate exact-target approval, re-evaluate receipt `9a91fe16-d62f-4477-a11e-830fd96f672a` through the existing reasoned command. Confirm preserved history, one allocation, one Case/PO/link, one custody work item, one Box folder/source retention outcome, and no replay duplicates.
11. Write `proof.md` and close only after merged-main and authorised live evidence pass. If any prerequisite still fails, preserve the exact evidence and stop at that boundary.

## Acceptance conditions

- A QDOS email with the principal identified in its email body can use separate instruction-document content for field extraction.
- Neither `OfQDOS` nor any other attachment-local QDOS text is required.
- An attachment alone does not identify the QDOS principal.
- Missing or ambiguous route, body identity, classification, or mandatory extraction evidence fails closed with no Case/PO or Box work.
- The corrected valid path allocates and enqueues custody exactly once under replay.
- No Box, queue, schema, OCR, or broad PDF-reader change is introduced.

## Stop point for this phase

Research and planning end here. Do not create a worktree, edit source, deploy, re-evaluate the live receipt, or perform any external write in this phase.
