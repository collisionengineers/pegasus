# Checklist — BUG-001

- [ ] Create the BUG-001 branch/worktree from fresh `origin/dev`, take it into Implementing, and record the exact head.
- [ ] Add the regression: accepted QDOS route + QDOS email body + valid classification + fields in separate attachment content.
- [ ] Assert the regression creates an applicable QDOS draft and reaches `CaseCreated`.
- [ ] Add negatives for missing/ambiguous route, missing QDOS body evidence, insufficient classification, and insufficient mandatory fields.
- [ ] Prove QDOS appearing only in an attachment does not identify the principal.
- [ ] Refactor principal-context establishment away from arbitrary document-fragment extraction.
- [ ] Pass accepted route/body identity/classification context into the extraction/decision boundary explicitly.
- [ ] Extract instruction fields without requiring a QDOS marker in the attachment.
- [ ] Populate draft principal `QDOS` only from the established QDOS email context.
- [ ] Apply policy-version updates required by repository conventions.
- [ ] Prove exactly one allocation, Case/PO, link, and custody work item for the corrected path.
- [ ] Prove replay idempotency and zero downstream work for all fail-closed paths.
- [ ] Run focused unit/integration/composition tests, Release build, and `git diff --check`; record exact evidence.
- [ ] Obtain independent review and green CI before merge to `dev`.
- [ ] Obtain exact-target approval before deployment or production receipt re-evaluation.
- [ ] After authorised deployment/re-evaluation, verify immutable revision, preserved history, Case/PO, link, custody, Box, and replay evidence.
- [ ] Write `proof.md` and close only when merged-main and authorised live proof pass.
