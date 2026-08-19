# DELIV-012 research — recent-tickets.md

Anchor facts: last production deployment = release 10, `d8de29cb`, 2026-08-18T13:52Z. `origin/main` = `d8de29cb` (confirmed locally). `origin/dev` = `560f741c` (confirmed locally). `origin/kanmer-board` = `67ebb901`. Anything merged after `d8de29cb`/13:52Z is **not deployed**.

Method note on "since last deploy": `updated_since` alone is unreliable here — a board-wide `order`-field reindex touched almost every non-archived ticket's `updated` timestamp at 2026-08-19T09:39:14–15Z (confirmed via per-ticket `get_activity`: e.g. TICK-002, a `done` ticket from long before this window, shows only `update order` at that instant, nothing else). The roster below is filtered to tickets with **real** activity since the cutoff (status/doc/take/commit/PR mutations), not just an order bump.

---

## 1. Roster since the last deploy

Non-archived tickets, real activity since 2026-08-18T13:52Z, status review/verifying/done. 29 tickets.

| ID | Title | Status | Profile | Taken (branch / worktree / assignee) | PR(s) | Merged to dev? | Deployment field | Docs present | Checklist | Unresolved open-Qs |
|---|---|---|---|---|---|---|---|---|---|---|
| TICK-093 | ENG-01 — canonical repair specification | verifying | feature | task/tick-093-versioned-repair-spec / ../pegasus-worktrees/tick-093-versioned-repair-spec / codex-mcp-client | #420 | yes | not-deployed | full set | 6/6 | 0 |
| INTK-007 | Replace Needs sorting with Unidentified work | review | feature | intk-007-unidentified-intake / .worktrees/intk-007 / Codex | #424 | no (open, CONFLICTING) | — | full set | 22/36 | 0 |
| TICK-045 | MAIL-03 shared classification policy | review | feature | task/tick-045-shared-classification-policy / ../pegasus-worktrees/tick-045-shared-classification-policy / Codex | #422 | no (open, MERGEABLE/UNSTABLE) | — | full set | 12/12 | 0 |
| INTK-008 | ImageIntake image-initiated lifecycle | review | feature | intk-008-image-initiated-lifecycle / .worktrees/intk-008 / Codex | #423 | no (open, CONFLICTING) | — | full set | 8/29 | 0 |
| INTK-006 | Grouped image routing | review | fix | intk-006-grouped-image-routing / .worktrees/intk-006 / Codex | #417 | no (open, CONFLICTING) | — | full set | 26/41 | 0 |
| TICK-213 | Decide density applies to all rendered docs | done | feature | released / codex-mcp-client | #421 | yes | n/a | full set | 15/15 | 0 |
| TICK-046 | MAIL-04 classification evidence/history | verifying | feature | task/tick-046-classification-history / ../pegasus-worktrees/tick-046-classification-history / codex-mcp-client | #418 | yes | — | full set | 10/10 | 0 |
| PR-009 | Preserve post-work-list report sections | done | fix | released / codex-mcp-client | #419 | yes | n/a | full set | 17/17 | 0 |
| INTK-005 | Grouped upload | review | feature | intk-005-grouped-upload / .worktrees/intk-005 / Codex | #416 | no (open, MERGEABLE/UNSTABLE) | — | full set | 7/33 | 0 |
| PLAT-001 | Claude Design UI implementation | done | feature | released / claude-code | #397 | yes | **field absent (not set)** | full set | 55/63 | 0 (all resolved; 8 checklist gaps are named follow-ups, see §3) |
| TICK-099 | RPT-04 diminution deferral decision | done | feature | released / codex-mcp-client | — | n/a (no diff) | n/a | full set | 13/13 | 0 |
| TICK-205 | Audit does not need dual-spec/uplift | done | feature | released / codex-mcp-client | — | n/a (no diff) | n/a | full set | 16/16 | 0 |
| TICK-212 | Add report-renderer package lock files | done | feature | released / codex-mcp-client | #415 (subsumed) | yes | n/a | full set | 12/12 | 0 |
| TICK-207 | Audit reuses Inspection template | done | feature | released / codex-mcp-client | — | n/a (no diff) | n/a | full set | 13/13 | 0 |
| TICK-211 | Decide report-renderer analyzer strictness | done | feature | released / codex-mcp-client | #415 (subsumed) | yes | — | full set | 11/16 | 0 |
| TICK-203 | Reconcile renderer MCP design vs Automation Actor | done | feature | released / codex-mcp-client | #415 (subsumed) | yes | n/a | full set | 12/12 | 0 |
| TICK-043 | MAIL-01 mailbox item identity | verifying | feature | task/tick-043-mailbox-identity / ../pegasus-worktrees/tick-043-mailbox-identity / codex-mcp-client | #414 | yes | — | full set | 10/10 | 0 |
| SIMPLI-014 | Integrate CollisionRenderer behind Core render contract | done | feature | released / codex-mcp-client | #415 | yes | — | full set | 18/24 | 0 |
| TICK-215 | Decide where report rendering executes | done | feature | released / codex-mcp-client | #413 (via DOCS-002) | yes | n/a | full set | 12/12 | 0 |
| TICK-204 | Missing assessment-report outcome variants | done | feature | released / codex-mcp-client | #412 | yes | n/a | full set | 11/11 | 0 |
| TICK-010 | MAIL-22 Received/Sent categories | done | feature | released / grok-shell-kanmer | #392 | yes (release 9) | production | full set | 8/8 | 0 |
| TICK-009 | MAIL-21 shared classification foundation | done | feature | released / grok-shell-kanmer | #391 | yes (release 9) | production | full set | 12/12 | 0 |
| DOCS-002 | ADR-0028 Web Container App as renderer | done | chore | released / codex-mcp-client | #413 | yes | n/a | full set | 11/11 | 0 |
| DELIV-009 | Release 10: dev→main + connector deploy | done | chore | released / claude-code | #406, #407 | yes, **and to main** | production | plan/checklist/proof only | 10/10 | n/a (no open-questions doc) |
| AUTO-002 | Auth-code + PKCE for external MCP connectors | done | feature | released / claude-code | #405 | yes, **and to main** (part of release 10) | production | no open-questions doc | 15/17 | n/a |
| TICK-011 | INT-17 vehicle-registration reading (retro) | done | feature | released / (unassigned) | none (retro reconciliation) | already on main | not-deployed | no open-questions doc | 10/10 | n/a |
| TICK-044 | MAIL-02 classification destination mapping | verifying | feature | task/tick-044-classification-catalogue / ../pegasus-worktrees/tick-044-classification-catalogue / codex-mcp-client | #411 | yes | — | full set | 12/18 | 0 |
| PLAT-006 | Centre shell + redesign Upload screen | verifying | fix | task/plat-006-shell-upload / ../pegasus-worktrees/plat-006-shell-upload / claude-code | #409 | no (open, CLEAN via PR#410 chain — PR #409 itself still open) | — | files/plan/checklist/PIR/scratch only | 9/10 | n/a (no open-questions doc) |
| TICK-033 | INT-31 temporary revocable upload link | verifying | feature | task/tick-033-request-upload-reconciliation / ../pegasus-worktrees/tick-033 / codex-mcp-client | #408 | yes | — | no proof doc | 4/5 | n/a (no open-questions doc) |

