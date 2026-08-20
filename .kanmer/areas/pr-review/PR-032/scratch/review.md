# Independent re-review — PR #474 — 2026-08-20

Reviewer: independent of the implementation.

## Changes

- The full six-file MAIL-05 diff remains as previously reviewed: Core derives a read-only recommendation from the canonical MAIL-23 policy and current approved-mailbox binding; Razor displays it; focused Core/Web tests and the capability/design records describe the local evidence tier.
- Commit `4bc3f158` fixes [[PR-032]] by rendering the existing Core unavailable recommendation when the classification dossier is null and adds one exact authenticated Web/LocalDB regression test.
- No folder move control, move handler, new POST, persistence write, Graph call, destination input, or duplicate folder mapping is introduced.

## Comments and disposition

- **Previous blocking comment — fixed-in-PR by [[PR-032]].** The null-classification result now renders in its own semantic `section` labelled by an `h2`, with a definition-list value and the Core-owned unavailable reason/policy.
- No new blocking or non-blocking comments.

## Report, governing docs, and simplification

- TICK-047 and PR-032 post-implementation reports match the six-file/two-file diffs respectively and record the replacement commit, exact caller test, and no-write boundary.
- FRD-08 and the epic contexts remain satisfied: classification, recommendation, and later move are separate; MAIL-23's `MailLogicalFolderPolicy`, `IApprovedMailboxStore.ListAsync`, exact mailbox identity, and typed binding are reused read-only.
- The plan did not miss an implied requirement, the implementation now meets the plan including null-dossier unavailability, and the recorded simplification dispositions are honest: existing Core result reused, no partial/framework/store/adapter/mutation added.

## Evidence checked

- Re-read both complete ticket folders, both epic contexts, FRD-08, the full PR diff, updated PIRs/open questions, and both tickets' gate state.
- `git diff --check origin/dev...HEAD` passed.
- Independent local Core: `RetainedMailTests` 27/27 passed.
- Independent exact authenticated Web/LocalDB: `MessageDetailShowsUnavailableFolderRecommendationBeforeClassificationExists` 1/1 passed.
- Replacement CI run 32369318716 on head `4bc3f158`: documentation, unit, browser, SQL shards 2/3 and other required jobs passed. SQL shard 1 initially had one unrelated SQL post-login timeout in `CaseTaskArchivePersistenceTests` (254/255 passed); attempt 2 reran the unchanged SHA and passed all 255 tests plus shard coverage.

## Verdict

**Pass.** [[PR-032]] is fixed in PR #474. PR #474 may merge to `dev`; then [[TICK-047]] and [[PR-032]] each move exactly one stage to Verifying.
