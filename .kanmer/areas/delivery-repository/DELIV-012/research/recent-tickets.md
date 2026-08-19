# Recent tickets — quality review of everything since the last deployment

Measured 2026-08-19 ~12:30–13:10 UTC, read-only, from the main checkout and the
Kanmer board. Sibling documents: `research/current-estate.md` (Azure) and
`research/codebase-evidence.md` (dev-vs-main diff). This document covers the
board and the PRs.

**Anchor facts, re-verified here.** Production serves **release 10**,
`d8de29cb94f396816595b1f9782980476166dbfa`, deployed 2026-08-18 13:52 UTC
(merge of PR #405); `/diagnostics/version` still returns that SHA.
`origin/dev` = `560f741c89cd109a0f28e53a4e8172fdc2d3c279` (PR #420, 2026-08-19
12:16 UTC) and did not move during this research. `origin/main` is an ancestor
of `origin/dev`; **`dev` is 42 commits / 16 merges (12 task PRs) ahead of
production**. Therefore **anything merged after 13:52 UTC on 2026-08-18 is not
deployed**, no matter what a ticket says.

Five task PRs are open against `dev`: **#416** INTK-005, **#417** INTK-006,
**#422** TICK-045, **#423** INTK-008, **#424** INTK-007. **#410** is the
`dev`→`main` release vehicle, not a task PR.

---

## 1. Roster since the last deploy

Every non-archived ticket whose `updated` is at or after 2026-08-18T13:52Z and
whose status is review, verifying or done. Sixty-eight tickets matched the raw
`updated_since` filter, but 39 of those were touched **only** by a board-wide
`order` renumber at 2026-08-19 09:39 UTC (actor `codex-mcp-client`) and had no
real activity in the window. Filtering those out against `get_activity` leaves
**29 tickets with genuine activity**, listed below.

Legend: *merged?* = merged into `dev` (never into `main`/production).
*OQ* = unticked `- [ ]` in `open-questions/` above the literal
`## Parked (explicitly deferred)` heading.

| id | title (short) | status | profile | taken (branch / worktree / assignee) | PR(s) | merged to dev? | deployment field | docs present | checklist | OQ |
|---|---|---|---|---|---|---|---|---|---|---|
| TICK-093 | ENG-01 canonical repair specification | verifying | feature | `task/tick-093-versioned-repair-spec` / `../pegasus-worktrees/tick-093-versioned-repair-spec` / codex-mcp-client | #420 | yes `560f741c` | `not-deployed` | all + PIR | 6/6 | 0 |
| INTK-007 | Replace Needs sorting with Unidentified | review | feature | `intk-007-unidentified-intake` / `.worktrees/intk-007` / Codex | #424 | **no — open** | *absent* | all + PIR | 22/36 | 0 |
| TICK-045 | MAIL-03 shared classification policy | review | feature | `task/tick-045-shared-classification-policy` / `../pegasus-worktrees/tick-045-shared-classification-policy` / Codex / execute_tick_045 | #422 | **no — open** | *absent* | all + PIR | 12/12 | 0 |
| INTK-008 | ImageIntake Image-initiated lifecycle | review | feature | `intk-008-image-initiated-lifecycle` / `.worktrees/intk-008` / Codex | #423 | **no — open** | *absent* | all + PIR | 8/29 | 0 |
| INTK-006 | Grouped image routing | review | fix | `intk-006-grouped-image-routing` / `.worktrees/intk-006` / Codex | #417 | **no — open** | *absent* | all + PIR | 26/41 | 0 |
| TICK-213 | Report density subsumption | done | feature | — | #421 | yes `4ba63888` | `n/a` | all + proof | 15/15 | 0 |
| TICK-046 | MAIL-04 classification evidence/history | verifying | feature | `task/tick-046-classification-history` / `../pegasus-worktrees/tick-046-classification-history` / codex-mcp-client | #418 | yes `181fe331` | *absent* | all + PIR | 10/10 | 0 |
| PR-009 | Preserve long report tails | done | fix | — | #419 | yes `4f67a83e` | `n/a` | all + proof | 17/17 | 0 |
| INTK-005 | Grouped upload, multiple files | review | feature | `intk-005-grouped-upload` / `.worktrees/intk-005` / Codex | #416 | **no — open** | *absent* | all + PIR | 7/33 | 0 |
| PLAT-001 | Claude Design UI implementation | done | feature | — | #397 | yes — **in release 10** | *absent* ⚠ | all + proof | 55/63 | 0 |
| TICK-099 | RPT-04 diminution rendering | done | feature | — | none | zero-diff record | `n/a` | all + proof | 13/13 | 0 |
| TICK-205 | Audit needs no dual specification | done | feature | — | none | zero-diff record | `n/a` | all + proof | 16/16 | 0 |
| TICK-212 | Report-renderer package locks | done | feature | — | #415 (SIMPLI-014's) | zero-diff record | `n/a` | all + proof | 12/12 | 0 |
| TICK-207 | Audit reuses Inspection template | done | feature | — | none | zero-diff record | `n/a` | all + proof | 13/13 | 0 |
| TICK-211 | Renderer analyzer strictness | done | feature | — | none | zero-diff record | *absent* | all + proof | 11/16 | 0 |
| TICK-203 | Renderer MCP design reconciliation | done | feature | — | #415 (SIMPLI-014's) | zero-diff record | `n/a` | all + proof | 12/12 | 0 |
| TICK-043 | MAIL-01 mailbox identity | verifying | feature | `task/tick-043-mailbox-identity` / `../pegasus-worktrees/tick-043-mailbox-identity` / codex-mcp-client | #414 | yes `33f00220` | *absent* | all + PIR | 10/10 | 0 |
| SIMPLI-014 | Integrate CollisionRenderer | done | feature | — | #415 | yes `b548b674` | *absent* | all + proof | 18/24 | 0 |
| TICK-215 | Where rendering executes | done | feature | — | #413 (DOCS-002's) | zero-diff record | `n/a` | all + proof | 12/12 | 0 |
| TICK-204 | Assessment-report outcome variants | done | feature | — | #412 | yes `314a9b26` | `n/a` | all + proof | 11/11 | 0 |
| TICK-010 | MAIL-22 detailed taxonomy | done | feature | — | #392 | **in production** (rel. 9) | `production` | all + proof | 8/8 | 0 |
| TICK-009 | MAIL-21 classification foundation | done | feature | — | #391 | **in production** (rel. 9) | `production` | all + proof | 12/12 | 0 |
| DOCS-002 | Web Container App as renderer host | done | chore | — | #413 | yes `4d1bff3d` | `n/a` | all + proof | 11/11 | 0 |
| DELIV-009 | Release 10 promotion | done | chore | — | #406, #407 | **is release 10** | `production` | plan/checklist/proof only | 10/10 | no doc |
| AUTO-002 | Auth-code + PKCE for MCP connectors | done | feature | — | #405 | **is release 10** (`d8de29cb`) | `production` | all + proof | 15/17 | no doc |
| TICK-011 | INT-17 automatic VRM reading | done | feature | — | none | already on `main` | `not-deployed` | all + proof | 10/10 | no doc |
| TICK-044 | MAIL-02 classification catalogue | verifying | feature | `task/tick-044-classification-catalogue` / `../pegasus-worktrees/tick-044-classification-catalogue` / codex-mcp-client | #411 | yes `dc77c29d` | *absent* | all + PIR | 12/18 | 0 |
| PLAT-006 | Centre shell, redesign Upload | verifying | fix | `task/plat-006-shell-upload` / `../pegasus-worktrees/plat-006-shell-upload` / **claude-code** | #409 | yes `feda958f` | *absent* | files/plan/checklist/PIR | 9/10 | no doc |
| TICK-033 | INT-31 upload reconciliation | verifying | feature | `task/tick-033-request-upload-reconciliation` / `../pegasus-worktrees/tick-033` / codex-mcp-client | #408 | yes `60fde326` | *absent* | all + PIR | 4/5 | no doc |

**Roster counts.** 29 tickets: **5 review** (all five open PRs), **6
verifying**, **18 done**. Of the done set, 5 are genuinely in production
(PLAT-001, TICK-009, TICK-010, DELIV-009, AUTO-002) and 7 are zero-diff
decision records that deploy nothing. **Every open-questions gate in the roster
is clean — zero unticked items above `## Parked` anywhere.** That gate is
therefore not what is holding anything back; the checklists and the PR review
findings are.

---

## 2. Per ticket with an open PR

### INTK-005 — PR #416, grouped upload

`intk-005-grouped-upload`, 1 commit `ed04f498` (10:28 UTC), +7247/−66 over 16
files, `MERGEABLE`/`UNSTABLE`, **1 ahead / 25 behind `dev`**, no human review.

**What it does.** Adds a Core grouped-submission boundary (`GroupedIntake.cs`)
wrapping the existing per-file `IIntakeSubmission`, invoked once per ordered
member with a child token `{token}:{ordinal}`; adds EF tables
`IntakeSubmissionGroups` / `IntakeSubmissionGroupMembers` with migration
`20260819101344_GroupedIntakeSubmission`; converts the Upload page to a file
collection and adds `/Upload/Group/{groupId}`. Every successful upload,
single-file included, now redirects to that new page.

**CI: RED.** `sql-integration (1)(2)(3)` all fail (run 32242883226); `unit`,
`browser`, `changes`, `documentation`, `reference-data` pass. Nine failures:
`IntakeWebNegativeTests` ×3, `InstructionDraftWebTests` ×2,
`QdosIntakeWebTests` ×2, and
`IntakePersistenceIntegrationTests.CommittedMigrationCreatesTheSqlServerSchema`.
The failure texts (`Expected: "abcdef…"`, `Expected start: "/Upload/Status/"`)
match findings 2 and 3 below exactly — CI is proving the review comments.

**Reviewer comments — 5, all `chatgpt-codex-connector[bot]`, 2026-08-19
10:35 UTC on `ed04f4982`, review state COMMENTED. No issue-level comments.
None addressed** — the branch has had no commit since 10:28 UTC, seven minutes
before the review.

1. P1 `Upload.cshtml:36` — raise `FormOptions.MultipartBodyLengthLimit`; the page advertises 10 MiB *per file* while the host caps the whole body at 10 MiB + 64 KiB. **Unaddressed.**
2. P1 `GroupedIntake.cs:128` — single-file uploads now post `token:0` instead of `token`, breaking receipt correlation. **Unaddressed; CI proves it.**
3. P2 `UploadGroupStatus.cshtml:12` — no `data-auto-refresh`, so statuses stay stale. **Unaddressed.**
4. P2 `EfIntakeSubmissionGroupStore.cs:122` — concurrent same-token insert races the unique `(GroupId, Ordinal)` index; should reuse `EfIntakeWorkStore.ReceiveWithRetryAsync`. **Unaddressed.**
5. P2 `Upload.cshtml.cs:129` — the "already received; no duplicate created" replay notice is lost. **Unaddressed.**

**Plan vs ticket, implementation vs plan.** The plan is proportionate and
covers the ticket. Two of its own steps were not executed: step 3 ("Include
only caller grants consistent with adjacent Azure SQL migrations") and step 8
("Run full `dotnet test`"). Checklist 7/33 — the entire 25-item planned
checklist is unticked.

**Simplification pass.** Present and dated 2026-08-19 with dispositions, but
not honest: it claims "no unapplied findings remain" while the full suite was
never green (the test host crashed after 61 tests) and the migration grants
were left open. The post-implementation report *does* disclose both gaps.

**Scope drift.** Low, except that routing *every* upload — including
single-file — through the group status page is a behaviour change to existing
flows that the ticket does not call for.

**Verified findings — INTK-005**

- **blocker** [verified] `src/Pegasus.Infrastructure/Persistence/Migrations/20260819101344_GroupedIntakeSubmission.cs` — creates both tables with **zero GRANT statements**, the same defect as TICK-093. No migration in this repo grants at `SCHEMA::` level, so per-object grants are the convention. *Remediation:* append to `Up`, following `20260819104953_MailClassificationCorrectionHistory.cs:96–106` (provider-guarded by `migrationBuilder.ActiveProvider.StartsWith("Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal)`): `GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeSubmissionGroups] TO [pegasus_web_runtime_role];` and the same for `[IntakeSubmissionGroupMembers]`; add `pegasus_worker_runtime_role` SELECT if the Worker reads membership (INTK-006 does). Test: apply against a SQL container as the web runtime role and read both tables, then `dotnet test --filter IntakePersistenceIntegrationTests`.
- **blocker** [verified] `src/Pegasus.Core/Intake/GroupedIntake.cs:~128` — ordinal-0 token rewrite. *Remediation:* use the parent `token` verbatim for ordinal 0 and `{token}:{n}` only for n ≥ 1. Test: `dotnet test tests/Pegasus.IntegrationTests --filter "InstructionDraftWebTests|IntakeWebNegativeTests|QdosIntakeWebTests"`.
- **blocker** [verified] `src/Pegasus.Web/Pages/Upload.cshtml.cs:~129` — unconditional redirect to `/Upload/Group/{id}` breaks `IntakeWebNegativeTests.cs:74` and `QdosIntakeWebTests.cs:39-40`. *Remediation:* redirect one-member groups to `/Upload/Status/{stagedReceiptId}`, satisfying the ticket's "one-member group" criterion without changing existing callers. Same test filter.
- **blocker** [verified] `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` — expected migration list omits `20260819101344_GroupedIntakeSubmission`. *Remediation:* add the id; merge `origin/dev` first, since `dev` has since added `20260819093019_RetainedMailboxInternetMessageIdentity` and `20260819104953_MailClassificationCorrectionHistory`.
- **should-fix** [verified] `src/Pegasus.Web/Program.cs:502-504` — `MultipartBodyLengthLimit` unchanged at 10 MiB + 64 KiB. *Remediation:* define a bounded batch policy (max N files × per-file limit + slack), set the limit from it, and validate the count in `Upload.cshtml.cs` with a named message.
- **should-fix** [verified] `UploadGroupStatus.cshtml` lacks `data-auto-refresh`; `site.js` refreshes only elements carrying it. *Remediation:* copy the attribute from `UploadStatus.cshtml`, gated on any member being non-terminal.
- **should-fix** [suspected, needs check] `EfIntakeSubmissionGroupStore.cs:118-122` — concurrent get-or-create race; needs a read of `EfIntakeWorkStore.ReceiveWithRetryAsync` to copy its retry shape.
- **nit** [verified] Duplicate-replay notice lost (comment 5).

### INTK-006 — PR #417, grouped image routing

`intk-006-grouped-image-routing`, 4 commits, **based on `ed04f498` (PR #416),
not on `dev`**, +7596/−71 over 22 files, **`CONFLICTING`/`DIRTY`**, 4 ahead /
25 behind `dev`, no human review.

**What it does.** Makes the *group* the routing unit: adds
`ImageIntakeGroupRouting.cs` (a Core decision table — WaitingForMembers,
WaitingForRecognition, AssociateExistingCase, HandOffToImageIntake,
TechnicalFailure), aggregates all member receipts in
`ImageIntakeAutomation.ApplyAsync` before association, and splits
`OnnxVrmRecognitionEngine`'s `NoReadableResult` into `detector_no_plate` vs
`recognizer_no_readable_text`. Commit `866d305e` partially reverts #416 by
restoring the single-file path through `IIntakeSubmission` directly.

**CI: the current head has never been tested.** `gh pr checks 417` reports **no
checks reported**. One run exists on the branch (`32244323472`, head
`70d7c89c`, failure, 8 integration failures inherited from #416). Commits
`866d305e` (11:17 UTC) and `599bfe6d` (11:20 UTC) — the entire claimed
remediation — have **no CI run at all**. The post-implementation report's
"Review remediation evidence" rests only on a local Release build plus five
targeted tests.

**Reviewer comments — 13 across two rounds, all Codex bot.**

*Round 1 — `70d7c89c`, 10:54 UTC (10 comments):*

1. P1 `ImageIntakeAutomation.cs:182` — `members.Count` used as both actual and expected count; a partially-persisted group can be treated as complete and permanently associated. **Not visibly addressed.**
2. P1 `Upload.cshtml:36` — aggregate multipart limit (as #416). **Not addressed** — no `Program.cs` limit change in the diff.
3. P1 `Upload.cshtml.cs:154` — group redirect breaks single-upload callers. **Addressed** by `866d305e`.
4. P1 `ImageIntakeGroupRouting.cs:80` — new durable behaviour table with **no FRD change**. **Not addressed** — `gh pr diff 417 --name-only | grep ^docs/` returns zero.
5. P1 `ImageIntakeAutomation.cs` (file-level) — a confident group with no eligible case exits before `TryRegisterAndAssociateAsync`, leaving it in `Needs sorting` with no Image Intake Reference, contra `frd-02:10`. **Possibly addressed** by `599bfe6d`; needs check.
6. P1 `ImageIntakeAutomation.cs:205` — swallowed persistence failure leaves a group split between associated and unassociated members. **Not visibly addressed.**
7. P2 `UploadGroupStatus.cshtml:14` — no auto-refresh. **Not addressed.**
8. P2 `ImageIntakeAutomation.cs:150` — non-image members (PDF/Word) fed to the image recognizer → `image_decode_failure` → whole group `TechnicalFailure`. **Not addressed.**
9. P2 `ImageIntakeAutomation.cs:150` — N² ONNX inference: each member re-runs recognition over the whole group. **Not addressed.**
10. P2 `GroupedIntake.cs:131` — replay duplicate feedback lost. **Not addressed.**

*Round 2 — `599bfe6d`, 11:26 UTC (3 comments, all post-remediation, all open):*

11. P1 `ImageIntakeAutomation.cs:200` — `HandOffToImageIntake` still calls `TryRegisterAndAssociateAsync`, which re-queries candidates and associates on a sole exact match, so a group the policy classified as *ambiguous* is auto-linked anyway.
12. P2 `Upload.cshtml.cs:104` — after `866d305e`, one-file posts use the bare token and multi-file posts use `{token}:{ordinal}`, so retrying the same token with a different file count creates extra receipts.
13. P2 `EfIntakeSubmissionGroupStore.cs:118` — unique-key race surfaces as a processing error.

**Plan vs ticket, implementation vs plan.** The plan's stated documentation
scope — amending `operator-notes.md`, the PRD, FRD-01/02/06/12,
`docs/design/README.md`, `docs/capabilities.md`, `docs/index.md`, `CONTEXT.md`,
and superseding ADR-0013 — is **entirely unimplemented** (zero docs files in
the diff), though later plan amendments narrow it and delegate to
INTK-007/INTK-008. Plan steps 5 (persisted idempotent group-outcome row keyed
by group id), 7 (Image-initiated Case branch) and 8 (status surface,
`OperatorLabels.cs` mapping) are absent. Checklist 26/41.

**Simplification pass.** Present, dated 2026-08-19, with honest dispositions —
and it explicitly says *"Do not merge this PR as the full INTK-006
acceptance."* The PR body repeats this. That honesty is real, but it means the
ticket sitting in `review` misrepresents completion.

**Scope drift.** Significant, in the *opposite* direction: the PR delivers
materially less than the ticket, and additionally reverts part of #416's
contract (single-file no longer flows through the group path, contradicting
INTK-005's "existing single-file upload remains supported as a one-member
group").

**Verified findings — INTK-006**

- **blocker** [verified] PR is `CONFLICTING` and based on an unmerged, red-CI branch; it duplicates #416's migration byte-for-byte, so whichever merges second conflicts. *Remediation:* fix and merge #416 first, then rebase this branch onto the merged result, dropping the duplicated `20260819101344_*` files.
- **blocker** [verified] No CI has ever run on head `599bfe6d`. *Remediation:* trigger checks; do not accept the "remediation evidence" claim until `sql-integration` is green.
- **blocker** [verified] Missing GRANT — the same migration file, present in this diff too. Fixing it once on #416 resolves both.
- **blocker** [verified] `ImageIntakeGroupRouting.cs` introduces a durable behaviour table with **no FRD change**; the repo makes FRD the owner of behaviour. *Remediation:* add the group-routing decision table (inputs, five states, precedence, fail-closed) to `docs/frd/frd-02-intake-and-source-identity.md` (or FRD-06 for vision diagnostics) and register the owner in `docs/capabilities.md`. Test: the `documentation` CI job.
- **blocker** [suspected, needs check] `ImageIntakeAutomation.cs:200` associates on `HandOffToImageIntake`, defeating the ambiguity fail-closed rule — a direct product-invariant violation. *Remediation:* branch on the decision in the member loop — a register-only path for `HandOffToImageIntake`, `TryRegisterAndAssociateAsync` only for `AssociateExistingCase`. Test: extend `ImageIntakeGroupRoutingPolicyTests` with a two-eligible-candidate fixture asserting no association.
- **should-fix** [suspected, needs check] `ImageIntakeAutomation.cs:182` derives the expected count from the persisted count. *Remediation:* persist the requested member count on `IntakeSubmissionGroups` (new column + migration), or seal the group before any member is routed.
- **should-fix** [verified] Mixed document/image groups (comment 8) and N² recognition (comment 9). *Remediation:* filter members through `ImageIntakeLifecycleRules.IsImageOnlyMaterial` before recognition; claim a group-level routing attempt so recognition runs once per image.
- **should-fix** [verified] Aggregate multipart limit, auto-refresh, replay-duplicate notice and cross-cardinality replay identity all remain open — same remediations as #416.

### INTK-007 — PR #424, Unidentified work

`intk-007-unidentified-intake`, 1 commit `abd8a923` (12:05 UTC), +8346/−48 over
49 files, **`CONFLICTING`/`DIRTY`**, 1 ahead / 42 behind `dev`, no human review.

**What it does.** Adds a Core-owned `UnidentifiedItem` aggregate with a `U<n>`
sequence allocator, a six-code reason taxonomy, Open/Resolved lifecycle with
append-only history, EF tables (`UnidentifiedItems`, `UnidentifiedSequences`,
`UnidentifiedHistory`) plus a backfill migration, Web queue/detail/resolution
pages, dashboard and nav exposure, and MCP list/get/resolve tools.

**CI: no checks reported on the branch at all.** Nothing has been built or
tested by CI. The PR body admits "the long IntegrationTests host did not emit a
final summary before lingering", and the checklist item "Run full
`dotnet test`" is unticked.

**Reviewer comments — 16 Codex inline (7×P1, 9×P2), 12:16:32 UTC. Zero
addressed** — the only commit predates the review by 11 minutes and nothing
followed. No issue-level comments. The consequential ones:

- P1 `ProcessIntake.cs:258` — below-threshold image-only receipts never reach Unidentified registration.
- P1 `ProcessIntake.cs:256` — every `TechnicalFailure`, including a transient reader exception on first attempt, burns an immutable U-reference. This contradicts the operator text the same PR writes ("Retryable processing remains in processing and does not allocate a U-reference").
- P1 `ProcessIntake.cs:268` — all `NeedsSorting` outcomes collapse to `NoUsableIdentification`; four of the six canonical reasons are unreachable.
- P1 `ProcessIntake.cs:243` — staff reevaluation to a non-Unidentified decision leaves a stale open U-item.
- P1 `Mail/Message.cshtml.cs:114` — maps legacy `NeedsSorting` states straight onto the `Unidentified` label, which Codex itself flags as breaking the settled-distinct-meanings invariant.
- P1 `EfUnidentifiedStore.cs:174` — resolution accepts any non-empty free-form `TargetId` and closes the item; no destination port consulted.
- P1 migration `:184` — backfilled rows get an all-zero fingerprint, so later re-registration of a migrated receipt is a hard conflict.

**Plan vs ticket, implementation vs plan.** The plan is thorough and matches the
ticket; the implementation does not match the plan. Checklist 22/36, with these
explicitly unticked: classify every normative old `Needs sorting` use; **"Add
migration … and required runtime grants"**; test migration from clean and
legacy databases; update the mail route/classification destination;
**"Preserve Triage, Blocked intake, incomplete Audit, Image Intake and
INTK-006 Image-Only semantics"**; update receipt/retained-mail/dashboard/
Operations/search projections; update navigation and status chips; all six
reason-mapping tests; the grouped-submission test; concurrency/replay tests;
search/count tests; the final stale-term audit; full `dotnet test`. The
post-implementation report is honest, calling the state "deliberately
compatible with the existing NeedsSorting storage code" — i.e. the wide
replacement the ticket demands is not done.

**Simplification pass.** Recorded, dated 2026-08-19, format-compliant, but the
disposition ("no behaviour-preserving simplification was identified") is thin
against 8k added lines and 16 unaddressed findings.

**Verified findings — INTK-007**

- **blocker** [verified] `docs/operator-notes.md` (+27 lines after line 70) — protected doc changed without recorded operator resolution, retiring `Needs sorting` against a product invariant. Full diff quoted in §4. *Remediation:* obtain and record verbatim operator confirmation in the ticket, or revert the hunk and move the vocabulary into `docs/prd/` + FRD-02 only. Do not proceed on the agent's own authority.
- **blocker** [verified] `src/Pegasus.Infrastructure/Persistence/Migrations/20260819115323_UnidentifiedWork.cs` — no runtime GRANT. *Remediation:* add a provider-guarded block copying `20260819104953_MailClassificationCorrectionHistory.cs:96–106`: `GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[UnidentifiedItems]`, `GRANT SELECT, UPDATE ON OBJECT::[dbo].[UnidentifiedSequences]`, `GRANT SELECT, INSERT ON OBJECT::[dbo].[UnidentifiedHistory]`, plus `DENY UPDATE, DELETE` on the history table, all `TO [pegasus_web_runtime_role]`. The ticket's own checklist names this and leaves it unticked.
- **blocker** [verified] `mergeable: CONFLICTING`. *Remediation:* merge `origin/dev` into the branch and resolve before anything else.
- **blocker** [verified] No CI checks have ever run and full `dotnet test` is unticked. *Remediation:* trigger the workflow; do not merge on the PR body's partial local run.
- **blocker** [verified] `src/Pegasus.Core/Intake/ProcessIntake.cs:256` — transient reader failures allocate an immutable U-reference on first attempt. *Remediation:* gate registration on the terminal / retry-exhausted branch only, matching the operator sentence. Add a Core test asserting a first-attempt recoverable exception produces no U-item.
- **should-fix** [verified] `ProcessIntake.cs:268` — reason-taxonomy collapse leaves `UnreadableOrCorruptContent`, `ConflictingIdentification` and `AmbiguousOwnershipOrDestination` dead. *Remediation:* thread the assessment result into the mapping; add the six mapping tests already listed unticked.
- **should-fix** [verified] `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs:114` — legacy `NeedsSorting` receipts relabelled `Unidentified` wholesale, the exact invariant collapse the ticket forbids. *Remediation:* show the Unidentified label only where a real U-item exists.
- **should-fix** [verified] `src/Pegasus.Infrastructure/Persistence/EfUnidentifiedStore.cs:174` — resolution validates only non-emptiness. *Remediation:* consult the destination port for `InstructionCase`/`ImageIntake`/`Triage`/`BlockedIntake` before closing.
- **should-fix** [verified] migration `:184` and `:175` — zero fingerprint blocks legitimate re-registration; backfill reads `DecisionReason` instead of `FailureReason`. *Remediation:* compute the same fingerprint the live path computes; `COALESCE(NULLIF(FailureReason,''), DecisionReason)`.
- **should-fix** [verified] 14 unticked checklist items while the ticket claims `review`. *Remediation:* finish them on this branch or split the residue into a follow-on ticket that blocks closeout.
- **nit** [verified] `src/Pegasus.Web/Mcp/UnidentifiedMcpTools.cs:52,55` — omitted `state` returns open+resolved and `Enum.TryParse` accepts `"999"`. Default to `Open`; reject non-named values.
- **nit** [verified] `src/Pegasus.Web/Pages/Unidentified/Details.cshtml:35` — raw enum and raw UTC rendered, bypassing `OperatorLabels.cs` and Europe/London conversion, which the plan's step 7 forbids.

### INTK-008 — PR #423, Image-initiated lifecycle

`intk-008-image-initiated-lifecycle`, 4 commits (last `855160b7`, 11:43 UTC),
+7301/−42 over 33 files, **`CONFLICTING`/`DIRTY`**, 4 ahead / 7 behind `dev`,
no human review. The ticket was moved to `review` **30 seconds** after its
first commit (commit 11:38:59 UTC → stage 11:39:29 UTC).

**What it does.** Adds `AwaitingInstruction` / `MergedIntoInstructionCase` /
`StaffClosed` states to the existing ImageIntake aggregate with an append-only
`ImageIntakeLifecycleEvents` table, CAS/operation-key transitions, a VRM-keyed
custody seam (`IImageIntakeCustody`), Web list/detail lifecycle UI with staff
closure, and ADR-0029 superseding ADR-0013.

**CI: RED.** `sql-integration (2)` failed (10m47s) with two failures:
`QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate`
and `ImageIntakeWebTests.StaffRegistersAnImageOnlyReceiptAndFindsItEverywhere`
(substring not found — almost certainly the terminology rename breaking an
expected string). All other jobs pass.

**Reviewer comments — 13 Codex inline (7×P1, 6×P2), 11:49:05 UTC. Zero
addressed** — last commit 11:43:30 UTC, ~6 minutes earlier, nothing since. No
issue-level comments.

- P1 `CustodyContracts.cs:41` — **`IImageIntakeCustody`/`CreateOrGetRootAsync` has no caller anywhere in the repo.** An interface plus adapters plus DI registration with no application caller is the "no abstraction without a second concrete caller" stop condition. The ticket's own checklist admits "VRM-keyed Box adapter invocation and custody state presentation still need final implementation/verification before PR" — left unticked, PR opened anyway.
- P1 `ImageIntakeCasePairing.cs:77` — `AutoLinkAsync` commits, then merge failure is swallowed; later pairing runs filter on `associated: false`, so the record is permanently stranded mid-transition.
- P1 `ImageIntakeCasePairing.cs:77` — the staff manual link in `Pages/Intake/Details.cshtml.cs` never invokes the merge transition, so staff-linked records stay `AwaitingInstruction` forever.
- P1 `EfImageIntakeStore.cs:264` — `MergeAsync`/`CloseAsync` bypass the new `ValidateMerge`/`ValidateClose` Core policy entirely; a >500-char reason fails at SQL instead. Infrastructure enforcing rules Core owns.
- P1 migration `:33` — every existing row backfilled to `awaiting_instruction` even when already linked via `IntakeManualAssociations`/`CaseIntakeLinks`, mislabelling live production data.
- P1 `docs/capabilities.md:215` — normative lifecycle behaviour inserted into the registry (which "never holds normative behaviour") **and it terminates the Markdown table between `INT-28` and `MAIL-01`**, structurally breaking the registry.
- P1 `docs/adr/README.md:30` — ADR-0013 set to `superseded` in frontmatter but its row left under "Current architecture decisions (`status: accepted`)".
- P1 `CONTEXT.md:148` — lifecycle requirements added to the terminology doc, creating a second normative owner and splitting the Interface vocabulary table.

**Plan vs ticket, implementation vs plan.** The plan traces the ticket well;
the implementation stops far short. **Checklist 8/29 — all 20 original
plan-derived items are unticked**, ticks appear only in an appended
"Implementation progress" block, and even that block carries an unticked
"VRM-keyed Box adapter invocation … still need final implementation/
verification before PR".

**Simplification pass: NOT RECORDED.** Plan step 11 and the checklist item
"Run simplification pass and record dispositions" both exist and are unticked;
the plan has no "Simplification pass" heading at all (INTK-007's does). That
violates the workflow requirement outright for a 7.3k-line code change.

**Verified findings — INTK-008**

- **blocker** [verified] `docs/operator-notes.md` line 79 — an existing operator sentence **rewritten**, not merely added to. Full diff quoted in §4. *Remediation:* revert the hunk and re-derive the terminology in PRD/FRD-02/FRD-06 only, or obtain recorded operator confirmation of this exact wording first.
- **blocker** [verified] CI `sql-integration (2)` — two failing integration tests. *Remediation:* fix `ImageIntakeWebTests.StaffRegistersAnImageOnlyReceiptAndFindsItEverywhere` (update the expectation only if the new operator label is authorised) and investigate `QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate`, an allocation-concurrency test that must not be dismissed as flaky without evidence.
- **blocker** [verified] `src/Pegasus.Core/Custody/CustodyContracts.cs:41` — `IImageIntakeCustody` has zero application callers; adapters and DI shipped dead. *Remediation:* either wire registration to invoke it with integration coverage, or remove the interface, both adapters and the DI registration from this PR and re-file as its own ticket.
- **blocker** [verified] `src/Pegasus.Infrastructure/Persistence/Migrations/20260819112914_ImageInitiatedLifecycle.cs` — no runtime GRANT for `ImageIntakeLifecycleEvents`. *Remediation:* add provider-guarded `GRANT SELECT, INSERT ON OBJECT::[dbo].[ImageIntakeLifecycleEvents] TO [pegasus_web_runtime_role];` plus `DENY UPDATE, DELETE`, following `20260819104953_MailClassificationCorrectionHistory.cs:96–106`.
- **blocker** [verified] No dated "Simplification pass" section exists in the plan. *Remediation:* run `/simplify` + `code-simplifier` over the branch diff, apply behaviour-preserving fixes, record findings and dispositions under a dated heading.
- **blocker** [verified] `mergeable: CONFLICTING`. *Remediation:* merge `origin/dev` and resolve.
- **should-fix** [verified] `EfImageIntakeStore.cs:264` — `MergeAsync`/`CloseAsync` skip the Core policy. *Remediation:* invoke `ValidateMerge`/`ValidateClose` before persisting; add a test for a 501-char closure reason returning a validation failure.
- **should-fix** [verified] `ImageIntakeCasePairing.cs:77`, two distinct bugs — (a) merge failure after a committed `AutoLinkAsync` is swallowed and unrecoverable because later runs query `associated: false`; make association+merge one transaction or add a reconciliation query for associated-but-not-merged rows. (b) the staff manual-link route in `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs` never triggers the merge transition; wire it to the same lifecycle command.
- **should-fix** [verified] migration `:33` — backfills all rows to `awaiting_instruction`. *Remediation:* set `merged_into_instruction_case` and populate merge-target columns for rows already joined through `IntakeManualAssociations`/`CaseIntakeLinks`.
- **should-fix** [verified] `docs/capabilities.md:215` — normative prose in the registry and the capability table structurally broken. *Remediation:* move the behaviour to FRD-01/FRD-06 and restore the table.
- **should-fix** [verified] `docs/adr/README.md:30` — ADR-0013 still under "Current architecture decisions". *Remediation:* move it to the superseded view with `Superseded-by: ADR-0029`.
- **should-fix** [verified] `CONTEXT.md:148` — second normative owner and a split vocabulary table. *Remediation:* remove the requirements section, leave terminology.
- **nit** [verified] `src/Pegasus.Web/Pages/ImageIntake/Index.cshtml.cs:78` — exact-reference search rebuilds `ImageIntakeSummary` with the old 7-arg constructor, defaulting every hit to `AwaitingInstruction`.
- **nit** [verified] `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml:38` — raw enum and snake_case event codes rendered directly; use the operator label map.
- **nit** [verified] `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml.cs:79` — `DbUpdateConcurrencyException` uncaught on stale close → HTTP 500 instead of a conflict notice.

### TICK-045 — PR #422, shared classification policy

`task/tick-045-shared-classification-policy`, 1 commit `139a4571` (11:33 UTC),
`MERGEABLE`/`CLEAN`, 1 ahead / 10 behind `dev`, no human review.

**What it does — and does not.** The PR changes exactly **two files**:
`docs/capabilities.md` (1 line) and
`tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` (+86).
**Zero production source lines.** It adds one integration test that seeds a
*fabricated* `MailClassificationResult` (policy key `"shared-mail-policy"`,
version `9` — a literal no policy in the repo emits) straight into the database
via `StoreClassifiedReceiptAsync`, then drives MAIL-04's
`CorrectRetainedMailClassification` over two rows differing only in a
`MailboxId` string. Neither `ProcessIntake` nor any
`IMailClassificationPolicy.Classify` implementation ever runs. It then upgrades
the MAIL-03 capability note from "Allocation only" to "Implemented locally
through the shared exact-message Core correction path."

**So: no. TICK-045 gives TICK-044/046 nothing, and the MAIL cluster is still
dark end to end.** I confirmed independently that
`MailOperationalDestinationPolicy` still has **zero non-test callers** on
`origin/dev` — only its own declaration at
`src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs:24`
and its test file. Note the real wiring that *does* exist and that this PR
ignores: `IMailClassificationPolicy` is genuinely live, consumed at
`src/Pegasus.Core/Intake/ProcessIntake.cs:13` and registered at
`src/Pegasus.Infrastructure/DependencyInjection.cs:129`
(`QdosMailClassificationPolicy`). A test proving "one shared policy across
mailboxes" had an obvious real path available and did not use it.

**CI: all green**, `mergeStateStatus: CLEAN` (run 32248224734; one
`sql-integration` shard was still pending at the time of the check and the rest
pass). Green CI here confirms only that a test which *cannot fail for the
claimed reason* does not fail.

**Reviewer comments — 2 Codex P1s, 11:38:19 UTC on `139a4571`, review state
COMMENTED. Both unaddressed** — the branch has one commit and PR `updatedAt`
equals the comment timestamp, so nothing was pushed after the review.

1. P1 "Exercise the classification policy instead of seeding its output" (`RetainedMailPersistenceTests.cs:345`) — only the MAIL-04 correction path is exercised; "mailbox-specific policy selection, registration, or divergence could be broken while this test remains green; the accompanying capability note therefore cannot claim that the shared classification policy is implemented from this evidence." **Verified correct.**
2. P1 "Use a documented supported mailbox in the acceptance test" (`:320`) — the estate is `desk`, `engineers`, `info`, `instructions` per `docs/operator-notes.md`; the test invents `claims@collisionengineers.co.uk` and fakes support by inserting a poll-state row. **Verified correct** — `claims` appears nowhere in operator-notes. This also trips the repository rule "Never fabricate domain emails."

**Plan vs ticket, implementation vs plan.** The plan's ordered implementation
(steps 2–4: add the smallest Core contract/policy; implement the Infrastructure
projection/transaction/adapter; wire the real caller through the Core use case)
was **not executed**. The plan was retroactively reconciled by an appended
"Evidence correction" section recasting the ticket as evidence-only. The ticket
body's own Verification block still carries two unticked items, and the ticket
still carries labels `blocked`, `post-alpha`, `requires-live-approval`.

**Checklist 12/12 is not earned.** At least three ticked items are false as
written: "Confirmed the real message-detail caller is wired through that Core
use case" (nothing was wired); "Added focused acceptance tests for
cross-mailbox invariance, ambiguity and unsupported/stale message failures"
(ambiguity is seeded as input, never tested as a fail-closed outcome); "Proved
the shared policy against two distinct mailbox identities" (it proved a
per-message-Id correction command over two rows — `CorrectRetainedMailClassification`
takes a message Id, not a mailbox, so cross-mailbox invariance is true by
construction and the test cannot fail for the reason claimed).

**Simplification pass.** Recorded and dated, all four lenses, and accurate
about the diff ("adds no production policy, port, store, or caller") — but it
treats that absence as a virtue rather than as the ticket's unmet objective,
and the two open P1s are not named or dispositioned anywhere.

**Scope drift.** Inverted — scope *collapse*, from "deliver one shared
classification policy" to "add one integration test and upgrade the registry
note". Against EPIC-006's context ("UI, Core policy, infrastructure adapters
and Automation Actor callers must reuse one canonical business
implementation"), no caller was unified.

**Deployment risk.** Mechanically low — no migrations, no GRANTs, no config, no
DI or Worker changes; this PR carries none of TICK-093's risk class. The risk
is evidence integrity.

**Verified findings — TICK-045**

- **blocker** [verified] `docs/capabilities.md:212` (MAIL-03 row) — the note would claim "Implemented locally through the shared exact-message Core correction path" on evidence that never invokes a classification policy. I confirmed the row on `dev` still reads "Allocation only; owning evidence still required." *Remediation:* revert the MAIL-03 note, or restrict it to exactly what was proved — that the *correction* path is mailbox-independent — with no claim about classification-policy implementation. Do not merge the current wording.
- **blocker** [verified] `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs:~322-338` — fabricates `MailClassificationResult.Ambiguous(..., "shared-mail-policy", 9)` and inserts it, bypassing classification. *Remediation:* drive both mailbox messages through the real registered path — resolve `IEnumerable<IMailClassificationPolicy>` (or `ProcessIntake`) from the DI scope the test already creates at `database.CreateAsyncScope()`, classify each retained message, and assert `PolicyKey`/`PolicyVersion` come from the registered policy rather than a literal. `src/Pegasus.Infrastructure/DependencyInjection.cs:129` registers `QdosMailClassificationPolicy`; `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs:33,71,175` shows the convention for substituting a policy, and `QdosAllocationRecoveryTests.cs:1497` (`ConsumerTypedClassificationPolicy`) is an existing test policy to reuse rather than write a new fake. Only then can the assertion be capable of failing.
- **blocker** [verified] `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs:316-317` — `const string secondMailboxId = "claims";` / `"claims@collisionengineers.co.uk"` is a fabricated domain address outside the documented estate. *Remediation:* replace with one of the four documented identities (e.g. `engineers`) and reach it through the real approval/configuration path rather than inserting a bare poll-state row via `SeedPollStateAsync`.
- **should-fix** [verified] TICK-045 checklist items 4, 5 and 11 assert work absent from the diff. *Remediation:* untick and restate them to match delivered scope, or complete the work. A 12/12 checklist on a diff containing no production code should not pass review.
- **should-fix** [verified] `docs/capabilities.md` MAIL-04 row (line 213) still reads "Allocation only; owning evidence still required", while this PR's body and report both rely on MAIL-04 having delivered the Core owner, transaction and Web caller. *Remediation:* update the MAIL-04 note in the ticket that owns it (TICK-046) before MAIL-03 cites it as a delivered prerequisite; do not let TICK-045 silently edit another capability's row.
- **should-fix** [verified] TICK-045 plan, "Simplification pass — 2026-08-19" — dispositions omit the two open P1 findings. *Remediation:* add them with explicit dispositions (fixed / rejected-with-reason / deferred-to-ticket).
- **nit** [verified] The stale-write assertion uses `retained[0].Id` only, so it proves the path for one mailbox despite a comment saying "must not affect either mailbox".

---

## 3. Per verifying / done ticket since the deploy

### TICK-093 — ENG-01 canonical repair specification (verifying, PR #420, `560f741c`)

Adds a versioned `CaseRepairSpecifications` aggregate with route provenance.
Merged to `dev` at 12:16 UTC today — **after** the release-10 deploy, so it is
not in production; the `deployment: not-deployed` field is accurate and no
proof.md exists yet to overclaim. **Entry point: none stated** — the store has
no caller.

- **blocker** [verified] `src/Pegasus.Infrastructure/Persistence/Migrations/20260819112640_VersionedRepairSpecifications.cs` — creates `CaseRepairSpecifications` (line 25) with **zero GRANT statements**, while `src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs:117-135` both reads (`context.CaseRepairSpecifications.AnyAsync`) and writes (`.Add(specification)`) that table from the Web runtime. I verified all three facts directly on `origin/dev`. The sibling migration merged 90 minutes earlier, `20260819104953_MailClassificationCorrectionHistory.cs:101-103`, *does* grant explicitly. **On the release-10 production database this fails with a permission error the moment a repair specification is drafted.** *Remediation:* add a provider-guarded block after the `CreateTable` call: `GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseRepairSpecifications] TO [pegasus_web_runtime_role];`, mirroring lines 96–106 of the MailClassificationCorrectionHistory migration exactly (including the `ActiveProvider.StartsWith("Microsoft.EntityFrameworkCore.SqlServer")` guard). Test: apply the bundle to a SQL container, connect as `pegasus_web_runtime_role`, and INSERT/SELECT the table; then run `dotnet test --filter IntakePersistenceIntegrationTests`.
- **should-fix** [verified] `IRepairSpecificationStore` (`src/Pegasus.Core/Assessment/RepairSpecifications.cs:210`) and `EfRepairSpecificationStore` (`src/Pegasus.Infrastructure/Persistence/EfRepairSpecificationStore.cs:12`) are **dark**: a repo-wide grep over `origin/dev` `src/` returns only the declaration and the implementation — no DI registration, no caller. That is the "no abstraction without a second concrete caller" stop condition, and it means release 12 would ship a store nothing can reach. *Remediation:* either register it in `src/Pegasus.Infrastructure/DependencyInjection.cs` and wire the real caller through the assessment use case, or remove both types from this release and re-file. Test: `git grep -n IRepairSpecificationStore -- src/` must show a registration and a call site.

**Blocked on release 12?** Yes for any deployment claim — but it must **not**
ship until the GRANT is fixed, or the deployment will break at first use.

### TICK-043 — MAIL-01 mailbox identity (verifying, PR #414, `33f00220`)

Adds durable inbound mailbox-item identity
(`20260819093019_RetainedMailboxInternetMessageIdentity`). Merged to `dev`
10:34 UTC today, after the deploy; not in production. Entry point: the retained
mail projection behind `/Inbox` — no new page. No proof.md yet, nothing
overclaimed.

**Blocked on release 12?** Yes — any production claim needs the deployment.

### TICK-044 — MAIL-02 classification catalogue (verifying, PR #411, `dc77c29d`)

Adds the canonical mail destination policy. Merged to `dev` 09:03 UTC today;
not in production. **Entry point: none — the policy is dark.**

- **blocker** [verified] `src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs:24` — a `public static class` with **zero non-test callers** repo-wide on `origin/dev` (verified by grep: only the declaration plus `tests/Pegasus.Core.Tests/Intake/Classification/MailOperationalDestinationPolicyTests.cs`). TICK-044's own open-questions records the operator instruction that *"A policy referenced only by tests is incomplete and must not pass review as delivered"*, and six checklist items about wiring it into the retained-mail projection and mailbox viewer remain unticked (12/18). TICK-045 was expected to supply the caller and does not (see §2). *Remediation:* wire `MailOperationalDestinationPolicy.Map` into the retained-mail read path — the projection behind `/Inbox` in `src/Pegasus.Web/Pages/Mail/` — so the operational destination is computed from the policy rather than derived ad hoc, and add an integration test asserting a classified message surfaces the policy's destination and `PolicyKey`/`PolicyVersion`. Alternatively defer the class out of release 12. Test: `git grep -n MailOperationalDestinationPolicy -- src/` must return a non-test caller.

**Blocked on release 12?** Yes for deployment, but the dark-policy finding is a
delivery-completeness problem the operator has already ruled on, independent of
the release.

### TICK-046 — MAIL-04 classification evidence and history (verifying, PR #418, `181fe331`)

Adds explainable classification evidence, policy version and correction history
(`20260819104953_MailClassificationCorrectionHistory`) — notably the *one*
migration in this batch that grants correctly, and which I used as the
convention reference throughout. Merged 11:23 UTC today; not in production.
Entry point: the classification correction path on the mail message detail
page.

- **should-fix** [verified] `docs/current-architecture.md:85` is now stale. It states: *"Both are read-only: the pages carry no handler, and the Web runtime role holds `SELECT` alone on the retained-mail tables."* TICK-046's own migration grants `SELECT, UPDATE ON OBJECT::[dbo].[IntakeMailClassificationDecisions]` and `SELECT, INSERT ON OBJECT::[dbo].[IntakeMailClassificationHistory]` to `pegasus_web_runtime_role` (lines 101–103), so Web no longer holds SELECT alone and the page does carry a correction handler. **The same claim is repeated at `docs/current-architecture.md:423`**: *"The Worker holds `SELECT, INSERT` on those tables and Web holds `SELECT` alone."* *Remediation:* update both lines to state that Web holds SELECT plus the correction grants on the classification decision and history tables, and that `/Inbox/{id}` carries a correction handler. Since the repository requires current-state docs to be refreshed in the same task as the deployment, fold this into the release-12 docs refresh. Test: `Test-DocumentationLinks.ps1` plus a read of both lines against the migration.

**Blocked on release 12?** Yes for deployment; the doc fix should ride with it.

### PLAT-006 — centre the shell, redesign Upload (verifying, PR #409, `feda958f`)

Real and verified: 6 files, +215/−13, matching PR metadata. Spot-checks confirm
`site.css` (`.app-rail-main { margin-inline: auto; }` plus new dropzone /
`.upload-layout` rules) and `Upload.cshtml` (dropzone markup with
`data-dropzone`, new "What happens next" / "Accepted files" panel) match the
plan verbatim. No proof.md exists yet, so nothing is overclaimed; the
post-implementation report correctly frames production checks as future work.
**Entry point: `/Upload` (and the public `/Uploads/{token}` request page)** —
the most visible user-facing change in this whole batch.

- **nit** [verified] PLAT-006 checklist item 10 ("PR to dev, review, merge") is unticked although PR #409 is merged (`feda958f`, 08:08 UTC). *Remediation:* tick item 10 via `set_ticket_doc`, citing the merge commit. Bookkeeping only.
- **should-fix** [suspected, needs check] The post-implementation report flags that `/Cases/Create` without a `receiptId` returns HTTP 500 (`ArgumentException` from `LoadAsync`) rather than a designed status page, explicitly marked "not this ticket". *Remediation for a follow-up ticket:* guard `src/Pegasus.Web/Pages/Cases/Create.cshtml.cs` `LoadAsync` to redirect to a friendly error view when `receiptId` is absent; add a browser or integration test hitting `/Cases/Create` with no query string.

**Blocked on release 12?** Yes — its acceptance criteria require confirming the
centred gutters and two-column Upload layout in production.

### TICK-033 — INT-31 upload reconciliation (verifying, PR #408, `60fde326`)

Docs-only, 1 file, +1/−1 on `docs/capabilities.md`. Merged 2026-08-18 15:38
UTC. No proof.md; the report explicitly disclaims live activation and operator
acceptance. **Entry point: none new** — it corrects inventory wording about the
existing `/Uploads/{token}` caller.

- **should-fix** [verified] The checklist leaves "Run focused request-upload integration tests" unticked, and the report records that the local `CaseDetailsWebTests`/`DocumentCustodyDurabilityTests` run timed out with no verdict. *Remediation:* run `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~DocumentCustodyDurabilityTests"` on merged `dev`, or pull the CI run for PR #408, and record the result in proof.md.

**Blocked on release 12?** No — it makes no deployment claim; it is a
source-truth documentation correction, complete as merged.

### SIMPLI-014 — integrate CollisionRenderer (done, PR #415, `b548b674`)

116 files, +1153/−12399. Verified on `dev`:
`src/Pegasus.Core/Reports/AssessmentReportRendering.cs` exists with real
content (`TemplateVersion = "rendererref1-v1"`), and `workspaces/report-renderer/**`
is genuinely deleted. **proof.md does not overclaim** — it explicitly states it
"does not claim… Azure deployment, production Chromium health, or a live user
caller" and "No cloud or `main` write occurred". **Entry point: none live** —
the callable use case exists in the Core/Infrastructure/Web composition, but
automatic triggering and durable report identity are deferred to DOCS-001, and
Azure runtime proof to PLAT-007.

- **nit** [suspected, needs check] The report notes the legacy monolithic integration invocation "exceeded its documented ~12-minute baseline while silent and was stopped" — slow, not failing, and the sharded CI lanes passed. *Remediation:* confirm `.github/workflows/*.yml` uses the sharded lanes as the required checks rather than the monolithic local invocation. No code change implied.

**Blocked on release 12?** No — its proof claims only source/CI-tier evidence,
already true.

### PR-009 — preserve long report tails (done, PR #419, `4f67a83e`)

2 files, +50/−7. The diff confirms the one-line production fix exactly as
described: `PlaywrightAssessmentReportRenderer.cs` changes `new TemplateContext()`
to `new TemplateContext { LimitToString = 0 }`, fixing Scriban's default 1 MiB
`LimitToString` silently truncating composed HTML during the third embedded
photo. proof.md correctly disclaims Azure deployment, live caller activation
and durable report custody. **Entry point: none live** — an Infrastructure-layer
rendering fix behind the not-yet-live render use case. **No findings** — proof
and report are consistent, specific (byte counts, SHA-256 hashes) and match the
diff.

**Blocked on release 12?** No — source/CI-tier evidence, already true.

### TICK-213 — report density subsumption (done, PR #421, `4ba63888`)

Records normal-density report stress evidence. `deployment: n/a`. Merged to
`dev` only. Entry point: none stated.

### TICK-204 — assessment-report outcome variants (done, PR #412, `314a9b26`)

Defines the missing outcome variants; `deployment: n/a`; merged to `dev` only.
Entry point: none stated (report content definitions).

### DOCS-002 — Web Container App as renderer host (done, PR #413, `4d1bff3d`)

Genuinely docs-only: +85/−0 across exactly
`docs/adr/0028-run-integrated-renderer-in-web-container-app.md` and
`docs/adr/README.md`. `deployment: n/a` accurate — no runtime code hid behind
it. **Doc changed: ADR-0028.** The ADR is on `dev` only, not yet in production
docs.

### DELIV-009 — release 10 promotion (done, PRs #406/#407, `d8de29cb`)

**This is release 10 itself** — commits include `d8de29cb`,
`deployment: production` is accurate, and production still serves it. Entry
point: the whole release. Its proof.md is the successful deployment.

**Blocked on release 12?** No — already true in production.

### AUTO-002 — authorization-code + PKCE for MCP connectors (done, PR #405, `d8de29cb`)

Merged at 13:52 UTC on 2026-08-18 and **is** the release-10 head commit, so
`deployment: production` is accurate. Entry point: the Automation MCP connector
authorization endpoints (`AutomationMcp__RedirectUris` →
`https://claude.ai/api/mcp/auth_callback`), live in production per the estate
census. Checklist 15/17.

**Blocked on release 12?** No — already true in production.

### TICK-010, TICK-009 — MAIL-22 / MAIL-21 (done, PRs #392/#391, production)

Both shipped in release 9, before release 10; `deployment: production` accurate
for both. Caveat worth recording: PR #392 changed exactly one file
(`tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs`, +154/−0)
and PR #391 changed `docs/capabilities.md`, `docs/operations.md` and
`tests/Pegasus.IntegrationTests/QdosEmailCohortTests.cs` — both added
*evidence*, not the capability code, which was already on `main`. TICK-010's
body saying the taxonomy "shipped via PR #392" overstates that PR, though the
deployment claim itself is sound. Not release-blocked.

### TICK-011 — INT-17 automatic VRM reading (done, `deployment: not-deployed`)

A retrospective reconciliation: INT-17 was already on merged `main` via
`ae6f0c2d`, `ef3eb4c7`, `f7d99b18`; the ticket created no diff and no PR.
proof.md records a focused run on `origin/main` at `d8de29cb` (ImageIntake Core
suite 78/78) and **explicitly declines to claim production caller execution** —
which is exactly why `not-deployed` is recorded despite `done`. That is
accurate in the strict sense the repository uses, even though the code sits
inside the release-10 binary. Entry point: none stated. **Not release-blocked**
— it needs live caller evidence, not a promotion.

### PLAT-001 — Claude Design UI (done, PR #397, in production)

21 screens in `Pegasus.Web` with the left-rail shell and 10 commissioned marks;
+1679/−560 across 42 files. I verified `git merge-base --is-ancestor 5ab3b773
d8de29cb` — **PR #397 is in release 10 and is live in production.** The
open-questions gate was formally satisfied (0 unticked above `## Parked`), and
the four 2026-08-19 operator resolutions each hand ownership to a real backlog
ticket (ENG-001, CASE-002, CASE-004, PLAT-008). Of the 8 unchecked checklist
items, 5 are superseded duplicates re-ticked in a second closeout block and the
a11y suite was later ticked ("32 passed, 0 failed"); the only genuinely
outstanding item is **visual screenshot proof from a local `DevelopmentOffline`
run** — cosmetic evidence, covered functionally by the 32-test Playwright suite.

- **should-fix** [verified] PLAT-001 frontmatter has **no `deployment` field** despite shipping UI demonstrably serving in production. The largest user-visible change in this batch is invisible to any deployment-state board query. *Remediation:* set `deployment: production` on PLAT-001 with release 10 / `d8de29cb` as the evidence.

### The zero-diff decision records — TICK-099, TICK-205, TICK-207, TICK-211, TICK-212, TICK-203, TICK-215

Seven honest decision/reconciliation records. **None changed runtime code while
claiming `n/a`** — the failure mode worth watching for did not occur. Each was
confirmed to have an empty `git diff --name-only origin/dev...HEAD`, except
TICK-215, whose recorded PR #413 is genuinely docs-only (ADR-0028, delivered by
DOCS-002). Entry points: none stated; TICK-215's doc is ADR-0028. Not
release-blocked.

Three small bookkeeping findings:

- **nit** [verified] TICK-211 frontmatter omits the `deployment` key entirely, unlike its six siblings which record `n/a`, so a board query for undeployed work cannot classify it. *Remediation:* set `deployment: n/a`.
- **nit** [verified] TICK-212 and TICK-203 frontmatter attributes PR #415 / commit `b548b674` (SIMPLI-014's large runtime change) to themselves while their own Outcome text says they produced no commit or PR. *Remediation:* keep the PR link in the Outcome prose as a subsumption reference and clear the `prs`/`commits` frontmatter, or add `deployment: not-deployed` so attributed code is not read as shipped.
- **nit** [verified] TICK-205's proof.md still argues the *pre-correction* premise ("RPT-03 intentionally preserves conservative and maximised specifications, records uplift") while the ticket body says that premise was false and superseded. *Remediation:* refresh the proof to state the superseding correction.

---

## 4. Open questions and contradictions for the operator

Every formal open-questions gate on the board is clean. These are the decisions
that only a human can make.

### 4.1 A protected document was edited without recorded operator resolution — twice

`docs/operator-notes.md` is "the binding business truth… Protected: stop for
user resolution before changing its meaning." Two open PRs change it. **This is
the item to decide before any remediation work is worth starting.**

**PR #424 (INTK-007)** adds a new section after line 70, whose key sentence is:

> "Unidentified replaces the old broad `Needs sorting` destination for this meaning; it does not rename or collapse Triage, Blocked intake, incomplete Audit evidence, or Image Intake."

The section further defines `U1, U2, U3…` immutable references, six reasons,
and resolution rules. INTK-007's **own research document concedes** the
problem: *"`docs/operator-notes.md` says 'Needs sorting' refers to unmatched
e-mail and also uses it for Triage material missing a registration. This
protected meaning must be reconciled, not silently overwritten."* The ticket
body says to update the protected documentation *"with explicit operator
confirmation before implementation"*. **No such confirmation exists anywhere in
the ticket folder.** The plan step simply instructs the agent to use "the
explicit operator instruction recorded in this ticket" — which is not recorded.

This also collides with the product invariant: "`Audit`, `Triage`, `Needs
sorting`, and `Blocked intake` retain their settled distinct meanings."
`Needs sorting` currently appears at `docs/operator-notes.md:42`, `:199` and
`:388`.

**PR #423 (INTK-008)** is the more serious of the two because it **rewrites an
existing operator sentence** rather than adding to the document:

> **removed:** "An image-only arrival **may be described operationally as** an 'image-initiated case'… Images alone **must not create a definitive association**."
> **added:** "An image-only arrival **is** an Image-initiated Case projection… Images alone **do not create a formal Case/PO association**."

It also adds a dated heading, "## Image-initiated Case clarification —
2026-08-19", which reads as a new operator statement made on that date. A
hedged, permissive operator sentence has become a definitional one, and a
prohibition ("must not") has been softened to a description ("do not").

**Question for the operator:** did you authorise either wording? **My
recommendation:** treat both as unauthorised until confirmed. For each PR,
either (a) confirm the exact wording verbatim and record that confirmation in
the ticket's open-questions before merge, or (b) drop the `operator-notes.md`
hunks entirely and let the new vocabulary live in the PRD and FRD-02/FRD-06,
which is where behaviour belongs anyway. Option (b) unblocks the code review
immediately and is what I would do. The fabricated dated "clarification"
heading in #423 should be removed regardless — a dated operator statement that
the operator did not make is the most corrosive of these edits.

### 4.2 Ten of eleven taken tickets belong to other agents — leave them alone

`take_ticket` records are claims. Only **PLAT-006** is claimed by `claude-code`
(this lane's own machine). The other ten must be left alone or coordinated
before any remediation touches their branches or worktrees:

| ticket | assignee | worktree | branch |
|---|---|---|---|
| TICK-093 | `codex-mcp-client` | `../pegasus-worktrees/tick-093-versioned-repair-spec` | `task/tick-093-versioned-repair-spec` |
| TICK-046 | `codex-mcp-client` | `../pegasus-worktrees/tick-046-classification-history` | `task/tick-046-classification-history` |
| TICK-045 | `Codex / execute_tick_045` | `../pegasus-worktrees/tick-045-shared-classification-policy` | `task/tick-045-shared-classification-policy` |
| TICK-044 | `codex-mcp-client` | `../pegasus-worktrees/tick-044-classification-catalogue` | `task/tick-044-classification-catalogue` |
| TICK-043 | `codex-mcp-client` | `../pegasus-worktrees/tick-043-mailbox-identity` | `task/tick-043-mailbox-identity` |
| TICK-033 | `codex-mcp-client` | `../pegasus-worktrees/tick-033` | `task/tick-033-request-upload-reconciliation` |
| INTK-005 | `Codex` | `.worktrees/intk-005` | `intk-005-grouped-upload` |
| INTK-006 | `Codex` | `.worktrees/intk-006` | `intk-006-grouped-image-routing` |
| INTK-007 | `Codex` | `.worktrees/intk-007` | `intk-007-unidentified-intake` |
| INTK-008 | `Codex` | `.worktrees/intk-008` | `intk-008-image-initiated-lifecycle` |

**Question:** are the Codex lanes still active, and who fixes the INTK PRs? Every
one of the four INTK PRs needs substantial rework (missing GRANTs, unaddressed
P1s, conflicts, red or absent CI). **Recommendation:** ask the operator to
confirm the Codex lanes are idle before DELIV-012 takes any of these branches;
otherwise two agents will fight over the same worktree. If Codex is idle, the
cleanest route is to fix #416 first (it unblocks #417's duplicated migration),
and to treat #422 as a documentation-only revert rather than a rework.

Two related git-hygiene facts, both squarely in DELIV-012's scope: the four
INTK worktrees sit **inside** `.worktrees/` with branches lacking the `task/`
prefix, against the repository convention (`../pegasus-worktrees/<slug>` on
`task/<slug>`); and a stale worktree `../pegasus-worktrees/deliv-011-release-11`
on `task/deliv-011-release-11` remains from the held release 11, whose branch
was never pushed. There are **15 remote branches** against a target of three.

### 4.3 No ticket claims production without evidence — one claims *less* than it should

I checked every `deployment: production` claim in the roster. All five are
sound: DELIV-009 and AUTO-002 *are* release 10 (`d8de29cb`); TICK-009 and
TICK-010 shipped in release 9; PLAT-001's PR #397 is verifiably an ancestor of
`d8de29cb`. **No overclaim was found anywhere in the roster** — the failure
mode the operator was worried about did not occur.

The inverse did occur: **PLAT-001 has no `deployment` field at all** despite
being live in production, and **TICK-211** has none despite being a zero-diff
record. Both are one-line board corrections (§3).

**Question:** should TICK-011 stay `not-deployed`? Its code is physically
inside the release-10 binary, but proof.md deliberately declines to claim
production caller execution. **Recommendation:** leave it as-is. The strict
reading — deployment means proven caller activation, not binary presence — is
the more useful one and matches how the rest of the board is written.

### 4.4 The MAIL cluster is dark end to end, and a capability row is about to say otherwise

TICK-044's policy has no caller; TICK-093's store has no caller; TICK-045 —
which the cluster expected to supply the caller — ships no production code at
all yet upgrades the MAIL-03 capability row to "Implemented". TICK-044's own
open-questions already records the operator's rule: *"A policy referenced only
by tests is incomplete and must not pass review as delivered."*

**Question:** should release 12 ship the dark MAIL/ENG code as-is, or should
these be wired or deferred first? **Recommendation:** ship the *code* (it is
inert and already merged) but (a) fix the TICK-093 GRANT before deploying, and
(b) refuse the MAIL-03 capability-row change in #422. A registry that says
"Implemented" for a policy nothing calls is the one artefact here that will
mislead a future reader.

### 4.5 A standing production fault, inherited from the estate research

Not a ticket finding, but it bears on release-12 verification:
`SentEvidencePollFunction` is enabled while `ApprovedMailboxes.AllowSentEvidence
= 0`, producing one `UnauthorizedAccessException` per minute (~1,440/day).
`docs/runbook.md` says that function stays disabled unless separately approved;
`scripts/Invoke-ProductionSmoke.ps1` asserts all nine functions enabled. **Any
"zero exceptions" assertion after release 12 must account for this**, and the
contradiction between runbook and smoke script needs an operator decision.

---

## 5. Implications for release 12

**Must be fixed before the deployment (hard blockers).**

1. **TICK-093's missing GRANT** — already merged to `dev` and therefore *in* release 12 by default. Deploying it as-is puts a permission failure into production the moment a repair specification is drafted. This is the single most important remediation in this document, and it is a ~6-line migration change. Because the migration is already merged, the fix must be a **new follow-up migration** (or an amendment before the `efbundle` is built) — do not edit an applied migration in place if the bundle has already run anywhere.
2. **The `docs/current-architecture.md:85` and `:423` staleness** (TICK-046) must be corrected in the same task as the deployment, per the safety rail that current-state docs are refreshed before the release merges.
3. **PLAT-001's missing `deployment: production`** and TICK-211's missing `deployment: n/a` — one-line board corrections, worth doing before the release so the post-release board is truthful.

**Proof depends on this deployment (blocked until release 12 ships).**

- **PLAT-006** — the centred shell and redesigned Upload page are the headline user-visible change; its acceptance requires a browser check on production.
- **TICK-043, TICK-044, TICK-046, TICK-093** — all four merged after the deploy; none can claim production until release 12. TICK-044 and TICK-093 additionally need their dark code wired or deferred before the claim is meaningful.
- **SIMPLI-014, PR-009, DOCS-002, TICK-204, TICK-213** — merged to `dev`, will ship with release 12, though their current proofs correctly claim only source/CI evidence and are already true at that tier.

**Can move to done once deployed** (given the fixes above): PLAT-006, TICK-043,
TICK-046. **TICK-033** can move now — it makes no deployment claim; it needs
only the focused integration-test verdict recorded.

**Needs remediation first, and should not ride release 12 unfixed:**
TICK-093 (GRANT + dark store), TICK-044 (dark policy).

**Not in release 12 — the five open PRs.** None is mergeable today:

| PR | ticket | CI | mergeable | blocking issues |
|---|---|---|---|---|
| #416 | INTK-005 | **red** (3 shards) | MERGEABLE | missing GRANT; 5 unaddressed comments; 2 P1s proven by CI |
| #417 | INTK-006 | **never run on head** | **CONFLICTING** | based on unmerged #416; duplicate migration; 12 of 13 comments open; no FRD |
| #422 | TICK-045 | green | CLEAN | zero production code; false capability claim; fabricated mailbox; 2 P1s open |
| #423 | INTK-008 | **red** (1 shard) | **CONFLICTING** | protected doc rewritten; missing GRANT; dead interface; **no simplification pass**; 13 comments open |
| #424 | INTK-007 | **no checks at all** | **CONFLICTING** | protected doc edited; missing GRANT; 16 comments open; 14 checklist items unstarted |

**Recommended sequencing.** Deploy release 12 from the current `dev` **after**
fixing the TICK-093 GRANT and refreshing the two stale architecture lines —
that carries twelve merged task PRs including the visible Upload/shell
redesign, and leaves the five troubled PRs out of it. Then resolve the
operator-notes authority question (§4.1), then work the open PRs in the order
#416 → #417 (it depends on #416) → #423 → #424, with #422 handled separately as
a documentation revert. Three of the five open PRs repeat the same missing-GRANT
defect as TICK-093, so a single convention fix — and ideally a CI check that
fails any `CreateTable` migration without a matching `GRANT … TO
[pegasus_web_runtime_role]` — would prevent the whole class recurring.

**Suggested new CI guard** [suspected, needs check]: a script in `scripts/`
invoked by the `changes` job that greps each new file under
`src/Pegasus.Infrastructure/Persistence/Migrations/` for `CreateTable(` and
fails unless the same file contains `GRANT` and the SQL-Server provider guard.
Four separate migrations in one day missed this; a convention that is documented
only by example is not holding.
