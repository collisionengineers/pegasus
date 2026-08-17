# Checklist — BUG-001

- [x] Recreate or clean the abandoned BUG-001 worktree from fresh `origin/dev`, take the ticket into Implementing, and record the base SHA.
- [x] Add direct-sender regressions for all three exact accepted QDOS domains with instruction fields containing no QDOS token.
- [x] Add a staff-forward regression where the Collision Engineers transport sender has one accepted QDOS prior/original sender and content contains no QDOS token.
- [x] Prove fields split across body and attachments produce a QDOS draft and `CaseCreated` after route acceptance.
- [x] Prove body, subject, filename, metadata, attachment, OCR, or AI text containing QDOS cannot establish the principal.
- [x] Prove manual-upload and automation content cannot invent QDOS without a separately established principal context.
- [x] Retain fail-closed tests for missing, malformed, internal, conflicting, or multiple prior senders and domain-widening attempts.
- [x] Add a required provider-neutral established-principal/extraction context to Core.
- [x] Make `ProcessIntake` derive QDOS context only from an accepted selected QDOS route and pass it into extraction.
- [x] Keep classification and case matching distinct from principal establishment.
- [x] Remove duplicate route evaluation, QDOS content/metadata markers, and the same-fragment identity gate from `QdosInstructionExtractionPolicy`.
- [x] Extract fields across readable fragments from established QDOS context while preserving conflicts, missing values, OCR information, and Triage matcher evidence.
- [x] Populate the draft principal from established context and bump the QDOS extraction policy version.
- [x] Make automatic mailbox allocation use the persisted accepted route principal and reject missing or mismatched route/draft identity.
- [x] Preserve manual staff-create authority and replay/idempotency semantics.
- [x] Update composition, architecture assertions, wrapper policies, and all content-only QDOS fixtures to state an authorised principal source.
- [x] Prove the corrected direct and staff-forward paths create exactly one allocation, Case/PO, link, and custody work item under replay.
- [x] Prove every content-only or identity-mismatch negative creates zero allocation, case link, custody, or Box work.
- [x] Run locked restore, Release build, focused QDOS/ProcessIntake/allocation/custody/Worker/architecture tests, full tests where practical, and `git diff --check`.
- [x] Write the post-implementation report with governing-doc mapping, changed-file rationale, risks, and exact verification output.
- [x] Obtain independent review confirming sender-only QDOS identity, route-authoritative allocation, fail-closed negatives, and no unrelated scope.
- [x] Push and open a PR targeting `dev`; merge only after independent review passes and CI is green.
- [ ] Obtain exact-target approval before any deployment, production receipt reevaluation, mailbox/data mutation, or Box write.
- [ ] After authorised deployment, refresh current-state docs and verify one production allocation/Case/PO/link/custody/Box outcome with no duplicates.
- [ ] Write `proof.md` on merged `main` and close out only after all resolved gates and authorised evidence pass.

## Closeout — BUG-001

- [x] PR merge verified (`gh pr view --json state,mergedAt`)
- [x] proof.md finalised (PR URL + merge date appended)
- [x] Moved to final stage
- [x] Outcome recorded in ticket body (PR link, follow-ups)
- [ ] cd out of worktree; `git worktree remove ../pegasus-worktrees/bug-001-qdos-intake`
- [ ] `git branch -d task/bug-001-qdos-intake` (`-D` if squash/rebase-merged)
- [ ] `git fetch --prune` + `git worktree prune`
- [ ] `take_ticket action: "release"`