Open PRs to `dev` right now (`gh pr list --state open`): **#416** INTK-005 (MERGEABLE/UNSTABLE), **#417** INTK-006 (CONFLICTING/DIRTY), **#422** TICK-045 (MERGEABLE/UNSTABLE), **#423** INTK-008 (CONFLICTING/DIRTY), **#424** INTK-007 (CONFLICTING/DIRTY). **#410** is the dev→main release vehicle (CLEAN/MERGEABLE, head=`dev`). Three of the five task PRs are already in merge conflict against current `dev` — a real integration-order problem for release 12, not just a CI question.

Note: DELIV-012's own ticket body (written earlier today) says "six task PRs are open in Review" — by the time this research ran, PR #420 (TICK-093) had merged and TICK-093/043/044/046 had advanced to `verifying`, leaving five open PRs. Treat the ticket body's count as stale relative to this research's snapshot.

---

## 2. Per-ticket detail — open PRs

### INTK-005 (PR #416) — "grouped upload"

**What it does:** Lets the authenticated Upload page accept multiple files in one POST. Adds a Core `GroupedIntake` orchestration calling the existing single-file `IIntakeSubmission` once per ordered member (deterministic `token:ordinal` child identity), a new EF-backed `IntakeSubmissionGroups`/`IntakeSubmissionGroupMembers` schema, and a new `/Upload/Group/{id}` status page listing every member with a link to its own receipt-status page. 16 files, single commit `ed04f498`.

