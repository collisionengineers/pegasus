# collisionspike — A Retrospective for Generation Four

*Compiled 2026-07-22 by Claude from the repository record: 950 commits, 298 tickets, 35 ADRs, 16 plans, 6 review sessions, and the session memories. Every claim below carries a ticket, PR, commit, or path; if a criticism has no ID, it was cut. Rules of the text: decisions are named, not passive ("you chose", "a sub-agent wrote"); the agents are instruments, not scapegoats — and this document is written by the same class of tool that produced several of the failures it describes, which is itself an argument for rule R9.*

---

## §0 The Verdict

Nine weeks and 950 commits produced a system that has never processed production traffic. It reached a one-provider alpha on 2026-07-21 at ~14:00Z and failed on its first real email about four hours later — a staff-forwarded QDOS instruction classified as `query`, no case minted (PLAN-016, rebuild branch). The alpha is paused. The core intake engine is being rebuilt from scratch in a draft PR (#166). Production readiness stands at 3 of 63 tickets (PLAN-004), and the production cutover ticket is blocked (TKT-178). This is at least the third generation of an attempt at this domain; the predecessors are in `archive/`.

The project did not fail from lack of effort or lack of rigor. It failed because **rigor was pointed at the repository instead of at reality** — and machine cadence let that misallocation compound at roughly 240 commits per week. The repo has a 128,427-line reconciliation ledger regenerated 118 times, ~20 CI gates, and a guard that guards the guard register; what it never had, until four hours after cutover, was contact with one real email.

The counterweight is real: the same record shows the habits that prevented catastrophe — a dry run that stopped a data-destroying migration (TKT-059), an eval harness that overruled a panic, ticket bundles honest enough to make this retrospective possible. Those go in §5, and they are keepers.

The test for generation four is one sentence: **real-shaped traffic through the thinnest possible end-to-end slice in week one — not week nine.**

---

## §1 The Numbers

| # | Measure | Value | One line |
|---|---|---|---|
| 1 | Duration | 9 weeks (2026-05-22 → 2026-07-21), ~6 active | Weeks 1–4 were hand-cadence (1, 2, 4, 0 commits) |
| 2 | Commits | 950; peak 246/week | Machine cadence from mid-June |
| 3 | Governed artifacts | 298 tickets, 35 ADRs, 16 plans | ~40% of tickets are bug/rework/hardening; 17 of 25 ADRs corrected in a single review day |
| 4 | Process : product files | ~1.7 : 1 (≈2,039 vs 1,173) | Process/docs/tooling outnumber product source |
| 5 | docs/tickets/ alone | 1,402 files = 34% of the repo | The ticket system has more files than all product source |
| 6 | Commit targets | 52.5% touch docs/, 22.5% touch product | 225 commits are pure markdown |
| 7 | Largest committed file | 128,427-line generated ledger | Regenerated in 118 commits; its sibling (48K lines) in 129 |
| 8 | npm scripts | ~40 of 58 (69%) are governance | ~16–18 actually build, bundle, or test |
| 9 | Governance PR run | ~30 consecutive PRs (#100, #110–#141) | Spanned the two highest-velocity weeks of the project |
| 10 | Production readiness | 3 / 63 (PLAN-004); TKT-178 blocked | After 950 commits |
| 11 | Alpha survival | ~4 hours to first real-email failure | Engine now being rebuilt from scratch (PR #166, draft) |
| 12 | Infra cost | ~£51/month, all dev-tier | The one unambiguously good number |

---

## §2 What This Project Was

The first commit is 2026-05-22. For three weeks the repo moved at hand speed — seven commits total, then a silent week. From mid-June the agents took over the cadence: 146, 57, 138, 246, 237, 119 commits per week until the alpha. The name says "spike," but what got built is a monorepo with two TypeScript services (253 Azure Functions between them), ~7 Python function apps, a React SPA, 66 Postgres tables, Box/EVA/Graph/DVLA integrations — and 298 tickets, 35 ADRs, and ~20 CI gates around it.

The lineage matters. `archive/` holds valuationbot, valuationbot-claude, collisioncc, ccc, and cedocumentmapper: this domain has been attempted and abandoned before, which makes collisionspike at least generation three. Mid-flight, the project also absorbed two sibling repos with history (collisioncapture PR #144, cedocumentmapper_v2 PR #145), performed a full "repository structure and documentation reset" (PLAN-006, PR #100 — whose own review found it 57 commits behind main, silently reverting five tables), and ends the period with a from-scratch rebuild of its core engine open in PR #166. Restarts are this domain's most expensive recurring line item — and the reason this document exists is to make the fourth one cheaper than the third.

---

## §3 Five Stories

### Story 1 — Nine hundred fifty commits to an untrusted engine

**What happened.** On 2026-07-21 at ~14:00Z the system went live on one mailbox for one provider. At 18:16Z the first real instruction arrived — forwarded by staff, subject `(EREF9) RTA on 19/07/2026` — and the classifier filed it as `query`. No case was minted; one appeared only because the ADR-0022 retro fallback reconstructed it from Box (PLAN-016, rebuild branch). The alpha paused the same day, and the decision was made to rebuild the engine rather than patch it.

**Root cause.** The classifier was 20 rules evaluated first-match-wins **in the order they had been added** — `0a 0 0b 0c 0d 0e 0f 1 2 3 4 4a 4a2 4b 4c 4d 5 5b 5c 6` — and that ordering "was never re-derived as rules accumulated" (TKT-312, rebuild branch). The most expensive, least-spoofable signal — actually opening the document and typing its content — ran last and could be vetoed by a filename regex: a file called `Bodyshopreport-V1.pdf` overrode what the content had already proven. Around the classifier, the same accretion pattern: three independent Box-folder-creation implementations with inconsistent safety checks, five overlapping "which Box root" settings, a superseded triage generation left permanently registered "out of caution" (commit 955eb4db), an 804-line `intakeOrchestrator.ts` with ~73 casts and its evidence-attach pipeline copy-pasted 3–4 times with silent policy divergence (review 200726).

**What it cost.** A formal simplification program (PLAN-007..012: six plans, ~23 tickets, a new package, a standing guard suite) to unwind nine hand-rolled token mints, four HTTP wrappers, three route lanes, three outbox drains — and after all that, the engine still had to be rebuilt. Worse: the rebuild repeated the failure class. The wire-in commit (3e6ecc90) records that the new engine "was registered but had NO caller," and that its Stage 1 keyed on the sender address while **every** alpha instruction arrives as a staff forward — "the pipeline short-circuited on its first branch for 100% of alpha traffic." Its corpus tests hid this: fixtures hardcoded the ideal provider address, and their `From:` lines were "decorative — the engine never read them."

**Agent-speed factor.** Each session re-solved already-solved problems faster than any human could notice the copies accumulating; several copies carry comments that literally say "mirrors lib/data-api.ts" — drift acknowledged in-code, at write time, and committed anyway.

**Rules produced:** R5 (real caller, real-shaped input), R6 (rule of three), R7 (ranked precedence, re-derived).

### Story 2 — Verification theater

**What happened.** On 2026-07-09, TKT-067 was marked **VERIFIED-LIVE** with millisecond-level wire proof ("zero residual turns … state captured at 11ms/210ms"). On 2026-07-13 the same ticket logged **FAILED (live regression)** — the New-chat button broke in a state the proof had never visited (attachment decision pending). TKT-001, TKT-024, and TKT-054 have the same reopen arc; TKT-067 sits in `now` today.

**Root cause.** The proof proved the state the author had just built, not the states the system could be in. The purest specimen came from the rebuild: a sub-agent implemented the Case/PO prefix mapping with two values swapped against your explicit spec — **and wrote the test file asserting the swapped values.** The suite was green; the business rule was backwards. Self-consistent is not spec-consistent. The pattern repeats at every level: the TKT-269 parity guard "passed" by encoding the VRM hyphen divergence as an `allowedDivergence` — which review 200726 (D2) then ruled "a defect … not a blessed divergence"; a docstring claimed two MIME tables "mirror EXACTLY" when they didn't, with no guard covering the pair at all (TKT-270 C3); and the repo-reset review (150726) ruled the `check:reconciliation` gate "false assurance … tautological (labels every path, exempts deletes); it cannot detect a dropped or byte-corrupted file" — which is *why* a branch 57 commits behind main, silently reverting five tables, showed green CI (PR #100).

**What it cost.** Every reopened ticket, plus something worse than bugs: calibration. When green can mean "the author agreed with itself," green stops carrying information, and the only reliable defect detector left was your own eyes on screenshots (§5).

**Agent-speed factor.** The author, the tester, and often the reviewer were the same generative process; green became a property of the session, not of the system.

**Rules produced:** R8 (state matrix), R9 (never self-grade), R10 (watch every guard fail once).

### Story 3 — The governance machine ate its operator

**What happened.** The machinery built to keep agents honest became the largest thing in the repository and the largest consumer of its commits. Two generated JSON ledgers totalling ~177K lines are committed and were regenerated in 118 and 129 commits respectively; ~380 commit-touches exist just to regenerate four ledgers; `generate:governance` runs the inventory generator four times, reconciliation three times, and the tree twice because the generators don't reach a fixed point in one pass. The hygiene CI job runs ~20 gates on every push, docs-only pushes included. There are ~30 check scripts, each with its own test twin, and a meta-guard that guards the guard register (TKT-271). 69% of the npm scripts are governance. And the machinery exists **twice** — the parent `collisionsuite/` repo has its own `.agents/`, manifest, and plans.

**Root cause.** Every control was added in good faith, usually after a real scare — but none carried an expiry or a named-incident requirement, so controls only ever accumulated. Process was sized to an imagined enterprise rather than to a single-operator spike whose actual bottleneck was "has a real email ever gone through?"

**What it cost.** The opportunity cost is dated and precise: PRs #100 and #110–#141 — roughly thirty **consecutive** PRs of pure internal governance — landed across the two highest-velocity weeks of the entire project (246 and 237 commits), while the intake engine those weeks could have hardened stayed untrusted and then failed on its first real message. Smaller symbols: the reciprocal AI-review guard generated ~22 remediation commits before being retired (TKT-149); 52.5% of all commits touch docs/ against 22.5% touching product.

**Agent-speed factor.** Governance was built to police the agents, then became the biggest thing the agents churned — the drift detector became the drift.

**Rules produced:** R4 (process budget), R12 (agents don't build process), R16 (weekly ratio check).

### Story 4 — The day the data almost died

**What happened.** The then-binding go-live plan called for wiping derived data and rebuilding it by replaying the intake mailboxes. On 2026-07-05 you ran a read-only dry run first (TKT-059). The mailboxes held 117 messages against 390 processed emails in the database — staff file or delete mail after handling it; Deleted Items held 7,081 / 9,485 / 7,107. The replay would have recovered ~88 of 390 and destroyed ~150 cases. The stored `.eml` fallback was also incomplete (212 of 390, keyed by the wrong message-id). The plan died in the dry run instead of in production.

**Root cause.** A binding plan written against an assumed world: "the mailboxes retain everything" was never a verified fact, just an unexamined premise — and two adjacent traps nearly compounded it. First, the same investigation initially showed a naive reprocess would change 62% of stored categories — a panic-grade number that turned out to be a **diagnostic artifact** (the test harness starved the classifier of attachment signal); the eval harness proved the stored classifications largely correct (`receiving_work` recall 94%), and you then declined even the "safe but low-value" reprocess. Second, RLS: without `SET ROLE csadmin`, every baseline query returns zero rows — a state that "looks exactly like a wiped database but is NOT," which is precisely the observation that could have justified a wipe.

**What it cost.** Almost nothing — which is the point. This is the cheapest catastrophic-loss prevention in the whole record, and it also produced the best follow-through: TKT-106 deleted the dark replay driver entirely, on the explicit reasoning that dead, permanently-off destructive code "can mislead a later session into thinking a wipe/replay is a live option."

**Agent-speed factor.** Any sufficiently obedient session would have executed the binding plan competently and confidently; the dry-run habit is what made the plan check itself against reality first.

**Rules produced:** R13 (destructive ops: rehearse read-only, verify the baseline under the right role, prove the recovery source), R15 (delete dangerous dark affordances).

### Story 5 — Running blind

**What happened.** One case whose `box_folder_id` pointed outside the pinned Box roots generated **1,896** `boxFolderCreate` exceptions in a single day (TKT-303): `functions-client.ts` mapped every non-2xx to a plain `Error`, two stacked Durable retry policies amplified each wake into ~12 activity executions, and the defer backoff never reached zero. The follow-up audit (TKT-305) found this was a *pattern*, not an incident: another monitor ran at four figures per day for at least six days with "no root cause … established for ANY"; 2,528 of 3,630 exceptions traced to a single stuck case. Meanwhile the nightly Box purge exhausted the dev-tier connection pool and "nothing was purged" (TKT-227) — while its own metric reported `{purged: results.length}`, counting attempts as successes. Elsewhere, a queue label rendered "Images received" regardless of what the file was, and four silent-catch paths hid Function death from staff (review 200726 B6; TKT-226).

**Root cause.** One missing primitive: nothing in either language distinguished terminal from transient failure, so nothing could ever give up, park a poison row, and say so honestly. On top of it, several signals lied politely — which meant "0 exceptions" and "0 errors" dashboards were unfalsifiable noise.

**Agent-speed factor.** Each retry lane was built fresh per feature (no shared retry primitive existed in either language — PLAN-007's own register), so the missing taxonomy was re-omitted every time.

**Rules produced:** R3 (terminal/transient/unknown from the first client), R14 (metrics count successes; "0 errors" needs a heartbeat).

---

## §4 The Minor Register

| What happened | Evidence | Lesson in one line |
|---|---|---|
| Windows/CLI friction: WAM broker dead, cmd.exe mangles `&`, App Insights CLI rejects multi-line KQL and `order by`, `-o tsv` mangles rows | session memories; docs/azure notes | Budget for tooling quirks; record each one once in a machine-quirks memory, never rediscover |
| Free-tier App Insights telemetry expires fast | TKT-226/303 evidence notes ("re-run same-day") | Capture evidence the day of the incident or treat it as gone |
| A 17-ticket misclassification wave was found by operator screenshots, not by any of ~20 CI gates | TKT-029–043, 081–083, 097, 120; reviews 190626/010726/020726 | Human eyes on real output out-detect machine gates (→ R17) |
| 17 of 25 ADRs needed substantive correction in one review day | docs/reviews/160726/decisions.md | Docs drift from code at machine speed; schedule reconciliation reads |
| Retention/erasure feature built, then withdrawn, then scheduled for deletion | ADR-0017 (withdrawn); TKT-206 | Features nobody asked to keep are pure carrying cost |
| Components split to sibling repos, then reabsorbed with history | ADR-0018→0035, ADR-0034; PRs #144/#145 | Repo boundaries redrawn twice = the domain contract wasn't settled |
| Four "LIVE" feature-wave PRs closed unmerged | PRs #47–51 | Work that never lands is invisible waste — count it |
| The repository *reset* itself shipped 57 commits behind main, silently reverting 5 tables, with green CI | PR #100; docs/reviews/150726 | The biggest cleanups need the most adversarial review |
| Dark surface: vision family built dark, EVA poll stub logs "poll body is not built", MCP ingest is a fail-closed no-op (permission never created), zombie evaValidation app, unrecorded "P2P Server" registration, a live capture SPA found untracked on 07-20 | docs/operations/feature-gates.md; TKT-154/228; LIVE_FACTS.json | Built-but-unwired accumulates silently; audit it on a clock (→ R18) |
| Three idempotency `request_hash` serializers are not byte-compatible — reordered keys defeat the guard | TKT-270 M2 | Guards that were never watched failing may not guard (→ R10) |
| The hottest file: 804 lines, ~73 casts, one pipeline pasted 3–4× with silent divergence | intakeOrchestrator.ts; review 200726 | Hot files need delete-or-generalize pressure, not more branches (→ R6) |
| Staff conflated "live on a mailbox" with "in production"; you had to pin a cutover note into LIVE_FACTS | commit bf5a71af, `liveCutoverNote` | Name the alpha/production boundary in writing on day 0 (→ R1) |

---

## §5 What Went Right — Keep Doing

These are the behaviors that limited the damage, with the same evidentiary standard as the failures.

**The dry run that saved 150 cases** (TKT-059) is the single highest-ROI act in the record: one read-only rehearsal killed a binding, data-destroying plan. **The eval harness beating intuition** is its equal: when a 62%-corruption panic appeared, the harness proved the stored data was largely correct (94% recall) and prevented a "fix" that would have been the actual corruption. Keep both habits exactly as they are.

**Honest ticket states.** TKT-303 was deliberately parked in `verify`, not `done`, because "the first acceptance line is not yet proven live" — and its verification file self-reports a git-hygiene slip and a tool fault. `done → now` regression reopen is a scripted, first-class transition. That candor is why this retrospective could be written from the record instead of from memory; the ticket bundles (spec / research / changes / verification / evidence) are the project's real memory and they worked.

**Your screenshot reviews were the best defect detector the project had.** The entire misclassification wave and most UX defects came from dated operator reviews of real output (190626, 010726, 020726) — not from any of the ~20 CI gates. The 2026-07-20 six-persona structural review also earned its cost: it found real defects and *overturned* one of the cleanup program's own ideas (the PLAN-008 outbox mega-drain) before it got built. And the 160726 ADR read-through — you personally reading all 25 — caught the doc-drift nobody else could.

**Deleting dangerous affordances** (TKT-106) — removing the dark replay driver so no future session could mistake a wipe for a live option — is the correct instinct generalized in R15. Finally, **£51/month, all dev-tier** for this much running infrastructure is genuinely disciplined; the project's costs were paid in attention, never in cloud spend.

---

## §6 The Playbook

*Admission test: a rule enters only if (1) it has a named scar from this project, (2) you can tell in the moment whether you're violating it, and (3) it survives the platitude check — if it would read as sensible on any engineering blog without its scar, it gets rewritten until the scar does the work, or cut. Six rules are marked **IRON**: the ones whose violation cost the most. Expiry: re-derive this list after generation four's first month; delete any rule whose trigger never fired.*

### P1 — Day 0: decisions that are only cheap once

**R1 · IRON — Write the finish line before the scaffold.** One page: what counts as alpha, what counts as production, and the single end-to-end slice that must survive real traffic before anything else is built.
- *Trigger:* before the first commit.
- *Scar:* readiness 3/63 after 950 commits, and a pinned `liveCutoverNote` (bf5a71af) needed to stop "live on a mailbox" being mistaken for production.

**R2 — Freeze the domain contract in one page** (entities, taxonomy, provider model) before generating code; changing it is a stop-the-line event, not a refactor.
- *Trigger:* the first time an agent proposes a new entity, prefix, or category.
- *Scar:* ≥3 predecessor generations, a mid-flight repo reset (PR #100), and 17/25 ADRs corrected in one day (160726).

**R3 · IRON — Every external call classifies its failure: terminal | transient | unknown**, in every language, from the first client written. Terminal parks the row and stops.
- *Trigger:* writing any `catch` around an outbound call.
- *Scar:* 1,896 exceptions/day from one case (TKT-303); six days of four-figure retries with no root cause for any (TKT-305); a purge that purged nothing (TKT-227).

**R4 — Set the process budget: zero committed generated artifacts; every CI gate names the incident that justifies it in a comment; governance scripts capped (~10).**
- *Trigger:* the first time anyone proposes committing a generated file or adding a gate.
- *Scar:* a 128,427-line committed ledger regenerated 118×; 69% of npm scripts are governance; `generate:governance` loops its own generators to force a fixpoint.

### P2 — The per-feature loop: every ticket, no exceptions

**R5 · IRON — Nothing merges until its real caller has exercised it with real-shaped input.** "Registered but uncalled" is an incomplete state, not a milestone.
- *Trigger:* the moment a component is declared done or its PR opens.
- *Scar:* the rebuilt engine deployed with "NO caller," keyed on sender addresses while 100% of alpha traffic was staff forwards; its fixtures' `From:` lines were decorative (3e6ecc90).

**R6 — Third copy = stop and extract.** Any ticket touching a hot file (intake, SPA contract, outbox lanes) must delete or generalize at least one branch before adding one.
- *Trigger:* writing something that already exists twice; opening the hottest file.
- *Scar:* token mint ×9 ("mirrors lib/data-api.ts"), wrapper ×4, Box folder-create ×3; an 804-line orchestrator whose cleanup needed six plans (PLAN-007..012).

**R7 — Rule systems keep an explicit ranked precedence model, re-derived every time a rule is added.** Append order is never allowed to be the semantics.
- *Trigger:* adding a rule/branch to any classifier, router, or matcher.
- *Scar:* 20 rules first-match-wins in accreted order; a filename regex vetoed content typing; first real email misfiled (TKT-312, rebuild branch).

**R8 — "Verified-live" requires an enumerated state matrix** naming the states the proof covered; an unlisted state is unverified by definition.
- *Trigger:* writing the word "verified" in any verification.md.
- *Scar:* TKT-067 — VERIFIED-LIVE on 07-09 with ms-level proof, FAILED 07-13 in a state the proof never visited.

### P3 — Working with agents

**R9 · IRON — The agent that wrote the code never grades it.** Acceptance evidence comes from an independent path: your eyes, a live probe, or a second agent given only the spec. For any hand-written mapping/enum table, read the literal values against the spec yourself.
- *Trigger:* any sub-agent reporting "tests pass," especially with a lookup table in the diff.
- *Scar:* a sub-agent wrote the prefix mapping backwards *and* the tests asserting the backwards values — green suite, wrong business rule.

**R10 — A guard is trusted only after you have watched it fail.** Every new gate ships with an adversarial negative case; a gate that has never fired by its expiry gets deleted.
- *Trigger:* adding any check script, parity corpus, or CI gate.
- *Scar:* `check:reconciliation` ruled "tautological" while green CI hid a 57-commit reversion (150726); a parity guard passed by blessing the defect as `allowedDivergence` (TKT-269 vs 200726-D2).

**R11 — Search before write.** Agent instructions require locating the existing implementation — or proving its absence — before creating any helper, client, or wrapper.
- *Trigger:* an agent about to create a file ending in `-client`, `-helper`, `-util`, or a second copy of anything.
- *Scar:* nine managed-identity token mints, several self-annotated as mirrors of the first.

**R12 — Agents don't get to build process.** Any governance, gate, ledger, or workflow work an agent proposes requires a human-named prior incident, or it is declined.
- *Trigger:* an agent proposing a new check, ledger, adapter layer, or "hygiene" improvement.
- *Scar:* ~30 consecutive governance PRs (#100, #110–#141) through the project's two fastest weeks while the engine stayed untrusted; a meta-guard for the guard register (TKT-271).

### P4 — Operating the live system

**R13 · IRON — Before anything destructive or irreversible: a read-only dry run, a baseline verified under the correct role, and the recovery source proven to exist.**
- *Trigger:* any plan containing wipe, drop, purge, rebuild, migrate, or bulk-update.
- *Scar:* the 2026-07-05 replay plan would have destroyed ~150 cases; mailboxes held 117/390; RLS false-zeros made a healthy DB "look exactly like a wiped database" (TKT-059).

**R14 — Metrics count successes, not attempts; "0 errors" is only meaningful next to a heartbeat proving activity.**
- *Trigger:* writing any counter, health check, or completion log line.
- *Scar:* `{purged: results.length}` while nothing purged (TKT-227); "0 exceptions" dashboards rendered meaningless by one stuck case emitting 2,528 (TKT-305).

**R15 — Dangerous dark affordances get deleted, not gated.** Code that could destroy or corrupt data is removed the day it's superseded — a later session can't tell "permanently off" from "available."
- *Trigger:* turning any destructive capability "off."
- *Scar:* positive — TKT-106 deleting the replay driver; negative — a superseded triage generation left co-registered "out of caution" (955eb4db).

### P5 — The standing weekly check

**R16 — Ratio check: if commits touching process exceed ~30% of the total two weeks running, stop building controls and start deleting them.**
- *Trigger:* a 5-minute `git log` count, same time every week.
- *Scar:* 52.5% of all commits touched docs/; 22.5% touched product.

**R17 · IRON — The screenshot ritual: a fixed weekly slot of your eyes on real system output.** It was the best defect detector this project had.
- *Trigger:* calendar, weekly, non-negotiable.
- *Scar:* the entire misclassification wave (TKT-029–043 and successors) was found by your screenshots; ~20 CI gates found none of it.

**R18 — Dark-surface audit: anything built-but-unwired for two weeks gets a caller or gets deleted; reconcile the cloud estate against the repo monthly.**
- *Trigger:* the same weekly slot as R16/R17, monthly for the estate.
- *Scar:* a dark vision family, an EVA stub logging "poll body is not built," a zombie function app, and a live SPA discovered untracked on 2026-07-20.

---

## §7 Appendix — Evidence Index

Base path: `collisionsuite/active/collisionspike/` (branch `main` unless noted). Session memories live in the Claude project memory for this repo.

| Claim | Where |
|---|---|
| Timeline, cadence, 950 commits, first commit 2026-05-22 | `git log` (repo); weekly histogram via `git log --format=%ad --date=format:%Y-%W` |
| Alpha cutover + scope; DB wiped/reseeded for alpha | `docs/tickets/plans/PLAN-015-app-alpha-testing.md`; commit bf5a71af (`LIVE_FACTS.safetyGates.liveCutoverNote`) |
| First real email misfiled; alpha paused; taxonomy rewrite | `PLAN-016-inbound-triage-taxonomy-rewrite.md` + TKT-312 (branch `worktree-email-engine-rebuild`); PR #166 (draft) |
| Classifier accreted-order root cause; filename veto | TKT-312 (rebuild branch); commit 955eb4db (three Box-folder impls, five root settings, co-registered generations) |
| Rebuild deployed with no caller; staff-forward shape; decorative fixtures | commit 3e6ecc90 (message, verified verbatim) |
| Over-build register: mint ×9, wrapper ×4, lanes ×3, drains ×3, trust seam | `workingspace/architecture-simplification/00-overview-register-and-plan0-handoff.md`; `docs/tickets/plans/PLAN-007..012*.md` |
| Structural review findings (804-line orchestrator, ~73 casts, pasted pipeline, dead casts, kitchen-sink feature) | `docs/reviews/200726/review1.md`, `review2.md` |
| Repo-reset blockers invisible to green CI; tautological gate | `docs/reviews/150726/` (PR #100) |
| 17/25 ADRs corrected; D1 per-marker numbering; D12 false header | `docs/reviews/160726/decisions.md` |
| Replay near-miss numbers (117 vs 390; 7,081/9,485/7,107; ~150 cases) | `docs/tickets/done/TKT-059-replay-wipe-rebuild/verification.md` (verified verbatim); driver removal: `done/TKT-106-remove-replay-backfill/` |
| RLS false zeros; csadmin baseline (164/389/390) | TKT-059; `docs/azure/postgres.md`; session memory |
| Retry storms: 1,896/day; ×12 per wake; four-figure audits | `docs/tickets/verify/TKT-303-…/` (spec + `evidence/diagnosis-2026-07-21.md`, verified); `backlog/TKT-305-eternal-monitor-retry-audit/` |
| Purge exhaustion, dishonest purge metric | `docs/tickets/now/TKT-227-box-purge-connection-exhaustion/evidence/audit-findings-2026-07-16.md` |
| Dishonest labels, silent-null subtype, FW26029 incident | `docs/tickets/now/TKT-226-…/evidence/incident-summary-2026-07-16.md` |
| VERIFIED-LIVE → FAILED arc | `docs/tickets/now/TKT-067-assistant-new-chat/verification.md` |
| Parity guard blessing a defect; "mirrors EXACTLY" false claim; hash divergence | `done/TKT-269-…/verification.md`; `done/TKT-270-hardcore-repository-drift-audit/evidence/audit-report-2026-07-20.md` (C3, M2) |
| Governance weight: ledger sizes/regen counts, script census, PR run | `docs/governance/repository-{reconciliation,inventory}.json`; `package.json`; `gh pr list`; `.github/workflows/ci.yml` |
| Dark surface inventory | `docs/operations/feature-gates.md`; `LIVE_FACTS.json`; TKT-154/206/228; TKT-149 |
| Lineage / prior generations | `collisionsuite/archive/` (valuationbot, valuationbot-claude, collisioncc, ccc, cedocumentmapper); PRs #144/#145 (absorptions); PR #100 (reset) |

**Primary-source reading list** (the eight documents worth rereading in full): `docs/reviews/200726/review1.md` · `docs/reviews/200726/review2.md` · `docs/reviews/150726/overview.md` · `docs/reviews/160726/decisions.md` · `docs/tickets/done/TKT-059-replay-wipe-rebuild/verification.md` · `docs/tickets/verify/TKT-303-terminal-archive-failure-retry-loop/` (spec + verification) · `docs/tickets/backlog/TKT-305-eternal-monitor-retry-audit/` · `docs/tickets/done/TKT-270-hardcore-repository-drift-audit/evidence/audit-report-2026-07-20.md`.

*Expiry note (per R10/R4): this document is itself a control. Re-derive the playbook after generation four's first month; delete any rule whose trigger never fired.*
