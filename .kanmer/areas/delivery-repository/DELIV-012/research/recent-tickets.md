# DELIV-012 — Research: recent tickets since the last deploy

Anchor facts used throughout: last production deployment = **release 10**, commit `d8de29cb` (git log confirms `2026-08-18 13:52:50 +0000`), deployed 2026-08-18T13:52Z. Production still serves release 10. `origin/dev` = `560f741c`. Anything merged/completed after 2026-08-18T13:52Z is **not** in production, regardless of what any proof/checklist document claims.

A note on method: `updated_since` on this board is unreliable as a "real activity" signal by itself — a board-wide `order`-field reindex (actor `codex-mcp-client`) touched the `updated` timestamp of essentially every ticket on the board at 2026-08-19T09:39:14–15Z, including tickets last touched weeks ago. The roster below is filtered to tickets whose activity log shows a **real** mutation (status/doc/take/commit/PR change, not just an `order` bump) at or after the 2026-08-18T13:52Z cutoff.

---

## 1. Roster since the last deploy

29 non-archived tickets in `review`/`verifying`/`done` show genuine activity since the release-10 cutoff.

| ID | Title (short) | Status | Profile | Taken (assignee / branch) | PR(s) | Merged to dev? | `deployment` field | Docs | Checklist | Unresolved open-questions |
|---|---|---|---|---|---|---|---|---|---|---|
| TICK-093 | ENG-01 canonical repair specification | verifying | feature | codex-mcp-client / `task/tick-093-versioned-repair-spec` (still taken) | #420 | Yes (12:16Z) | not-deployed | full | 6/6 | none |
| INTK-007 | Replace Needs sorting with Unidentified | review | feature | Codex / `intk-007-unidentified-intake` (still taken) | #424 (open, no CI) | No | — | full | 22/36 | none (but doc itself says "implementation deliberately blocked until kanmer-docs reconciles protected/governing docs" — see §4) |
| TICK-045 | MAIL-03 shared classification policy | review | feature | Codex / `task/tick-045-shared-classification-policy` (still taken) | #422 (open, mostly green, 1 pending) | No | — | full | 12/12 | none |
| INTK-008 | Image-initiated Case lifecycle | review | feature | Codex / `intk-008-image-initiated-lifecycle` (still taken) | #423 (open, no CI) | No | — | full | 8/29 | none |
| INTK-006 | Grouped image routing | review | fix | Codex / `intk-006-grouped-image-routing` (still taken) | #417 (open, red CI) | No | — | full | 26/41 | none |
| TICK-213 | Density subsumption decision | done | feature | codex-mcp-client (released) | #421 | Yes (11:37Z) | n/a | full+proof | 15/15 | none |
| TICK-046 | MAIL-04 classification evidence/history | verifying | feature | codex-mcp-client / `task/tick-046-classification-history` (still taken) | #418 | Yes (11:23Z) | — | full | 10/10 | none |
| PR-009 | Preserve report tails under long content | done | fix | codex-mcp-client (released) | #419 | Yes (11:21Z) | n/a | full+proof | 17/17 | none |
| INTK-005 | Grouped upload | review | feature | Codex / `intk-005-grouped-upload` (still taken) | #416 (open, red CI) | No | — | full | 7/33 | none |
| PLAT-001 | Claude Design UI implementation | done | feature | claude-code (released) | #397 | Yes (2026-08-18T09:23Z, predates cutoff) | **absent from item record** (finding) | full+proof | 55/63 (mostly stale duplicate checklist sections — see §3) | none (rich resolution log) |
| TICK-099 | RPT-04 diminution — decision | done | feature | codex-mcp-client (released) | none (zero diff) | n/a | n/a | full+proof | 13/13 | none |
| TICK-205 | Audit repair-spec model — decision | done | feature | codex-mcp-client (released) | none (zero diff) | n/a | n/a | full+proof | 16/16 | none |
| TICK-212 | Renderer package lock files — decision | done | feature | codex-mcp-client (released) | none (zero diff) | n/a | n/a | full+proof | 12/12 | none |
| TICK-207 | Audit template reuse — decision | done | feature | codex-mcp-client (released) | none (zero diff) | n/a | n/a | full+proof | 13/13 | none |
| TICK-211 | Renderer analyzer strictness — decision | done | feature | codex-mcp-client (released) | none (zero diff) | n/a | None (unset, not `n/a`) | full+proof | 11/16 (unchecked = decision-profile template artifacts) | none |
| TICK-203 | Renderer MCP surface — decision | done | feature | codex-mcp-client (released) | none (zero diff) | n/a | n/a | full+proof | 12/12 | none |
| TICK-043 | MAIL-01 mailbox/thread/message identity | verifying | feature | codex-mcp-client / `task/tick-043-mailbox-identity` (still taken) | #414 | Yes (10:34Z) | — | full | 10/10 | none |
| SIMPLI-014 | Integrate CollisionRenderer behind Core port | done | feature | codex-mcp-client (released) | #415 | Yes (10:29Z) | None (unset) | full+proof | 18/24 (unchecked = closeout housekeeping) | none |
| TICK-215 | Renderer execution boundary — decision (ADR-0028) | done | feature | codex-mcp-client (released) | none (sourced via DOCS-002's #413) | n/a | n/a | full+proof | 12/12 | none |
| TICK-204 | Assessment-report outcome variants | done | feature | codex-mcp-client (released) | #412 | Yes (09:17Z) | n/a | full+proof | 11/11 | none |
| TICK-010 | MAIL-22 taxonomy persistence | done | feature | grok-shell-kanmer (released) | #392 | Yes — release 9, predates window | production (release 9) | full+proof | 8/8 | none above Parked |
| TICK-009 | MAIL-21 classification volume cohort | done | feature | grok-shell-kanmer (released) | #391 | Yes — release 9, predates window | production (release 9) | full+proof | 12/12 | none above Parked |
| DOCS-002 | ADR-0028: Web Container App as renderer boundary | done | chore | codex-mcp-client (released) | #413 | Yes (09:19Z) | n/a | full+proof | 11/11 | none |
| DELIV-009 | Release 10: promote dev→main, deploy | done | chore | claude-code (released) | #406, #407 | Yes → **main**, deployed | production (release 10 itself) | plan/checklist/proof only (no research/open-q — correct for this profile) | 10/10 | n/a (no open-questions doc for this profile) |
| AUTO-002 | Authorization-code + PKCE for MCP connectors | done | feature | claude-code (released) | #405 | Yes (13:52:51Z — part of release 10) | production | full minus open-q | 15/17 | n/a |
| TICK-011 | INT-17 VRM reading — reconciliation | done | feature | (none) | none (reconciliation only; code already on `main` via `ae6f0c2d`/`ef3eb4c7`/`f7d99b18`) | n/a | not-deployed (caller-activation gap, not code-absence — see §3) | full minus open-q | 10/10 | n/a |
| TICK-044 | MAIL-02 classification→destination mapping | verifying | feature | codex-mcp-client / `task/tick-044-classification-catalogue` (still taken) | #411 | Yes (09:03Z) | — | full | 12/18 (6 unticked, **not** under Parked — real gap, see §3) | none in open-questions.md itself |
| PLAT-006 | Centre shell content region, redesign Upload | verifying | fix | claude-code / `task/plat-006-shell-upload` (still taken) | #409 | **Yes**, merged 08:08:07Z (confirmed directly via `gh`; a sub-agent's draft read this as still-open — corrected here) | — | full minus research/open-q | 9/10 (item 10 "PR to dev, review, merge" stale-unticked post-merge) | n/a |
| TICK-033 | INT-31 capability-inventory reconciliation (docs-only) | verifying | feature | codex-mcp-client / `task/tick-033` (still taken) | #408 | Yes (2026-08-18T15:38Z) | — | full minus open-q | 4/5 (CI evidence for #408 not yet confirmed in-ticket) | n/a |

Docs legend: "full" = research/files/plan/checklist/open-questions/post-implementation-report/scratch all present; "full+proof" adds proof.md (verifying/done-stage requirement); some chore/custom-profile tickets correctly omit research/open-questions per their profile.

**No unresolved (`- [ ]` above `## Parked`) open-questions items were found in any of the 23 roster tickets that carry an open-questions document.** Every ticket that shows `- [ ]` items has them correctly below `## Parked (explicitly deferred)`, which is not gate-counted.

---

## 2. Open PRs — one section per ticket

A shared pattern across all five open PRs: every one carries at least one **unaddressed `chatgpt-codex-connector[bot]` review comment**, and no PR has a human reviewer comment. A second shared pattern: **every PR whose diff includes a `CREATE TABLE` migration omits the `GRANT ... TO [pegasus_web_runtime_role]` statement** that the repo's own recent convention requires (see `20260819104953_MailClassificationCorrectionHistory.cs` for the correct idiom) — this is the same defect class already confirmed on merged TICK-093, so it is now a **pattern**, not an isolated miss.

### INTK-005 — PR #416 "grouped upload" (open, RED CI)

One authenticated Upload POST now accepts multiple files as a durable submission group (`IntakeSubmissionGroups`/`IntakeSubmissionGroupMembers` + migration, Core `GroupedIntake` orchestration, new `UploadGroupStatus` page). Single commit `ed04f498`. Checklist 7/33.

**Reviewer comments (Codex bot, review at 10:35:35Z, all unaddressed — no commits since):**
1. P1 `Upload.cshtml:36` — multi-file selections can exceed `Program.cs`'s unchanged 10 MiB+64 KiB `MultipartBodyLengthLimit`.
2. P2 `UploadGroupStatus.cshtml:12` — no `data-auto-refresh`; rows stick at "Processing".
3. P2 `EfIntakeSubmissionGroupStore.cs:122` — concurrent same-token inserts can collide on `(GroupId, Ordinal)`, no retry.
4. P2 `Upload.cshtml.cs:129` — exact-replay redirect loses "already received" messaging.
5. **P1 `GroupedIntake.cs:128` — rewrites the single-file token to `token:0`**, breaking `ExternalReceiptToken` correlation for existing callers.

Plan covers the ticket's acceptance criteria; implementation matches plan structurally, but finding #5 directly contradicts the plan's own step 5 ("retain `ExternalReceiptToken`"). Simplification pass recorded (dated 2026-08-19) but predates the CI run and doesn't name the deterministic failures below. No scope drift.

**CI is red and the root cause is confirmed**: `sql-integration (1/2/3)` fail 8 tests, all in the same family (`InstructionDraftWebTests`, `IntakeWebNegativeTests`, `QdosIntakeWebTests`) — traced directly to finding #5's `token:0` rewrite, plus one unrelated stale-migration-name fixture test.

**Deployment risk — confirmed GRANT gap**: `20260819101344_GroupedIntakeSubmission.cs` creates both new tables with no `GRANT` statement. The PR's own post-implementation report even flags it ("Runtime-role grants... should be confirmed... before production promotion") without fixing it.

**Verified findings:**
| Sev | File:line | Finding | Tag |
|---|---|---|---|
| Blocker | `src/Pegasus.Infrastructure/Persistence/Migrations/20260819101344_GroupedIntakeSubmission.cs` | No GRANT to `pegasus_web_runtime_role` on `IntakeSubmissionGroups`/`IntakeSubmissionGroupMembers`. Remediation: add `migrationBuilder.Sql("GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeSubmissionGroups] TO [pegasus_web_runtime_role];")` (and the member table) matching the `20260819104953_MailClassificationCorrectionHistory.cs` idiom; verify with a permission-role integration test. | [verified] |
| Blocker | `src/Pegasus.Core/Intake/GroupedIntake.cs:128` | Single-file token rewritten to `token:0`, breaking `ExternalReceiptToken` correlation; causes 8 CI failures. Remediation: only synthesize `{token}:{ordinal}` for group members beyond the first (or key membership internally without touching `ExternalReceiptToken`); pass the original token through unmodified for single-member groups. Test: `dotnet test --filter "FullyQualifiedName~InstructionDraftWebTests|FullyQualifiedName~IntakeWebNegativeTests|FullyQualifiedName~QdosIntakeWebTests"` → 0 failures. | [verified] |
| Should-fix | `src/Pegasus.Web/Program.cs` (`MultipartBodyLengthLimit`, ~line 502-505) | Global multipart limit unchanged while multi-file groups can exceed it. Remediation: derive the limit from `IntakeEnvelopeLimits.MaximumContentLength * <max files per group>`; add a 2-file-over-10MiB integration test. | [verified] |
| Should-fix | `src/Pegasus.Web/Pages/UploadGroupStatus.cshtml:12` | Missing `data-auto-refresh`. Remediation: add the same attribute used elsewhere; add a browser test asserting its presence. | [suspected, needs check] |
| Nit | `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` (`CommittedMigrationCreatesTheSqlServerSchema`) | Expected-migrations fixture missing the new migration name. Remediation: add it to the expected list. | [verified] |
| Should-fix | `src/Pegasus.Infrastructure/EfIntakeSubmissionGroupStore.cs:122` | No retry on concurrent same-token `(GroupId, Ordinal)` collision. Remediation: catch the unique-constraint exception, reload, return the existing member. | [suspected, needs check] |

### INTK-006 — PR #417 "grouped image routing" (open, RED CI, built on INTK-005)

Aggregates a group's terminal VRM recognitions and associates the whole group to one existing Case only when exactly one usable VRM matches exactly one eligible Case. A same-day "Scope split" ticket-body amendment explicitly narrows this PR to that one outcome, deferring Image-initiated-Case creation to INTK-008 and Unidentified routing to INTK-007 — a self-consistent, ticket-authorized narrowing, not a silent gap, but it means the PR cannot be read against the ticket's *original* three-outcome acceptance criteria without that split being accepted in review. Checklist 26/41.

**Reviewer comments — two Codex rounds.** Round 1 (10:54:14Z, on commit `70d7c89c`) had 9 comments; two are confirmed **addressed** by later commits `866d305e`/`599bfe6d` (single-VRM groups with no eligible case now fall through correctly; single-file uploads redirect back to `/Upload/Status/{id}`). The rest remain open, most seriously:
- **P1 `ImageIntakeAutomation.cs:205` — still unaddressed at current HEAD (verified by direct read)**: `TryRegisterAndAssociateAsync`/`TryAssociateAsync` results are ignored in the member loop, so a recoverable per-member failure silently completes the group.
- P1 `ImageIntakeAutomation.cs:182` — expected member count read at evaluation time; an interrupted group can pass a false-equal check. [suspected, needs check]
- Round 2 (11:26:15Z, on current HEAD `599bfe6d` — by definition unaddressed): **P1 `ImageIntakeAutomation.cs:200`** — `HandOffToImageIntake` (multi-candidate ambiguity) decisions still enter the same association loop and can re-query into a different match than intended [verified by direct read]; P2 replay-identity/idempotency issues inherited from #416.

**CI provenance problem (process blocker):** the only workflow run for this branch (`32244323472`) was triggered at commit `70d7c89c`, **before** the two remediation commits. No CI has ever run against current HEAD, and the branch is `mergeStateStatus: DIRTY`/`CONFLICTING` against `dev`. The checklist's "tests passed after rebuild" line is a local, non-CI claim.

Simplification pass recorded twice, honest and specific (names concrete reuse, states the Image-initiated-Case branch is deliberately deferred to INTK-008). No migration of its own — inherits, does not add to, INTK-005's GRANT gap.

**Verified findings:**
| Sev | File:line | Finding | Tag |
|---|---|---|---|
| Blocker (process) | PR #417 @ `599bfe6d` | Current HEAD never went through CI; branch is CONFLICTING against `dev`. Remediation: rebase onto a fixed INTK-005, push, require a fresh green run before further review. | [verified] |
| Should-fix | `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` (member loop, ~195-210) | Per-member association result never checked; `HandOffToImageIntake` decisions share the unconditional loop instead of being routed away. Remediation: capture the per-member result and don't finalize the group on failure; gate the loop on `routing.Decision == AssociateExistingCase` explicitly. Test: Core test forcing one member's registration to fail, assert group not marked complete. | [verified] |
| Should-fix | `ImageIntakeAutomation.cs:182`, `:150` | Possible member-count race and non-image/N² recognizer inclusion. | [suspected, needs check] |
| Nit | inherited | Multipart limit / auto-refresh issues from #416 apply here too via the shared branch base. | [verified] |

### INTK-007 — PR #424 "Replace Needs sorting with Unidentified" (open, NO CI RUN)

Adds a Core-owned `Unidentified` aggregate (immutable `U<n>` references, six-code reason taxonomy, Open/Resolved lifecycle, EF persistence + legacy backfill, operator queue/detail UI, MCP tools) as a wide replacement for `Needs sorting`. Checklist 22/36. The ticket's own open-questions.md states: *"Implementation is deliberately blocked until kanmer-docs reconciles the protected and governing documents. That is a prerequisite, not an open product question."* — yet the PR is already open with code implemented (see §4).

**Reviewer comments (Codex bot, unaddressed, no CI has ever run on this PR):**
- P1 `ProcessIntake.cs:258` — below-threshold image intake excluded from Unidentified registration by scan ordering, becomes untracked.
- P1 `EfUnidentifiedStore.cs:174` — resolution accepts any nonempty free-form `TargetId` with no destination-port validation.
- **P1 `Message.cshtml.cs:114` "Keep Needs sorting distinct from Unidentified"** — verified against diff: `MailRouteDisposition.NeedsSorting` and `IntakeDecision.NeedsSorting` are both directly relabeled `"Unidentified"` in the UI regardless of whether a U-item was actually registered.
- P1 `ProcessIntake.cs:256` — every `TechnicalFailure` (including a first-attempt transient error) immediately allocates an immutable U-reference rather than retrying.
- P1 `ProcessIntake.cs:243` — a later staff reevaluation that resolves a receipt never reconciles the existing open U-item, leaving it stale.
- P2 replay/idempotency and detail-page evidence gaps (`EfUnidentifiedStore.cs:148`, `Unidentified/Details.cshtml.cs:63`, `Unidentified/Details.cshtml:25`).

**Plan vs. implementation:** the plan explicitly anticipated the hard part, requiring "a producer-by-producer matrix" and stating existing `docs/operator-notes.md` "Needs sorting" usages "must be reconciled, not silently overwritten" before code — but the merged diff only **adds** a new "Unidentified received material" section to `operator-notes.md` and leaves **three pre-existing "Needs sorting" mentions unreconciled** (verified at lines 42, 199, 388 on `dev`@560f741c). Simplification pass present, dated, honest (names deferred grouped-submission/retained-mail/audit work).

**Deployment risk — confirmed GRANT gap**: migration `20260819115323_UnidentifiedWork.cs` creates three new tables (`UnidentifiedItems`, `UnidentifiedSequences`, `UnidentifiedHistory`) with zero GRANT statements — the ticket's own checklist self-flags this as incomplete.

**operator-notes.md / invariant analysis:** The added text is careful and explicitly claims to preserve distinctness ("does not rename or collapse Triage, Blocked intake, incomplete Audit evidence, or Image Intake"). The actual CLAUDE.md-protected invariant sentence lives in `docs/prd/pegasus-product.md`, not `operator-notes.md`, and **is rewritten there**: `Needs sorting` is removed from the definitional list and replaced by `Unidentified`/`Image Intake` bullets, done with an explicit terminology mapping — a deliberate, ticket-authorized rename, not a silent collapse. The real defect is narrower: `operator-notes.md` itself was left internally inconsistent post-merge (new + stale old language coexisting), contradicting the ticket's own reconciliation commitment, and the UI-label collapse (finding above) can present "Unidentified" for cases that aren't actually U-registered.

**Verified findings:**
| Sev | File:line | Finding | Tag |
|---|---|---|---|
| Blocker | `src/Pegasus.Infrastructure/Persistence/Migrations/20260819115323_UnidentifiedWork.cs` | No GRANT for `UnidentifiedItems`/`UnidentifiedSequences`/`UnidentifiedHistory`. Remediation: add the standard GRANT block per table, scoped to actual caller verbs; add a runtime-grant regression test (ticket's own checklist already names this item). | [verified] |
| Should-fix | `docs/operator-notes.md` lines 42, 199, 388 (base `dev`) | Three pre-existing "Needs sorting" references left unreconciled after the PR only adds new text — contradicts the ticket's own plan commitment. Remediation: update each to reference `Unidentified` or explicitly mark as historical/compatibility text. | [verified] |
| Should-fix | `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs:114` | `MailRouteDisposition.NeedsSorting`/`IntakeDecision.NeedsSorting` both render as "Unidentified" regardless of actual U-registration. Remediation: only show "Unidentified" where a backing `UnidentifiedItem` exists; use a distinct interim label otherwise. | [verified] |
| Should-fix | `ProcessIntake.cs:258`, `:243`, `:256` | Below-threshold intake untracked; stale open U-items on reevaluation; transient failures immediately consume an immutable U-reference. | [suspected, needs check] |
| Nit | (process) | No CI has ever run on PR #424. Must run before merge — cannot rely on any of the above being the only issues. | [verified] |

### INTK-008 — PR #423 "Image-initiated lifecycle" (open, RED CI: `sql-integration (2)` failing)

Turns `ImageIntake` into an explicit lifecycle projection (`AwaitingInstruction` → `MergedIntoInstructionCase`/`StaffClosed`) with VRM-keyed custody, history, and a new ADR-0029 superseding ADR-0013. Checklist 8/29.

**Reviewer comments (Codex bot, unaddressed):**
- P1 `EfImageIntakeStore.cs:264` — close reason can exceed 500 chars, fails at SQL instead of being validated.
- P2 `EfImageIntakeStore.cs:325` — operation-key replay doesn't check command fields, so a mismatched replay can silently return the first result.
- P2 `ImageIntake/Details.cshtml.cs:79` — `DbUpdateConcurrencyException` on a stale close isn't caught (500 instead of documented conflict UX).
- P2 `ImageIntake/Details.cshtml:38` — raw enum/snake_case values render instead of going through `OperatorLabels.cs`.
- **P1 `docs/capabilities.md:215`** — normative lifecycle behavior inserted into a doc CLAUDE.md defines as schedule/registry-only, table left truncated.
- **P1 `docs/adr/README.md:30`** — ADR-0013 marked `superseded` in its own frontmatter but still listed under "Current architecture decisions (status: accepted)".
- **P1 `CONTEXT.md:148`** — normative lifecycle requirements duplicated into the terminology doc, creating a second normative owner alongside the FRDs.

**CI is red and the failure is a real behavioral gap, not flake**: `sql-integration (2)` fails 2/171, including `ImageIntakeWebTests.StaffRegistersAnImageOnlyReceiptAndFindsItEverywhere` (page doesn't render "awaiting definitive instruction") — matches the ticket's own checklist admission that "VRM-keyed Box adapter invocation and custody state presentation still need final implementation/verification before PR." A second failing test (`QdosAllocationRecoveryTests`) touches files outside this PR's diff, indicating a regression from the shipped code rather than an unrelated flake.

**Simplification pass: not recorded** — plan step 11 and the checklist item "Run simplification pass and record dispositions" are both still open; no dated heading exists anywhere (a CLAUDE.md process gap, unlike INTK-007's honest pass).

**Scope drift confirmed**: normative behavior added to `docs/capabilities.md` and `CONTEXT.md` (both meant non-normative), plus the ADR index left internally inconsistent.

**operator-notes.md / invariant analysis:** the diff **replaces** the paragraph describing image-only arrivals — old text described an "image-initiated case" only as an operational description with pre-case evidence; new text declares it formally *is* an Image-initiated Case projection with its own reference scheme, plus a new "clarification" section. This is a genuine meaning change, but it was flagged and resolved through the ticket's own open-questions.md (all ticked) and a corresponding ADR-0029 — a properly authorized change, not a silent one. No `Needs sorting`/`Triage`/`Blocked intake` text is touched by this PR.

**Verified findings:**
| Sev | File:line | Finding | Tag |
|---|---|---|---|
| Blocker | `src/Pegasus.Infrastructure/Persistence/Migrations/20260819112914_ImageInitiatedLifecycle.cs` | No GRANT for the new `ImageIntakeLifecycleEvents` table or new `ImageIntakes` columns. Remediation: same GRANT pattern as the other three PRs. | [verified] |
| Blocker | CI @ current HEAD | `sql-integration (2)` red: `ImageIntakeWebTests.StaffRegistersAnImageOnlyReceiptAndFindsItEverywhere` fails on missing UI text — genuine behavioral gap, ticket's own checklist agrees custody/presentation work is unfinished. Must not merge until green. | [verified] |
| Should-fix | (process) | No simplification pass recorded despite CLAUDE.md requiring one for non-docs-only tasks; plan step 11 and its checklist item are open. Remediation: run and record it before merge. | [verified] |
| Should-fix | `docs/capabilities.md:215`, `docs/adr/README.md:30`, `CONTEXT.md:148` | Normative behavior leaked into non-normative registries; ADR-0013 still listed as accepted despite superseded frontmatter. Remediation: move behavioral content to the FRDs only; trim capabilities.md/CONTEXT.md back to index entries; move ADR-0013's row out of the accepted table. | [verified] |
| Should-fix | `EfImageIntakeStore.cs:264`, `:325`; `Details.cshtml.cs:79`; `Details.cshtml:38` | Unvalidated close-reason length, weak replay-field check, uncaught concurrency exception, raw enum leakage. | [suspected, needs check] |

### TICK-045 — PR #422 "shared classification policy" (open, mostly green — 1 pending)

Touches exactly two files: one new integration test (`RetainedMailPersistenceTests.cs`, +86 lines) and a one-line `docs/capabilities.md` evidence-tier update — no production `src/` code, no migration. Proves the existing MAIL-04 `CorrectRetainedMailClassification` Core command behaves identically/independently for two mailbox identities. Checklist 12/12 (fully checked).

**Reviewer comments (Codex bot, two P1s, both unaddressed — no commit after `139a4571`, still the tip):**
1. **P1 `RetainedMailPersistenceTests.cs:345`** "Exercise the classification policy instead of seeding its output" — verified: the test builds a `MailClassificationResult` inline via `.Ambiguous(...)` and passes it straight to `StoreClassifiedReceiptAsync`; no `IMailClassificationPolicy`/classifier is invoked anywhere in the new test. The test proves the correction/persistence path is mailbox-agnostic, **not** that classification itself is shared across mailboxes — undercutting the ticket's own title.
2. **P1 `RetainedMailPersistenceTests.cs:320`** "Use a documented supported mailbox" — verified: the test invents `claims@collisionengineers.co.uk` as its second mailbox; `docs/operator-notes.md`'s supported estate is `desk@`/`engineers@`/`info@`/`instructions@collisionengineers.co.uk` only. `claims@` is not a real, documented mailbox.

**Plan vs. implementation:** the plan deliberately avoids a second classification command (correctly, per "one Core owner") but frames cross-mailbox acceptance as satisfied by the correction/persistence test alone — the Codex findings are substantively correct that this doesn't evidence "shared classification policy." Simplification pass recorded (dated 2026-08-19) but predates review and wasn't updated after.

**Deployment risk:** none — no migration, no schema change, no config/Worker wiring. CI is effectively green (`unit`, `browser`, `sql-integration (2)`, `sql-integration (3)`, `reference-data`, `changes`, `documentation` all pass; `sql-integration (1)` is `pending`, `infrastructure` is `skipping` — `mergeStateStatus: UNSTABLE` reflects those, not a failure).

**`MailOperationalDestinationPolicy` dark-code gap — explicit answer: this PR does NOT resolve it.** `src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs` has zero references outside its own file anywhere in `src/` (confirmed by repo-wide grep) — no DI registration, no caller. TICK-045's own research never names the class; it scopes MAIL-03 strictly to taxonomy/correction sharing, explicitly deferring destination/routing behavior elsewhere. `MailOperationalDestinationPolicy` (operational destination routing, owned by TICK-044) is a different capability than MAIL-03 (classification-policy sharing) — TICK-045 was never meant to wire it up, and doesn't.

**Verified findings:**
| Sev | File:line | Finding | Tag |
|---|---|---|---|
| Should-fix | `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs:313-397` | New test seeds a `MailClassificationResult` directly rather than invoking a classification policy; proves mailbox-agnostic correction, not shared classification. Remediation: either rescope `docs/capabilities.md`'s MAIL-03 claim to "correction path is mailbox-agnostic," or add a real classifier invocation to the scenario before claiming local-evidence tier. | [verified] |
| Should-fix | `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs:320` | Second mailbox `claims@collisionengineers.co.uk` is not in the documented supported estate. Remediation: use `instructions@collisionengineers.co.uk` (or another documented mailbox) as the second identity, or add `claims@` to approved-mailbox config with justification first. | [verified] |
| Nit (info) | `docs/capabilities.md` MAIL-03 row | Evidence-tier wording reads as if classification-policy sharing was proven; tighten once the above is resolved. | [verified] |

---

## 3. Verifying / Done tickets since the deploy

### Tickets already reviewed in §1/§2 context — clean, no findings

**TICK-213** (density subsumption decision, done, merged #421): confirms no compact/density option exists anywhere in the codebase and only renamed/consolidated a stress test. Entry point: none stated — correctly so, test/decision-only. Zero findings [verified]. Not blocked on release 12 (`n/a` is accurate).

**TICK-204** (assessment-report outcome variants, done, merged #412): documents the four canonical outcomes in FRD-11 and fixes a prior PR-003 review defect on the Contract-repair cap. Entry point: none stated — correct, no renderer caller exists yet (SIMPLI-014/DOCS-001 own that). Zero findings [verified]. Not blocked on release 12.

**DOCS-002** (ADR-0028 Web Container App as renderer boundary, done, merged #413): `docs/adr/README.md` correctly lists ADR-0028 as accepted; `docs/current-architecture.md` correctly does *not* yet describe the boundary in implementation terms (that's TICK-215/PLAT-007's job, not overrun by DOCS-002). Zero findings [verified]. Not blocked on release 12.

**DELIV-009** (release 10 promotion, done): proof.md accurately documents the actual production deploy — atomic `origin/dev`→`origin/main` push at `d8de29cb`, matching web revision, Worker redeploy, smoke test pass, artifacts retained. This ticket **is** release 10; its claims are already true. Zero findings [verified]. Not applicable to release 12.

**AUTO-002** (PKCE connector auth, done, part of release 10): proof.md's live-evidence table is dated 2026-08-18 within release 10's window; a later addendum confirms an actual end-to-end connector run. Entry point `<origin>/authorize` and `<origin>/mcp`, genuinely live in production. Zero findings [verified]. Not applicable to release 12.

**TICK-099, TICK-205, TICK-212, TICK-207, TICK-211, TICK-203** (six EPIC-004 decision/reconciliation tickets, done, zero repo diff each): all correctly `n/a`/no-deployment, no proof overclaims, no user-facing entry point (all decisions, not code). Zero findings [verified] across all six. Not blocked on release 12 — nothing to deploy.

**TICK-215** (renderer execution boundary decision, done, zero diff, sourced via DOCS-002's PR #413): correctly `n/a`, explicitly defers runtime/capacity proof to PLAT-007. Zero findings [verified].

**TICK-010, TICK-009** (MAIL-22/MAIL-21, done, `deployment: production` — but dated to **release 9**, predating this window): both proofs correctly scope to release-9 evidence and explicitly disclaim later capability (staff-confirmation UI, live user-confirmed classification). No entry point beyond backend classification persistence (TICK-010) / mail-intake pipeline (TICK-009). Zero findings [verified]. Not applicable to release 12 (already shipped).

### PLAT-001 — Claude Design UI implementation (done, merged to dev #397, PREDATES cutoff but never released to main)

Folds the Claude Design UI into `Pegasus.Web`: 21 screens, left-rail shell, 10 marks. Entry point: **the Pegasus.Web app itself** (Dashboard, Upload, Queues, Cases, Inbox, Operations, Administration, Case Details, Assessment via left rail) — the one roster ticket with a genuine, stated, user-facing entry point. Proof correctly cites only the merged-`dev` commit `5ab3b773`, never claims production.

Checklist 55/63 — but **7 of the 8 unchecked boxes are stale-duplicate artifacts**, not real gaps: the checklist doc has a duplicated "## Closeout — PLAT-001" section (first copy 3/8 checked, second copy 8/8 checked) and a duplicated "## Verification" block, from being appended-to rather than edited. Only **one** open box is genuine outstanding work: local DevelopmentOffline visual screenshots, explicitly named as a follow-up in the ticket's own Outcome text.

**Verified findings:**
| Sev | File:line | Finding | Tag |
|---|---|---|---|
| Nit | PLAT-001 `checklist` document | Duplicated stale "## Closeout" and "## Verification" sections produce 7 of 8 false-open boxes. Remediation: delete the first (stale) copies, keep the fully-checked later versions, so checked/total reflects the one real remaining item (visual screenshots). | [verified] |
| Should-fix | PLAT-001 item record | `deployment` field is **entirely absent** (not even `n/a`/`not-deployed`), unlike every sibling ticket. Remediation: set it to `not-deployed` (merged to `dev` only, never to `main`) so downstream tooling reading the field doesn't treat it as unset. | [verified] |

**Blocked on release 12:** **Yes** — the only ticket in this roster genuinely waiting on the next `dev→main` promotion to become a true "delivered to operators" claim; its "done" status covers implementation, not delivery.

### TICK-011 — INT-17 automatic VRM reading (done, reconciliation only, `deployment: not-deployed`)

No new commit; the feature is already on `main` via `ae6f0c2d`/`ef3eb4c7`/`f7d99b18`, and the verified commit is **`d8de29cb`, the exact release-10 anchor** — the code is physically in production. `deployment: not-deployed` reflects that **no live caller is wired in**, not that the code is missing; proof explicitly disclaims "production caller execution or deployment." Zero findings beyond a documentation-clarity nit.

| Sev | Finding | Tag |
|---|---|---|
| Nit | `not-deployed` is technically accurate but easy to misread as "code not shipped" when the real gap is caller-activation. Remediation: no code change — reword the Outcome/proof to distinguish "code deployed, capability not activated" from "code not yet deployed," so a future reader doesn't file a redundant "wait for release" follow-up. | [verified] |

**Blocked on release 12:** No in the simple sense — the code already rode along in release 10; the gap needs a caller-wiring ticket, not a redeploy.

### TICK-093 — ENG-01 canonical repair specification (verifying, merged #420, `not-deployed`)

One case-scoped canonical accepted `CaseRepairSpecifications` aggregate (immutable versions, source-route provenance, correction/supersession), replacing the earlier rejected dual conservative/maximised Audit model per TICK-205's correction. Entry point: **none stated** — consumed internally via `EfCaseAssessmentStore`; no cited Web page (TICK-092, a separate ticket, owns render-snapshot exposure). No `proof.md` exists yet; nothing overclaims deployment.

**Verified findings:**
| Sev | File:line | Finding | Tag |
|---|---|---|---|
| Blocker | `src/Pegasus.Infrastructure/Persistence/Migrations/20260819112640_VersionedRepairSpecifications.cs:25` | `CREATE TABLE CaseRepairSpecifications` has no GRANT to `pegasus_web_runtime_role`, breaking the established migration convention; `EfCaseAssessmentStore.cs:117-135` performs a real `.Add()` write to it. Remediation: add `migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseRepairSpecifications] TO [pegasus_web_runtime_role];")` (guarded by the repo's standard `IF DATABASE_PRINCIPAL_ID(...)` idiom, see `20260819104953_MailClassificationCorrectionHistory.cs`) plus matching `REVOKE` in `Down()`. Test: extend `RepairSpecificationMigrationTests`/`AssessmentPersistenceIntegrationTests` to run under the actual `pegasus_web_runtime_role` connection and assert the write succeeds. | [verified] |
| Should-fix | `src/Pegasus.Infrastructure/.../EfRepairSpecificationStore.cs` / `IRepairSpecificationStore` | Zero references outside their own files anywhere in `src/Pegasus.Web`, `src/Pegasus.Worker`, or any DI composition root — dark code; the real write path is the unrelated `EfCaseAssessmentStore`. Remediation: either register `EfRepairSpecificationStore` in `src/Pegasus.Infrastructure/DependencyInjection.cs` and route a real caller through it, or delete it as dead code via a follow-up ticket. | [verified] |
| Should-fix | Ticket's own plan/checklist/PIR | Neither gap above is acknowledged anywhere in the ticket's documents; the checklist (6/6) and PIR's "Risks/follow-ups" section list only rendering/provider/deployment items. Remediation: note both gaps in a proof/PIR addendum before this reaches Done. | [verified] |

**Blocked on release 12:** Yes, for any "deployed"/live claim — currently correctly `not-deployed`.

### TICK-043 — MAIL-01 mailbox/thread/message identity (verifying, merged #414, `not-deployed`)

Adds a canonical RFC Message-ID (NFKC-normalized, invariant-uppercase) as the durable duplicate/identity boundary for retained mail, separate from Graph's provider ID; fails closed on missing/contradictory identity. Entry point: the existing Graph/poll ingestion caller — no new UI. Migration only adds columns/indexes to an existing table (no `CREATE TABLE`), so **no GRANT gap applies**. open-questions.md fully resolved. Zero findings beyond the general pattern already covered.

**Blocked on release 12:** Yes, for any deployed claim.

### TICK-044 — MAIL-02 classification→destination mapping (verifying, merged #411, `not-deployed`)

Adds an exhaustive Core taxonomy-to-operational-destination mapping (`MailOperationalDestinationPolicy`, versioned `mail_operational_destination` v1). Entry point stated in the ticket's own open-questions.md: **the retained mailbox viewer** — but this is exactly the wiring left undone.

**Verified findings:**
| Sev | File:line | Finding | Tag |
|---|---|---|---|
| Blocker (process) | Ticket checklist (6 unticked items, none under Parked) | `git grep -ln "MailOperationalDestinationPolicy" origin/dev` returns only the policy file, its unit test, and one doc mention — **zero references in `src/Pegasus.Web` or any caller**. The ticket's own open-questions.md records the operator's bar verbatim: *"A policy referenced only by tests is incomplete and must not pass review as delivered."* The ticket has already cleared review (stage: verifying) with that bar unmet. Remediation: wire `MailOperationalDestinationPolicy` into `EfRetainedMailboxMessageStore`'s projection and `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs`/`Message.cshtml.cs`; add a `MailWorkspaceWebTests` case (pattern from TICK-046) proving the real page renders the derived destination; re-run the six unchecked checklist items and update the PIR. This is a review-process finding, not just a code gap — flag for the review lane. | [verified] |
| Info | `docs/current-architecture.md:563` | Correctly, *not* stale — already describes the policy as unwired ("the pure mapping performs no Outlook mutation"). | [verified] |

**Blocked on release 12:** Yes, for any deployed claim — and additionally blocked on the wiring finding above regardless of deployment.

### TICK-046 — MAIL-04 classification evidence, policy version, correction history (verifying, merged #418, `not-deployed`)

Adds an exact-message classification dossier and a reasoned staff correction workflow. Entry point: `GET /Inbox/{id}` (`src/Pegasus.Web/Pages/Mail/Message.cshtml`), plus new handler `OnPostCorrectClassificationAsync`. Migration correctly GRANTs `SELECT, UPDATE` on `IntakeMailClassificationDecisions` and `SELECT, INSERT` (+ DENY UPDATE/DELETE) on the append-only `IntakeMailClassificationHistory` — no GRANT gap here. open-questions.md fully resolved.

**Verified findings:**
| Sev | File:line | Finding | Tag |
|---|---|---|---|
| Should-fix | `docs/current-architecture.md:85` | Confirmed stale: current text says `GET /Inbox/{id}` "carries no handler" and "the Web runtime role holds SELECT alone" on retained-mail tables — both now false once TICK-046 deploys (`OnPostCorrectClassificationAsync` exists; the runtime role now also holds UPDATE/INSERT on the two named tables). Remediation: update line 85 as part of whichever task performs the release-12 deploy (per the safety-rail requiring current-state docs refreshed in the same task as the deploy) — not TICK-046 itself, which correctly didn't pre-claim deployed state. | [verified] |

**Blocked on release 12:** Yes, for any deployed claim (and the current-architecture.md fix is explicitly tied to that same deploy task).

### PLAT-006 — Centre shell content region, redesign Upload (verifying, merged #409 08:08:07Z, `not-deployed`)

Presentation-only fix: centres `.app-rail-main` beyond ~1520px, replaces the raw file input with a real dropzone, adds a "What happens next"/"Accepted files" panel to `/Upload`, fixes a ≤1023px blank-band regression found along the way. Entry point: **`/Upload`** and **`/Uploads/{token}`**, plus shell CSS site-wide. Simplification pass recorded honestly (6 findings, all applied, including one self-found defect fixed in-branch).

**Verified findings:**
| Sev | Finding | Tag |
|---|---|---|
| Nit | Checklist item 10 ("PR to dev, review, merge") is still unticked even though PR #409 merged at 08:08:07Z — confirmed directly via `gh pr view 409` (`state: MERGED`). The checklist doc simply wasn't updated post-merge. Remediation: tick item 10 and note the merge commit. | [verified] |

**Blocked on release 12:** Yes — and it's already staged: PLAT-006 is exactly the ticket named in the (now-superseded) release-11 PR #410 and in DELIV-011's outcome ("Release 12 carries PLAT-006 and everything since").

### TICK-033 — INT-31 upload-link capability-inventory reconciliation (verifying, merged #408, `not-deployed`)

Despite the title reading like the temp-upload-link feature itself, the actual scope is a **one-line documentation correction** to `docs/capabilities.md` (removing stale "UI removal pending" wording); it does not implement or modify FRD-02's token/expiry/scope/revocation behaviour, which already exists behind the pre-existing `/Uploads/{token}` caller. Entry point: none created by this ticket (pre-existing `/Uploads/{token}`). Simplification pass recorded as "n/a — docs-only," an appropriate disposition.

**Verified findings:**
| Sev | Finding | Tag |
|---|---|---|
| Should-fix | No `proof.md` exists yet; the one unticked checklist item ("Run focused request-upload integration tests") reflects a local 2-minute timeout, not a pass/fail result, and PR #408's checks were only "queued" at handoff. Remediation: before Done, fetch the actual CI verdict for #408 (`gh pr checks 408`) and record `CaseDetailsWebTests`/`DocumentCustodyDurabilityTests`' real result in a new proof.md — this is a security-adjacent area (upload-link revocation/replay). | [verified] |
| Nit | Title implies a feature-implementation scope the ticket doesn't actually carry; the security-relevant token logic was implemented earlier, elsewhere. Remediation: no code change — clarify in the ticket record which earlier ticket/commit owns the Core token/expiry/scope implementation so a future reader doesn't misattribute it. | [suspected, needs check] |

**Blocked on release 12:** Not deployment-blocked as scoped (docs-only, no runtime effect), but its Done-readiness is blocked on a real CI verdict for #408.

### SIMPLI-014 — Integrate CollisionRenderer behind a Core-owned port (done, merged #415, deployment unset)

Folds the standalone `workspaces/report-renderer/` into the monolith: `Pegasus.Core/Reports` owns the render contract and all four outcome calculations; `Pegasus.Infrastructure/Reports` is the sole adapter; composition happens only in `Pegasus.Web` (per ADR-0028), not Worker. The "one Core owner" rule is honored and independently confirmed (`DependencyDirectionTests`, 39/39, cited in proof.md, assert exactly one Infrastructure adapter and no renderer libraries in Core). Entry point: **none stated by design** — plan step 5 explicitly forbids adding any HTTP/Razor/MCP/CLI/background trigger; only test code calls it today, with the real caller deferred to [[DOCS-001]]. Two dated simplification passes recorded, both honest ("No finding was deferred").

**Verified findings:**
| Sev | Finding | Tag |
|---|---|---|
| Nit | Checklist's "Released Kanmer claim" line is unticked in both the original and "completion" closeout sections despite status = done. Remediation: check for a lingering SIMPLI-014 claim/worktree and release it if still held; tick the stale boxes for hygiene. | [verified] |
| Info | Deliberately no live caller — material only if another document later implies reports are user-reachable before DOCS-001 lands one. No remediation needed for SIMPLI-014 itself. | [verified] |

**Blocked on release 12:** Yes, for any deployment claim — proof correctly scopes to integration/CI tier only ("No cloud or main write occurred").

### PR-009 — Preserve report tails under long content (done, merged #419, deployment n/a)

Fixes a real regression TICK-213's own stress test exposed on merged `dev`: an 80-item/8-photo Repairable render dropped the trailing Statement of Truth section. Root cause precisely diagnosed: Scriban's `TemplateContext.LimitToString` defaults to 1,048,576 characters and silently truncates; a third embedded base64 photo pushed composed HTML past that limit. Fix: `LimitToString = 0` in `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` — a one-line, well-targeted change. Covered by a new real-Chromium regression test in `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` with a documented failing-before/passing-after cycle; full 6/6 renderer suite green afterward. Simplification pass recorded, honest, dated. Zero findings — a clean fix.

**Blocked on release 12:** Yes, for any deployment claim (same as SIMPLI-014 — no live caller yet either).

---

## 4. Open questions / contradictions for the operator

1. **PR #410 ("Release 11") is stale but still open on GitHub.** `git log`/`gh pr view 410` confirms it is `MERGEABLE`/`CLEAN` with 42 commits, titled "Release 11: dev → main (PLAT-006 centred shell + Upload redesign, release-10/INT-31 docs)". DELIV-011's own Outcome (recorded 2026-08-19) states: *"Superseded by [[DELIV-012]]. Release 11 was prepared locally at `feda958f`... the operator held the `main` push... Release 12 carries PLAT-006 and everything since."* DELIV-012's own verification checklist requires `gh pr list --state open` → empty before release 12 closes. **Recommendation:** close PR #410 once DELIV-012 opens its own release-12 dev→main PR, rather than trying to reuse or fast-forward it — its base is already behind `dev`.
2. **INTK-007's own open-questions.md says implementation was "deliberately blocked until kanmer-docs reconciles the protected and governing documents," calling that "a prerequisite, not an open product question."** Yet PR #424 is already open with a full implementation and the ticket is at "review" stage. **Question for the operator:** has that kanmer-docs reconciliation actually happened? If not, INTK-007 was implemented ahead of its own stated blocking condition — worth confirming before merge, independent of the code-level findings in §2.
3. **TICK-044 cleared "review" with its own recorded operator bar unmet.** Its open-questions.md quotes the operator directly: *"A policy referenced only by tests is incomplete and must not pass review as delivered."* Its own checklist shows `MailOperationalDestinationPolicy` still has zero non-test callers. **Recommendation:** either return TICK-044 to implementing for the wiring, or let DELIV-012 remediate it directly as part of PR integration (this research treats it as a should-fix, not a full revert).
4. **Two PRs (#423 INTK-008, #424 INTK-007) make genuine meaning-changes to protected/invariant documents** (`operator-notes.md`'s image-initiated-case description; the PRD sentence CLAUDE.md quotes verbatim about `Needs sorting`'s distinct meaning). Both were run through the ticket's own open-questions.md and, for INTK-008, a corresponding ADR (ADR-0029) — properly authorized, not silent. The residual issue is narrower and mechanical: INTK-007 left `operator-notes.md` internally inconsistent (new + old "Needs sorting" text coexisting) rather than fully reconciling it as its own plan promised. This is a should-fix, not a governance violation, but the operator may still want to eyeball the final `operator-notes.md` diff before release 12 given its protected status.
5. **Git hygiene, pre-existing DELIV-012 scope:** a local branch `pr417check` (head `599bfe6d`, i.e. INTK-006's tip, created 2026-08-19T12:20:01+0100) exists with no matching ticket — likely a manual review branch from this research session or a sibling lane. A stale worktree/branch for `task/deliv-011-release-11` (superseded, per §4.1) also remains checked out at `../pegasus-worktrees/deliv-011-release-11`. Both fall inside DELIV-012's own stated git-hygiene scope ("three local branches and two worktrees") — no operator decision needed, just noting them for the plan phase.

### Tickets currently taken by other agents/machines — must be left alone or coordinated, not touched directly

All ten are taken by **Codex** / **codex-mcp-client** (not `claude-code`), and all are mid-pipeline (review or verifying) with open worktrees:

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
| TICK-033 | codex-mcp-client | `task/tick-033` | `../pegasus-worktrees/tick-033` |

(PLAT-006 is also still taken, but by `claude-code` — this session's own family, not a coordination concern.)

DELIV-012's remediation plan should route fixes for these tickets' PRs through normal PR-comment/follow-up-commit channels (or a scoped subagent working directly on the existing branch, per repo convention for "your own task worktree"), not by touching another agent's worktree directly or force-pushing over in-flight work.

### Deployment-field claims checked against evidence

No ticket in the roster was found to claim `production`/`deployed` incorrectly. The two data-hygiene issues found were **absence**, not overclaim: PLAT-001's `deployment` field is unset entirely (§3), and TICK-011's `not-deployed` is accurate but easy to misread (§3).

---

## 5. Implications for release 12

**Already correctly settled, no release-12 dependency:** TICK-099, TICK-205, TICK-212, TICK-207, TICK-211, TICK-203, TICK-215, TICK-213, TICK-204, DOCS-002 (all zero-diff or docs-only decisions, `n/a`), TICK-010, TICK-009 (already shipped in release 9), DELIV-009, AUTO-002 (already shipped in release 10), TICK-011 (code already shipped in release 10; the remaining gap is caller-wiring, not deployment).

**Proof depends on release 12 / becomes provable once it deploys** (all currently correctly `not-deployed`/unset, already merged to `dev`): **PLAT-001** (only entry-point-bearing UI ticket in the roster — needs the promotion to make its "delivered" claim true, plus fix its checklist doc and missing `deployment` field first), **TICK-093**, **TICK-043**, **TICK-044**, **TICK-046**, **PLAT-006**, **TICK-033**, **SIMPLI-014**, **PR-009**. Of these, **TICK-046**'s `docs/current-architecture.md:85` fix should be bundled into the same release-12 deploy task (per the safety-rail requiring current-state docs to move with the deploy that makes them true).

**Need remediation before they should be considered release-ready, independent of deployment:**
- **TICK-093** — missing GRANT (blocker) + dark `EfRepairSpecificationStore` (should-fix), neither acknowledged in its own docs.
- **TICK-044** — `MailOperationalDestinationPolicy` still has zero callers despite the ticket's own operator bar requiring a real caller before "delivered"; already past review with this unmet.
- **PLAT-006** — trivial (tick a stale checklist box), not release-blocking.
- **TICK-033** — needs a real CI verdict for #408 before its own Done claim is honest; not release-blocking (docs-only, no runtime effect).

**Open PRs — none are safe to merge as-is; all five carry unaddressed reviewer findings and four of five carry the same missing-GRANT defect:**
- **INTK-005** (#416): 2 blockers (missing GRANT; `token:0` regression causing 8 confirmed CI failures) + 2 should-fix.
- **INTK-006** (#417): 1 process blocker (HEAD never CI-tested, CONFLICTING against dev) + 1 should-fix (unchecked per-member association result) — inherits INTK-005's GRANT gap.
- **INTK-007** (#424): 1 blocker (missing GRANT) + 3 should-fix (unreconciled operator-notes.md text; UI label collapse; untracked below-threshold intake) — never CI-tested at all.
- **INTK-008** (#423): 2 blockers (missing GRANT; red CI on a genuine behavioral gap) + 2 should-fix (no simplification pass recorded; normative content in non-normative docs).
- **TICK-045** (#422): 2 should-fix (test doesn't actually exercise classification sharing; undocumented mailbox used in the test) — otherwise green and low-risk; does not touch `MailOperationalDestinationPolicy`, so that gap persists regardless of merge order.

**Recommended remediation order for DELIV-012's plan phase:** fix the recurring missing-GRANT defect once as a shared pattern-fix (TICK-093, INTK-005, INTK-006 shared base, INTK-007, INTK-008 — five migrations, one fix pattern) before integrating any of the open PRs; resolve INTK-005's `token:0` regression before INTK-006 (which is built on top of it) can be usefully re-tested; get INTK-008's CI green before merge; settle the INTK-007 kanmer-docs-reconciliation question (§4.2) before or alongside its merge; TICK-045 can merge with only minor test-quality caveats once its own should-fix items are triaged.