**Reviewer comments** (bot, all on `ed04f498`, 2026-08-19T10:35:35–36Z — single-commit PR, **no follow-up push, none addressed in this PR**):
- P1 `Upload.cshtml:36` — Kestrel's `MultipartBodyLengthLimit` (`Program.cs:504`, ~10 MiB+64 KiB) isn't raised, so two valid ≤per-file-limit images can still overflow the whole-request cap and be rejected before `OnPostAsync` runs. [verified — `Program.cs:504` unchanged].
- P2 `UploadGroupStatus.cshtml:12` — no `data-auto-refresh`, rows stay stale until manual reload. [verified].
- P2 `EfIntakeSubmissionGroupStore.cs:122` — concurrent same-token member insert can hit the unique `(GroupId, Ordinal)` index with no retry. [suspected, needs check — re-flagged again on PR #417, suggesting still open].
- P2 `Upload.cshtml.cs:129` — redirect always opens the generic group page, dropping "already received" duplicate messaging on exact replay. [suspected — resolved only for the *single-file* case, in PR #417's remediation commit, not here].
- P1 `GroupedIntake.cs:128` — single-file uploads now get identity rewritten from `token` to `token:0`, breaking existing callers/tests keyed on the plain token. [verified not fixed in this PR — PR #417's commit `866d305e` is what restores the plain-token path].

**Plan vs ticket:** plan's 8 steps map cleanly to the ticket's 7 verification bullets; nothing unaddressed.

**Implementation vs plan:** spot-checked — migration `20260819101344_GroupedIntakeSubmission.cs` matches plan step 3 exactly (both tables, unique indexes, FK-restrict); `Upload.cshtml.cs`/`UploadGroupStatus.cshtml.cs` match steps 5/6. File list (16 files) is a 1:1 match to the plan's own inventory — **no scope drift**.

**Simplification pass:** recorded, dated, honest (reused `IIntakeSubmission`, no second batching framework, reused status queries; one open caveat logged — full integration suite crashed after 61 tests — not hidden).

**Deployment risk:** [verified] `20260819101344_GroupedIntakeSubmission.cs` `Up()` creates both new tables with **no GRANT statements at all**, while `EfIntakeSubmissionGroupStore.cs` reads/writes both at runtime — the same TICK-093 pattern (confirmed as an omission, not project convention, by finding matching GRANT/DENY blocks in three sibling recent migrations). CI is red: `sql-integration (1/2/3)` all fail — consistent with the runtime role being unable to reach the new tables. `mergeStateStatus: UNSTABLE`, `mergeable: MERGEABLE`, no human review.

**Checklist:** 7/33. **Open questions:** 0 unresolved.

### INTK-006 (PR #417) — "grouped image routing"

**What it does:** Makes the *group* (not one image) the VRM-routing unit. `ImageIntakeAutomation.ApplyAsync` looks up the INTK-005 group, waits until every member has terminal recognition, then runs a new `ImageIntakeGroupRoutingPolicy.Evaluate` decision table (AssociateExistingCase / HandOffToImageIntake / RouteToUnidentified / TechnicalFailure / two waiting states) and applies the outcome to every member. `OnnxVrmRecognitionEngine` now distinguishes `detector_no_plate` from `recognizer_no_readable_text`. Ticket scope was explicitly narrowed mid-flight (own "Scope split" note): Image-initiated Case lifecycle/persistence deferred to INTK-008, Unidentified routing to INTK-007 — this PR claims only grouped recognition + diagnostics + the single-VRM/single-eligible-case path. Branched from INTK-005's single commit; inherits its files.

**Reviewer comments** (bot; round 1 10:54:14Z on `70d7c89c`, round 2 11:26:15Z on `599bfe6d`, after remediation commit `866d305e` at 11:17:12Z):
- P1 `ImageIntakeAutomation.cs:182` — `members.Count` used as both actual and expected count — premature finalization if the group isn't fully persisted yet. [verified, still unfixed — no declared-size check added].
- P1 `Upload.cshtml:36` — same aggregate multipart-limit issue as INTK-005. [verified unfixed].
- P2 `UploadGroupStatus.cshtml:14` — same missing `data-auto-refresh`. [verified unfixed].
- P2 `ImageIntakeAutomation.cs:150` — non-image (PDF/Word) group members passed into image recognition. [suspected, needs check].
- P2 `GroupedIntake.cs:131/129` — duplicate/replay messaging: **fixed for single-file** (redirect now carries `duplicate=received.IsDuplicate`), **still lost for the grouped path** (`UploadGroupStatus.cshtml` never reads member `IsDuplicate`). [verified, partial].
- P2 `ImageIntakeAutomation.cs:150` — N² ONNX rescanning: every member's automation hook reruns recognition over the whole group, no "already resolved" short-circuit. [verified unfixed].
- P1 `ImageIntakeGroupRouting.cs:80` — new behaviour table has no owning FRD. [verified — zero `docs/frd/*` files touched; plan explicitly defers doc reconciliation to INTK-008].
- P1 (no line) — confident single-VRM/zero-or-multi-eligible-case groups exited before registering. [verified fixed — `HandOffToImageIntake` is not excluded by the early-return guard and its registration runs].
- P1 `Upload.cshtml.cs:154` — single-file callers broken by group-route redirect. [verified fixed — remediation commit `866d305e` restores the `Length==1` → `/UploadStatus` route].
- P1 `ImageIntakeAutomation.cs:205` — recoverable persistence failure mid-loop swallowed, group marked complete anyway. [suspected, needs check].
- P1 `ImageIntakeAutomation.cs:200` — ambiguous-candidate handoff still calls the auto-associator, which can pick a near-match. [suspected, needs check — round-2 comment, no round-3 review to confirm].
- P2 `Upload.cshtml.cs:104` — retrying with a different file count changes the identity namespace (`token` vs `token:0`). [verified — inherent design consequence, no reconciliation logic].
- P2 `EfIntakeSubmissionGroupStore.cs:118` — same unaddressed concurrent-insert gap as INTK-005.

**Plan vs ticket / implementation vs plan:** plan's 10 steps match the ticket's already-narrowed "Scope split" addendum; checklist honestly leaves the Image-initiated-Case branch, telemetry, and most of the 10-case routing-matrix tests unchecked rather than claiming false completion. Spot-checked steps 2/4/7 (decision table, group aggregation, existing-Case branch reuse) all match the diff.

**Simplification pass:** recorded ("Execution boundary and simplification pass — 2026-08-19"), honest — explicitly states the Image-initiated Case branch was deliberately left unimplemented as a policy-seam decision, not hidden.

**Scope drift:** none beyond what the ticket's own "Scope split" note already authorizes.

**Deployment risk:** [verified] same unmodified migration as INTK-005, same GRANT gap (fix once, both PRs inherit it once rebased). **No CI checks ran at all** (`gh pr checks 417` → "no checks reported"); `mergeStateStatus: DIRTY`, `mergeable: CONFLICTING` — cannot currently be evaluated or merged. `Program.cs` DI-registers `SubmitGroupedIntake`/`IGroupedIntakeSubmission` correctly; no Worker registration gap (feature is Web-only, consistent with scope).

**Checklist:** 26/41. **Open questions:** 0 unresolved.

**Verified-findings list — INTK-005 / INTK-006:**
1. **[verified] blocker** — `20260819101344_GroupedIntakeSubmission.cs` `Up()`: no GRANT statements for `IntakeSubmissionGroups`/`IntakeSubmissionGroupMembers`, read/written at runtime by `EfIntakeSubmissionGroupStore.cs`. **Remediation:** add `migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[IntakeSubmissionGroups] TO [pegasus_web_runtime_role];")` and the equivalent for the members table (SELECT/INSERT at minimum), following the pattern in `20260819104953_MailClassificationCorrectionHistory.cs:99-105`. Test: run the `sql-integration` job locally against a fresh migrated DB as the `pegasus_web_runtime_role` login and confirm the currently-failing `sql-integration (1/2/3)` checks go green. Fix once upstream in INTK-005 — INTK-006 inherits it.
2. **[verified] blocker** — `Program.cs:504` `MultipartBodyLengthLimit` unchanged while the Upload page now advertises per-file limits with no aggregate-cap warning. **Remediation:** raise the limit (per-file max × a reasonable max file count) or make it configurable; add a friendly `ModelState` error for the aggregate-too-large case instead of a raw 413. Test: submit two files each under the per-file limit but combined over ~10 MiB via a web integration test, assert 200 with a clear validation message.
3. **[verified] blocker** — INTK-006 `ImageIntakeAutomation.cs:~150-182` (`TryApplyGroupAsync`): live `members.Count` used as `expectedMemberCount`, so a group can look "complete" before all members are persisted if a worker races the multi-file POST. **Remediation:** persist a declared/expected member count at group-creation time (known from the form's file list) and pass that instead of the live count. Test: add group members one at a time with an automation-hook call interleaved after the first, assert no routing decision fires until all declared members exist.
4. **[verified] should-fix** — INTK-006 `ImageIntakeAutomation.cs` `TryApplyGroupAsync`: no memoization once a group outcome is persisted → O(N²) ONNX rescans. **Remediation:** check for a persisted group-outcome row before re-running `ScanAsync` for already-resolved members; test asserting the recognizer runs exactly once per member across N automation-hook calls.
5. **[verified] should-fix** — `UploadGroupStatus.cshtml` (both PRs): missing `data-auto-refresh`; multi-file duplicate replay loses the "already received" message. **Remediation:** add the existing `data-auto-refresh` convention (see `site.js`) and wire `member.IsDuplicate` into the per-row display.
6. **[suspected, needs check]** — round-2 INTK-006 comments (mid-loop persistence-failure swallowing at `:205`, ambiguous-candidate auto-associator at `:200`) were not independently re-confirmed against current code — re-check before treating as resolved or open.

**Cross-ticket note:** #416 and #417 are effectively one branch stack (417 is based on 416's single commit); INTK-005's unresolved P1s (multipart limit, migration GRANTs) propagate unchanged into INTK-006 and should be fixed once, upstream, not duplicated per-PR.

### INTK-007 (PR #424) — "Replace Needs sorting with referenced Unidentified work"

**What it does:** Adds a Core-owned `Unidentified` aggregate replacing the broad `NeedsSorting` intake destination: six-code reason taxonomy, Open/Resolved state, immutable sequential `U<n>` references (single or group origin), EF persistence (3 new tables: `UnidentifiedItems`, `UnidentifiedSequences`, `UnidentifiedHistory`) with serializable allocation, a legacy-data backfill migration, routing of terminal `ProcessIntake` outcomes into registration, a Web queue/detail/resolution UI, a dashboard metric, and MCP list/get/resolve tools. 49 files, +8346/-48, single commit `abd8a923`.

**Reviewer comments** — `chatgpt-codex-connector[bot]`, all at 2026-08-19T12:16:32–33Z (after and last event on the PR — **none addressed**, no commit since): 6×P1 / 8×P2:
- P1 `ProcessIntake.cs:258` — below-threshold image intake bypasses Unidentified registration.
- P1 `EfUnidentifiedStore.cs:174` — resolution destination accepted as free-form text, no destination-port validation.
- P1 `Mail/Message.cshtml.cs:114` — legacy `NeedsSorting` mapped directly to `Unidentified`, risking state collapse.
- P1 `ProcessIntake.cs:256` — transient/retryable reader failures can allocate a permanent U-reference.
- P1 `ProcessIntake.cs:243` — reevaluation to a non-Unidentified outcome doesn't close the existing U-item.
- P1 `ProcessIntake.cs:268` — all NeedsSorting outcomes hardcode `NoUsableIdentification`, discarding the other 5 canonical reason codes.
- P1 migration `:184` — backfilled rows get an all-zero fingerprint, mismatching on reevaluation replay (false conflicts).
- P2 `EfUnidentifiedStore.cs:148` — replay comparison ignores `TargetKind`/`TargetReference`.
- P2 `Details.cshtml.cs:63` — no stable `OperationKey` on the resolution form (replay-safety broken).
- P2 `Details.cshtml:25` — detail page omits filenames/custody/processing evidence the FRD requires.
- P2 `EfUnidentifiedStore.cs:101` — 1000-char `SafeDetail` copied into a 500-char `Reason` column (truncation/overflow risk).
- P2 `UnidentifiedMcpTools.cs:55` — numeric strings silently parse as a valid state filter instead of being rejected.
- P2 migration `:175` — backfill reads generic `DecisionReason` instead of the more precise `FailureReason`.
- P2 `UnidentifiedMcpTools.cs:52` — `state` filter defaults to both Open+Resolved rather than Open-only.
- P2 `ProcessIntake.cs:277` — uses `ProcessedAtUtc` instead of received time, contradicting the oldest-first queue UI.
- P2 `Details.cshtml:35` — raw enum/UTC timestamp rendered, bypassing `OperatorLabels`/London time.

**Plan vs ticket:** plan's 10 steps match ticket scope. Step 5 ("route every producer") is honestly only partially done — mail/retained-mail/Operations projections explicitly left incomplete in the checklist.

**Simplification pass:** recorded, dated 2026-08-19, honest disposition (no further simplification found; remaining scope named, not hidden).

**Scope drift:** the `docs/operator-notes.md` / `docs/prd/pegasus-product.md` edits replace `Needs sorting`/`Blocked intake` vocabulary with `Unidentified`/`Image Intake` — this changes wording this repo's own CLAUDE.md quotes as a settled invariant ("`Audit`, `Triage`, `Needs sorting`, and `Blocked intake` retain their settled distinct meanings"). The ticket claims explicit operator instruction backs this, but it is protected content — **flag to the operator, do not treat as pre-approved** (see §4).

**Deployment risk:** [verified] migration `20260819115323_UnidentifiedWork.cs` creates 3 new tables with **no GRANT** to any runtime role — same gap pattern as TICK-093. The matching checklist item ("Add migration, deterministic legacy backfill, canonical reason mapping, snapshot, and required runtime grants") is honestly left unchecked. `gh pr checks 424` → no checks ran at all.

**Checklist:** 22/36. **Open questions:** 0 unresolved.

### INTK-008 (PR #423) — "Give ImageIntake an Image-initiated Case lifecycle"

**What it does:** Turns `ImageIntake` into an explicit lifecycle projection (`AwaitingInstruction` → `MergedIntoInstructionCase` | `StaffClosed`), VRM-keyed reference reuse, append-only lifecycle events, a migration adding lifecycle columns + `ImageIntakeLifecycleEvents`, wiring `ImageIntakeCasePairing` auto-link into the merge transition, a VRM-keyed custody target, Web list/detail/staff-close UI, and ADR-0029 superseding ADR-0013. 33 files, +7301/-42, 4 commits (last 11:43:30Z).

**Reviewer comments** — bot, all at 11:49:05–06Z (after last commit — **none addressed**): 6×P1 / 6×P2:
- P1 migration `:33` — backfill marks every existing row `awaiting_instruction` even where a formal link already exists.
- P1 `ImageIntakeCasePairing.cs:77` (×2) — auto-link/lifecycle-merge not atomic; **manual** staff link/reversal never invokes the new lifecycle transition at all.
- P1 `CustodyContracts.cs:41` — `IImageIntakeCustody.CreateOrGetRootAsync` has no caller anywhere — custody is never actually established.
- P1 `EfImageIntakeStore.cs:264` — `MergeAsync`/`CloseAsync` bypass `ValidateMerge`/`ValidateClose`, so an over-length reason fails at SQL instead of returning a clean validation error.
- P1 `docs/capabilities.md:215` — normative lifecycle behaviour written into capabilities.md (schedule/registry only per CLAUDE.md), table malformed between INT-28 and MAIL-01.
- P1 `docs/adr/README.md:30` — ADR-0013 frontmatter marked superseded but its row is still listed under "Current architecture decisions (accepted)".
- P1 `CONTEXT.md:148` — adds normative lifecycle requirements to the terminology doc, duplicating FRD ownership.
- P2 `Index.cshtml.cs:78` — exact-reference search rebuilds the summary via the old 7-arg constructor, losing real lifecycle state (defaults to AwaitingInstruction).
- P2 `ImageIntakeContracts.cs:97` — "Awaiting instruction" queue still filters on `AssociatedCaseId is null`, not lifecycle state — `StaffClosed` records leak back in.
- P2 `EfImageIntakeStore.cs:325` — replay-by-operation-key doesn't check the replayed request matches the original.
- P2 `Details.cshtml.cs:79` — `DbUpdateConcurrencyException` uncaught → HTTP 500 instead of a conflict UI on stale-close.
- P2 `Details.cshtml:38` — raw enum/snake_case event codes rendered instead of operator labels.

**Plan vs ticket:** plan step 6 (wire pairing into lifecycle merge) only half-done (manual path not wired) despite the checklist claiming "Wired reverse accepted-Case pairing" — checklist self-assessment reads optimistic here. Step 7 (custody seam) checked done in checklist, but the adapter is never invoked per the bot finding.

**Simplification pass:** **not recorded** — no dated section found; checklist item "Run simplification pass and record dispositions" is unchecked.

**Scope drift:** same protected `docs/operator-notes.md` caveat as INTK-007 (new "Image-initiated Case clarification" section) — flag to operator, don't judge correctness.

**Deployment risk:** [verified] migration `20260819112914_ImageInitiatedLifecycle.cs` — **no GRANT** statements, same gap as INTK-007. [verified] **CI is red**: `sql-integration (2)` failed — `QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate` (expected Succeeded, actual Pending) and `ImageIntakeWebTests.StaffRegistersAnImageOnlyReceiptAndFindsItEverywhere` (missing "awaiting definitive instruction" string) — both plausibly caused by the custody-not-invoked / manual-lifecycle-not-wired gaps above [suspected, needs check for exact causal link].

**Checklist:** 8/29. **Open questions:** 0 unresolved.

### Verified-findings list — INTK-007 / INTK-008

1. **[verified] blocker** — `20260819115323_UnidentifiedWork.cs` (INTK-007) and `20260819112914_ImageInitiatedLifecycle.cs` (INTK-008): new tables/columns carry no `GRANT` to the Web/Worker runtime SQL role. **Remediation:** add a `migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE ON ... TO pegasus_web_runtime_role")` block per new table, following the pattern in `20260814092852_AddWorkerCaseCreationGrants.cs`; add a runtime-grant integration test asserting the app's SQL login can read/write the new tables post-migration.
2. **[verified] blocker** — INTK-008 `ImageIntakeCasePairing.cs:77`: manual staff link/reversal (`Pages/Intake/Details.cshtml.cs`) never calls the lifecycle merge transition, so staff-created associations stay `AwaitingInstruction` forever. **Remediation:** call the same merge/lifecycle-transition use case from the manual link handler that automatic pairing uses, guarded by the same replay/CAS rules.
3. **[verified] blocker** — INTK-008 `CustodyContracts.cs:41`: `IImageIntakeCustody.CreateOrGetRootAsync` has zero callers. **Remediation:** invoke custody creation/transfer from the ImageIntake registration flow (mirroring formal-Case custody invocation) before marking a group `AwaitingInstruction`.
4. **[verified] should-fix** — INTK-007 `ProcessIntake.cs:268`: all NeedsSorting-origin outcomes hardcode `NoUsableIdentification`. **Remediation:** thread the actual assessment outcome (unreadable/conflicting/ambiguous/technical) into the registration call instead of one fallback code.
5. **[verified] should-fix** — INTK-007 `EfUnidentifiedStore.cs:174`: resolution accepts any non-empty `TargetId` with no destination-port validation. **Remediation:** validate `TargetId`/`TargetKind` against the actual destination store before committing the resolve transaction.
6. **[suspected, needs check]** — INTK-008's two CI failures likely trace to findings #2/#3; confirm by re-running those tests after the fixes land.

Both PRs' `docs/operator-notes.md` edits should be routed to the operator for explicit resolution before merge (§4) — plausible and ticket-scoped, but they change text CLAUDE.md itself quotes as a settled invariant.

### TICK-045 (PR #422) — "MAIL-03 shared classification policy across mailboxes"

**What it does:** Two-file, evidence-only change — adds one integration test (`RetainedMailPersistenceTests.cs`) exercising the existing MAIL-04 `CorrectRetainedMailClassification` command across two mailbox identities, and updates the MAIL-03 row in `docs/capabilities.md`. No production Core/Infrastructure/Web change, no migration, no DI registration. MAIL-03 correctly reuses the MAIL-04 Core owner.

**Reviewer comments** — bot, both at 2026-08-19T11:38:19Z (after the PR's only commit, 11:33:08Z — **neither addressed**):
- P1 `RetainedMailPersistenceTests.cs:345` — test seeds a hand-built `MailClassificationResult.Ambiguous(...)` for both mailboxes rather than exercising the classification policy itself; only the correction path is driven.
- P1 `RetainedMailPersistenceTests.cs:320` — second mailbox `claims@collisionengineers.co.uk` is not one of the four documented mailboxes (`operator-notes.md:413`: desk/engineers/info/instructions).

**Plan vs ticket:** matches scope well. Step 7 ("update capabilities only to the evidence tier reached") — the new `docs/capabilities.md` wording ("proves identical validation...") **overstates** what's tested given the P1 above: identical *correction*, not classification-policy determination, is what's actually proved.

**Simplification pass:** recorded, dated, honest ("no code changes required after the pass").

**Scope drift:** none.

**Deployment risk:** no migration, no config change. `grep MailOperationalDestinationPolicy` over the diff → zero matches: **this PR does not wire the caller** — the policy remains dark after TICK-045, same as after TICK-044 (consistent with this ticket's stated scope, but the dark-code condition stays unresolved and should be tracked against whichever ticket is meant to add the real caller). `mergeStateStatus: UNSTABLE` — `sql-integration (1)` pending/`infrastructure` skipping at last check; no human review recorded.

**Checklist:** 12/12. **Open questions:** 0 unresolved.

**Verified-findings:**
1. **[verified] should-fix** (borderline blocker — affects a canonical-doc claim) — `RetainedMailPersistenceTests.cs:~330-345`: test seeds a fabricated ambiguous result instead of deriving it from the classification policy, then `docs/capabilities.md`'s MAIL-03 row (~line 212) claims this "proves identical validation." **Remediation:** reword the capabilities.md line to say the test proves identical *correction/persistence* behaviour, not classification-policy validation (smaller, plan-respecting fix) — or extend the test to actually evaluate the policy for both mailboxes first.
2. **[verified] should-fix** — `RetainedMailPersistenceTests.cs:317-319`: uses an undocumented `claims@` mailbox. **Remediation:** change to one of the four documented mailboxes (e.g. `engineers@collisionengineers.co.uk`); re-run the focused test filter.
3. **[verified] nit** — PR is UNSTABLE with a pending SQL job and no human review; re-poll CI before merge, not a code defect.
4. **[suspected, needs check]** — capabilities.md wording/tense consistency vs sibling MAIL-02/04 rows, cosmetic only.

---

## 3. Per-ticket detail — verifying/done since the deploy

### MAIL/ENG cluster (verifying)

**TICK-093 — ENG-01 canonical repair specification.** PR #420 merged to dev. `deployment: not-deployed`; no `proof` doc (expected pre-done); PIR correctly states "Not deployed; no cloud or `main` write." No entry point stated. Open-questions clean. Checklist 6/6.
- **[verified] blocker** — `20260819112640_VersionedRepairSpecifications.cs` `Up()` creates `CaseRepairSpecifications` with **no GRANT statements**, while `EfCaseAssessmentStore.cs:117` (`AnyAsync`) and `:135` (`Add`) read/write it from the Web-role path. **Remediation:** add a follow-up migration granting `SELECT, INSERT, UPDATE` to `pegasus_web_runtime_role` on `CaseRepairSpecifications` (pattern: `20260803205759_SendToAiAssessmentToolset.cs`), verify against a role-restricted test DB before deploy.
- **[verified] should-fix** — `IRepairSpecificationStore`/`EfRepairSpecificationStore` have no DI registration and no non-test caller — dark code delivered as "implemented." **Remediation:** register and wire a real caller (per TICK-092 dependency), or state explicitly in the PIR that this is scaffolding with no live caller.

**TICK-043 — MAIL-01 mailbox item identity.** PR #414 merged to dev. No `proof` doc; PIR: "Deployment and fresh live-mailbox verification are not claimed." Entry point: reuses the existing `Message.cshtml.cs`/Graph poll caller (not new). Open-questions clean. Checklist 10/10. No new blocker/should-fix findings in this pass.

**TICK-044 — MAIL-02 classification destination mapping.** PR #411 merged to dev. No `proof` doc; PIR makes no deploy claim ("No live mailbox/cloud operation ran"). Entry point: **none live** — retained-mail viewer wiring explicitly not done. Checklist **12/18** — six unchecked items are exactly the missing-caller work (wire `MailOperationalDestinationPolicy` into the retained-mail projection, carry values to list/detail, viewer display, integration/Web tests, post-deploy read-only check).
- **[verified] blocker** — `MailOperationalDestinationPolicy.cs:24` (static class) has zero references anywhere in `src/` outside its own declaration — genuinely dark. The checklist honestly admits this; the PIR's "Delivered..." framing risks overstating completion relative to it. **Remediation:** do not treat this ticket as ready for `done` until the caller-wiring checklist items land; treat the current PIR as partial.

**TICK-046 — MAIL-04 classification evidence/correction history.** PR #418 merged to dev. No `proof` doc; PIR: "locally built and SQL/fake-backed, not deployed or verified against a live mailbox" — no false deploy claim. Entry point: **real, wired** — new POST correction handler on `Message.cshtml(.cs)`, confirmed DI-registered; migration `20260819104953_MailClassificationCorrectionHistory.cs` grants `SELECT, UPDATE` on `IntakeMailClassificationDecisions` and `SELECT, INSERT` on `IntakeMailClassificationHistory` to `pegasus_web_runtime_role`, with `DENY UPDATE, DELETE` on the history table (this is the one migration in the whole cluster that got its GRANTs right). Open-questions clean. Checklist 10/10.
- **[verified] should-fix** — `docs/current-architecture.md:85` states *"Both are read-only: the pages carry no handler, and the Web runtime role holds `SELECT` alone on the retained-mail tables."* **Stale**: TICK-046 added a real POST handler (`OnPostCorrectClassificationAsync`, `Message.cshtml.cs:123`) and the migration grants `SELECT, UPDATE` on `IntakeMailClassificationDecisions` plus `SELECT, INSERT` (with `DENY UPDATE, DELETE`) on the new `IntakeMailClassificationHistory` table — both clauses of the line-85 sentence are now false for `/Inbox/{id}` specifically (`/Inbox`'s list GET remains correctly described as read-only). **Remediation:** edit only the `/Inbox` bullet at line 85 to state the new POST handler and the expanded grant set; do not touch surrounding bullets, and do not add "deployed"/"production" language — this is a source-state correction, required by this repo's own safety-rails rule that current-architecture stays in sync with what merged.
- **[nit] verified** — TICK-046's labels still include `blocked` and `requires-live-approval` despite `blocked:false` and the ticket sitting in `verifying` past review/merge. Board-hygiene only; raise at next groom pass.

Blocked on release 12 for proof: **TICK-093, TICK-043, TICK-044, TICK-046** — all merged to dev only, no `proof.md`, none falsely claims deployment.

### PLAT-006, TICK-033, SIMPLI-014, PR-009

Cutoff check: none of these tickets' commits are ancestors of `d8de29cb` (verified via `git merge-base --is-ancestor`). None of the four claims "deployed"/"production" anywhere — all explicitly disclaim it.

**PLAT-006 — verifying, PR #409 open (not merged).** No `proof` doc; ticket's own verification checklist correctly leaves "Deployed and confirmed on production" unchecked. Entry point: `/Upload` and `/Uploads/{token}` pages, shell-wide `.app-rail-main` layout. Checklist 9/10 (open item = "PR to dev, review, merge" — still open). No open-questions doc.
- Spot-checks all **[verified]**: `site.css:416` `.app-rail-main { margin-inline: auto; }` present as claimed; `site.css:1958-1959` `.has-file` declared before `.is-dragover` (claimed simplification fix); `site.js:119-165` dropzone enhancement block present as described.
- No blocker/should-fix found; nit only (pre-existing, out-of-scope `/Cases/Create` 500 without `receiptId`, correctly deferred).

**TICK-033 — verifying, PR #408.** No `proof` doc (docs-only profile); PIR explicitly: "No live activation, production custody test, browser acceptance exercise, cloud mutation, or operator acceptance was performed." Entry point: reaffirms the existing `/Uploads/{token}` caller — docs reconciliation only. Checklist 4/5 — open item is "run focused request-upload integration tests" (exceeded local 2-min timeout, no recorded result; honestly flagged in PIR/plan).
- **[verified]** `docs/capabilities.md:200` INT-31 row updated exactly as PIR claims; commit `f43e3a2b` (removes dead Box-file-request UI/path) confirmed in history.
- **[verified] should-fix** — checklist item 2 open, no verdict recorded. **Remediation:** rerun `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~DocumentCustodyDurabilityTests"` on CI or a machine with headroom; tick the box or file the real failure.

**SIMPLI-014 — done, PR #415 merged to dev `b548b674` (2026-08-19T10:29Z).** proof.md explicitly disclaims Azure deployment/production Chromium health/live caller; credible. Entry point: **none live** — Core `AssessmentReportRendering` use case + `PlaywrightAssessmentReportRenderer` adapter composed only in Web, no HTTP/MCP caller wired (deferred to DOCS-001). Open-questions fully resolved. Checklist 18/24 (remaining 6 are closeout bookkeeping, not substantive work).
- **[verified]** `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` exists with the claimed `AssessmentReportContract`; `workspaces/report-renderer/` confirmed absent from dev (workspace correctly retired).
- No blocker/should-fix found.

**PR-009 — done, PR #419 merged to dev `4f67a83e` (2026-08-19T11:21Z).** proof.md disclaims Azure deployment/live caller; credible. Entry point: none new — internal fix to the not-yet-live SIMPLI-014 renderer path. Open-questions clean. Checklist 17/17.
- **[verified]** `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs:111` — `LimitToString = 0` matches the claimed Scriban 1 MiB truncation fix exactly.
- No blocker/should-fix found.

Blocked on release 12 for proof: **PLAT-006, TICK-033, SIMPLI-014, PR-009** (all correctly self-report as not-deployed/dev-only).

### TICK-213, TICK-204, DOCS-002, DELIV-009, AUTO-002

`origin/main` locally confirmed at `d8de29cb` — matches DELIV-009's claimed deployed SHA exactly (it's the AUTO-002 PR #405 merge commit).

**TICK-213 — done, PR #421, merged dev `4ba63888`.** `deployment: n/a`, correct — proof.md: "no production, styling, density, deployment, cloud or `main` change was made"; test-file-only change (1 file). No entry point (internal test regression). Open-Qs clean; one item correctly parked. Checklist 15/15. No findings.

**TICK-204 — done, PR #412.** `deployment: n/a`, docs-only (single FRD file, 33 insertions); `gh pr checks 412` shows build/browser/SQL jobs correctly skipped by the change classifier. Entry point: none — explicitly disclaims renderer implementation/caller/Azure/live verification. Open-Qs: single item ticked with an explicit operator resolution quote. Checklist 11/11. No findings.

**DOCS-002 — done, PR #413, merged dev `4d1bff3d`.** `deployment: n/a`. **[verified]** `docs/adr/0028-run-integrated-renderer-in-web-container-app.md` exists, frontmatter `status: accepted`, `date: 2026-08-19`, indexed at `docs/adr/README.md:41`. Entry point: none — architectural decision only. Open-Qs/Parked empty. Checklist 11/11. No findings.

**DELIV-009 — done, release-10 dev→main promotion, PRs #406/#407.** `deployment: production` — quote: "`main` = `dev` = `d8de29cb`... web revision `pegasus-prod-web-252ow37gij--d8de29cb94f3`... Worker redeployed via `config-zip`... smoke passed." **Credibility: high** — `origin/main` HEAD locally confirmed exactly `d8de29cb94f396816595b1f9782980476166dbfa`. Entry point: the `/authorize` OAuth flow for the Claude.ai MCP connector (evidence delegated to AUTO-002). Checklist 10/10. No findings.

**AUTO-002 — done, PR #405, shipped via release 10.** `deployment: production` — quote includes a full request/response table (discovery, sign-in redirect, consent, code exchange, `/mcp` 15 tools, scope refusal, refresh) plus an addendum on the actual Claude.ai connection. **Credibility: high**, and code-level spot-check confirms the mechanism exists, not just the docs: **[verified]** `AuthorizationEndpointPath = "/authorize"` and `RedirectUris` parsing in `AutomationMcp.cs:18,44,99`; `AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange()`/`AllowRefreshTokenFlow()` in `AutomationMcpExtensions.cs:40,46-47`; consent pages `Pages/Connect/Authorize.cshtml(.cs)` exist; Bicep params present in `infra/main.bicep`, `infra/main.parameters.json`, `infra/modules/platform.bicep`; ADR-0027 accepted, indexed. Entry point: `/authorize` → `/mcp` (15 tools, scope-enforced), client id `pegasus-automation`.
- **[verified] should-fix** — checklist 15/17, two unchecked items ("Independent review; merge." / "Release 10 promotion...") are stale checkbox hygiene — the Progress notes beneath them already confirm both happened. **Remediation:** tick the two boxes in the Kanmer checklist doc; no code change.

Not blocked on release 12: TICK-213, TICK-204, DOCS-002 (correctly `n/a`, docs/test-only) and DELIV-009, AUTO-002 (correctly `production`, independently verified against `main` HEAD and repo source).

### Docs-decision cluster and older/already-deployed tickets

Confirmed: none of TICK-099/205/207/211/212/203/215 ships code — each is a zero-diff decision/reconciliation record (several explicitly "subsumed by SIMPLI-014/PR #415, produced no repository diff, commit, PR, deployment, cloud action, or `main` update"). All have clean, fully-resolved open-questions docs. `deployment` is `n/a` or unset across the cluster, correctly.

| ID | Status | Deployment claim | Entry point | Checklist | Flags |
|---|---|---|---|---|---|
| TICK-099 | done | n/a | none (RPT-04 explicitly deferred/unsupported) | 13/13 | none |
| TICK-205 | done | n/a | none (decision record only) | 16/16 | none |
| TICK-207 | done | n/a | none (decision record only) | 13/13 | none |
| TICK-211 | done | none set | none (zero-diff subsumption via PR #415) | 11/16 | checklist gap is closeout bookkeeping only, not blocking — open-questions doc has zero unresolved items |
| TICK-212 | done | n/a | none (zero-diff subsumption via PR #415) | 12/12 | none |
| TICK-203 | done | n/a | none (zero-diff subsumption via PR #415) | 12/12 | none |
| TICK-215 | done | n/a | none (decision executed by DOCS-002/ADR-0028) | 12/12 | none |
| TICK-010 | done | **production** | existing mailbox classification UI | 8/8 | legitimate — shipped via PR #392, release 9, before release 10; outcome note distinguishes "deployed" from "live user-confirmed classification against the deployed estate," which remains a separate evidence state |
| TICK-009 | done | **production** | existing Core classification path | 12/12 | legitimate, release 9, PR #391; outcome note is careful to keep "local volume-cohort evidence" separate from "labelled holdout and operator acceptance" (still parked) |
| TICK-011 | done | not-deployed | none — "Production caller execution was not established" | 10/10 | retrospective reconciliation only, code already present on `main` via commits `ae6f0c2d`/`ef3eb4c7`/`f7d99b18`, no TICK-011 PR; correctly not claimed as deployed despite being on `main` |
| PLAT-001 | done | **field not set at all** | 21 design screens across `Pegasus.Web` (left rail shell, marks) | 55/63 | **flag**: `deployment` field is blank, not `not-deployed`/`n/a`/`production` — inconsistent with every other ticket in this roster; should be `not-deployed` since PR #397 only merged to `dev`. The 8 unchecked checklist items are honestly named follow-ups (rail counts wiring, Experian AutoCheck capability ID, case notes/engineer-query allocation, unplaced marks, and — the one gating this ticket's own verification checklist — visual screenshot capture from a local run, explicitly left as "follow-up," with the 32-test Playwright browser suite substituting for it in the interim) |

No blocker/should-fix findings surfaced in this cluster beyond the PLAT-001 `deployment` field gap (nit — Kanmer hygiene, not a code or deployment risk).

---

## 4. Open questions / contradictions for the operator

1. **`docs/operator-notes.md` edits in INTK-007 (#424) and INTK-008 (#423).** Both PRs edit this protected file: INTK-007 replaces `Needs sorting`/`Blocked intake` wording with `Unidentified`/`Image Intake` terminology in `docs/prd/pegasus-product.md` and adds a new "Unidentified received material" section to `operator-notes.md`; INTK-008 adds an "Image-initiated Case clarification" section. Both tickets claim this reflects explicit operator instruction, but this repo's own CLAUDE.md quotes the exact invariant text being changed ("`Audit`, `Triage`, `Needs sorting`, and `Blocked intake` retain their settled distinct meanings; `Triage` is the only current term"). **This needs the operator's explicit sign-off before either PR merges** — it is exactly the class of "protected: stop for user resolution before changing its meaning" case CLAUDE.md describes. Recommendation: don't treat the ticket's claimed operator instruction as sufficient; get a direct confirmation quoting the section, then merge.

2. **`PLAT-001`'s `deployment` field is unset**, unlike every other ticket in this roster (which use `production`/`not-deployed`/`n/a`). Since PR #397 only merged to `dev`, this should read `not-deployed`. Low-stakes but worth a groom pass so release-12 tooling that filters on `deployment` doesn't silently skip it.

3. **Three of the five open task PRs are in merge conflict against current `dev`** (`#417` INTK-006, `#423` INTK-008, `#424` INTK-007 — all `CONFLICTING`/`DIRTY`); only `#416` INTK-005 and `#422` TICK-045 are cleanly mergeable (though `UNSTABLE` on CI). This is a real integration-order problem for release 12's PR-merge plan, not just a comment-remediation one — the conflicting three will need a rebase/resolve pass, and INTK-006/007/008 likely conflict with each other too since they all touch the intake/ImageIntake surface. Recommendation for the plan document: sequence INTK-005 and TICK-045 first (clean), then rebase INTK-006/007/008 one at a time, re-checking conflicts after each merge.

4. **Tickets currently taken by other agents/machines — leave worktrees alone, coordinate before touching:**

   | ID | Assignee | Branch | Worktree |
   |---|---|---|---|
   | TICK-093 | codex-mcp-client | task/tick-093-versioned-repair-spec | ../pegasus-worktrees/tick-093-versioned-repair-spec |
   | TICK-043 | codex-mcp-client | task/tick-043-mailbox-identity | ../pegasus-worktrees/tick-043-mailbox-identity |
   | TICK-044 | codex-mcp-client | task/tick-044-classification-catalogue | ../pegasus-worktrees/tick-044-classification-catalogue |
   | TICK-045 | Codex / execute_tick_045 | task/tick-045-shared-classification-policy | ../pegasus-worktrees/tick-045-shared-classification-policy |
   | TICK-046 | codex-mcp-client | task/tick-046-classification-history | ../pegasus-worktrees/tick-046-classification-history |
   | INTK-005 | Codex | intk-005-grouped-upload | .worktrees/intk-005 |
   | INTK-006 | Codex | intk-006-grouped-image-routing | .worktrees/intk-006 |
   | INTK-007 | Codex | intk-007-unidentified-intake | .worktrees/intk-007 |
   | INTK-008 | Codex | intk-008-image-initiated-lifecycle | .worktrees/intk-008 |
   | PLAT-006 | claude-code | task/plat-006-shell-upload | ../pegasus-worktrees/plat-006-shell-upload |
   | TICK-033 | codex-mcp-client | task/tick-033-request-upload-reconciliation | ../pegasus-worktrees/tick-033 |

   All 11 taken tickets on the board right now belong to `codex-mcp-client`/`Codex` or `claude-code` (this session's own prior work) — none to a third machine — but per this repo's workflow rules these are still live claims and DELIV-012 must not `take`, edit source in, or push to any of these worktrees; only merge their already-open, reviewed PRs.

5. **DELIV-011 was held by the operator** (per DELIV-012's own body: "Release 11 (DELIV-011, `feda958f`) was fully prepared but **held** by the operator on 2026-08-19 before the `main` push"), and DELIV-012 explicitly supersedes it, calling its local artefacts stale. Its worktree (`../pegasus-worktrees/deliv-011-release-11`) still exists on disk — confirm with the operator whether it should be released/removed as part of DELIV-012's git-hygiene pass, since it's a stale claim by the same rules that would flag any other orphaned worktree.

---

## 5. Implications for release 12

- **Depends on this deployment to reach `done`:** TICK-093, TICK-043, TICK-044, TICK-046 (MAIL/ENG cluster, all `verifying`, merged to dev only); PLAT-006, TICK-033 (both `verifying`); SIMPLI-014, PR-009 (both `done` at the dev-merge evidence tier, explicitly disclaiming deployment). None of these will move to `done`-with-deploy-evidence, or gain a credible `proof.md` claiming production, until release 12 ships and each ticket's proof is re-run against the deployed estate.
- **Already correctly closed regardless of release 12:** the whole docs-decision cluster (TICK-099/205/207/211/212/203/215) plus TICK-213/204/DOCS-002 — all `deployment: n/a`, zero-diff or docs-only, nothing pending. TICK-010/009/TICK-011 are also settled (production via release 9, or explicitly not-deployed by design). DELIV-009/AUTO-002 are release 10's own record and need no further action.
- **Needs remediation before it can honestly reach `done`, independent of deployment:** TICK-044 (retained-mail viewer wiring for `MailOperationalDestinationPolicy` is unfinished — 6 checklist items open, not just a deploy gap) — this is a completeness gap, not a deployment gap, and should be scoped as its own remediation step in the release-12 plan rather than assumed to close once deployed.
- **Blocking findings that should land as fixes before or as part of release 12** (see §2/§3 for full remediation briefs): the GRANT-omission pattern recurring across **five separate migrations** — TICK-093 (`CaseRepairSpecifications`), INTK-005/INTK-006 (shared migration `20260819101344_GroupedIntakeSubmission.cs`), INTK-007 (`UnidentifiedWork`), and INTK-008 (`ImageInitiatedLifecycle`) — the same defect class five times over, worth one consolidated remediation pass (and a permanent CI check that flags any new-table migration with no matching GRANT) rather than five individual fixes; INTK-006's premature-finalization race and O(N²) rescanning; INTK-008's two red CI tests and its un-invoked custody adapter; INTK-007's un-wired producer routing and destination validation; the Upload page's un-raised `MultipartBodyLengthLimit` (INTK-005/006, same fix, same file); `docs/current-architecture.md:85` staleness from TICK-046.
- **Merge-order risk for release 12's PR-integration step:** three of five open task PRs are already `CONFLICTING` against current `dev` (§4.3) — the release-12 plan needs an explicit rebase sequence, not just a merge-in-order list.
- **Operator sign-off required before merging, not just before release:** the two `operator-notes.md` edits in INTK-007/INTK-008 (§4.1) — these should be resolved ahead of, not during, the release-12 PR-integration step, since a blocked protected-doc question would otherwise stall the whole sequenced merge.
