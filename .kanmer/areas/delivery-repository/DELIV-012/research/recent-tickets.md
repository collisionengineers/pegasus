# Recent tickets since the last production deployment

Research for **DELIV-012** (Release 12), compiled 2026-08-19 from the Kanmer board, `gh`
PR data, and read-only reads of `origin/dev`. Companion to `research/current-estate.md`
(Azure) and `research/codebase-evidence.md` (diff/CI).

## Anchors

| Fact | Value |
| --- | --- |
| Production release | **10**, source `d8de29cb94f396816595b1f9782980476166dbfa`, deployed 2026-08-18T13:52Z |
| Release 11 | `feda958f` (PR #409) — prepared, **held by the operator, never deployed**. No ACR tag, no revision, no ARM deployment. |
| `origin/main` | `d8de29cb` — equals the deployed `sourceSha`. No gap between `main` and production. |
| `origin/dev` | `560f741c` (PR #420, 2026-08-19T12:16Z) — 42 commits / **12 PR merges** ahead of `main` |
| Open PRs → `dev` | #416 INTK-005, #417 INTK-006, #422 TICK-045, #423 INTK-008, #424 INTK-007 |
| Open PR → `main` | #410, the dev→main release vehicle (`CLEAN`/`MERGEABLE`) |
| Pending migrations | 3 — `20260819093019_RetainedMailboxInternetMessageIdentity`, `20260819104953_MailClassificationCorrectionHistory`, `20260819112640_VersionedRepairSpecifications` |

**Everything merged to `dev` after 2026-08-18T13:52Z is undeployed.** Because release 11 was
held there is no intermediate release: the whole 12-merge backlog lands in release 12.

The 12 merges since the deploy, oldest first: #407 (DELIV-009 docs), #408 (TICK-033),
#409 (PLAT-006), #411 (TICK-044), #412 (TICK-204), #413 (DOCS-002), #415 (SIMPLI-014),
#414 (TICK-043), #419 (PR-009), #418 (TICK-046), #421 (TICK-213), #420 (TICK-093).

---

## 1. Roster since the last deploy

Every non-archived ticket with `updated >= 2026-08-18T13:52Z` in **review**, **verifying**
or **done**. The raw `updated_since` query returns 68 rows, but 39 of those were touched
only by a board-wide `order` renumber at 2026-08-19T09:39:15Z (one bulk operation by
`codex-mcp-client`) and had no substantive change in the window. Filtering those out
against `get_activity since: 2026-08-18T13:52:00Z` leaves **29 tickets with real activity**.

**Counts: 5 review · 6 verifying · 18 done.**

| id | title | status | profile | taken (branch / worktree / assignee) | PR | merged to dev? | deployment | docs | checklist | unresolved OQ |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| INTK-005 | Upload submission accepts/tracks multiple files | review | feature | `intk-005-grouped-upload` / `.worktrees/intk-005` / **Codex** | #416 | no — CI **red** | *(unset)* | r,f,p,c,oq,pir,s | 7/33 | none |
| INTK-006 | Associate each vehicle-image group / create Image intake | review | fix | `intk-006-grouped-image-routing` / `.worktrees/intk-006` / **Codex** | #417 | no — **no CI**, CONFLICTING | *(unset)* | r,f,p,c,oq,pir,s | 26/41 | none |
| INTK-007 | Replace `Needs sorting` with referenced Unidentified work | review | feature | `intk-007-unidentified-intake` / `.worktrees/intk-007` / **Codex** | #424 | no — **no CI**, CONFLICTING | *(unset)* | r,f,p,c,oq,pir,s | 22/36 | none |
| INTK-008 | Give ImageIntake an Image-initiated Case lifecycle | review | feature | `intk-008-image-initiated-lifecycle` / `.worktrees/intk-008` / **Codex** | #423 | no — CI **red**, CONFLICTING | *(unset)* | r,f,p,c,oq,pir,s | 8/29 | none |
| TICK-045 | MAIL-03 — one shared classification policy | review | feature | `task/tick-045-shared-classification-policy` / `../pegasus-worktrees/tick-045-…` / **Codex / execute_tick_045** | #422 | no — CI **green**, CLEAN | *(unset)* | r,f,p,c,oq,pir,s | 12/12 | none |
| TICK-033 | INT-31 — request-scoped upload link reconciliation | verifying | feature | `task/tick-033-request-upload-reconciliation` / `../pegasus-worktrees/tick-033` / codex-mcp-client | #408 | yes `60fde326` | *(unset)* | r,f,p,c,pir,s | 4/5 | *no OQ doc* |
| PLAT-006 | Centre the operator shell; redesign Upload | verifying | fix | `task/plat-006-shell-upload` / `../pegasus-worktrees/plat-006-shell-upload` / claude-code | #409 | yes `feda958f` | *(unset)* | f,p,c,pir,s | 9/10 | *no OQ doc* |
| TICK-043 | MAIL-01 — identify every inbound mailbox item | verifying | feature | `task/tick-043-mailbox-identity` / `../pegasus-worktrees/tick-043-mailbox-identity` / codex-mcp-client | #414 | yes `33f00220` | *(unset)* | r,f,p,c,oq,pir,s | 10/10 | none |
| TICK-044 | MAIL-02 — map every detailed email classification | verifying | feature | `task/tick-044-classification-catalogue` / `../pegasus-worktrees/tick-044-…` / codex-mcp-client | #411 | yes `dc77c29d` | *(unset)* | r,f,p,c,oq,pir,s | **12/18** | none in OQ; **6 unticked in checklist** |
| TICK-046 | MAIL-04 — explainable classification evidence | verifying | feature | `task/tick-046-classification-history` / `../pegasus-worktrees/tick-046-…` / codex-mcp-client | #418 | yes `181fe331` | *(unset)* | r,f,p,c,oq,pir,s | 10/10 | none |
| TICK-093 | ENG-01 — one canonical repair specification | verifying | feature | `task/tick-093-versioned-repair-spec` / `../pegasus-worktrees/tick-093-…` / codex-mcp-client | #420 | yes `560f741c` | `not-deployed` | r,f,p,c,oq,pir,s | 6/6 | none |
| SIMPLI-014 | Integrate CollisionRenderer behind a Core render contract | done | feature | *(released)* / codex-mcp-client | #415 | yes `b548b674` | *(unset)* | all + proof | 18/24 | none |
| PR-009 | Preserve post-work-list report sections under long content | done | fix | *(released)* / codex-mcp-client | #419 | yes `4f67a83e` | `n/a` | all + proof | 17/17 | none |
| TICK-213 | Decide whether density applies to all rendered documents | done | feature | *(released)* / codex-mcp-client | #421 | yes `4ba63888` | `n/a` | all + proof | 15/15 | none |
| TICK-204 | Define the missing assessment-report outcome variants | done | feature | *(released)* / codex-mcp-client | #412 | yes `314a9b26` | `n/a` | all + proof | 11/11 | none |
| DOCS-002 | Record the Web Container App as the renderer boundary (ADR-0028) | done | chore | *(released)* / codex-mcp-client | #413 | yes `4d1bff3d` | `n/a` | all + proof | 11/11 | none |
| TICK-203 | Reconcile renderer MCP design vs Automation Actor inventory | done | feature | *(released)* / codex-mcp-client | *(subsumed #415)* | zero-diff | `n/a` | all + proof | 12/12 | none |
| TICK-205 | Record that Audit needs no dual-specification/uplift model | done | feature | *(released)* / codex-mcp-client | *(zero-diff)* | zero-diff | `n/a` | all + proof | 16/16 | none |
| TICK-207 | Record Audit reuse of the Inspection report template | done | feature | *(released)* / codex-mcp-client | *(zero-diff)* | zero-diff | `n/a` | all + proof | 13/13 | none |
| TICK-211 | Decide report-renderer analyzer strictness | done | feature | *(released)* / codex-mcp-client | *(subsumed #415)* | zero-diff | *(unset)* | all + proof | 11/16 | none |
| TICK-212 | Add report-renderer package lock files | done | feature | *(released)* / codex-mcp-client | *(subsumed #415)* | zero-diff | `n/a` | all + proof | 12/12 | none |
| TICK-215 | Decide where report rendering executes in production | done | feature | *(released)* / codex-mcp-client | *(delivered by #413)* | zero-diff | `n/a` | all + proof | 12/12 | none |
| TICK-099 | RPT-04 — diminution rendering (deferred boundary) | done | feature | *(released)* / codex-mcp-client | *(zero-diff)* | zero-diff | `n/a` | all + proof | 13/13 | none |
| PLAT-001 | Claude Design UI implementation | done | feature | *(released)* / claude-code | #397 | yes `5ab3b773` — **in release 10** | *(unset)* | all + proof | 55/63 | none (all resolved) |
| AUTO-002 | Authorization-code + PKCE for external MCP connectors | done | feature | *(released)* / claude-code | #405 | yes — **is part of release 10** | `production` | r,f,p,c,pir,proof,s | 15/17 | *no OQ doc* |
| DELIV-009 | Release 10: promote dev to main and deploy | done | chore | *(released)* / claude-code | #406,#407 | yes — **is release 10** | `production` | p,c,proof | 10/10 | *no OQ doc* |
| TICK-009 | MAIL-21 — minimum shared Core classification foundation | done | feature | *(released)* / grok-shell-kanmer | #391 | yes — **in release 9** | `production` | all + proof | 12/12 | none |
| TICK-010 | MAIL-22 — user-confirmed Received/Sent categories | done | feature | *(released)* / grok-shell-kanmer | #392 | yes — **in release 9** | `production` | all + proof | 8/8 | none |
| TICK-011 | INT-17 — automatic VRM reading from vehicle images | done | feature | *(released)* / *(no assignee)* | none | code already in `main` | **`not-deployed`** ⚠ | r,f,p,c,pir,proof,s | 10/10 | **no OQ doc** ⚠ |

Docs key: r=research, f=files, p=plan, c=checklist, oq=open-questions,
pir=post-implementation-report, s=scratch.

### Roster observations

- **Every open-PR ticket is held by an agent other than `claude-code`** — four by `Codex`,
  one by `Codex / execute_tick_045`. See §4.
- **The single most reassuring finding of this review: no ticket merged after the cutoff
  falsely claims production.** Every proof/PIR that exists explicitly disclaims deployment.
  The four tickets carrying `deployment: production` (AUTO-002, DELIV-009, TICK-009,
  TICK-010) are all legitimately in release 9 or 10 — verified by
  `git merge-base --is-ancestor` against `d8de29cb`.
- Four of the five verifying tickets in the MAIL/ENG cluster have **no `proof` document at
  all** (`exists: false`). That is correct per the workflow — proof is written on merged
  `main` after the release — and is exactly why they cannot leave `verifying` until
  release 12 ships.
- Five verifying tickets leave `deployment` **unset** rather than `not-deployed`. Not a
  false claim, but not an honest positive record either.

---

## 2. Open PRs, in detail

### INTK-005 — PR #416, grouped upload

**What it does.** Adds a durable submission-group boundary around the existing per-file
`IIntakeSubmission`. Upload now binds a file collection, creates one
`IntakeSubmissionGroups` row plus ordered `IntakeSubmissionGroupMembers` rows (migration
`20260819101344_GroupedIntakeSubmission.cs`), and derives each member's replay token as
`{formToken}:{ordinal}`. A new `UploadGroupStatus` page lists each member's receipt status.

**Reviewer comments** — all `chatgpt-codex-connector[bot]`, all 2026-08-19T10:35:35–36Z on
the single commit `ed04f498`. **All five unaddressed** — the PR has exactly one commit and
none postdates the review.

| Comment | File | Addressed |
| --- | --- | --- |
| P1 Raise the request limit for multi-file batches | `Upload.cshtml:36` | no |
| P1 Preserve the submitted token for single-file occurrences | `GroupedIntake.cs:128` | no — **this is the CI failure** |
| P2 Keep the group status page refreshing | `UploadGroupStatus.cshtml:12` | no |
| P2 Retry concurrent group-member insertion | `EfIntakeSubmissionGroupStore.cs:122` | no |
| P2 Preserve duplicate feedback for exact replays | `Upload.cshtml.cs:129` | no |

**CI: red.** `sql-integration` shards 1–3 fail. Shard 1 (run 32242883226):
`InstructionDraftWebTests.SameManualUploadTokenReplaysOneReceiptDraftAndAssetSet` —
`Assert.Equal() Failure: Expected "77…7" / Actual "77…7:0"` at
`InstructionDraftWebTests.cs:36`. Every single-file upload is silently rewritten to a
`:0`-suffixed source-identity token, breaking existing replay/token correlation.

**Plan vs implementation.** Plan step 5 explicitly required "Existing single-file upload
remains supported as a one-member group" with unchanged token semantics. The
implementation unconditionally appends `:0`, contradicting both the plan and the ticket's
own verification criterion. **Implementation does not match plan.**

**Simplification pass.** Recorded in `plan`, dated 2026-08-19, with real dispositions
(reused `IIntakeSubmission`, one store, one use case, no second queue). Honest, but written
before the regression was caught — the PR was opened with red CI.

**Scope drift.** None.

**Deployment risk.** Migration `20260819101344_GroupedIntakeSubmission.cs` creates
`IntakeSubmissionGroups` and `IntakeSubmissionGroupMembers` with **no GRANT statement at
all**, while `UploadModel`/`UploadGroupStatusModel` (Web runtime) read and write them
directly. Contrast the same-day `20260819104953_MailClassificationCorrectionHistory.cs:101-105`,
which grants correctly.

**Verified findings**

| Sev | file:line | Remediation brief | Tag |
| --- | --- | --- | --- |
| blocker | `src/Pegasus.Core/Intake/GroupedIntake.cs:128` | Do not append `:{ordinal}` to the source-identity token when the submission has exactly one member; return the bare `ExternalReceiptToken` instead, so existing replay correlation is byte-identical to today. Gate on member count, not on `Upload.Length`, so a genuine one-file group and a legacy single upload produce the same token. Test: `dotnet test tests/Pegasus.IntegrationTests --filter "FullyQualifiedName~InstructionDraftWebTests.SameManualUploadTokenReplaysOneReceiptDraftAndAssetSet"` must pass, and the multi-file group tests must still see distinct per-member tokens. | [verified] |
| blocker | `src/Pegasus.Infrastructure/Persistence/Migrations/20260819101344_GroupedIntakeSubmission.cs` | Add a guarded SQL block at the end of `Up`, copying the shape of `20260819104953_MailClassificationCorrectionHistory.cs:95-105` (`IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL` + SqlServer provider check), emitting `GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeSubmissionGroups] TO [pegasus_web_runtime_role];` and the same for `[IntakeSubmissionGroupMembers]`. Add matching `REVOKE` in `Down`. Test with the new head-of-stream grant test proposed under TICK-093 finding B4. | [verified] |
| should-fix | `src/Pegasus.Web/Program.cs:504` | `MultipartBodyLengthLimit` is still 10 MiB + 64 KiB while the UI now invites multiple files, so a two-file batch of ordinary photographs is rejected at the pipeline before the page model sees it. Either raise the limit to `maxFiles × perFileLimit + headroom` or state the aggregate cap in the Upload copy and validate it client-side. Test: post a two-file form of 6 MiB each and assert a validation message rather than a 413. | [verified] |
| should-fix | `src/Pegasus.Infrastructure/Persistence/EfIntakeSubmissionGroupStore.cs:122` | Concurrent same-token inserts can violate the unique `(GroupId, Ordinal)` index. Catch `DbUpdateException` on the unique-index violation and reload the existing group rather than surfacing a 500, matching the replay-idempotency contract the rest of intake already honours. | [suspected, needs check] |
| nit | `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml:12` | Add `data-auto-refresh` (the convention already used by `UploadStatus.cshtml`) so pending member statuses update without a manual reload. | [verified] |

---

### INTK-006 — PR #417, grouped image routing

**What it does.** Builds on INTK-005's branch (base `ed04f498`) to make the upload group the
vehicle-recognition unit: waits for all members' terminal recognition, aggregates distinct
accepted VRMs, and either associates the whole group to one existing eligible Case or hands
off to the existing ImageIntake owner. Records distinct `detector_no_plate` vs
`recognizer_no_readable_text` diagnostics. Explicitly defers Image-initiated Case lifecycle
to INTK-008 and conflicting-VRM routing to INTK-007.

**Reviewer comments** — all `chatgpt-codex-connector[bot]`, in two rounds.

Round 1 (10:54:14Z, on `70d7c89c`) — 10 comments. Commits `866d305e` (11:17) and
`599bfe6d` (11:20) followed.

| Comment | Addressed |
| --- | --- |
| P1 Update single-upload callers for the group route | **yes** — HEAD restores an `Upload.Length == 1` branch using `submission.ExecuteAsync`/`/UploadStatus` with the bare token |
| P2 Preserve duplicate feedback for replayed groups | **yes** — fixed in the same change, `duplicate=received.IsDuplicate` preserved |
| P1 Register confident groups even without an eligible case | **yes** — `ImageIntakeGroupRoutingDecision.HandOffToImageIntake` is no longer in the early-return exclusion list |
| P1 Persist expected member count | unclear |
| P1 Raise request limit | **no** — `Program.css:504` unchanged (shared root cause with INTK-005) |
| P1 Specify grouped routing in the owning FRD | **no** — no FRD file in the diff |
| P2 Refresh grouped status | **no** |
| P2 Exclude non-image receipts from recognition | unclear |
| P2 Avoid rescanning the whole group | **no** — `ScanAsync` still re-runs full ONNX recognition on every member on every trigger |
| P2 Retry incomplete group registration | unclear |

Round 2 (11:26:15Z, on `599bfe6d`, the last commit) — 3 comments, **all unaddressed by
construction**: "Honor the handoff decision before associating members" (P1),
"Keep one replay identity across cardinalities" (P2), "Make concurrent member insertion
idempotent" (P2).

**CI: none.** `gh pr checks 417` → "no checks reported on the
`intk-006-grouped-image-routing` branch". `mergeStateStatus: DIRTY`, `mergeable: CONFLICTING`.

**Plan vs implementation.** The plan and checklist narrow scope mid-flight (scope-split
notes, "Follow-on boundary" section) to hand Image-initiated Case creation to INTK-008 and
conflicting-VRM routing to INTK-007. This matches the ticket body's own 2026-08-19 "Scope
split" addendum, so the narrowing is **authorised, not silent drift**.

**Simplification pass.** Recorded twice (checklist "Execution boundary" and "Review
remediation") with concrete reuse claims (`IImageIntakeCaseCandidates`,
`TryRegisterAndAssociateAsync`, no duplicate matcher, no direct EF Case write). Honest.

**Deployment risk.** No new migration of its own; it inherits INTK-005's
`GroupedIntakeSubmission.cs` and therefore the same GRANT omission. Fix belongs on the
INTK-005 branch and propagates on rebase — **do not fix independently on both branches**.

**Verified findings**

| Sev | file:line | Remediation brief | Tag |
| --- | --- | --- | --- |
| blocker | `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs:319-352` (`TryRegisterAndAssociateAsync`) | The per-member path independently re-queries `FindEligibleByRegistrationAsync` and applies its own fuzzy "one-character-missing" match, so a group whose group-level decision was `HandOffToImageIntake` (because `eligibleCaseCount` was 0 or ambiguous) can still be auto-associated member-by-member. Pass the group's `routing.Decision` into `TryRegisterAndAssociateAsync` and return early without association unless the decision is the associate branch; the per-member fuzzy match must never overrule the group decision. Test: build a group whose members individually near-match two different cases, assert the group hands off and no `AssociatedCaseId` is written. | [verified] |
| should-fix | `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` (`ScanAsync` loop) | Skip recognition for members that already carry a terminal recognition outcome, so an N-image group does not re-run ONNX inference N times on every triggering event. Persist the per-member terminal outcome and branch on it at the top of the loop. Test: trigger the automation twice for the same group and assert the recognizer is invoked once per member in total. | [verified] |
| should-fix | `src/Pegasus.Web/Program.cs:504` | Same request-limit fix as INTK-005; shared root cause, fix once. | [verified] |
| should-fix | `docs/frd/frd-02-intake-and-source-identity.md`, `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | The grouped-routing decision table (all-members-terminal wait, distinct-VRM aggregation, associate vs hand-off) is behaviour and belongs in an FRD per the repo's routing rules, but no FRD was touched. Add the decision table to the owning FRD section and cite it from the ticket plan. | [suspected, needs check] |
| nit | `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml` | Same missing `data-auto-refresh`. | [verified] |

---

### INTK-007 — PR #424, Unidentified work

**What it does.** Adds a Core-owned `Unidentified` aggregate (6 canonical reason codes,
Open/Resolved state, `U<n>` reference allocator, EF persistence with sequence/history
tables, migration `20260819115323_UnidentifiedWork` with deterministic legacy backfill),
routes terminal `ProcessIntake` outcomes into it, adds a Web queue/detail/resolution UI and
MCP tools, and reconciles operator-notes/PRD/FRDs/design/runbook. The old `NeedsSorting`
path is deliberately kept for rolling compatibility.

**Reviewer comments** — `chatgpt-codex-connector[bot]`, all at 2026-08-19T12:16:32Z, one
review, **14 comments (7×P1, 7×P2). All unaddressed** — the PR has exactly one commit
(`abd8a923`, 12:05:32Z) predating the review, with no follow-up.

Highlights: `ProcessIntake.cs:256/258/243/268/277` (retryable failures wrongly allocate
U-references; image-only material mis-excluded; stale U-items not reconciled on
re-evaluation; wrong reason mapping; wrong timestamp source);
`EfUnidentifiedStore.cs:101/148/174` (history column truncation, weak replay-fingerprint
comparison, no destination-port validation before resolving);
`Unidentified/Details.cshtml(.cs):25/35/63` (raw enum/UTC rendering, missing source
evidence, unstable operation key); `UnidentifiedMcpTools.cs:52/55` (state filter defaults
to all, accepts invalid numeric enum); `Migrations/…UnidentifiedWork.cs:175/184` (backfill
reads the wrong reason column; seeds an all-zero replay fingerprint enabling false
conflicts); `Mail/Message.cshtml.cs:114` (maps `NeedsSorting` directly to `Unidentified`,
contradicting the settled-distinct-meanings invariant).

**CI: none have run at all.** `gh pr checks 424` → "no checks reported"; the commit's
combined status is `pending` with zero check-runs. This correlates with
`mergeable: CONFLICTING` — the workflow appears never to have triggered.

**`docs/operator-notes.md` change.** A pure 33-line **addition** (`@@ -70,6 +70,33 @@`)
inserting a new "## Unidentified received material" section defining the queue, the `U<n>`
reference, the six reasons and the resolution rules. No existing sentence is reworded.
**Assessment: a legitimate, ticket-sanctioned elaboration, not a silent meaning change** —
`research` documents the operator-confirmed requirement in detail and `open-questions`
treats it as resolved. But it is an **incomplete** reconciliation: three pre-existing
"Needs sorting" mentions survive untouched in the same protected file (current lines 42,
199, 388 — e.g. line 388, *"'Needs sorting' refers to e-mail that cannot be matched; it is
not a case stage"*), so a direct contradiction sits in the protected doc post-merge. The
ticket's own checklist items "Classify every normative old Needs sorting use" and "Run the
final stale-term search" are both unticked, consistent with the leftovers.

**Plan vs implementation.** Plan is thorough and matches ticket scope; 22/36 checked. The
unticked items match the report's stated follow-ups honestly (legacy backfill runtime
grants, migration tests, mail/retained-mail/Operations projection completion, all six
reason-mapping tests, INTK-005 integration, concurrency/replay tests, final stale-term
audit, full `dotnet test`).

**Simplification pass.** Recorded, dated 2026-08-19, four lenses, with an honest
disposition ("no behaviour-preserving simplification identified beyond implemented reuse;
unchecked scope remains explicit").

**Verified findings**

| Sev | file:line | Remediation brief | Tag |
| --- | --- | --- | --- |
| blocker | `src/Pegasus.Infrastructure/Persistence/Migrations/20260819115323_UnidentifiedWork.cs` (tables at 14/49/63) | Three new tables (`UnidentifiedItems`, `UnidentifiedSequences`, `UnidentifiedHistory`) are created with no GRANT anywhere in the file — the only `migrationBuilder.Sql` block is the legacy backfill. Add a guarded grant block following `20260819104953_MailClassificationCorrectionHistory.cs:95-105`: `GRANT SELECT, INSERT, UPDATE` on the items and sequence tables and `GRANT SELECT, INSERT` (with `DENY UPDATE, DELETE`) on the history table, to `[pegasus_web_runtime_role]`; add the Worker role too if the Worker writes them. Test with the head-of-stream grant test (TICK-093 finding B4). | [verified] |
| blocker | PR #424 — 14 Codex findings, 0 addressed | Triage all 14. Highest priority in order: `ProcessIntake.cs:256` (a retryable failure must not burn a `U<n>` reference — references are scarce operator-visible identity), `Mail/Message.cshtml.cs:114` + `ProcessIntake.cs` (do not collapse `NeedsSorting` into `Unidentified`; they are separately settled meanings per CLAUDE.md), `…UnidentifiedWork.cs:184` (seed a real per-row replay fingerprint, not all-zero, or the backfilled rows all collide on replay detection). Re-run the full suite after each. | [verified] |
| should-fix | `docs/operator-notes.md` lines 42, 199, 388 | Reconcile the three surviving literal "Needs sorting" mentions the PR left unedited, or explicitly mark them as historical/compatibility text with a dated note. This is a protected doc: the change must preserve every material business statement, so prefer annotating over deleting, and complete the ticket's own unticked "final stale-term search". | [verified] |
| should-fix | `CLAUDE.md` product-invariants section | The repo's own invariant still lists `Needs sorting` among the settled distinct meanings while PR #424 already rewrites that exact sentence in `docs/prd/pegasus-product.md`. After #424 merges the two governing files disagree. Update the CLAUDE.md line in the same PR, or add `Unidentified` alongside and state the transition. | [verified] |
| nit | PR #424 branch state | `CONFLICTING`/`DIRTY` and zero CI runs. Rebase onto current `dev` before any further review; the absent workflow runs are very likely a consequence of the conflicted state. | [verified] |
| should-fix | `src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs:24` (shipped by TICK-044, merged; not part of this PR) | `gh pr diff 424 --name-only` confirms this file is untouched by INTK-007. Its `MailOperationalDestination` enum still defines a `NeedsSorting` member and is the class that would emit it for ambiguous mail — an existing `Needs sorting` producer that INTK-007's own verification bullet ("all existing producers are inventoried and either migrated or mapped") never actually inventories. Low-impact today only because the policy has zero real callers yet (§3.1, TICK-044/B2) — it will resurface as a live "Needs sorting" value the moment TICK-044's caller-wiring work lands, undoing this PR's stated replacement. Remediation: rename/replace `MailOperationalDestination.NeedsSorting` and update `MailOperationalDestinationPolicyTests.cs` in the same change that finally wires TICK-044/B2's caller, not as an afterthought. | [verified] |

---

### INTK-008 — PR #423, image-initiated lifecycle

**What it does.** Adds an explicit lifecycle (`AwaitingInstruction`,
`MergedIntoInstructionCase`, `StaffClosed`) over the existing `ImageIntake` aggregate via
migration `20260819112914_ImageInitiatedLifecycle`, wires automatic merge from reverse Case
pairing, adds a VRM-keyed custody seam, a staff-close UI, and ADR-0029 superseding
ADR-0013, and reconciles PRD/FRD-01/02/05/06/12/CONTEXT/operator-notes/capabilities/index.

**Reviewer comments** — `chatgpt-codex-connector[bot]`, all at 2026-08-19T11:49:05–06Z on
commit `2cefd9425e`, **13 comments (6×P1, 7×P2). All unaddressed** — the last commit
`855160b7` is 11:43:30Z, four minutes *before* the review.

Highlights: migration backfill mis-states pre-existing associated `ImageIntakes` as
`awaiting_instruction` (`…ImageInitiatedLifecycle.cs:33`); `AutoLinkAsync` success + merge
failure silently leaves state inconsistent (`ImageIntakeCasePairing.cs:77`); manual
link/reversal never invokes the lifecycle transition, so staff-linked records never leave
`AwaitingInstruction`; **no caller ever invokes `IImageIntakeCustody.CreateOrGetRootAsync`**
(`CustodyContracts.cs:41`) — the custody root/transfer is dead code; exact-reference search
reconstructs `ImageIntakeSummary` via the old 7-arg constructor, defaulting every result to
`AwaitingInstruction` (`Pages/ImageIntake/Index.cshtml.cs:78`); the Awaiting-queue filter
still keys off `AssociatedCaseId is null` so staff-closed records stay in it
(`ImageIntakeContracts.cs:97`); `MergeAsync`/`CloseAsync` bypass `ValidateMerge`/`ValidateClose`
(`EfImageIntakeStore.cs:264`); the replay path ignores whether the replayed command matches
(`EfImageIntakeStore.cs:325`); a stale-version conflict throws an unhandled
`DbUpdateConcurrencyException` → 500 (`Pages/ImageIntake/Details.cshtml.cs:79`); raw
enum/snake_case rendered instead of operator labels; normative lifecycle behaviour placed in
`docs/capabilities.md:215` (schedule-only) and `CONTEXT.md:148` (terminology-only) rather
than the FRDs; ADR-0013 marked superseded in frontmatter but left under "Current
architecture decisions" in `docs/adr/README.md:30`.

**CI: red.** `sql-integration (2)` failed (run 32249007243). Two real assertions:
`QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate` expected
`Succeeded`, got `Pending`; and
`ImageIntakeWebTests.StaffRegistersAnImageOnlyReceiptAndFindsItEverywhere` —
`Assert.Contains` failed, the page HTML does not contain `"awaiting definitive
instruction"`, i.e. the ticket's own new lifecycle label is not rendered where its own test
expects it. All other lanes pass.

**`docs/operator-notes.md` change — this one rewords an existing sentence.**
Old: *"An image-only arrival **may be described operationally as** an 'image-initiated
case'… its immutable source occurrence and evidence remain pre-case and distinct from any
accepted editable Case…"*
New: *"An image-only arrival **is** an Image-initiated Case projection… Images alone **do
not** create a formal Case/PO association"*, plus a new subsection formalising the
VRM-sequenced reference, merge and staff-close behaviour, and a PRD addition creating a
named second "Case-origin" concept.

**Assessment.** This moves the concept from an informal descriptor to a defined,
reference-bearing product entity with its own lifecycle — a genuine meaning upgrade, not
copy-editing. `research` and `open-questions` (all items ticked with recorded answers) show
it was scoped as the ticket's explicit purpose and treated as operator-confirmed via
EPIC-007. On that basis it reads as a **legitimate, ticket-sanctioned protected-doc
update**. However the implementation does **not** yet deliver what the reworded text now
promises — custody root never invoked, manual-link path never reaches the new lifecycle
state — so the protected doc currently **overstates shipped behaviour**. That is the part
worth putting to the operator (§4).

**Plan vs implementation.** Plan step 6 (invoke lifecycle merge; treat merge failure as
recoverable) and step 8 (custody seam, staff-close UI) are only partially realised.
Checklist confirms: **8/29**, and the checklist's own progress note says "VRM-keyed Box
adapter invocation and custody state presentation still need final
implementation/verification before PR" — yet the PR was opened anyway.

**Simplification pass: NOT RECORDED.** Neither `checklist` nor `plan` contains a dated
"Simplification pass" section, unlike INTK-007. This is a gap against the required
workflow step.

**Verified findings**

| Sev | file:line | Remediation brief | Tag |
| --- | --- | --- | --- |
| blocker | `src/Pegasus.Infrastructure/Persistence/Migrations/20260819112914_ImageInitiatedLifecycle.cs:55` | Creates `ImageIntakeLifecycleEvents` with no `migrationBuilder.Sql` and no GRANT anywhere in the 136-line file. Add the guarded grant block (pattern: `20260819104953_MailClassificationCorrectionHistory.cs:95-105`) emitting `GRANT SELECT, INSERT ON OBJECT::[dbo].[ImageIntakeLifecycleEvents] TO [pegasus_web_runtime_role];` plus whatever the Worker needs, with `REVOKE` in `Down`. Without it the Web runtime cannot read or write lifecycle events post-deploy. | [verified] |
| blocker | `tests/…/ImageIntakeWebTests.StaffRegistersAnImageOnlyReceiptAndFindsItEverywhere` + `src/Pegasus.Web/…/OperatorLabels.cs` | The new `AwaitingInstruction` state has no operator label, so the page never renders "awaiting definitive instruction" and the ticket's own test fails. Add the label mapping in `OperatorLabels` and render it on both the ImageIntake list and detail pages (this also resolves the reviewer's raw-enum finding). Test: the named test must pass. | [verified] |
| blocker | `src/Pegasus.Core/ImageIntake/CustodyContracts.cs:41` | `IImageIntakeCustody.CreateOrGetRootAsync` has no caller — the whole VRM-keyed custody seam is dead code, while `docs/operator-notes.md` now describes it as product behaviour. Either wire the real caller into the registration path, or remove the seam from this PR and delete the corresponding sentence from the protected doc. Shipping the doc without the caller is the "closed gate documented as delivered" failure the safety rails prohibit. | [verified] |
| blocker | `src/Pegasus.Core/ImageIntake/ImageIntakeCasePairing.cs:77` | The manual link/reversal path never invokes the lifecycle transition, so staff-linked records stay `AwaitingInstruction` forever, and an `AutoLinkAsync` success followed by a merge failure leaves the aggregate inconsistent with no compensating action. Route both the manual and automatic paths through one lifecycle transition method and make merge failure either transactional with the link or explicitly retryable. Test: manual-link a record and assert its state becomes `MergedIntoInstructionCase`. | [verified] |
| blocker | `src/Pegasus.Infrastructure/…/EfImageIntakeStore.cs:264` | `MergeAsync`/`CloseAsync` bypass `ValidateMerge`/`ValidateClose`, so an over-length close reason reaches SQL and throws a truncation error instead of a validation message. Call the Core validators before persisting, mirroring how the other stores in this file gate their writes. | [verified] |
| should-fix | `QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate` | Expected `Succeeded`, actual `Pending`. Determine whether the optional `IImageIntakeStore` parameter added to `ImageIntakeCasePairing` changed Case-allocation retry semantics, or whether this is a pre-existing flake — rerun the test on clean `dev` first, and only then bisect this PR. | [suspected, needs check] |
| should-fix | INTK-008 `plan` / `checklist` | No dated "Simplification pass" section exists. Run the four lenses over the branch diff and record every finding with its disposition under a dated heading before the PR can pass review; this is a required workflow step, not optional. | [verified] |
| should-fix | `docs/capabilities.md:215`, `CONTEXT.md:148` | Normative lifecycle behaviour is written in a schedule-only and a terminology-only document. Move the behavioural statements into the owning FRD and leave only the capability-ID row and the term in place, per the repo's routing table. | [verified] |
| nit | `docs/adr/README.md:30` | ADR-0013 is `superseded` in frontmatter but still listed under "Current architecture decisions". Regenerate the index view so the accepted-only filter excludes it. | [verified] |

---

### TICK-045 — PR #422, shared classification policy (MAIL-03)

**What it does.** Two files, +87/−1. Adds one SQL integration scenario
(`RetainedMailPersistenceTests.OneCorrectionPolicyAppliesIdenticallyAndIndependentlyAcrossMailboxes`)
proving the existing MAIL-04 exact-message correction command behaves identically and
independently for two mailbox identities, and rewrites the MAIL-03 row in
`docs/capabilities.md` from "Allocation only" to a local-evidence claim. **No production
source, schema, migration, FRD or ADR changed** — the ticket deliberately reuses TICK-046's
Core owner rather than creating a second policy implementation, which is the right call
under the one-Core-owner rule.

**Reviewer comments** — `chatgpt-codex-connector[bot]`, both at 2026-08-19T11:38:19Z. The
PR has exactly one commit (`139a4571`, 11:33:08Z) predating them. **Both unaddressed.**

1. **P1, `RetainedMailPersistenceTests.cs:345` — "Exercise the classification policy
   instead of seeding its output."** `StoreClassifiedReceiptAsync` inserts the same
   fabricated `MailClassificationResult` for both mailboxes; the test then exercises only
   the MAIL-04 correction/history path. Neither `ProcessIntake` nor any
   `IMailClassificationPolicy.Classify` implementation runs, so mailbox-specific policy
   selection could be broken while the test stays green.
2. **P1, `RetainedMailPersistenceTests.cs:320` — "Use a documented supported mailbox."**
   The test invents `claims@collisionengineers.co.uk` and makes it look supported by
   inserting a poll-state row. The documented estate is exactly `desk`, `engineers`,
   `info`, `instructions` (`docs/operator-notes.md:413`, verified on `origin/dev`).

**CI: fully green** — unit, browser, all three SQL shards, coverage, documentation,
reference-data, changes all pass (run 32248224734); `infrastructure` skipped.
`mergeStateStatus: CLEAN`, `mergeable: MERGEABLE`. This is the only open PR that is
mergeable and green.

**Plan vs ticket.** The plan covers what the ticket implies *given* its own scoping
decision — that MAIL-03 owns only the shared cross-mailbox contract and not the automatic
predicates (deferred to TICK-035, recorded and operator-resolved in `open-questions`). That
scoping is defensible. The gap is between the plan's evidence and the capability claim, not
in the plan's coverage.

**Implementation vs plan.** Matches — the plan promised exactly one two-mailbox scenario
plus one registry edit, and that is what shipped.

**Simplification pass.** Recorded in the PIR and plan, four lenses, concluding no
production abstraction or duplicate taxonomy was added. Honest but thin — a prose paragraph
rather than the findings/dispositions table the other tickets use. Acceptable for an
87-line test-only diff.

**Scope drift.** None in code. The drift is in the **claim**: an 87-line test change is
being used to move a capability from "allocation only" to "implemented locally".

**Deployment risk.** None directly — no migration, no schema, no DI, no config, no Worker
or Web registration change. The risk is downstream: the capability registry would assert
more than the code proves.

**Open questions.** All resolved; two operator-resolved items correctly sit under
`## Parked (explicitly deferred)`. Checklist 12/12.

**Verified findings**

| Sev | file:line | Remediation brief | Tag |
| --- | --- | --- | --- |
| blocker | `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs:320` | The test fabricates the mailbox address `claims@collisionengineers.co.uk`, which is not in the documented estate (`docs/operator-notes.md:413` names only `desk`, `engineers`, `info`, `instructions`). CLAUDE.md's safety rails state plainly: never fabricate domain emails. Replace `secondMailboxId`/`secondMailboxAddress` with a second **documented** mailbox — `engineers@collisionengineers.co.uk` is the natural choice — and seed it through the real approval/poll-state path rather than a bare row insert. Test: `dotnet test tests/Pegasus.IntegrationTests --filter "FullyQualifiedName~OneCorrectionPolicyAppliesIdenticallyAndIndependentlyAcrossMailboxes"` must still pass. | [verified] |
| blocker | `docs/capabilities.md:212` | The MAIL-03 row is rewritten to *"Implemented locally through the shared exact-message Core correction path: two-mailbox integration evidence proves identical validation…"*, but the evidence seeds a fabricated `MailClassificationResult` and never invokes any classifier — it proves the **correction** path (MAIL-04), not the **classification** policy (MAIL-03). Either drive both messages through the real `ProcessIntake`/`IMailClassificationPolicy.Classify` caller before merging, or narrow the capability note to exactly what is proven ("the shared correction/decision-retention contract behaves identically across mailbox identities; automatic classification selection remains allocation-only"). Test: assert the classifier was actually invoked, e.g. by asserting the persisted `PolicyKey`/`PolicyVersion` came from the policy rather than the fixture. | [verified] |
| should-fix | `docs/capabilities.md:213` | On merged `origin/dev`, MAIL-04 still reads "Allocation only; owning evidence still required" even though TICK-046 merged as `181fe331` and shipped a working `POST /Inbox/{id}` correction handler with its migration and grants. If #422 merges as-is the table becomes self-contradictory: MAIL-03 would claim it is implemented *through* MAIL-04's path while MAIL-04 claims it has no evidence. Update the MAIL-04 row in the same change to record TICK-046's local implementation tier. | [verified] |

---

## 3. Verifying and done tickets since the deploy

### 3.1 The MAIL/ENG cluster in `verifying` — TICK-093, TICK-043, TICK-044, TICK-046

| id | claimed entry point | proof claims deployed? | blocked on rel. 12? | checklist | unresolved OQ |
| --- | --- | --- | --- | --- | --- |
| TICK-093 | **none stated** — diff touches zero Web/Worker files. Affects existing `GET /Cases/{id}/Assessment` and `AssessmentMcpTools` via `EfCaseAssessmentStore`. | **N** — no `proof` doc exists. PIR: *"Not deployed; no cloud or `main` write."* Field: `not-deployed`. | **yes** — merged 12:16Z 08-19 | 6/6 | none |
| TICK-043 | **none new** — existing Worker `InboxPollFunction` path + read-only `/Inbox`, `/Inbox/{id}` | **N** — no `proof` doc. PIR: *"local implementation and test evidence only: no Outlook, Graph, Azure, deployment, or external write was performed"* | **yes** | 10/10 | none |
| TICK-044 | **none stated / none exists** — Core-only + docs | **N** — no `proof` doc. `capabilities.md:211` concedes *"workspace caller, deployment, and live Outlook evidence remain separately allocated"* | **yes** | **12/18** | none in OQ; **6 unticked in checklist** |
| TICK-046 | **`POST /Inbox/{id}` handler `OnPostCorrectClassificationAsync`** — `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs:123`, route `@page "/Inbox/{id:guid}"` | **N** — no `proof` doc. PIR: *"locally built and SQL/fake-backed, not deployed or verified against a live mailbox"* | **yes** | 10/10 | none |

**No ticket in this cluster has a `proof` document at all, so none makes a false deployment
claim.** One wording defect: TICK-046's PIR heads its verification *"On merged `main`"*,
which is wrong — the work is on `dev` only; `main` is release 10.

**Findings**

**B1 — BLOCKER — `src/Pegasus.Infrastructure/Persistence/Migrations/20260819112640_VersionedRepairSpecifications.cs:25` — [verified]**
The migration creates `dbo.CaseRepairSpecifications` with zero GRANT statements. The
database uses a strict per-object grant model with no schema-wide grant
(`20260729176000_AzureSqlRuntimeLeastPrivilege.cs:35-112` enumerates every table
individually; `20260803205759_SendToAiAssessmentToolset.cs:187,189` grants
`CaseAssessmentFields`/`CaseEstimateLines` to `pegasus_web_runtime_role`). The new table is
therefore unreadable by both runtime roles. It **is** reachable from `Pegasus.Web`:
`ICaseAssessmentStore → EfCaseAssessmentStore` is registered at
`src/Pegasus.Infrastructure/DependencyInjection.cs:261` inside `AddPegasusInfrastructure`,
which `Pegasus.Web/Program.cs` calls; consumers are
`src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs:27` (`@page "/Cases/{id:guid}/Assessment"`)
and `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs:146-147`.

The sibling lane's claim is confirmed **and is narrower than reality**:
`EfCaseAssessmentStore.cs:117` (`AnyAsync`) and `:135` (`Add`) are the write path, but the
**read** path also touches the table unconditionally at `:52` and `:318` via
`CurrentSpecificationIdAsync` (`:468-483`) and `CurrentDraftAsync` (`:459-466`).

*Production failure mode:* once release 12 applies this migration, **every** load of
`/Cases/{id}/Assessment` for **every** case throws `SqlException` 229 — *"The SELECT
permission was denied on the object 'CaseRepairSpecifications'"* — returning HTTP 500;
`assessment_update` via MCP fails identically inside its serializable transaction. This is
a total, immediate outage of the assessment surface, not a degraded path: the read happens
before any branch.

*Remediation:* add a new forward-only migration under
`src/Pegasus.Infrastructure/Persistence/Migrations/` (do **not** edit the merged one),
guarded by the same `IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL`
plus `ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer"` pattern used in
`20260819104953_MailClassificationCorrectionHistory.cs:95-105`, emitting
`GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseRepairSpecifications] TO [pegasus_web_runtime_role];`
and `DENY DELETE ON OBJECT::[dbo].[CaseRepairSpecifications] TO [pegasus_web_runtime_role];`
(UPDATE is needed for the accept/supersede transitions; DELETE stays denied to match the
terminal reconciliation's blanket DENY DELETE). Add matching `REVOKE`/`REVOKE DENY` in
`Down`. Test by extending `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs`
(see B4) or, minimally, applying the full chain against LocalDB, `EXECUTE AS USER` a member
of the role, and asserting `SELECT TOP 1 * FROM CaseRepairSpecifications` succeeds.

**B2 — BLOCKER — `src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs:24` — [verified]**
Definitive verdict on **merged** `origin/dev`: `MailOperationalDestinationPolicy` is
**dark**. It is a `static class`, so no DI registration is possible or present; a repo-wide
grep for the type and for the `MailOperationalDestination` enum returns only (a) the file
itself, (b) `tests/Pegasus.Core.Tests/Intake/Classification/MailOperationalDestinationPolicyTests.cs`,
and (c) one row in `docs/current-architecture.md:563`. Zero references in `Pegasus.Web`,
`Pegasus.Worker`, `Pegasus.Infrastructure`, or elsewhere in `Pegasus.Core` — including
`src/Pegasus.Core/Intake/RetainedMail.cs`, which has no `OperationalDestination` member.

This directly contradicts TICK-044's own resolved open question, which records the
operator's ruling: *"the retained mailbox viewer is meant to show this information.
TICK-044 must wire the Core mapping into the retained-mail projection and display the
detailed classification plus operational destination in the mailbox viewer. **A policy
referenced only by tests is incomplete and must not pass review as delivered.**"* Yet the
PR merged and the ticket sits in `verifying` with 6 unticked checklist items covering
exactly that work.

*Remediation:* TICK-044 must not clear `verifying`. Either (a) reopen it and implement the
caller — derive the destination in the retained-mail projection in
`src/Pegasus.Core/Intake/RetainedMail.cs` (derive, do not persist a second copy, per the
ticket's own checklist), surface it in `src/Pegasus.Web/Pages/Mail/Index.cshtml` and
`Message.cshtml`, and add a `MailWorkspaceWebTests` case asserting the rendered
destination — or (b) split the remaining 6 items into a successor ticket that blocks
MAIL-02's capability claim. `docs/capabilities.md:211` is already honest about this, so only
the board state and the merge decision are wrong.

**B3 — SHOULD-FIX — `docs/current-architecture.md:85` — [verified]**
The doc says of `/Inbox` and `/Inbox/{id}`: *"Both are read-only: the pages carry no
handler, and the Web runtime role holds `SELECT` alone on the retained-mail tables."*
TICK-046 shipped the opposite on both counts:
`src/Pegasus.Web/Pages/Mail/Message.cshtml.cs:123` defines
`OnPostCorrectClassificationAsync` on the `@page "/Inbox/{id:guid}"` page, and
`20260819104953_MailClassificationCorrectionHistory.cs:101,103` grants
`SELECT, UPDATE` on `IntakeMailClassificationDecisions` and `SELECT, INSERT` on
`IntakeMailClassificationHistory` to `pegasus_web_runtime_role`. Grepping the whole document
for "correction", "IntakeMailClassificationHistory" or "classification history" finds
nothing about TICK-046 — the feature is entirely absent from the as-built snapshot, which
CLAUDE.md requires to match reality.

*Remediation:* edit line 85 to record the antiforgery-protected `OnPostCorrectClassification`
handler (Core correction use case, required reason, expected decision version) and the new
grants (`SELECT, UPDATE` / `SELECT, INSERT` with `DENY UPDATE, DELETE`); drop the
"read-only … no handler … `SELECT` alone" phrasing. Add an Implementation-map row near line
563 for the correction port/use case in `src/Pegasus.Core/Intake/RetainedMail.cs` and
`EfRetainedMailboxMessageStore.cs`. Verify by re-grepping the doc for "read-only" near
`/Inbox`.

**B4 — SHOULD-FIX — `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs:11` — [verified]**
*This is the root cause of why B1 shipped green, and it is the highest-leverage fix in this
document.* The only grant-matrix test,
`TerminalUpgradeReconcilesEveryRuntimeTableToTheExactCallerMatrix` (line 416), migrates to
the **pinned** constant `RuntimeRoleMigration = "20260729199000_RuntimeRoleReconciliation"`
(line 11) and asserts against a frozen `ExpectedSchemaTableSpec`/`ExpectedWebGrantSpec`
snapshot from 2026-07-29. It never runs against the head of the migration stream, so any
table added after July gets no grant coverage at all. `CaseRepairSpecifications`,
`IntakeSubmissionGroups`, `UnidentifiedItems` and `ImageIntakeLifecycleEvents` could all be
created ungranted with a fully green suite — and all four were.

*Remediation:* add a `[Fact]` in the same file that migrates to **latest**
(`context.Database.MigrateAsync()` with no target, as `IntakePersistenceIntegrationTests`
does) and asserts that every row of `sys.tables` where
`is_ms_shipped = 0 AND name <> '__EFMigrationsHistory'` is a grantee target for at least one
of the two runtime roles — i.e. the set difference against
`sys.database_permissions WHERE grantee_principal_id IN (both roles) AND state = 'G'` must
be empty, with an explicit allow-list constant for deployer-only tables. Reuse the existing
`ReadGrantedPermissionsAsync`/`ReadValuesAsync` helpers. Run with
`--filter "FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests"`; it must fail before B1's
fix and pass after.

**N1 — NIT — `docs/capabilities.md:260` — [verified]** TICK-093 touched no
`docs/capabilities.md`, so the ENG-01 row still reads only *"Each route requires its own
accepted source, mapping, caller, and Engineer review."* with no evidence qualification,
unlike the MAIL-01/MAIL-02 rows. Append an equivalent sentence noting the canonical
versioned specification is locally implemented and test-backed, undeployed, and that
`EfRepairSpecificationStore` has no caller yet.

**N2 — NIT — `docs/current-architecture.md:563` — [verified]** The Implementation-map row
TICK-044 added lists `MailOperationalDestinationPolicy` as current source without noting it
has no caller, which reads as delivered wiring. Remove the row until B2's caller lands, or
append "no production caller yet; consumed only by Core tests pending the mailbox-viewer
caller."

**Dark code, for completeness.** `EfRepairSpecificationStore` is confirmed dark: no
`AddScoped`/`AddSingleton`/`AddTransient` anywhere in
`src/Pegasus.Infrastructure/DependencyInjection.cs`, and its only instantiations are
`tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs:429,447,478`.
`IRepairSpecificationStore` is declared at `src/Pegasus.Core/Assessment/RepairSpecifications.cs:210`
and consumed by no Core use case. **Being dark it causes no runtime failure by itself** —
the B1 outage comes entirely from the *live* `EfCaseAssessmentStore`, which was adapted to
the new table. This distinction matters for triage: fixing the GRANT is urgent; wiring the
dark store is not.

### 3.2 PLAT-006, TICK-033, SIMPLI-014, PR-009

| id | claimed entry point | proof claims deployed? | blocked on rel. 12? | checklist | unresolved OQ |
| --- | --- | --- | --- | --- | --- |
| PLAT-006 | `/Upload`, `/UploadStatus`, `/Uploads/{token}` (Razor Pages) | **N** — no `proof` doc; the PIR only describes checks to run *"after the release"* | **yes** | 9/10 | *no OQ doc* |
| TICK-033 | **none** — docs-only; references the pre-existing `/Uploads/{token}` | **N** — PIR explicitly disclaims *"No live activation… production custody test… was performed"* | **yes** | 4/5 | *no OQ doc* |
| SIMPLI-014 | **none stated** — explicitly adds no HTTP/Razor/MCP/CLI/Worker trigger; consumed only by tests | **N** — proof: *"does **not** claim Azure deployment… No cloud or `main` write occurred"* | **yes** | 18/24 | none |
| PR-009 | **none** — same internal renderer as SIMPLI-014, no live caller | **N** — proof: *"does not claim Azure deployment… No cloud or `main` write occurred"* | **yes** | 17/17 | none |

**Correction to a plausible-looking conclusion.** A first pass suggested PLAT-006 and
TICK-033 were blocked on release *11*, not 12, because both are ancestors of
`task/deliv-011-release-11` (`feda958f`). That is true of the branch but irrelevant to the
estate: release 11 was **held and never deployed** — no ACR tag for `feda958f`, no
container revision, no ARM deployment (`research/current-estate.md` §4). **All four
tickets, and every other post-cutoff ticket, are blocked on release 12.**

**PLAT-006 verified as a genuinely pure front-end change**, from the diff rather than the
title: 6 files, all `.cshtml`/`.css`/`.js` plus one design doc — zero `.cs`, no `Program.cs`
or DI change. Lower deployment risk confirmed. Its four Codex P2 comments (2026-08-19T07:52:29Z)
were partly addressed by commit `0fb92865` at 07:52:57Z — the multi-file drop is now
truncated to one file with an explanatory comment, and copy was corrected. The
"canonical file-size format" and "two keyboard controls for one picker" comments have no
corresponding change. The accepted-extension list on `origin/dev` still shows
`.eml .msg .pdf .doc .docx .jpg .png` while the input's `accept` attribute also permits
`.jpeg` — the reviewer's point stands.

**SIMPLI-014's 18/24 is bookkeeping, not a delivery gap.** All 11 substantive checklist
items are ticked. The 6 unticked entries are a stale "## Closeout" list superseded by a
later "## Closeout completion — 2026-08-19" section that re-confirms 4 of them.
Independently verified: `git branch -a` and `git worktree list` show the simpli-014 branch
and worktree are genuinely gone, and `get_item` returns no `taken_at`/`branch`/`worktree`,
so the claim really is released.

**PR-009's 17/17 spot-checked and accurate.** The production diff is exactly the one-line
fix claimed — `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs:111`,
`new TemplateContext()` → `new TemplateContext { LimitToString = 0 }` — and the "6/6"
real-Chromium claim matches the test file: one `[Theory]` with 4 `[InlineData]` outcomes
plus 2 `[Fact]`s.

**No migrations in any of these four tickets** (`git diff --name-status … -- '**/Migrations/**'`
empty for all), so no GRANT exposure. SIMPLI-014's Web-only composition claim verified:
`AddPegasusReportRendering()` is called only from `src/Pegasus.Web/Program.cs:541`;
`src/Pegasus.Worker/Program.cs` has no such call — no Worker/Web registration mismatch.

**Findings**

| Sev | doc / file | Remediation brief | Tag |
| --- | --- | --- | --- |
| should-fix | PLAT-006 `checklist` item 10 | Reads "PR to `dev`, review, merge — PR #409 open", but #409 merged at `feda958f` on 2026-08-19T08:08:07Z. Tick the item and record the merge commit and date. Board-doc edit only, no source change; a stale "open" could cause a reviewer to re-request a merge that already happened. | [verified] |
| should-fix | PLAT-006 — `src/Pegasus.Web/Pages/Upload.cshtml` accepted-file list | The visible list omits `.jpeg` while the input's `accept` attribute permits it, so an operator with a `.jpeg` is told it is unsupported. Derive both the list and the `accept` attribute from one format definition in the page model (or add `.jpeg` to the list). Test: assert the rendered list and the `accept` attribute contain the same extension set. | [verified] |
| should-fix | PLAT-006 — `src/Pegasus.Web/wwwroot/js/site.js:141` | The file-size readout implements a second, inconsistent formatter (KB below 1 MiB, reporting a one-byte file as `1 KB`) while `docs/design/README.md:162` requires MB to one decimal and `OperatorLabels.FileSize` already implements that policy. Either omit the size from the readout or emit the canonical format. | [verified] |
| nit | PLAT-006 — `src/Pegasus.Web/wwwroot/js/site.js:160` | With script enabled the `.sr-only` native input stays focusable alongside the new visible button, so keyboard and screen-reader users traverse two controls for one action. Remove the input from the tab order when enhanced, or drop the extra button. | [verified] |
| nit | TICK-033 `checklist` item 2 | "Run focused request-upload integration tests" is unchecked with an honest note that `CaseDetailsWebTests`/`DocumentCustodyDurabilityTests` exceeded the local two-minute timeout. Re-run under CI or with a longer timeout and tick with the result — this is the only real outstanding verification gap of the four. | [verified] |
| nit | SIMPLI-014 `checklist` "## Closeout" (first list) | Collapse the stale duplicate Closeout list into the later completion section so the 18/24 count stops misreading as incomplete delivery. No functional risk — branch and worktree removal independently confirmed. | [verified] |

### 3.3 Report-renderer and decision tickets in `done`

| id | claimed entry point | proof claims deployed? | claim true? | blocked on rel. 12? | checklist |
| --- | --- | --- | --- | --- | --- |
| TICK-213 | none — test-only diff (`AssessmentReportRendererTests.cs`) | N — *"not deployment or live-caller evidence; no cloud or `main` action occurred"* | ✓ | yes | 15/15 |
| TICK-204 | none — docs-only, one FRD file | N — *"does not claim renderer implementation, application caller activation, Azure deployment…"* | ✓ | yes | 11/11 |
| DOCS-002 | **none stated** — confirmed docs-only ADR-0028 | N — *"No Azure write or `main` update was performed"* | ✓ | yes | 11/11 |
| TICK-099 | none — deferred boundary, zero diff | N — *"Deployment: `n/a`. PR/merge: `n/a — zero repository diff`."* | ✓ | n/a (zero diff) | 13/13 |
| TICK-205 | none | N — *"does not claim … deployment, or operator acceptance"* | ✓ | n/a | 16/16 |
| TICK-207 | none | N — *"Deployment: `n/a`. PR/merge: `n/a — zero repository diff`."* | ✓ | n/a | 13/13 |
| TICK-211 | none — zero-diff subsumption record | N — *"does not claim deployment or live runtime behaviour"* | ✓ | n/a | 11/16 |
| TICK-212 | none — zero-diff subsumption record | N — *"verifies source and dependency composition, not container/runtime deployment"* | ✓ | n/a | 12/12 |
| TICK-215 | none | N — *"Deployment is `n/a`"* | ✓ | n/a | 12/12 |

TICK-211's and TICK-212's zero-diff claims were checked rather than taken on trust:
`git log --all --grep=` returns **no commits** for TICK-212, TICK-211, TICK-203, TICK-215,
TICK-099, TICK-205 or TICK-207. Corroborating, `origin/dev` holds exactly the 7 solution
locks TICK-212's proof names, `workspaces/report-renderer` is absent from `origin/dev`
(90 paths still present at `d8de29cb`), and #415's merge changed exactly the 5 lock files
the proof names. Both change-suggestive titles genuinely produced no repository change.

**Findings**

**F3 — SHOULD-FIX — TICK-205 `proof` vs TICK-205 `open-questions` — [verified]**
The proof is built on a premise the operator overturned the same day. Proof:
*"TICK-205's operator-resolved question records two immutable Audit repair
specifications — `conservative` and `maximised`"*. Open-questions: *"**Does Audit require
two conservative/maximised repair specifications or any uplift calculation?** — No. That
premise was incorrect."* and *"Audit and Inspection reports are identical."* The ticket
body's Outcome records the correction; the proof does not.
*Remediation:* rewrite the proof's decision section to record the actual resolution
(identical physical report; the difference is workflow and reference identity only —
`a.{Case/PO}` for repairable, `ap.{Case/PO}` for total loss), then re-check the downstream
consumers the proof names (TICK-093, TICK-098), which it says "consume this decision" in
its now-retracted form.

**F4 — SHOULD-FIX — TICK-207 `proof` vs TICK-207 `open-questions` — [verified]**
Same defect. Proof: *"Audit rendering remains absent and unavailable until a concrete
representative Audit artifact is supplied and explicitly approved through a new linked
activation ticket."* Open-questions: *"The premise was incorrect… Reuse the approved
inspection/assessment report template… **Do not wait for, request, or invent a separate
Audit artifact.**"* The proof institutionalises a deferral the operator explicitly
cancelled.
*Remediation:* rewrite the proof to record reuse-of-the-Inspection-template as the accepted
outcome, retire the "new linked activation ticket" gate, and check whether TICK-098 /
TICK-205 links propagate the stale blocker.

### 3.4 Already-deployed tickets — PLAT-001, AUTO-002, DELIV-009, TICK-009, TICK-010

| id | claimed entry point | proof claims deployed? | claim true? | blocked on rel. 12? |
| --- | --- | --- | --- | --- |
| DELIV-009 | production Web app (`…--d8de29cb94f3`), `/diagnostics/version`, Worker poll | **Y** — *"`azd provision`… live/ready 200"*, *"`Invoke-ProductionSmoke.ps1`… passed"* | ✓ — it **is** release 10; timestamps and SHA match `d8de29cb`/13:52Z exactly | **no** |
| AUTO-002 | `/authorize` (Administrator consent), `/connect/token`, `/mcp` (15 tools) | **Y** — *"verified on merged `main` `d8de29cb`, deployed as release 10"* | ✓ — its commits `17545b6f` (13:20Z) and `15e98424` (13:35Z) are both inside PR #405 = `d8de29cb`. Addendum records the operator's own Claude.ai connector completing the flow ~15:07–15:09Z | **no** |
| PLAT-001 | 21 operator screens in `Pegasus.Web`, left-rail shell | proof scoped to merged `dev` `5ab3b773` | ✓ — `git merge-base --is-ancestor fe44ec8a d8de29cb` confirms it is **in release 10** | **no** |
| TICK-009 | Core classification foundation (QDOS route) | **Y** — *"shipped to production in release 9 (revision `…--f1e116c6eb93`, Worker package `f1e116c6`); smoke passed"* | ✓ — `b8ed3110` and `a6d801b4` are both ancestors of `d8de29cb`; release-9 head `f1e116c6` is itself an ancestor | **no** |
| TICK-010 | taxonomy persistence for Other/Sent categories | **Y** — *"shipped in release 9 (web revision `--f1e116c6eb93`, Worker `f1e116c6`); smoke passed"* | ✓ — `ea25816b` and `376bef3f` are both ancestors of `d8de29cb` | **no** |

**Findings**

| Sev | doc / file | Remediation brief | Tag |
| --- | --- | --- | --- |
| nit | AUTO-002 `checklist` | Two top-level items remain unticked — "Independent review; merge." and "Release 10: promotion, provision (new env), smoke; live connector evidence; proof; docs refresh." — both contradicted by the ticket's own status, its proof ("independent review PASS", live production evidence) and its own progress notes two lines below. Tick both via `set_ticket_doc`; board-doc edit only. | [verified] |
| nit | `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs` | The live discovery document AUTO-002 captured shows `code_challenge_methods_supported: [plain, S256]`, i.e. OpenIddict's default still permits the weaker `plain` PKCE method. AUTO-002's own outcome already names dropping `plain` as a follow-up. If picked up as new work: restrict the OpenIddict server builder to S256 only, update the discovery test expectations, re-run `AutomationConnectorAuthorizationTests`. **Not a regression from this ticket set** — file it, don't fold it into release 12. | [suspected, needs check] |
| should-fix | PLAT-001 — no code change needed | 55/63 with the two unticked verification items being "Local `DevelopmentOffline` run; visual proof of the rail and one screen per family" (deferred to the verifying stage on merged `main`) and the earlier duplicate of the same. PLAT-001 is already in production, so this visual proof can be captured **now**, against production, without waiting for release 12. Worth doing during the release-12 verification window since PLAT-006 changes the same shell. | [verified] |

### 3.5 TICK-011 — the one real board contradiction

TICK-011 is `status: done`, `deployment: not-deployed`, has **no PR**, **no
open-questions document**, and cites three commits.

**Ancestry check against `d8de29cb`:**

- `ae6f0c2d` — **NOT an ancestor**; `git branch -a --contains` returns empty (an unreachable,
  pre-rebase object).
- `f7d99b18` — **NOT an ancestor**; also unreachable from any branch.
- `ef3eb4c7` — **ancestor** of `d8de29cb`, `origin/main` and `origin/dev`.

The INT-17 *capability* nevertheless **is** in the deployed tree: `d8de29cb` contains
`src/Pegasus.Core/ImageIntake/*` (5 files), `EfImageIntakeStore.cs`, migration
`20260803071539_ImageIntakeRegistration`, the Web pages and 7 test files. Reachable delivery
came via `ef3eb4c7` and `ba65c1ed`.

| Sev | doc | Remediation brief | Tag |
| --- | --- | --- | --- |
| should-fix | TICK-011 `proof`, "Verified target" section | The line *"Historical INT-17 commits `ae6f0c2d`, `ef3eb4c7`, and `f7d99b18` are ancestors of this commit"* is false for two of three SHAs, both unreachable from any ref. Correct the proof to cite only `ef3eb4c7` (plus `ba65c1ed`) as the reachable delivery commits, or re-derive the real post-rebase SHAs with `git log d8de29cb -- src/Pegasus.Core/ImageIntake`. The conclusion survives — the capability really is in the deployed tree — but the citation as written cannot be reproduced by a reviewer. | [verified] |
| should-fix | TICK-011 board field `deployment: not-deployed` | **This is a board-hygiene contradiction, not an honest record.** The ImageIntake source, migration, Web pages and tests are demonstrably present in the deployed release-10 tree. "Not-deployed" is a factually wrong statement about shipped code; what the ticket actually means — no production *caller execution* — is an activation fact, not a deployment fact, and the proof narrative states it correctly. Set `deployment: production` with an explicit "shipped, no live caller" qualifier, or introduce a distinct value for shipped-but-unactivated. **Critically: do not count TICK-011 as undeployed work awaiting release 12** — its code is already in production, and release-12 scoping built on the current field value will be wrong. | [verified] |
| nit | TICK-011 missing `open-questions` document | `get_ticket_doc doc:"open-questions"` returns `exists: false`, yet the ticket reached `done`. Every other ticket in this set has one, and the questions-resolved gate counts unticked lines in a file that does not exist — so it passed vacuously. Create the document retrospectively recording the INT-17 activation question (no production caller) as the live unresolved item, which also gives the activation gap a tracked home. | [verified] |

---

## 4. Open questions and contradictions for the operator

### 4.1 Tickets held by other agents — leave alone or coordinate

Eleven tickets are currently `taken`. **Nine are held by agents other than `claude-code`**
and must not be touched by this lane without coordination.

| ticket | assignee | branch | worktree |
| --- | --- | --- | --- |
| INTK-005 | **Codex** | `intk-005-grouped-upload` | `.worktrees/intk-005` |
| INTK-006 | **Codex** | `intk-006-grouped-image-routing` | `.worktrees/intk-006` |
| INTK-007 | **Codex** | `intk-007-unidentified-intake` | `.worktrees/intk-007` |
| INTK-008 | **Codex** | `intk-008-image-initiated-lifecycle` | `.worktrees/intk-008` |
| TICK-045 | **Codex / execute_tick_045** | `task/tick-045-shared-classification-policy` | `../pegasus-worktrees/tick-045-shared-classification-policy` |
| TICK-033 | codex-mcp-client | `task/tick-033-request-upload-reconciliation` | `../pegasus-worktrees/tick-033` |
| TICK-043 | codex-mcp-client | `task/tick-043-mailbox-identity` | `../pegasus-worktrees/tick-043-mailbox-identity` |
| TICK-044 | codex-mcp-client | `task/tick-044-classification-catalogue` | `../pegasus-worktrees/tick-044-classification-catalogue` |
| TICK-046 | codex-mcp-client | `task/tick-046-classification-history` | `../pegasus-worktrees/tick-046-classification-history` |
| TICK-093 | codex-mcp-client | `task/tick-093-versioned-repair-spec` | `../pegasus-worktrees/tick-093-versioned-repair-spec` |
| PLAT-006 | claude-code | `task/plat-006-shell-upload` | `../pegasus-worktrees/plat-006-shell-upload` |

**Q1. The four INTK worktrees break two repository conventions.** They live at
`.worktrees/intk-00X` — *inside* the repository, alongside the Kanmer board's own
`.worktrees/kanmer` — and their branches carry no `task/` prefix, where the workflow
specifies worktree `../pegasus-worktrees/<slug>` on branch `task/<slug>`. Both are also
visible on `origin` (`origin/intk-005-grouped-upload` … `origin/intk-008-…`).
**Recommendation:** do not rename or move another agent's branches. Accept the deviation for
this release, integrate the PRs as-is, and clean the branches after merge as part of the
DELIV-012 hygiene step. Raise the convention with whoever operates the Codex lane so the
next batch conforms.

**Q2. Five of the six open PRs are not mergeable, and four carry zero-addressed review
findings.** INTK-005 has red CI; INTK-006 has no CI at all and conflicts; INTK-007 has no
CI at all and conflicts; INTK-008 has red CI and conflicts. Between them Codex raised
**32 unaddressed review comments** on the four INTK PRs plus 2 on TICK-045 — none has a
commit after its review. **Recommendation:** these four are not merge candidates in their
current state. Ask the operator whether release 12 should ship the merged backlog only
(12 PRs already on `dev`, once B1 is fixed) and let INTK-005/6/7/8 land in release 13, or
whether DELIV-012 should first drive the remediation of all 32 findings. Shipping the
merged backlog alone is materially lower risk and unblocks 12 tickets' proof immediately.

### 4.2 Genuine operator decisions

**Q3 — protected document overstates shipped behaviour (INTK-008).**
PR #423 rewords `docs/operator-notes.md` from *"An image-only arrival **may be described
operationally as** an 'image-initiated case'"* to *"An image-only arrival **is** an
Image-initiated Case projection"* and formalises a VRM-sequenced reference, merge and
staff-close behaviour. The reword itself appears ticket-sanctioned and operator-confirmed
via EPIC-007. But the implementation does not deliver what the new text promises — the
custody root (`CustodyContracts.cs:41`) has no caller at all, and the manual-link path never
reaches the new lifecycle state.
**Recommendation:** the doc change should not merge ahead of the caller. Either hold the
`operator-notes.md` and PRD edits until the custody and manual-link paths work, or merge the
code and cut the two sentences describing unimplemented behaviour. Only the operator can
confirm the reword reflects their actual intent, since the safety rails require stopping
for user resolution before changing that file's meaning.

**Q4 — `Needs sorting` invariant (INTK-007).**
CLAUDE.md's product invariants state: *"`Audit`, `Triage`, `Needs sorting`, and `Blocked
intake` retain their settled distinct meanings."* PR #424 rewrites that exact sentence in
`docs/prd/pegasus-product.md` to replace `Needs sorting` with `Unidentified`/`Image
Intake`, adds a 33-line `Unidentified` section to `operator-notes.md`, but leaves three
literal "Needs sorting" statements standing in `operator-notes.md` (lines 42, 199, 388) and
does not touch CLAUDE.md. `Mail/Message.cshtml.cs:114` additionally maps `NeedsSorting`
straight to `Unidentified`, collapsing two supposedly distinct meanings.
**Question for the operator, quoted from the reviewer:** *"Does `Needs sorting` retain a
distinct meaning after Unidentified work exists, or is it fully superseded?"*
**Recommendation:** answer this before #424 merges. If superseded, update CLAUDE.md,
`operator-notes.md` lines 42/199/388 and the PRD together in one change and the code mapping
is correct. If distinct, the code mapping at `Message.cshtml.cs:114` is a defect and the PRD
edit should be reverted.

**Q5 — MAIL-03's capability claim rests on evidence that does not test classification
(TICK-045).**
PR #422 would move MAIL-03 from "Allocation only" to "Implemented locally", on the strength
of a test that seeds a fabricated classification result and then exercises only the MAIL-04
correction path. It also introduces `claims@collisionengineers.co.uk`, an address outside
the four documented mailboxes.
**Recommendation:** hold #422. The fabricated address is a clear safety-rail violation and
must change regardless. The capability wording should then be narrowed to what is proven,
or the test should drive the real classifier. This is the cheapest of the five open PRs to
put right — 2 files, green CI, no conflicts.

**Q6 — TICK-044 merged against its own operator ruling.**
TICK-044's `open-questions` records the operator's instruction verbatim: *"the retained
mailbox viewer is meant to show this information… **A policy referenced only by tests is
incomplete and must not pass review as delivered.**"* The PR nonetheless merged with
`MailOperationalDestinationPolicy` referenced only by its own tests, and the ticket sits in
`verifying` with 6 unticked items covering exactly the missing caller.
**Recommendation:** TICK-044 should not clear `verifying`. Either reopen it for the caller,
or split the remaining 6 items into a successor ticket that blocks the MAIL-02 capability
claim. The operator should decide which, since they set the "must not pass review" bar.

**Q7 — no ticket falsely claims production, but five leave `deployment` unset.**
TICK-033, PLAT-006, TICK-043, TICK-044 and TICK-046 have an empty `deployment` field. **No
ticket anywhere in this roster claims production without evidence** — the four that claim it
(AUTO-002, DELIV-009, TICK-009, TICK-010) are all verified ancestors of `d8de29cb`.
**Recommendation:** set the five to `not-deployed` during the release-12 pass so the board
records the state positively rather than by omission. Low priority.

**Q8 — PR #410's own description is stale relative to what it will actually promote.**
`gh pr view 410 --json body` shows text written for the superseded release-11 scope
("Carries beyond `main` (`f79c24d9`): PR 407 — release-10 record… PR 408 — INT-31…
PR 409 — PLAT-006…"), naming only 3 of the 12 merges now on `dev`. Its `headRefOid` tracks
`dev` live (it is a `dev`→`main` PR, not a frozen branch snapshot), so the SHA it would
promote is already correct — only the prose description undersells the diff.
**Recommendation:** rewrite #410's description (or close it and let DELIV-012 open a fresh
`dev`→`main` PR) to name everything since release 10 before anyone reviews it against the
stated scope; a reviewer working from the current description would materially
under-estimate what they're approving.

**Q9 — the "operator clarified the Image-initiated Case model on 2026-08-19" premise cited
by INTK-006/007/008 has no record in `docs/open-decisions.md`.** `grep -i "image-initiated"
docs/open-decisions.md` returns nothing. This does not mean the clarification did not
happen — it may have been given directly and simply not logged there, or logged and later
archived — but three PRs and multiple protected-document edits all rest on it, so the
operator confirming it directly (and where, if anywhere, it is recorded) would remove doubt
before release 12 integrates all three. Related git-hygiene note: a stray local branch
`pr417check` (head `599bfe6d`, INTK-006's tip, created 2026-08-19T12:20+0100) exists with no
matching ticket, likely a manual review branch from this research window — falls inside
DELIV-012's own stated git-hygiene scope, no operator decision needed.

---

## 5. Implications for release 12

### 5.1 Must be fixed before the release ships

**B1 is a release blocker in the strict sense.** `20260819112640_VersionedRepairSpecifications`
is already merged to `dev` and will be applied by the release's `efbundle` step. The moment
it applies, `/Cases/{id}/Assessment` returns HTTP 500 for every case, because
`EfCaseAssessmentStore` reads `CaseRepairSpecifications` unconditionally and the Web runtime
role has no permission on it. **Do not run release 12 until the follow-up grant migration is
merged.** B4 (the head-of-stream grant test) should land in the same change so this class of
defect cannot recur — it would have caught all four ungranted tables.

Second-order, same release: `docs/current-architecture.md:85` (B3) is stale about TICK-046,
and the safety rails require the current-state docs to match reality before the release
merges. That is a docs edit, cheap, and should ride along.

### 5.2 Tickets whose proof depends on this deployment

**Ten tickets cannot complete their proof until release 12 ships.** Six are in `verifying`
and are blocked from reaching `done`; four are in `done` with proof correctly self-limited
to merged `dev`.

| Blocked in `verifying` (cannot move to `done`) | Blocked in `done` (proof self-limited to `dev`) |
| --- | --- |
| TICK-033, PLAT-006, TICK-043, TICK-044, TICK-046, TICK-093 | SIMPLI-014, PR-009, TICK-213, TICK-204, DOCS-002 |

PLAT-006's own PIR names the production check it is waiting for: at ≥1600px,
`getComputedStyle(document.querySelector('.app-rail-main')).marginLeft > 0` and equal to
`marginRight`; `/Upload` showing the two-column layout with the `Choose file` button visible
and no `[style]` attributes. That is a browser check in the release-12 verification window.

TICK-093's and TICK-046's proofs additionally need the migration applied and the assessment
and `/Inbox/{id}` surfaces exercised against production.

### 5.3 What moves to `done` once deployed

**Immediately, with only proof-writing left:** TICK-033, PLAT-006, TICK-043, TICK-046.
All four have complete checklists (bar TICK-033's timed-out integration run and PLAT-006's
merge box), honest PIRs, and no outstanding code findings.

**Only after remediation:**

- **TICK-093** — needs B1's grant migration deployed and the assessment surface verified,
  or it ships an outage.
- **TICK-044** — needs the Q6 decision. It has 6 unticked checklist items and a dark policy;
  it should not move on the strength of a deploy alone.

**Not affected by release 12:** the seven zero-diff decision tickets (TICK-099, 203, 205,
207, 211, 212, 215) are already terminal — though TICK-205's and TICK-207's proofs need the
F3/F4 corrections regardless. TICK-011 is already in production (§3.5) and must not be
counted as undeployed work.

### 5.4 Recommended sequence

1. **Remediate B1 + B4** on a fresh branch off `dev`: the grant migration for
   `CaseRepairSpecifications`, plus the head-of-stream grant test. Merge to `dev`.
2. **Fix B3** (`docs/current-architecture.md:85`) and the TICK-205/TICK-207 proof
   corrections (F3/F4) — cheap, docs-only.
3. **Decide Q2**: ship release 12 from the 12 already-merged PRs, leaving the four INTK PRs
   for release 13. This is the recommendation — it unblocks ten tickets' proof now, and the
   INTK set needs 32 review findings addressed, two red CI lanes fixed, two branches
   rebased out of conflict, and Q3/Q4 operator answers before any of it is mergeable.
4. **Optionally land TICK-045** first if Q5 is resolved — it is green, clean, 2 files, and
   would carry MAIL-03 with it.
5. Run release 12, verify, refresh `docs/operations.md` and `docs/current-architecture.md`,
   then walk the ten blocked tickets to `done`.

### 5.5 Release-mechanics reminders carried from the estate research

- Three migrations apply in this release (four with B1's fix). Migration head to start from
  is `20260814094632_DropBoxFileRequests` (45 rows).
- `Pegasus.Core`/`Pegasus.Infrastructure` both change, so a new Worker package is required —
  via `az functionapp deployment source config-zip`, the only route that has succeeded on
  this estate.
- No `infra/` or `azure.yaml` diff between `main` and `dev`.
- The Log Analytics daily cap (0.1 GB) trips around 11:50 UTC; telemetry-based verification
  after that is blind until 03:00 UTC.
- `SentEvidencePollFunction` throws an `UnauthorizedAccessException` once a minute in the
  current estate (enabled function, `AllowSentEvidence=0`). Any "zero exceptions"
  post-release assertion must account for it.
