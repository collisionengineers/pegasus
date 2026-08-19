# DELIV-012 research — recent tickets since the last deploy

Anchor facts: last production deployment = release 10, `d8de29cb`, 2026-08-18T13:52Z; production still serves that build. `origin/dev` = `560f741c` at research time (confirmed by `git fetch origin dev`). Anything merged to `dev` after 2026-08-18T13:52Z is **not** in production. Local main-repo working tree (`dev` branch, HEAD `4ba63888`) is one commit *behind* `origin/dev` (missing PR #420's merge commit `560f741c` itself) — file-level checks below use `git show origin/dev:<path>` rather than the working tree where that matters.

Note on method: `list_items` filtered to `updated_since: 2026-08-18T13:52:00Z` returned 68 tickets, but a board-wide `order` reindex by `codex-mcp-client` at 2026-08-19T09:39:14–15Z touched nearly every ticket's `updated` timestamp as a side effect (confirmed via `get_activity` on several — their only post-cutoff entry is `update / order`). The roster below is filtered to tickets with genuine non-order activity since the cutoff (status change, doc write, take, commit/PR update), cross-referenced against `review`/`verifying`/`done` status. 29 tickets qualify.

---

## 1. Roster since the last deploy

| ID | Title (short) | Status | Profile | Taken (branch / worktree / assignee) | PR(s) | Merged→dev? | `deployment` field | Docs present | Checklist | Unresolved open-questions |
|---|---|---|---|---|---|---|---|---|---|---|
| TICK-093 | ENG-01 canonical repair specification | verifying | feature | `task/tick-093-versioned-repair-spec` / `../pegasus-worktrees/tick-093-versioned-repair-spec` / codex-mcp-client | #420 | Yes (12:16Z) | not-deployed | all 7 | 6/6 | none |
| INTK-007 | Replace Needs sorting with Unidentified | review | feature | `intk-007-unidentified-intake` / `.worktrees/intk-007` / Codex | #424 | No (open) | — | all 7 | 22/36 | none (impl. gated on docs, done) |
| TICK-045 | MAIL-03 shared classification policy | review | feature | `task/tick-045-shared-classification-policy` / Codex | #422 | No (open) | — | all 7 | 12/12 | none (all parked items resolved) |
| INTK-008 | ImageIntake → Image-initiated Case lifecycle | review | feature | `intk-008-image-initiated-lifecycle` / Codex | #423 | No (open) | — | all 7 | 8/29 | none listed (but no dated Simplification-pass section — see §2) |
| INTK-006 | Grouped image routing | review | fix | `intk-006-grouped-image-routing` / Codex | #417 | No (open, DIRTY/conflicting) | — | all 7 | 26/41 | none (all [x]) |
| TICK-213 | Decide density scope for rendered docs | done | feature | — / codex-mcp-client | #421 | Yes | n/a | all 8 | 15/15 | none |
| TICK-046 | MAIL-04 classification evidence/history | verifying | feature | `task/tick-046-classification-history` / codex-mcp-client | #418 | **Yes, merged 11:23:50Z** | None (stale — should be n/a, see §3) | 7 (no proof yet) | 10/10 | none |
| PR-009 | Fix long-list/photo Chromium truncation | done | fix | — / codex-mcp-client | #419 | Yes | n/a | all 8 | 17/17 | none |
| INTK-005 | Grouped Upload (multi-file) | review | feature | `intk-005-grouped-upload` / Codex | #416 | No (open) | — | all 7 | 7/33 | none |
| PLAT-001 | Claude Design UI implementation | done | feature | — / claude-code | #397 | Yes | **not set — should be `production`, see §4** | all 8 | 55/63 | 1 unchecked verification item (visual proof), acknowledged follow-up |
| TICK-099 | RPT-04 diminution deferral | done | feature | — / codex-mcp-client | none (zero-diff) | n/a | n/a | all 8 | 13/13 | none |
| TICK-205 | Audit needs no dual-spec/uplift | done | feature | — / codex-mcp-client | none (zero-diff) | n/a | n/a | all 8 | 16/16 | none |
| TICK-212 | Renderer package lock files | done | feature | — / codex-mcp-client | #415 (subsumed) | Yes | n/a | all 8 | 12/12 | none |
| TICK-207 | Audit reuses Inspection template | done | feature | — / codex-mcp-client | none (zero-diff) | n/a | n/a | all 8 | 13/13 | none |
| TICK-211 | Renderer analyzer strictness | done | feature | — / codex-mcp-client | #415 (subsumed) | Yes | n/a | all 7 | 11/16 | none |
| TICK-203 | Reconcile renderer MCP vs Automation Actor | done | feature | — / codex-mcp-client | #415 (subsumed) | Yes | n/a | all 8 | 12/12 | none |
| TICK-043 | MAIL-01 mailbox/thread/message identity | verifying | feature | `task/tick-043-mailbox-identity` / codex-mcp-client | #414 | Yes | None | 7 (no proof yet) | 10/10 | not checked in depth (see §3) |
| SIMPLI-014 | Integrate CollisionRenderer behind Core render contract | done | feature | — / codex-mcp-client | #415 | Yes | None | all 8 | **18/24, incomplete** | 1 unchecked verification item — "a real Pegasus caller renders… end to end" is **not actually true**, see §3 |
| TICK-215 | Decide renderer execution location | done | feature | — / codex-mcp-client | #413 (via DOCS-002) | Yes | n/a | all 8 | 12/12 | none |
| TICK-204 | Define assessment-report outcome variants | done | feature | — / codex-mcp-client | #412 | Yes | n/a | all 8 | 11/11 | none |
| TICK-010 | MAIL-22 taxonomy persistence | done | feature | — / grok-shell-kanmer | #392 | Yes (release 9) | production (correctly, release 9 not 10/12) | all 8 | 8/8 | none |
| TICK-009 | MAIL-21 classification foundation | done | feature | — / grok-shell-kanmer | #391 | Yes (release 9) | production (correctly, release 9) | all 8 | 12/12 | none |
| DOCS-002 | ADR-0028: renderer runs in Web Container App | done | chore | — / codex-mcp-client | #413 | Yes | n/a | all 8 | 11/11 | none |
| DELIV-009 | Release 10 promotion | done | chore | — / claude-code | #406, #407 | Yes (main+dev) | **production** (correct — this IS the deploy) | plan/checklist/proof only | 10/10 | none |
| AUTO-002 | Authorization-code + PKCE for MCP connectors | done | feature | — / claude-code | #405 | Yes (release 10) | **production** (correct, live-evidenced) | 7 (no files) | 15/17 | none |
| TICK-011 | INT-17 automatic VRM reading (retrospective) | done | feature | — / (unassigned) | none (retrospective, no PR) | Yes (already on main) | not-deployed (honestly self-reported — caller not established) | 6 (no files/open-q) | 10/10 | none |
| TICK-044 | MAIL-02 classification→destination mapping | verifying | feature | `task/tick-044-classification-catalogue` / codex-mcp-client | #411 | Yes | None | 7 (no proof yet) | **12/18, incomplete — real caller explicitly not wired yet**, see §3 | **self-acknowledged unresolved item**, see §3 |
| PLAT-006 | Centre shell content region + redesign Upload | verifying | fix | `task/plat-006-shell-upload` / claude-code | #409 | Yes | None | 5 (no research/open-q — not required for `fix` profile) | 9/10 | n/a (no open-questions doc) |
| TICK-033 | INT-31 request-upload capability-inventory correction | verifying | feature | `task/tick-033-request-upload-reconciliation` / codex-mcp-client | #408 | Yes | None | 6 (no open-questions doc) | 4/5 (integration tests locally timed out, left for CI) | n/a (no open-questions doc) |

**28 real tickets** (TICK-213/PR-009/TICK-046/TICK-204/DOCS-002/SIMPLI-014/etc. above), all genuinely active since the cutoff. 5 have open PRs against `dev` (INTK-005/006/007/008, TICK-045); the rest are merged-to-dev-not-production or genuinely deployed (TICK-009/010 in release 9, AUTO-002/DELIV-009 in release 10).

---

## 2. Open PRs — INTK-005, INTK-006, INTK-007, INTK-008, TICK-045

All five PRs are an EPIC-007 (INTK-*) / EPIC-006 (TICK-045) cluster built in the last few hours. **A systemic deployment risk runs through four of the five**: every new EF Core migration in this batch creates tables the Web app writes to, and **none of the four grant `pegasus_web_runtime_role` any permission** — the repo convention (confirmed against `20260819104953_MailClassificationCorrectionHistory.cs`, TICK-046's already-merged migration, and `20260814092852_AddWorkerCaseCreationGrants.cs`) is `migrationBuilder.Sql("GRANT SELECT, INSERT/UPDATE ON OBJECT::[dbo].[Table] TO [pegasus_web_runtime_role];")` per new table. `grep -n GRANT` across all five PR diffs found **zero** GRANT statements anywhere in #416, #417, #423, or #424 (TICK-045/#422 adds no schema, so it's exempt). This is the same class of defect the sibling research lane already verified for TICK-093/#420 (merged).

### #416 INTK-005 — Allow one Upload submission to accept multiple files

**What it does:** Adds a Core `GroupedIntake` boundary (group + ordered member contracts, one store port, one use case) around the existing per-file `IIntakeSubmission`; new `IntakeSubmissionGroups`/`IntakeSubmissionGroupMembers` tables; changes the authenticated Upload page to accept multiple files and redirect to a new `UploadGroupStatus` page listing every member's outcome.

**CI:** `sql-integration (1/2/3)` **fail**; `browser`, `unit`, `sql-integration-coverage`, `changes`, `documentation`, `reference-data` pass. `infrastructure` skipped. Mergeable, but `mergeStateStatus: UNSTABLE`.

**Reviewer comments (Codex bot, all at 10:35:36Z, none addressed — no commits pushed after the review):**
- **P1** `Upload.cshtml:36` — multipart body limit still capped at ~10 MiB in `Program.cs`, so two valid 6 MiB files fail before `OnPostAsync` even though the page now advertises multi-file support.
- **P2** `UploadGroupStatus.cshtml:12` — the new status page doesn't set `data-auto-refresh`, so rows stay at `Received`/`Processing` until manual reload.
- **P2** `EfIntakeSubmissionGroupStore.cs:122` — concurrent same-token submissions racing on the unique `(GroupId, Ordinal)` constraint aren't retried, so one request in a double-submit can fail outright.
- **P2** `Upload.cshtml.cs:129` — exact-replay of the same token now always redirects to the generic group page, losing the "already received; no duplicate" feedback.
- **P1** `GroupedIntake.cs:128` — single-file uploads now get their occurrence token rewritten from `token` to `token:0`, breaking any code/tests that correlate by the original `ExternalReceiptToken`.

**Plan vs ticket:** covers what INTK-005's body asks for. **Simplification pass:** recorded, dated 2026-08-19, in `plan/`, credible (reuses `IIntakeSubmission`, no new queue/framework). **Scope drift:** none — an explicit "Parallel-branch execution note" documents that INTK-006 was deliberately built from this PR's branch before merge (non-blocking, reconciled by rebase later); this is unusual but is disclosed, not hidden.

**Deployment risk:** `IntakeSubmissionGroups`/`IntakeSubmissionGroupMembers` created with no GRANT [verified — `grep GRANT` on the migration diff hunk is empty]. Own post-implementation-report already flags this: *"Runtime-role grants for the new tables should be confirmed against the deployment migration conventions before production promotion."*

**Verified findings:**
- **[blocker] [verified]** Missing GRANT on `IntakeSubmissionGroups`/`IntakeSubmissionGroupMembers`. File: `src/Pegasus.Infrastructure/Persistence/Migrations/20260819101344_GroupedIntakeSubmission.cs`. Remediation: in `Up()`, after the two `CreateTable` calls, add `migrationBuilder.Sql("GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeSubmissionGroups] TO [pegasus_web_runtime_role];");` and the same for `IntakeSubmissionGroupMembers` (INSERT/SELECT — the store only inserts and queries, never updates/deletes members), matching the pattern in `20260819104953_MailClassificationCorrectionHistory.cs:101-105`. Test: apply the migration to a throwaway SQL Server instance under the `pegasus_web_runtime_role` login and confirm an insert into both tables succeeds without a permission error.
- **[should-fix] [verified]** Multipart body size cap unchanged in `Program.cs` while the UI now advertises multi-file uploads (Codex P1 comment, unaddressed). Remediation: raise `FormOptions.MultipartBodyLengthLimit` (and any Kestrel/IIS equivalent) in `src/Pegasus.Web/Program.cs` to a limit derived from `IntakeEnvelopeLimits.MaximumContentLength * <max files>` plus overhead, or reject the excess client-side before submit with a clear message. Test: submit two 6 MiB files via the integration multipart helper and assert success instead of a 400.
- **[nit] [verified]** `UploadGroupStatus.cshtml` missing `data-auto-refresh` (Codex P2, unaddressed). Remediation: add the attribute used elsewhere (e.g. `UploadStatus.cshtml`) to the group status row container.

### #417 INTK-006 — Grouped image routing

**What it does:** Routes an entire vehicle-image group (not one image) to a single outcome — associate all members to one existing eligible Case when there's one confident VRM and one match, or defer to INTK-008's ImageIntake lifecycle when there's no match. Distinguishes detector-empty vs recognizer-empty vision outcomes. **Also edits `docs/operator-notes.md`** (protected) to introduce the "Image-initiated Case" concept.

**CI:** *no checks reported* on the branch (confirmed via `gh pr checks 417`). `mergeStateStatus: DIRTY`, `mergeable: CONFLICTING`.

**Reviewer comments (Codex bot, 10:54:14Z + 11:26:15Z across two reviews, unaddressed):**
- **P1** `ImageIntakeAutomation.cs:182` — expected member count is read from `members.Count` (currently-persisted count), not the group's declared total, so an interrupted/racing submission can under-count and finalize prematurely.
- **P1** `Upload.cshtml:36` — same multipart size-limit issue as #416 (shared file).
- **P2** `UploadGroupStatus.cshtml:14` — same missing auto-refresh as #416.
- **P2** `ImageIntakeAutomation.cs:150` — non-image receipts (PDFs/Word docs) in a mixed group are passed into the image recognizer without checking `ImageIntakeLifecycleRules.IsImageOnlyMaterial` first.
- **P2** `GroupedIntake.cs:131` — replayed/duplicate groups lose the `IsDuplicate` feedback flag.

**Plan vs ticket:** the plan is candid about scope: it explicitly narrows to "grouped recognition, diagnostics, stable aggregation, and unique existing-Case association" and defers the Image-initiated Case branch to INTK-008 — consistent with the ticket body's own "Scope split — 2026-08-19" addendum. **Implementation vs plan:** matches; own post-implementation-report states the Image-initiated Case branch is deliberately not claimed complete here. **Simplification pass:** recorded and dated, credible (reuses `IImageIntakeCaseCandidates`, `TryRegisterAndAssociateAsync`; no duplicate matcher). A documented "Review remediation evidence" section shows one round of self-fix (commit `866d305e`) already applied to restore single-file behaviour broken by an earlier commit — good sign of real iteration, but happened *before* the Codex review shown above, which is still unaddressed.

**operator-notes.md diff (verified, quoted):** Changes the "image-only arrival" paragraph from *"may be described operationally as an 'image-initiated case'… remains pre-case and distinct from any accepted editable Case… Images alone must not create a definitive association"* to *"is an Image-initiated Case projection… remains distinct from any formal Instruction-initiated Case while instructions or case association are pending… Images alone do not create a formal Case/PO association"* and adds a new "Image-initiated Case clarification — 2026-08-19" section introducing the VRM-sequenced reference, searchability, and merge/close lifecycle. **This is a material meaning change**, not a restatement: it converts a "pre-Case, not yet a real record" concept into a first-class second Case-origin type with its own reference sequence and lifecycle. The ticket's plan/open-questions cite *"the operator has clarified…"* as authority, but **`docs/open-decisions.md` contains no corresponding entry** — the only record of this operator confirmation is the implementing agent's own ticket notes (see §4).

**Deployment risk:** shares the `IntakeSubmissionGroups`/Members migration with #416 (identical file, same GRANT gap — not double-counted). No new tables of its own in this PR.

**Verified findings:**
- **[blocker] [verified]** `ImageIntakeAutomation.cs:182` uses `members.Count` (persisted-so-far) as both actual and expected member count, so a racing/interrupted group can finalize before all members arrive. Remediation: persist the declared expected member count at group creation time (INTK-005's group store already has the full member list on submission) and pass that value — not a live count query — into the completion check in `ImageIntakeAutomation.ApplyAsync`. Test: a Core unit test that stores 3 declared members, processes 2, and asserts the routing policy returns `WaitingForMembers` rather than finalizing.
- **[should-fix] [suspected, needs check]** operator-notes.md meaning change lacks an independent confirmation record outside the ticket's own notes — flagged for operator resolution in §4, not a code fix.
- **[should-fix] [verified]** Non-image receipts in a mixed group bypass `IsImageOnlyMaterial` before recognizer invocation (Codex P2). Remediation: in `ImageIntakeAutomation.cs` around line 150, filter the group to members where `IsImageOnlyMaterial` is true before calling the recognizer, and route non-image members through the existing document path unchanged. Test: a Core test with one image + one PDF in a group, asserting the PDF is never passed to `OnnxVrmRecognitionEngine`.

### #423 INTK-008 — Image-initiated Case lifecycle

**What it does:** Adds explicit lifecycle states (`AwaitingInstruction`, `MergedIntoInstructionCase`, `StaffClosed`) over the existing `ImageIntake` aggregate; new `ImageIntakeLifecycleEvents` table plus 5 new columns on `ImageIntakes`; wires merge-on-match from `ImageIntakeCasePairing`; adds ADR-0029 and supersedes ADR-0013 (frontmatter only, not edited in place — correct per repo convention). Also edits `docs/operator-notes.md` and `docs/prd/pegasus-product.md`.

**CI:** `sql-integration (2)` **fails**; the rest pass. `mergeStateStatus: DIRTY`, `mergeable: CONFLICTING`.

**Reviewer comments (Codex bot, 11:49:05–06Z, unaddressed):**
- **P1** migration `20260819112914_ImageInitiatedLifecycle.cs:33` — backfill sets every *existing* `ImageIntakes` row to `awaiting_instruction` even if it's already linked via `IntakeManualAssociations`/`CaseIntakeLinks`, silently losing their real merged state after upgrade.
- **P1** `ImageIntakeCasePairing.cs:77` — if `AutoLinkAsync` succeeds but the follow-up merge call fails, the exception is swallowed with the association already committed, leaving the record permanently stuck (future pairing runs see `associated: false` and never retry the merge).
- **P1** `ImageIntakeCasePairing.cs:77` (second comment, same line) — the new merge transition is invoked only from automatic pairing; the existing manual staff link/reverse route in `Pages/Intake/Details.cshtml.cs` never calls it, so a staff-created link never reaches `MergedIntoInstructionCase`.
- **P1** `CustodyContracts.cs:41` — **repo-wide search for `IImageIntakeCustody`/`CreateOrGetRootAsync` finds only the interface, its adapters, and DI registration — no application caller ever invokes it.** [This independently confirms the sibling lane's "dark code" pattern for a *third* ticket in this batch — matches TICK-093's `IRepairSpecificationStore` and TICK-044's `MailOperationalDestinationPolicy`.]
- **P2** `ImageIntake/Index.cshtml.cs:78` — exact-reference search reconstructs `ImageIntakeSummary` with the old 7-arg constructor, so every exact-reference search result shows the default `AwaitingInstruction` state regardless of actual lifecycle state.

**Plan vs ticket / implementation vs plan:** plan is thorough and matches the ticket's boundaries (explicitly excludes INTK-006/007's scope). **Simplification pass: not found.** Unlike every sibling ticket in this batch (INTK-005/006/007, TICK-045 all have a dated "Simplification pass" section in `plan/`), INTK-008's plan only has a *planned step* ("11. Run the simplification pass…") with no dated findings/dispositions recorded anywhere in plan.md or checklist.md — a process gap against this repo's required convention (CLAUDE.md §4: "record findings and dispositions in the ticket's plan under a dated 'Simplification pass' heading").

**operator-notes.md diff:** same "Image-initiated Case clarification" section as #417 (near-identical hunk — these two branches share the same governing-doc reconciliation work). Same concern as §"#417" above.

**Deployment risk:** `ImageIntakeLifecycleEvents` table + 5 new `ImageIntakes` columns, **no GRANT** [verified — empty `grep GRANT`]. The custody-invocation gap (P1 above) means even once merged, no code path actually transfers registered images to Box custody for new Image-initiated Cases.

**Verified findings:**
- **[blocker] [verified]** Missing GRANT for `ImageIntakeLifecycleEvents` (and implicitly for UPDATE on the 5 new `ImageIntakes` columns, since existing GRANTs predate this migration and may not cover new columns depending on how the original table grant was scoped — needs the exact original grant statement checked). File: `20260819112914_ImageInitiatedLifecycle.cs`. Remediation: same as #416 — add `GRANT SELECT, INSERT ON OBJECT::[dbo].[ImageIntakeLifecycleEvents] TO [pegasus_web_runtime_role];` in `Up()`, and confirm (via `sp_helprotect` or an integration test under the runtime role) that the pre-existing `ImageIntakes` table grant already covers `UPDATE` on all columns generically (SQL Server column-level GRANT is all-or-nothing unless explicitly column-scoped, so this is likely fine, but must be verified, not assumed).
- **[blocker] [verified]** No caller ever invokes `IImageIntakeCustody`/`CreateOrGetRootAsync` (Codex P1, corroborated by direct grep against `origin/dev`'s DI/Program.cs — zero matches outside definition/adapters/DI registration). Remediation: wire the custody-root creation into the same code path that transitions a group to the Image-initiated outcome (likely `ImageIntakeCasePairing` or the INTK-006 group-routing policy once merged) — call `CreateOrGetRootAsync` and transfer registered images at the point a new Image-initiated Case reference is allocated. Test: an integration test asserting the fake/local custody adapter receives a `CreateOrGetRootAsync` call when a group with a usable VRM and no eligible match is processed.
- **[should-fix] [verified]** Backfill migration overwrites existing linked `ImageIntakes` rows to `AwaitingInstruction`, losing their real state (Codex P1). Remediation: in the migration's data-backfill SQL, `CASE` on whether a row has a matching `IntakeManualAssociations`/`CaseIntakeLinks` entry and set `MergedIntoInstructionCase` with the linked Case id/reference for those rows instead of a blanket `AwaitingInstruction`.
- **[should-fix] [verified]** Manual staff link/reverse route bypasses the new lifecycle transition (Codex P1). Remediation: call the same merge-transition method from `Pages/Intake/Details.cshtml.cs`'s existing `linkIntake.ExecuteAsync`/`reverseIntakeLink.ExecuteAsync` handlers that `ImageIntakeCasePairing` calls for automatic pairing.
- **[nit] [verified]** No dated Simplification-pass record in plan.md/checklist.md, unlike every sibling ticket in this batch.

### #424 INTK-007 — Replace Needs sorting with Unidentified

**What it does:** The largest PR in the batch (49 files, +8346/-48). Adds a Core `Unidentified` aggregate with atomic `U<n>` reference allocation, six canonical reasons, Open/Resolved state, full Web queue/detail/resolution UI, MCP tools, and a legacy-`NeedsSorting`-to-`Unidentified` backfill migration. Edits `docs/operator-notes.md` and `docs/prd/pegasus-product.md`.

**CI:** *no checks reported* on the branch. `mergeStateStatus: DIRTY`, `mergeable: CONFLICTING`.

**Reviewer comments (Codex bot, 12:16:32Z, unaddressed — PR opened minutes before this research ran):**
- **P1** `ProcessIntake.cs:258` — a below-threshold image-only receipt with no confident VRM is excluded from Unidentified registration by the same condition that should trigger it, so it's silently dropped rather than getting a `U<n>` reference.
- **P1** `EfUnidentifiedStore.cs:174` — resolution to `InstructionCase`/`ImageIntake`/`Triage`/`BlockedIntake` accepts any nonempty free-form `TargetId` with no destination-port validation, so a typo'd or fabricated target id silently removes an item from the queue with no real link created.
- **P1** `Mail/Message.cshtml.cs:114` — maps the existing `NeedsSorting` route/state directly to `Unidentified`, which **the repo's own product invariant requires to remain distinct** (see below).
- **P1** `ProcessIntake.cs:256` — a transient/retryable source-reader exception is converted to `TechnicalFailure` on the *first* attempt, allocating an immutable `U<n>` for what may be a recoverable outage.
- **P2** `EfUnidentifiedStore.cs:148` — replay-detection compares only actor/reason/target, not `TargetKind`/`TargetReference`, so a conflicting command with the same operation key can be silently treated as a successful replay.

**Product-invariant check (verified, quoted diff):** `docs/prd/pegasus-product.md`'s "Terminology and outcomes" line changes from *"`Audit`, `Triage`, `Needs sorting`, and `Blocked intake` have distinct meanings"* to *"`Audit`, `Triage`, `Unidentified`, `Image Intake`, and `Blocked intake` have distinct meanings."* This is the ticket's explicit, disclosed purpose (title: "Replace Needs sorting with referenced Unidentified work") — not a rogue change. **However**, this repo's own `CLAUDE.md` "Product invariants" section still states verbatim: *"`Audit`, `Triage`, `Needs sorting`, and `Blocked intake` retain their settled distinct meanings; `Triage` is the only current term."* CLAUDE.md is a repository governance file, not one of the `docs/` files this PR touches — **no ticket in this batch updates it**, so once INTK-007 merges, CLAUDE.md's own invariant text will be stale relative to the product it governs. This is an operator/repo-governance question, not a code defect (§4).

Separately, the Codex P1 comment on `Mail/Message.cshtml.cs:114` is a genuine implementation concern: mapping `NeedsSorting` straight to `Unidentified` risks conflating cases that should route to Triage/Blocked intake/Audit instead — exactly the distinction the invariant (in either wording) requires to be preserved.

**Plan vs ticket:** the plan is unusually rigorous — explicit "Implementation is deliberately blocked until kanmer-docs reconciles the protected and governing documents" gate, which was respected (governing docs updated first per the checklist). **Simplification pass:** recorded, dated, and candid — explicitly lists what's *not* done ("the unchecked grouped-submission, retained-mail/Operations projection, and full stale-term audit work remains explicit scope"). Checklist is 22/36 — consistent with its own post-implementation-report's disclosed follow-ups (retained-mail/Operations projections still using legacy compatibility paths, runtime-grant verification pending).

**Deployment risk:** `UnidentifiedItems`/`UnidentifiedSequences`/`UnidentifiedHistory`, **no GRANT** [verified — empty `grep GRANT`]. Own post-implementation-report explicitly lists "Runtime-role grant verification and clean/upgrade migration integration tests remain for review/verification" as an open risk — self-aware but unresolved.

**Verified findings:**
- **[blocker] [verified]** Missing GRANT for all three new tables. File: `20260819115323_UnidentifiedWork.cs`. Remediation: add `GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[UnidentifiedItems]`, `GRANT SELECT, INSERT ON OBJECT::[dbo].[UnidentifiedSequences]` (sequence table only needs read+insert under serializable allocation, no update), and `GRANT SELECT, INSERT ON OBJECT::[dbo].[UnidentifiedHistory]` (append-only, consider `DENY UPDATE, DELETE` to match the `IntakeMailClassificationHistory` convention) to `pegasus_web_runtime_role`. Test: same as #416.
- **[blocker] [verified]** Below-threshold image-only receipts silently excluded from Unidentified registration (Codex P1). Remediation: in `ProcessIntake.cs` around line 258, invert or correct the guard so image-only receipts with no confident VRM *do* reach `RegisterUnidentifiedAsync` unless they're still retryable/processing — check the exact boolean logic against the six canonical reasons in `UnidentifiedContracts.cs`. Test: a Core test with a below-bar VRM suggestion asserting the receipt ends up with a `U<n>` reference and `NoUsableIdentification` reason, not silently dropped.
- **[should-fix] [verified]** Free-form `TargetId` resolution with no destination-port validation (Codex P1). Remediation: in `EfUnidentifiedStore.cs` around line 174, validate `TargetId` against the actual destination store (`ICaseAcceptanceStore`, `IImageIntakeStore`, `ITriageStore`, `IBlockedIntakeStore` as applicable to `TargetKind`) before accepting the resolution, rather than accepting any nonempty string.
- **[should-fix] [suspected, needs check]** `NeedsSorting`→`Unidentified` mapping in `Mail/Message.cshtml.cs:114` may conflate distinct destinations (Codex P1) — needs a manual trace of which `NeedsSorting` producers this mapping covers vs. the FRD-03 Triage/Blocked-intake/Audit routing rules to confirm whether it's actually wrong or just imprecisely worded.
- **[nit] [suspected, needs check]** CLAUDE.md's Product Invariants section will be stale (still says "Needs sorting") once this merges — no ticket in the visible batch updates it. Flagged for operator/maintainer decision in §4.

### #422 TICK-045 — MAIL-03 shared classification policy

**What it does:** The smallest PR (87 additions, 2 files). No production code or schema change — adds one SQL integration test proving `CorrectRetainedMailClassification` behaves identically for two distinct mailbox identities, and updates `docs/capabilities.md`'s MAIL-03 evidence-tier note. Explicitly does **not** claim live/deployed verification (production currently has one linked mailbox; the ticket's own open-questions record the operator's 2026-08-19 resolution that a two-mailbox live check is out of scope for this ticket).

**CI:** all green except `sql-integration (1)` still `pending` at the time of this check; `infrastructure` skipped as usual.

**Reviewer comments (Codex bot, 11:38:19Z, unaddressed):**
- **P1** `RetainedMailPersistenceTests.cs:345` — the test seeds the *same* fabricated `MailClassificationResult` for both mailboxes via `StoreClassifiedReceiptAsync` before exercising only the correction/history path, so it doesn't actually exercise the classification *policy* itself for either mailbox — it only proves the correction path is mailbox-agnostic, not that classification is.
- **P1** `RetainedMailPersistenceTests.cs:320` — the test invents `claims@collisionengineers.co.uk` [verified — `const string secondMailboxAddress = "claims@collisionengineers.co.uk";` present in the PR diff at that hunk] as a second mailbox, but the actually-supported estate per `docs/operator-notes.md:413` is `desk`, `engineers`, `info`, `instructions` — none of which is `claims`. The test's premise (this address is "supported") isn't backed by the documented mailbox list.

**Plan vs ticket:** the plan is explicit that MAIL-03 is *"functionally carried by the existing MAIL-04 exact-message correction path"* and this branch adds only the missing cross-mailbox integration evidence — a narrow, well-scoped read. Checklist 12/12, spot-checked against the diff (only 2 files, matches exactly what's claimed). **Simplification pass:** recorded, dated, minimal and honest ("no code changes required after the pass").

**Deployment risk:** none — no migration, no schema, no production code touched. `TICK-044`'s `MailOperationalDestinationPolicy` dark-code status (see §3) is **not** resolved by this PR — TICK-045 exercises `CorrectRetainedMailClassification` (a different, already-wired MAIL-04 command), not the MAIL-02 destination policy.

**Verified findings:**
- **[should-fix] [verified]** Test seeds classification output directly rather than exercising the classification policy, undermining its value as MAIL-03 acceptance evidence (Codex P1). Remediation: in `RetainedMailPersistenceTests.cs`, replace the direct `StoreClassifiedReceiptAsync` seed with a call through the actual classification entry point (whatever Core service performs initial classification for a retained message) for at least one of the two mailboxes, so the test proves the *policy* — not just the correction/history persistence — is mailbox-invariant.
- **[nit] [verified]** Test uses an undocumented mailbox address `claims@collisionengineers.co.uk` not in the four-mailbox estate recorded in `docs/operator-notes.md:413` (Codex P1). Remediation: swap to one of the four documented mailboxes not already used by the first test identity (`desk`, `engineers`, `info`, or `instructions`), or add a short comment clarifying this is a synthetic/hypothetical second identity for the purpose of proving mailbox-scoping, not an implied fifth supported mailbox.

---

## 3. Verifying/done tickets since the deploy

### TICK-093 — ENG-01 canonical repair specification (verifying, PR #420, **merged to dev**)

Delivers one case-scoped, immutable, versioned repair specification with source-route provenance (`CaseRepairSpecifications` table), superseding the earlier "conservative/maximised Audit spec" idea per TICK-205's operator correction. No `proof.md` yet (fine — not required until `done`). Checklist 6/6, all real.

**Claimed entry point:** `EfCaseAssessmentStore` (existing, wired assessment flow) writes `CaseRepairSpecifications` rows as part of normal case-assessment editing — a real caller exists for the *table*, even though the dedicated `IRepairSpecificationStore`/`EfRepairSpecificationStore` added by this ticket has none.

**Verified findings:**
- **[blocker] [verified]** No `GRANT` for `CaseRepairSpecifications` in `20260819112640_VersionedRepairSpecifications.cs` (confirmed: `git show origin/dev:<migration> | grep GRANT` → empty), while `EfCaseAssessmentStore.cs:117-135` (existing, already-wired store) does `context.CaseRepairSpecifications.Add(specification)` on the normal case-assessment save path — an already-live, already-called code path that **will hit a SQL permission error in production** the first time a case-assessment draft is saved after this deploys, because `pegasus_web_runtime_role` has no grant on the new table. Remediation: add a follow-up migration (do not edit the merged one) with `GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseRepairSpecifications] TO [pegasus_web_runtime_role];` — INSERT for new specs/corrections, UPDATE only if any field is mutated in place (the "successor" correction model in the ticket body suggests append-only/new-row-per-version, so SELECT+INSERT may suffice; confirm against `EfCaseAssessmentStore.cs`'s exact write pattern). Test: apply migrations to a clean DB under the `pegasus_web_runtime_role` login, then exercise the case-assessment save path that reaches line ~132 and confirm no permission error.
- **[should-fix] [verified]** `IRepairSpecificationStore`/`EfRepairSpecificationStore` (330 new lines) has zero callers anywhere in `origin/dev` outside its own definition, its EF implementation, and DI registration — confirmed via `git grep -n IRepairSpecificationStore origin/dev -- '*.cs'`, which returns only the interface declaration. This is genuinely dark code as delivered; it may be intentionally forward-looking for a follow-on ticket (TICK-092/096/097/098/100/081/092 all block on TICK-093 per its `blocks` field), in which case this is expected and not a defect — but it should be confirmed against one of those blocked tickets' plans before release 12, not assumed.

**Blocked on release 12 for proof to become true:** Yes — `proof.md` doesn't exist yet, and the GRANT gap must be fixed before any downstream ticket that writes `CaseRepairSpecifications` in production can be trusted.

### TICK-043 — MAIL-01 mailbox/thread/message identity (verifying, PR #414, merged)

Not deeply audited beyond `get_item`/checklist (10/10) due to scope/time; no `proof.md` yet. Body is the generic "Plan and research" MAIL-* template shared with TICK-044/045/046 (all four MAIL-0x tickets share this templated body — a board-generation artefact, not a defect). **Recommend a follow-up pass on TICK-043 specifically** before it reaches `done`, using the same method as TICK-044/046 below (open-questions + checklist cross-check), since its sibling tickets both surfaced real gaps.

### TICK-044 — MAIL-02 classification→operational destination (verifying, PR #411, merged)

**This ticket documents its own incompleteness.** `open-questions.md` records an operator resolution: *"the retained mailbox viewer is meant to show this information. TICK-044 must wire the Core mapping into the retained-mail projection and display the detailed classification plus operational destination in the mailbox viewer. **A policy referenced only by tests is incomplete and must not pass review as delivered.**"* The checklist (12/18) confirms this is unresolved:
```
- [ ] Wire `MailOperationalDestinationPolicy` into the retained-mail Core projection as the real caller.
- [ ] Carry the exact classification and derived operational destination to the mailbox list/detail view without duplicate persistence.
- [ ] Display both values in the retained mailbox viewer with distinct fail-closed states.
- [ ] Add integration/Web tests proving the deployed-shaped viewer path consumes the Core policy.
- [ ] After deployment, run and record an authenticated read-only production mailbox-viewer check…
```
Independently confirmed: `git grep -n MailOperationalDestinationPolicy origin/dev -- '*.cs'` returns only its own definition file — zero callers, matching the sibling lane's exact claim.

**Claimed entry point:** none yet — self-acknowledged.

**Verified findings:**
- **[blocker] [verified, self-acknowledged by the ticket]** `MailOperationalDestinationPolicy` has no caller. This ticket should **not advance past `verifying`** until the checklist's unchecked "wire into retained-mail projection" items are done — the ticket's own bar for "review-passable" is explicitly not yet met. Remediation: wire the policy into whatever Core query backs the retained-mail viewer's `GET /Inbox/{id}` handler (see `docs/current-architecture.md`'s "Current callers and entry points" section for the existing read path), surface both the detailed classification and the derived destination in the view model, add a Web/integration test asserting the viewer path calls the policy, and only then request the read-only production check the operator specified.

**Blocked on release 12:** Yes, and should arguably not reach `done` before the caller-wiring checklist items are completed regardless of the deploy.

### TICK-046 — MAIL-04 classification evidence/policy version/correction history (verifying, PR #418, **merged 11:23:50Z**)

Adds `IntakeMailClassificationDecisions`/`IntakeMailClassificationHistory` with real GRANTs this time (`GRANT SELECT, UPDATE` on Decisions, `GRANT SELECT, INSERT` + `DENY UPDATE, DELETE` on History — a clean example of the convention done right). Checklist 10/10, no unresolved open-questions.

**Stale doc confirmed:** `docs/current-architecture.md:85` states *"Both are read-only: the pages carry no handler, and **the Web runtime role holds SELECT alone on the retained-mail tables**."* This is now inaccurate — TICK-046's merged migration grants `UPDATE`/`INSERT` on the two new classification tables to `pegasus_web_runtime_role`, and the correction path (`CorrectRetainedMailClassification`, exercised by both TICK-045 and TICK-046) is a real write path from the mail detail view. **[should-fix] [verified]** Remediation: update `docs/current-architecture.md:85` to reflect that the Web runtime role now has write access to the classification-correction tables (not the retained-mail *source* tables themselves, which likely remain SELECT-only — the wording needs to distinguish the two, not just soften "alone").

**Entry point:** the retained-mail correction UI (`/Inbox/{id}`) via `CorrectRetainedMailClassification` — real, already exercised by the merged migration's own GRANT and by TICK-045's test.

**Blocked on release 12:** No code risk found; the doc staleness is independent of deployment status and should be fixed regardless.

### PLAT-006 — Centre shell content + redesign Upload (verifying, PR #409, merged)

Presentation-only (6 files under `Pegasus.Web` + design README), thoroughly self-tested (32 browser tests run twice, 513 integration tests, local visual sweep at three viewports). Six simplification findings recorded and applied, including a self-caught CSS ordering bug fixed within the same branch. Honestly discloses non-scope items (Case detail/Assessment/New case not swept; a 500 error on `/Cases/Create` without `receiptId` noted as a pre-existing issue, not this ticket's). Checklist 9/10 — the one open item is production confirmation, correctly deferred to post-deploy.

**Entry point:** `/Upload`, `/Uploads/{token}`, and the shell content region generally — directly observable, well-evidenced.

**No new findings** beyond what the report itself already discloses. **Blocked on release 12:** yes, for the "confirmed on production" checklist item and the design-tool `/design-sync` refresh it flags as owed.

### TICK-033 — INT-31 capability-inventory correction (verifying, PR #408, merged)

One-line fix to `docs/capabilities.md` (removes a stale "UI removal pending" note; the removal already happened via an earlier commit `f43e3a2b`). Docs-only, simplification pass correctly recorded as n/a. Checklist 4/5 — the open item is that `CaseDetailsWebTests`/`DocumentCustodyDurabilityTests` timed out locally and were left for CI, which is honestly disclosed rather than claimed as passing.

**No findings.** **Blocked on release 12:** no — this ticket makes no claim that depends on deployment.

### SIMPLI-014 — Integrate CollisionRenderer behind a Core-owned render contract (done, PR #415, merged)

**The ticket's own Outcome text overstates readiness.** It says *"the active surface is the approved rendererref1 assessment plus fee note"*, implying a working, reachable feature. Verified against `origin/dev`: the render contract is real (`IAssessmentReportRenderer` at `AssessmentReportRendering.cs:265`, correctly DI-registered as `PlaywrightAssessmentReportRenderer` in `DependencyInjection.cs:407`), and its use case `GenerateAssessmentReportDraft` is also DI-registered (`AddScoped`, `DependencyInjection.cs:408`) — but **`git grep -n GenerateAssessmentReportDraft origin/dev` finds it invoked only from `Pegasus.Core.Tests` and `Pegasus.IntegrationTests` — zero Web/Worker/MCP callers.** There is no page, controller, or MCP tool anywhere in `origin/dev` that actually triggers report generation for an operator. This directly matches the systemic "dark code" pattern already found in TICK-093 (`IRepairSpecificationStore`), TICK-044 (`MailOperationalDestinationPolicy`), and INTK-008 (`IImageIntakeCustody`) — a fourth independent instance in this same deploy window.

Checklist 18/24 — six unchecked items are consistent with this: the original ticket verification criterion *"A real Pegasus caller renders at least one accepted report variant end to end through the composed Web/Worker path"* is unmet. Simplification pass and CI evidence otherwise credible (Release build clean, focused tests pass).

**Claimed entry point:** none — self-contradicts its own outcome text once checked against code.

**Verified findings:**
- **[should-fix] [verified]** `GenerateAssessmentReportDraft`/`IAssessmentReportRenderer` has no application caller despite the ticket's outcome text implying an "active surface." Remediation: this is very likely intentionally deferred to a follow-on ticket (the ticket links TICK-203/204/205/206/207/208/211/212/213/214/215/216 as "sub-decisions to resolve at activation," several of which are the docs-only decision tickets already closed in this same window) — confirm which specific follow-on ticket owns wiring the real Web/Worker caller (candidates: TICK-092 "render-snapshot projection," or a not-yet-created ticket), and correct SIMPLI-014's outcome wording to say "renderer integrated, real caller pending [[TICK-XXX]]" rather than "the active surface is…", which reads as delivered when it is not yet reachable by any operator action.

**Blocked on release 12:** the integration itself (code merged, builds, tests pass) doesn't need deployment to be "true" — but the outcome-text overclaim should be corrected regardless of deploy status, and the "real caller" gap is a functional gap independent of environment.

### PR-009 — Fix long-list/photo Chromium truncation (done, PR #419, merged)

A genuine, well-evidenced regression fix: TICK-213's stress test (80 entries × 3 families + 8 photos) exposed that Scriban's default 1 MiB template-output cap truncated the composed HTML during the third large embedded photo, silently dropping the trailing Statement-of-Truth/signature section from real Chromium-rendered PDFs. Fix (commit `f08961ea`) switches the template context to documented "unlimited output." New real-Chromium regression test added and passing (per TICK-213's own outcome text, "6/6" including this scenario). Checklist 17/17, `deployment: n/a` correctly (not yet deployed, no overclaim).

**No findings** — this is a clean, narrow fix with a real repro and a real regression test added.

**Blocked on release 12:** yes, for the fix to reach production (report rendering isn't live yet regardless — see SIMPLI-014 above).

### TICK-213 — Decide whether density applies to all rendered document bodies (done, PR #421, merged)

Decision ticket: normal/default density only, no per-caller density option exists or is planned. Its own stress test discovered the PR-009 defect (a good example of a "decision" ticket doing real verification work, not just recording a choice). `deployment: n/a` correctly — no production change, only a verification-test addition.

**No findings.**

### TICK-204 — Define assessment-report outcome variants (done, PR #412, docs-only)

FRD-11 now defines the four canonical outcomes and makes the Core-computed VAT-inclusive total the Contract repair cap. Also resolved a separate correction (PR-003) in the same PR — a small scope note, disclosed, not hidden. `deployment: n/a` correct (docs-only).

**No findings.**

### DOCS-002 — ADR-0028: renderer runs in Web Container App (done, PR #413, merged)

Clean thin-ADR ticket. `deployment: n/a` correct. ADR frontmatter/index conventions followed correctly (verified against the repo's own ADR conventions in CLAUDE.md — supersession recorded as `status: superseded` pattern was used correctly for the *linked* ADR-0013 in the related INTK-008 ticket, not this one directly).

**No findings.**

### DELIV-009 — Release 10 promotion (done, PRs #406/#407, `deployment: production` — correct, this **is** the deploy)

This is the deploy event anchor itself. `deployment: production` is correct and expected (this ticket's whole job is the promotion). Verified the post-deploy doc-refresh requirement (CLAUDE.md: *"docs/current-architecture.md… and docs/operations.md… must match the reality just shipped"*): PR #407 (`f79c24d9`) touches only `docs/operations.md`. `docs/current-architecture.md` was **not** touched by #407 — but independently verified it already correctly describes AUTO-002's authorization-code+PKCE flow (line 465: *"authorization code with PKCE after Administrator consent (ADR-0027)"*), because AUTO-002's own implementation PR (#405) evidently updated it directly. **Net: the safety-rail requirement is satisfied**, just split across two PRs rather than one — not a defect, but worth noting for anyone auditing "did #407 alone refresh both docs" (it didn't; #405 + #407 together did).

**No findings.** Correctly self-documents CI flakiness encountered during the release (hosted-runner checkout timeouts, one known deadlock-flake retry) rather than hiding it.

### AUTO-002 — Authorization-code + PKCE for MCP connectors (done, PR #405, `deployment: production` — correct)

Very well evidenced: lists the exact live verification performed (discovery endpoint, sign-in redirect, Administrator consent naming claude.ai, code exchange to `https://claude.ai/api/mcp/auth_callback`, `/mcp` with 15 tools and scope enforcement, refresh, `ActionHistory` event `automation_connector_authorized`). Entry point is concrete and real: `<origin>/mcp`, client id `pegasus-automation`. Checklist 15/17 — two open items are external/operator-side (the Claude.ai product completing the flow from the operator's own account; dropping `plain` from `code_challenge_methods_supported`), correctly not blocking `done`.

**No findings.**

### Docs-only decision cluster (TICK-099, 205, 207, 211, 212, 203, 215) — done

All seven are EPIC-004 renderer-scope decision/subsumption tickets, mostly zero-diff ("subsumed by SIMPLI-014/PR #415" or "produced no repository diff"). Spot-checked bodies and outcomes: internally consistent, correctly scoped, `deployment: n/a` throughout, no overclaims found. TICK-099 (RPT-04 diminution) is a clean, explicit deferral with a well-written "prohibited substitutes" list guarding against scope creep. No findings in this cluster.

### TICK-010 / TICK-009 — MAIL-22 / MAIL-21 (done, release 9, not this window's deploy)

Both correctly attribute their `deployment: production` to **release 9** (PR #392/#391, verified on `main` `f1e116c6`), not release 10/12 — no overclaim relative to the current deploy anchor. Included in the roster only because a `groups` field update touched their `updated` timestamp; no new work in this window. No findings.

### PLAT-001 — Claude Design UI implementation (done, PR #397, merged 2026-08-18)

**Finding: stale/missing `deployment` field.** PLAT-001's own `deployment` field is unset (`None`), but PLAT-006's ticket body — written by a different agent/session — states outright: *"Two visual defects reported by the operator against **release 10 (the first release carrying PLAT-001's Claude Design shell)**."* This is independent, cross-ticket confirmation that PLAT-001 **is** in production as of release 10, yet its own record doesn't say so. **[nit] [verified]** Remediation: set PLAT-001's `deployment` field to `production` via `update_item` (out of scope for this read-only research task — flagged for the operator/board maintainer). Checklist 55/63 — the eight open items are disclosed follow-ups (rail counts, Experian AutoCheck capability ID, case notes, unplaced marks, screenshots), not silent gaps.

### TICK-011 — INT-17 automatic VRM reading, retrospective (done, no PR, already on `main`)

A genuinely unusual but honest closure: the capability was already implemented on `main` before this ticket existed (commits `ae6f0c2d`/`ef3eb4c7`/`f7d99b18`), so the ticket is a retrospective reconciliation with no new diff. `deployment: not-deployed` is **correct and deliberately conservative** — the outcome text explicitly says *"Production caller execution was not established, so deployment is recorded as not-deployed"* even though the code is on `main`, distinguishing code-presence from a proven live caller. This is the kind of honest self-report the other findings above show is sometimes missing elsewhere (SIMPLI-014, PLAT-001) — worth noting as a positive example, not a defect.

---

## 4. Open questions / contradictions for the operator

1. **Tickets currently taken by other agents/machines — do not touch, coordinate first.** All are `codex-mcp-client` or `Codex` (a different agent identity than this session's `claude-code`), still actively worked:

   | Ticket | Assignee | Branch | Worktree |
   |---|---|---|---|
   | TICK-093 | codex-mcp-client | `task/tick-093-versioned-repair-spec` | `../pegasus-worktrees/tick-093-versioned-repair-spec` |
   | INTK-007 | Codex | `intk-007-unidentified-intake` | `.worktrees/intk-007` |
   | TICK-045 | Codex / execute_tick_045 | `task/tick-045-shared-classification-policy` | `../pegasus-worktrees/tick-045-shared-classification-policy` |
   | INTK-008 | Codex | `intk-008-image-initiated-lifecycle` | `.worktrees/intk-008` |
   | INTK-006 | Codex | `intk-006-grouped-image-routing` | `.worktrees/intk-006` |
   | TICK-046 | codex-mcp-client | `task/tick-046-classification-history` | `../pegasus-worktrees/tick-046-classification-history` |
   | INTK-005 | Codex | `intk-005-grouped-upload` | `.worktrees/intk-005` |
   | TICK-043 | codex-mcp-client | `task/tick-043-mailbox-identity` | `../pegasus-worktrees/tick-043-mailbox-identity` |
   | TICK-044 | codex-mcp-client | `task/tick-044-classification-catalogue` | `../pegasus-worktrees/tick-044-classification-catalogue` |
   | PLAT-006 | claude-code | `task/plat-006-shell-upload` | `../pegasus-worktrees/plat-006-shell-upload` |
   | TICK-033 | codex-mcp-client | `task/tick-033-request-upload-reconciliation` | `../pegasus-worktrees/tick-033` |

   None of these should be released/force-moved by another agent while taken. (PLAT-006 is `claude-code`-assigned but a *different* session than this one — same caution applies.)

2. **Systemic missing-GRANT pattern across five independent tickets in one day (TICK-093/merged, INTK-005/006/007/008 all still open).** This is the same class of defect recurring five times from what appear to be different implementing sessions — worth asking the operator whether this should become an explicit checklist/CI gate (e.g., a test that fails if a new EF migration creates a table with no matching `GRANT … TO [pegasus_web_runtime_role]` statement) rather than relying on each ticket's own reviewer to catch it. **Recommendation:** add such a gate before release 12; it would have caught all five instances mechanically.

3. **INTK-006/INTK-008's operator-notes.md "Image-initiated Case" change has no record in `docs/open-decisions.md`.** Both tickets' plans cite "the operator has clarified…" (dated 2026-08-19) as authority for a material meaning change to protected `docs/operator-notes.md` and `docs/prd/pegasus-product.md`, but the only trace of that clarification is inside the implementing tickets' own notes — `grep -i "image-initiated" docs/open-decisions.md` finds nothing. **Recommendation:** the operator should confirm this decision was actually theirs (not an agent's inference dressed as "operator clarification") before these PRs merge, and record it in `docs/open-decisions.md` per the repo's own protected-doc discipline.

4. **CLAUDE.md's Product Invariants section will contradict the shipped product once INTK-007 merges.** CLAUDE.md still lists `Needs sorting` as one of four settled terms; INTK-007's entire purpose is replacing that term with `Unidentified` throughout the product and its governing `docs/`. CLAUDE.md itself is outside `docs/` and isn't touched by any ticket in this batch. **Recommendation:** either the operator confirms CLAUDE.md's invariant line should be updated alongside INTK-007's merge (a repo-governance edit, per CLAUDE.md's own routing table — "a repository rule, convention, or process" belongs in CLAUDE.md), or confirms `Needs sorting` should remain a valid alternate term at the governance level even after the product-facing rename.

5. **PLAT-001's `deployment` field says nothing, but it's confirmed live in production via a different ticket's cross-reference (PLAT-006).** Low-stakes bookkeeping fix — flagged in §3, listed here because it's a `deployment` field claiming-nothing-when-it-should-claim-something case, the mirror image of the overclaim pattern the task asked to check for.

6. **Local main-repo checkout's `dev` branch (HEAD `4ba63888`) is one commit behind `origin/dev` (`560f741c`)** — the very last merge (PR #420/TICK-093) hadn't been fetched into the working tree at research time. Not itself a problem (this research used `git show origin/dev:<path>` throughout to compensate), but worth a `git pull` before anyone does file-level work in that checkout.

---

## 5. Implications for release 12

**Tickets whose proof depends on this deployment (release 12), or that become provable only once it ships:**
- **TICK-093** — needs the GRANT fix *before* release 12, or the very first case-assessment save in production will throw a SQL permission error via the already-wired `EfCaseAssessmentStore` write path. This is the single highest-priority item to fix before promoting `dev` to `main`.
- **PLAT-006** — its own checklist item "Deployed and confirmed on production" is explicitly pending release 12.
- **PR-009 / TICK-213 / SIMPLI-014** — the entire integrated-renderer chain is merged to `dev` but not live; release 12 is what would let anyone actually verify the Chromium-render fix and density decision in production — **except SIMPLI-014 still needs a real Web/Worker caller wired before there's anything to verify in production at all** (see §3 finding).
- **TICK-043 / TICK-044 / TICK-046** — none has `proof.md` yet; TICK-044 explicitly should not advance until its caller-wiring checklist is finished, independent of deploy timing.
- **AUTO-002 / DELIV-009** — already fully provable; no release-12 dependency (they *are* release 10).
- **TICK-033** — needs CI to confirm the two locally-timed-out integration tests before this can be considered done with real (not just local) evidence, independent of deploy.

**Tickets that can move to `done` once deployed (assuming their remaining checklist items are otherwise clean):** PLAT-006, and — once the GRANT fixes land — TICK-093, TICK-043, TICK-044, TICK-046.

**Tickets that need remediation *before* release 12, not just deployment:**
1. TICK-093 — add the missing `CaseRepairSpecifications` GRANT (blocker, live write path already exists).
2. TICK-044 — wire `MailOperationalDestinationPolicy` into the retained-mail viewer (ticket's own stated bar for passing review).
3. All four open INTK PRs (#416/417/423/424) — fix the missing GRANTs in their respective migrations before merge, address the P1 reviewer findings (especially INTK-006's premature-finalization race and INTK-008's custody-invocation gap and unreachable-manual-merge gap), and resolve the two DIRTY/CONFLICTING merge states (#417, #423, #424) before any of them can even be merged to `dev`, let alone promoted.
4. SIMPLI-014 — either wire a real caller for `GenerateAssessmentReportDraft` or correct its outcome text so it doesn't imply a reachable feature exists.
