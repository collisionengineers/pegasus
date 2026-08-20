# Independent review — PR #474 — 2026-08-20

Reviewer: independent of the implementation.

## Changes

- `docs/capabilities.md`: records MAIL-05 as locally implemented and test-backed, while retaining deployment/live-write qualifications.
- `docs/design/README.md`: activates only the read-only message-detail recommendation and keeps MAIL-06/07 and Outlook writes deferred.
- `src/Pegasus.Core/Intake/RetainedMail.cs`: derives one current recommendation in the existing authorized exact-message read from the canonical MAIL-23 policy and approved-mailbox binding.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml`: renders the logical folder/policy or an unavailable reason inside the classification-evidence panel.
- `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs`: covers configured, ambiguous, disabled/missing/wrong-mailbox, No action, and re-derived Core outcomes.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs`: covers the authenticated configured and ambiguous/unavailable Razor caller.
- No persistence, migration, Graph adapter/call, operation key, confirmation, mutation, or duplicated policy mapping is introduced.

## Comments and disposition

- **Blocking — filed as [[PR-032]].** `GetRetainedMail` deliberately returns an unavailable `FolderRecommendation` when `Classification` is null, but the Razor markup renders all recommendation output only inside `if (Model.Detail.Classification is { } dossier)`. A retained message with no classification dossier therefore exposes neither the recommendation nor its accessible unavailable reason. This misses the ticket body and the refreshed research/files/plan promise that absence as well as ambiguity is visibly unavailable. The added Web test exercises Ambiguous (a non-null dossier), so it cannot catch this branch.
- No non-blocking comments.

## Report, governing docs, and simplification

- The post-implementation report's six-file inventory matches the PR diff and its scope claims are otherwise accurate.
- FRD-08 and EPIC-006 are respected: the recommendation is read-derived for one exact message, reuses MAIL-23's sole mapping and approved binding port, accepts no destination, and performs no Outlook or application mutation.
- The plan did not miss an implied ticket requirement; the implementation missed its explicitly planned no-dossier unavailable-state branch.
- The recorded four-lens simplification pass is honest: the diff reuses the existing Core owner and external boundary, removes future-facing opaque identity/version projection, performs no policy duplication, and adds no speculative framework. No unapplied simplification finding was found.

## Evidence checked

- Read the complete ticket folder, EPIC-003/EPIC-006 contexts, FRD-08, merged MAIL-23 contracts, PR body/diff, and gate state.
- `git diff --check origin/dev...HEAD` passed.
- Independent focused Core run: 27/27 passed.
- Independent authenticated Web/LocalDB run: 16/16 passed.
- GitHub CI was inspected; the initial metadata/check jobs were green and the test jobs were still running when the blocking verdict was reached.

## Verdict

**Needs changes.** [[PR-032]] blocks [[TICK-047]]. PR #474 is not merged and TICK-047 remains in Review. Re-run `kanmer-review` after the blocker lands in the PR.

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
