# Pegasus documentation audit

**Audit date:** 8 September 2026  
**Repository:** `collisionengineers/pegasus`  
**Reviewed branch:** `dev`, not `main`  
**Pinned revision:** `26ba4ed408317cccdb354dc1e115b0297f15df94`  
**Head commit:** PR #701, Windows ORAS release-repair guidance; 8 September 2026, 07:11:28 UTC. The final branch recheck returned the same revision.  
**Disposition:** documentation needs substantive reconciliation before a structural cleanup. No repository, board, cloud, mailbox or Box state was changed.

## 1. Verdict

The main problem is not that Pegasus lacks documentation. It is that several documents answer the same question, and a newer paragraph is often appended without removing or explicitly retiring the incompatible older rule. The result is a repository in which an agent can follow a plausible, apparently authoritative instruction and still implement the wrong behavior.

There are three separate jobs:

1. Reconcile current requirements, especially Audit allocation, native reports, readiness, upload limits, account administration and EVA re-sends.
2. Give each kind of information one owner: product requirement, functional rule, technical decision, engineering policy, procedure, delivery state, or observed runtime state.
3. Shorten the automatically loaded agent instructions and move task-specific procedures behind explicit, discoverable entry points.

Do these in that order. Moving contradictory text into skills would merely make the contradictions harder to discover. Deleting history indiscriminately would lose evidence and would not establish a correct current rule.

### What was examined

The audit examined the requested root and top-level documentation, the PRD and its index, all twelve FRDs, all 39 issued ADRs through ADR-0040, their index and supersession relationships, the QDOS mapping companion, workspace retirement guidance, the historical Stitch guide, the two earlier session plans, selected Kanmer skills/templates and the existing release skill. Repository trees and blob identities were read from GitHub at the pinned revision. Selected executable consumers were read, particularly CI change routing and the QDOS acceptance script.

The supplied archive was extracted. Its four files include a structured 5 September audit with **29 groups and 71 individual observations**, a corresponding Markdown report, a path list, and an undated earlier review containing **30 observations**. The accompanying crosswalk accounts for each observation separately.

Eight of the twelve standalone uploads match the corresponding `dev` blobs after line-ending normalization. `AGENTS.md`, `docs/engineering.md`, `docs/index.md` and `docs/runbook.md` differ. Current GitHub content was consulted for those differences; the runbook's current build section was fetched directly, while the supplied full runbook also informs the procedural extraction map. That map is a migration proposal, not a certification that every copied command is executable at the pinned SHA.

### Evidence limits

This is a source and documentation audit, not a live acceptance test. No .NET build, browser workflow, Azure query, external integration call or Kanmer stage change was performed. No complete repository checkout was available for a whole-tree link-check execution. The substantive requested governing documents were read; this is not a claim that every generated HTML snapshot, raw vendor reference, principal example, historical changelog line or application implementation was independently verified. Findings about runtime state are explicitly based on the repository's dated observations, not fresh observations by this audit.

All repository path references below mean the pinned revision unless explicitly identified as an uploaded historical review. A path plus section is the evidence locator. Repository source permalinks use this prefix:

```text
https://github.com/collisionengineers/pegasus/blob/26ba4ed408317cccdb354dc1e115b0297f15df94/
```

## 2. Highest-priority findings

Priority P1 means a documentation conflict can change implementation, approval or acceptance behavior. P2 means a misleading owner, status or technical description. P3 means a maintenance or navigation defect. These priorities do not assert an observed production incident.

| Audit ID | Priority | Finding | Required correction and evidence |
| --- | --- | --- | --- |
| AUD-01 | P1 | Standalone Audit allocation still has two incompatible contracts. | FRD-01, PRD, CONTEXT and the QDOS companion allocate normal Case/PO before the later Audit reference. FRD-02 **Mandatory pre-case gates** still makes the original report and literal outcome prerequisites to allocation. Correct FRD-02, capability INT-25 and current-architecture's old descriptions to the accepted contract. |
| AUD-02 | P1 | Final, retained report generation coexists with a draft-only, save-nothing action description. | FRD-11 **Initial renderer activation** now specifies immutable generated artifacts and custody, but **Report-draft entry point** says “Generate report draft” and nothing is saved. Separate preview, generation, approval and sending precisely; use the accepted Generate report label. |
| AUD-03 | P1 | Native engineering/report scope is still constrained by earlier mandatory-EVA statements. | The PRD and 6 September decisions make EVA optional. Operator-notes **Report readiness after Review**, architecture's EVA section and capability narratives preserve older dependencies. Replace those current-facing constraints; keep dated deployed limitations separately. |
| AUD-04 | P1 | Accepted source upload limits disagree across normative files. | Open-decisions records 100 MiB/file, 20 files and 200 MiB aggregate plus 64 KiB multipart overhead. FRD-02, FRD-05, FRD-12 and engineering/capability descriptions retain 10 MiB or pending-research wording. Move the settled contract to FRD-02 and reference it elsewhere. Do not change the distinct Provider API envelope or historical release-37 limits. |
| AUD-05 | P1 | `capabilities.md` is executable acceptance input, despite describing itself as allocation-only. | `scripts/Invoke-QdosAlphaAcceptance.ps1` parses its six-column table and treats every alpha-targeted row as required evidence. Structural edits must update this consumer/tests. Allocation is not a sufficient acceptance-scope predicate. |
| AUD-06 | P1 | A legacy acceptance runner can resurrect expressly deferred evidence work. | The same script requires an approved dataset of exactly 2,000 cases, while the current programme excludes that cohort/soak run and OPS-09 gates no release. Identify the runner as an optional legacy/evidence profile or revise its scope deliberately; it is not proof that current CI already blocks all v1 work. |
| AUD-07 | P1 | Password-reset and account-review requirements conflict. | FRD-04's generated temporary-password workflow conflicts with FRD-12/capability wording about an Administrator-entered password. FRD-12/older operator role text retain account-review actions after periodic reviews were removed. Reconcile the functional and UI contracts together. |
| AUD-08 | P1 | Current readiness rules are contradicted by blanket “any save returns Not ready” prose. | FRD-01's D44 rules distinguish actual relevant changes from unchanged/unrelated saves. FRD-07 and architecture retain broader invalidation statements. Describe exactly which changes invalidate which gate, not an unconditional reset. |
| AUD-09 | P1 | EVA re-send rules remain contradictory. | ADR-0038 selects manual-only submission, including explicit re-send. The resolved EVA entry in open-decisions and older runbook/capability prose retain at-most-once or automatic-submission language. Retain unknown-outcome protection without prohibiting the separately confirmed re-send. |
| AUD-10 | P1 | Persistent OAuth keys and grant attribution lack a reconciled active technical decision. | ADR-0027 describes ephemeral keys and restart invalidation, while FRD-10/runbook specify persistent certificate material and distinct grant attribution. Write the technical successor and update its consumers. |
| AUD-11 | P1 | Mail occurrence identity differs between the technical decision and functional contract. | ADR-0024's immutable Graph-item-based occurrence identity differs from FRD-08's mailbox/RFC Message-ID duplicate boundary. Record the intended identities and exactly where each deduplicates; do not conflate transport identity, arrival occurrence and thread identity. |
| AUD-12 | P1 | Image-reference Box custody contradicts ADR-0029's exclusions. | FRD-05 specifies a dedicated image-reference folder and merge behavior, whereas ADR-0029 explicitly withholds that folder pending another decision. Record the custody successor; do not erase either identity's history. |
| AUD-13 | P1 | The durable staff-send operation journal is not reconciled with the outbound-mail ADR. | FRD-08 describes durable draft/upload/submission recovery. ADR-0036 says no new store/second outbound record. Explain the journal as operational state, separate from authoritative observed Sent evidence, and record the changed persistence choice. |
| AUD-14 | P1 | A mutable, derived `operator-notes.md` is still treated as absolute upstream truth. | The document combines original statements, interpretations, future scope and implementation claims. Retire it through a statement-preservation map; do not simply move it to another umbrella “business truth” file. |
| AUD-15 | P1 | The allowed-document rule prevents the intended cleanup. | AGENTS/index permit only new PRDs, FRDs and technical ADRs. Explicitly permit scoped engineering/reference/procedure and skill-support files in named locations while keeping ticket plans/reviews/proof on Kanmer. |
| AUD-16 | P2 | AGENTS makes a whole-solution build the gate for every delivery. | Current AGENTS **Commands/Verification** and runbook **Locked restore, build, and test** conflict with path-routed CI, focused evidence and the one-heavy-verifier policy. Adopt the change-sensitive policy in section 4. |
| AUD-17 | P2 | Startup context is close to the default Codex instruction-byte budget. | Current AGENTS is 31,557 bytes, not the uploaded 27 KB version. Under an unchanged 32 KiB project-instruction cap that leaves only 1,211 bytes. This is a configurable combined budget, not a universal per-file hard maximum. |
| AUD-18 | P2 | The authority ladder makes schedule outrank technical decisions and hides question-specific ownership. | Replace the linear chain in docs/index.md with the owner matrix in section 3. Code can establish as-built behavior; passing tests cannot establish that the required behavior is correct or accepted. |
| AUD-19 | P2 | `open-decisions.md` is mainly a mixture of policy, history and evidence work. | Remove settled decisions after moving any unique content; retain only an actual unanswered question, its decision owner and the precise work it blocks. See section 6. |
| AUD-20 | P2 | Capability totals and release identities are internally inconsistent. | The current table has 244 unique IDs, 215 planned and 29 Not planned. Actual horizons are 154/26/35/29; two targets are `v1`, contrary to the exact-SemVer rule. See section 8 and metrics.json. |
| AUD-21 | P2 | `boundaries.md` uses deferred work and approvals as product exclusions. | Provider API endpoints, OCR composition, vehicle lookup and production deployment are not permanently absent. Classify product exclusions, technical constraints, allocation and exact-target authorization separately. |
| AUD-22 | P2 | Architecture mixes dev assembly, deployed state and historical narrative. | Replace the old three-stream pending assembly and legacy “not deployed” statements with a pinned source snapshot, and link to dated operations evidence. Do not infer deployment from the integrated source. |
| AUD-23 | P2 | Operations mixes current observations, old releases, procedures and product boundaries. | Keep a small dated current-state record. Preserve release failures and evidence in immutable history/owned release records, move procedures to their owners and remove its deferred-seam policy table. |
| AUD-24 | P2 | The documentation-update instruction itself creates duplicate state. | The release skill tells maintainers to put SHA, manifest, digest, revision and migration in both architecture and operations. Change that instruction: operational identities belong in operations; architecture changes only for changed source structure. |
| AUD-25 | P2 | The release-trigger rule is incorrectly limited to changes under `src/`. | Operations says a source revision is a release claim only if `src/` changed. The existing release skill correctly includes deployable infrastructure, dependencies, migration and runtime configuration. Use the broader deployable-input rule. |
| AUD-26 | P2 | Several accepted ADRs are only partly current, without a usable active-clause map. | ADR-0002/0005/0006/0007/0008/0018/0026 need precise successor relationships; ADR-0013/0029 overstate full supersession. Preserve IDs and original decision meaning. |
| AUD-27 | P2 | Some FRDs contain implementation-stage and ticket-plan prose. | Remove expired stream A/B/C contracts, “not delivered” notes and ticket dates from normative behavior after preserving evidence. A source signature is not a second functional requirement. |
| AUD-28 | P2 | Some UI requirements disagree with their own domain rules. | FRD-12 retains holder-expiry displays, Not-ready image rows, internal intake labels and an overbroad disabled-control ban. Align it with FRD-01, CONTEXT and the specific accepted interactions. |
| AUD-29 | P2 | Valuation adjustments are both specified and deferred. | FRD-06 defines the accepted calculation/application behavior, while its later scope text, boundaries and capability narratives still defer adjustments. Preserve the approved calculation order and remove only superseded deferral language. |
| AUD-30 | P2 | Genuine unresolved coverage gaps are obscured by false open questions. | Non-email Triage completion, MarketResearch job completion/acceptance, API Triage result representation and Audit report activation need precise clauses or bounded decisions. See the FRD coverage appendix. |
| AUD-31 | P2 | Kanmer's documentation skill still carries foreign-repository assumptions. | Its current text says “this one” uses docs/product/prd etc. and cites FRD-014 R2/R8b history. Correct the upstream target-neutral asset and reconcile the three installed mirrors. |
| AUD-32 | P2 | Proof templates do not supply the verifier's required frontmatter. | All three now use the configured integration branch, but still omit the canonical YAML `kind`, `merged_sha`, `environment`, `verified_at`, `result`, `attempts` record. Update templates, not the verifier into a second format. |
| AUD-33 | P3 | Markdown conventions have more than one owner and do not cleanly admit metadata. | Keep formatting rules once in engineering; AGENTS routes to them. Permit YAML frontmatter and the managed preamble before the first H1. Do not make every author duplicate table/wrapping rules. |
| AUD-34 | P3 | Stale protocol counts and signatures invite drift. | Tool inventories, Worker function counts, source signatures and capability recounts should be derived from their actual owners or tied to a dated exact revision, not manually repeated across guides. |

## 3. Recommended ownership model

An **owner** here means the one document or executable source that defines a particular kind of fact. A summary or index may link to that fact without restating the rule.

| Question | Canonical owner | Must not own |
| --- | --- | --- |
| What must an agent know before choosing work? | Short AGENTS.md; managed Kanmer block for generic workflow | Full product rules, release commands, implementation status, old incidents |
| Which document answers this question? | docs/index.md | A second copy of every rule or a universal precedence ladder |
| What do business and interface terms mean? | CONTEXT.md, linking to normative FRD clauses | Delivery status, calculations or procedures |
| Why does the product exist, and what outcomes/boundaries matter? | PRD | Detailed transport schemas or current deployment state |
| Exactly how does a capability behave? | Owning FRD | Ticket progress or broad architecture alternatives |
| Why was a durable technical choice made? | ADR | Product scheduling, generic Markdown governance or a deployment diary |
| How should code and evidence be engineered? | engineering.md | Ticket stages or the full commands for every task |
| How do I perform a specific operation safely? | One repository skill and its human-readable reference procedure | New product policy or standing production-write permission |
| What source structure exists at a known revision? | current-architecture.md | What is currently deployed unless tied to separate evidence |
| What was observed in an environment, and when? | operations.md plus immutable release/evidence records | Future product requirements or unqualified current claims from old observations |
| Which stable capability ID points to which requirement? | capabilities.md, or a generated registry view | Hand-maintained ticket status or independent acceptance gates |
| What is planned, blocked, assigned, reviewed or done? | Kanmer | A replacement for durable requirement ownership |
| What genuinely remains undecided? | open-decisions.md | Already-settled rules or a list of tests nobody has run |
| Where did a requirement come from? | A provenance pointer attached to the owning PRD/FRD/ADR | A second mutable omnibus requirements authority |

The current approved task can change intended requirements; the durable owner must then be updated explicitly. Code and tests establish observed implementation, not permission to replace an approved business rule. A disagreement should be resolved against the relevant authority, or recorded as a narrowly bounded question, not resolved by whichever file appears earlier in a chain.

### Proposed end state

Keep `README.md`, `CONTEXT.md`, `AGENTS.md`, `docs/index.md`, the PRD/FRD/ADR directories, a narrower engineering guide, a source architecture snapshot, a dated operations summary and a capability registry. Retire `operator-notes.md` after complete transfer. Reduce `runbook.md` to a procedure directory. Either retire `boundaries.md` after moving its few unique constraints or retain only a concise boundary index; it must not remain another roadmap.

Do not introduce a replacement 80 KB “documentation governance” file. The placement model should be a short table in the index, with the authoring conventions in engineering and the generic workflow in Kanmer.

## 4. AGENTS.md: exact recommendations

### 4.1 Testing and delivery

The current root file tells agents to run restore/build/test “before delivery” and separately calls those commands “the delivery gate.” The runbook repeats it. In contrast, `.github/workflows/ci.yml` and `scripts/Get-CiChangeFlags.ps1` already distinguish build-relevant and infrastructure-relevant changes. That is the right starting point for the wording.

**Suggested replacement policy, not yet applied:**

> Select verification from the changed behavior and affected executable inputs. Read-only analysis and isolated document deliverables do not require a .NET build. Prose-only repository changes require the relevant documentation checks. Changes to application code, build inputs, scripts, infrastructure or machine-consumed documentation require the checks that exercise those inputs. Use focused checks while implementing; reuse applicable successful CI evidence for the exact candidate and covered scope. Whole-solution verification and release packaging run only when the task's evidence contract requires them, under the named heavy-verification owner. Never treat unavailable or omitted evidence as passing.

| Change class | Evidence to require | Do not require automatically |
| --- | --- | --- |
| Read-only review or isolated audit output | Correct source revision, evidence citations, explicit coverage limits | Restore, .NET build, application tests, ticket/worktree creation |
| Prose-only repository edits | Relevant link/placement checks, reviewed semantic diff, no lost authoritative statements | Full application suite |
| Machine-consumed Markdown, templates or scripts | Their parser/consumer tests and applicable path-routed CI | A prose-only classification just because the suffix is .md |
| Application changes | Focused owning-project tests, relevant integration/caller evidence and required CI | Multiple agents independently rebuilding the entire solution |
| Razor/UI changes | Affected capture cohort, current generated snapshots, catalogue and appropriate browser checks | Every unrelated browser workflow on every edit |
| Release, schema or runtime configuration | Exact candidate artifacts, migration/grant/approval evidence and targeted smoke required by the release | Treating a green code-only build as deployment acceptance |

The canonical full commands can remain in the verification procedure. In particular, `--no-build` is valid only for matching compiled inputs; it must not be used to manufacture an inexpensive but stale result. Exact-SHA CI reuse must name its environment and covered checks. A pre-merge head result is not automatically equivalent to a later merged candidate with different inputs.

The existing documentation lane runs placement regression, documentation links and the UI catalogue. Some Markdown under `docs/design/test-ui/` is build-relevant. The acceptance runner also reads `capabilities.md`. Neither should be hidden behind a blanket “Markdown never needs tests” exception.

### 4.2 Triage wording

Remove “Triage is the only current term” from the unqualified sentence. It creates a false impression that Audit and Blocked intake have ceased to exist.

**Suggested replacement:**

> Audit, Triage, Blocked intake, Unidentified and Image Intake are distinct domain concepts. Triage names only the separate pre-Case assessment workflow; it is not generic inbox sorting. Unidentified replaces the former Needs sorting destination for safely retained material that cannot yet be identified or routed. That rename does not change Audit, Triage, Blocked intake or Image Intake. CONTEXT.md owns the domain-to-interface vocabulary.

The detail of the six Unidentified reasons and lifecycle belongs in FRD-02, not in startup instructions. The operator-facing ban on the word intake is a presentation rule, not a ban on internal type names.

### 4.3 Size: what is verified and what is not

The current Git blob is **31,557 bytes**. The supplied attachment is 27,390 raw bytes and 27,190 after normalization, so it is not the version to optimize blindly.

The current official OpenAI instruction-discovery documentation describes a configurable `project_doc_max_bytes` limit of **32 KiB by default**. It is a combined instruction-discovery budget, not a universal 32 KB per-file format maximum. At the unchanged default, 32,768 − 31,557 = **1,211 bytes** of nominal space remain beyond this root file. This audit did not inspect Alex's effective Codex configuration or prove that instructions were truncated.

The requested research window was **1 June 2026 onward**. I could not verify an official OpenAI or Anthropic publication in that window establishing a universal **ten-line AGENTS.md limit**. Search results from community gists and articles are not official guidance. A living manual being crawled recently does not prove when a recommendation was published.

For the same reason, the following are separated as current manual facts rather than passed off as dated post-June guidance: Anthropic's current Claude Code guide says to keep startup instructions short and move task-specific material to skills; its skill-authoring guide places the **under-500-line recommendation on the SKILL.md body**, not on all AGENTS.md or CLAUDE.md files. A short example is not a mandatory line limit.

**Project-specific recommendation:** aim initially for roughly 60–100 human-owned lines of high-value routing and safety instructions, plus the upstream-managed Kanmer block. Treat a substantially smaller byte budget as a design target, not a new hard gate. Reducing the entire file to ten lines would first require redesigning Kanmer's managed preamble, which is already longer than that. Do not replace readable prose with enormous one-line bullets just to meet a line count.

Always-loaded instructions should retain only universal, non-obvious constraints and the trigger for reading the correct owner. A skill is a task recipe loaded when relevant; this keeps a release or mailbox procedure out of the context of a simple UI edit. The procedure itself may remain detailed and human-readable.

### 4.4 What moves out

Move full command sequences and release inputs to procedure owners; ADR templates and placement detail to the documentation model; product invariants to the PRD/FRDs with short stop-condition pointers; dated v1 remediation permissions and host coordination to their owning Kanmer execution record; implementation signatures to code/as-built architecture; generic ticket lifecycle to the managed Kanmer layer. Preserve exact-target write restrictions and corpus immutability as concise global guardrails.

`CLAUDE.md` is a tracked symlink to `AGENTS.md`. Preserve that single-source arrangement rather than producing a second manually maintained guide. A correct symlink is not itself a size solution.

## 5. engineering.md and documentation governance

Keep the engineering-specific material: dependency direction, shared policy ownership, failure semantics, test evidence classes, source/protocol fixture distinctions, and proportionality of verification. Separate those rules from ticket lifecycle and step-by-step operational commands.

The Markdown convention belongs naturally here. The defect is its repetition in AGENTS and competing placement descriptions, not the mere fact that engineering mentions Markdown. AGENTS should point to the convention only when documentation is being changed. Kanmer should govern how a ticket links its durable owner, not become a second Pegasus formatting authority.

Change the heading rule to permit supported YAML frontmatter and managed preambles before the first H1. The current absolute “H1 on line 1” instruction conflicts with ADR metadata and the actual root file. Formatting should not force metadata removal.

Remove or explicitly retire the one-off DELIV transition/history notes and the v1 shared-Foundation checkpoint after checking their retained ticket evidence. Current requirements do not belong in an “incomplete development checkpoint” table once the source assembly has been integrated. Preserve any still-relevant architectural boundary in the owning ADR/architecture section, not in a ticket-specific C# signature table.

Fix the evidence ladder's stale 10 MiB upload figure and avoid repeating every product acceptance clause. The ladder explains *what a kind of test proves*; the FRD explains *which behavior is required*. The correct order is affected policy, adapter, persistence and real-caller evidence as appropriate—not twelve universal gates for every change.

## 6. open-decisions.md: disposition map

The file's stated rule is correct: accepted decisions move to a durable owner, and delivery status does not belong in the register. Apply that rule to the existing content rather than adding another explanatory disclaimer.

| Existing section or item | Disposition | Destination / remaining question |
| --- | --- | --- |
| Historical QDOS-alpha release sequencing | Remove from active register; preserve dated provenance | Historical release/decision evidence. Current v1 acceptance belongs in PRD/FRDs and the release's Kanmer record. Its “three PRs remain unmerged” statement is obsolete. |
| Accepted Box case-folder layout | Move any unique rule, then remove | FRD-05 behavior; operations records actual roots; procedure owns exact-target checks. |
| Future AI Operations catalogue/lifecycle | Close settled question | ADR-0035 and FRD-11 own the ledger; client round-trip and activation evidence are ticket work. |
| INT-31 limits and fixed submission session | Move settled policy, then remove | FRD-02/05; technical token/rate-limit mechanisms only need ADR treatment where they are an enduring architectural choice. |
| External credential ownership | Split | Named owner/rotate/revoke/emergency operation in operations and credential procedure. Retain only a specific unassigned ownership decision, not a generic task list. |
| QDOS extractor field acceptance thresholds | Retain, narrowly | Exact field criteria, ground-truth representation and the reviewed cohort/holdout authority remain genuine decisions/evidence prerequisites. Expand by route only when a route is being accepted. |
| Telemetry sampling/cap and budget history | Split | Dated settings/cost observations in operations; measured workload in its ticket. A genuinely proposed increase needs a bounded decision. No new monthly spending ceiling is inferred. |
| Performance dataset ownership | Move out of current v1 blockers | Optional deferred capacity evidence. Retain the accepted target in PRD without claiming the excluded 2,000-case run passed. |
| Closed QDOS automatic Triage predicates | Remove after fixing inbound links | FRD-03/09; retain original acceptance provenance. |
| Mailbox precedence/confidence/governance questions | Retain only unresolved subsets | Do not reopen the known route taxonomy or invent a confidence score. Name the exact route, conflicting predicates and decision owner. |
| EVA thirteen-key mapping and manual import acceptance | Split | FRD-07 owns mapping; actual import acceptance is evidence work. A genuinely unknown mapping remains a precise question. |
| Resolved EVA API activation | Remove after correcting obsolete rule | FRD-07 and ADR-0038 own current manual/re-send behavior; credentials and first live acceptance have separate tasks. |
| Glass's repair-estimate selection | Close selection question | Selection is settled; operator-owned live acceptance remains work. Do not treat selection of repair estimates as selection of Glass's valuation. |
| Provider API contract | Close already-specified wire questions | FRD-09 API-01. Retain only new client/tenant choices not settled there; issuing credentials is an operation. |
| DVLA/DVSA selection and activation | Rewrite into precise residual | Already-selected source contract versus credential/live-response acceptance. Remove the blanket keep-disabled directive unless a current accepted rule requires it. |
| Post-report query/dispute lifecycle | Retain | Exact states, actors, reply evidence, reopening and closure are a real behavior gap. |
| Missing report wording | Retain | Identify the unsupported report family/paragraph and the person who must supply accepted wording. Do not infer legal or engineering text. |
| Fee/valuation markup ambiguities | Close settled portions | FRD-06/11 already own much of placement and storage; uncontracted vendor data remains a separate question. |
| External AI round-trip run | Move | Test/evidence ticket. AiWork push and AiJobs pull must not be collapsed merely to remove an entry. |
| Resolved Case layout and signatory | Remove | FRD-12 and FRD-01/04/11; preserve source references. |
| Resolved 15-minute mail freshness/no backfill | Remove after repairing architecture anchor | FRD-08. Keeping a resolved section solely because it is linked is not a reason to keep it open. |
| Manual upload in deployed environment | Reframe investigation, not historical prohibition | Determine the current composed custody path and intended condition. ADR-0003's old local-storage description cannot prove that every later deployment still writes locally. No production enable/disable action is authorized by this audit. |
| Generic Azure ownership/retirement questions | Remove from standing register | Exact-target approval procedure. Open a concrete decision only for a named proposed ownership/retirement change. |

A remaining entry should fit a short schema: **question; why the current owners do not answer it; decision owner; exact affected capability; evidence/options; blocked action; safe interim behavior**. A release not yet tested is not an unresolved product decision.

## 7. Retiring operator-notes.md without losing requirements

The current file's claims that every statement is authoritative conflict with its inclusion of dated implementation claims and derived paraphrases. I did not reconstruct the full authorship history, so this audit does not assert who overwrote which original wording. The current content is sufficient to show that it is no longer simply a notebook of verbatim operator statements.

Retirement must preserve meaning and provenance, not every obsolete sentence as a current rule.

| Current content | Durable destination | Preservation rule |
| --- | --- | --- |
| Current v1 product outcomes, native reports, optional EVA, Glass's estimate scope | PRD, then relevant FRD clauses | State the current scope directly. Do not make readers chase a later override to understand earlier text. |
| Three-stream execution, replacement PRs, subsequent remediation authority | Owning Kanmer records | Preserve exact dates/targets and ended exceptions. Do not turn a past authorization into permanent repository permission. |
| Triage, Unidentified, image-origin and Case identity | FRD-01/02/03; glossary pointers in CONTEXT | Preserve separate references, no reuse, source history and reasoned reassociation. |
| Completeness, review, chasing and lifecycle | FRD-01; mail behavior in FRD-08 | Reconcile the current handoff and no-autonomous-send rules before transfer. |
| Required instruction fields and routing | FRD-02/09 | Preserve issuer, sender, intermediary and Principal as distinct facts. |
| Inspection method/location and Principal defaults | FRD-06; reusable party/default behavior in FRD-04 | Preserve that a location is report data and is not evidence of physical attendance. |
| Engineering findings, estimates, valuation and reports | FRD-06/11 | Preserve approved calculations and literal operator statements; resolve stronger derived prohibitions separately. |
| Staff roles, access removal, sign-off identity | FRD-04 with FRD-01/11 references | Remove obsolete periodic-review requirements; preserve historical actors and printed tuples. |
| CAP-001 through CAP-022 identifiers | Capability crosswalk into current stable registry | Do not silently discard identifiers used by old requirements; preserve aliases/provenance without a second capability table. |
| UI language and layout | CONTEXT, design and FRD-12 | Separate universal display language from the Case record's specific scrolling-layout exception. |
| Tool/development/corpus restrictions | engineering; concise AGENTS guardrail | Distinguish authorized local development use from external-service activation and egress permissions. |
| External-system descriptions | Relevant FRD/ADR plus dated operations observations | Mark old EVA/vendor availability claims historical; do not promote them into current limits. |
| Secondary Audit Box folder nesting | FRD-05 | Preserve its exact parent-child relationship. |
| Continuous intake and out-of-hours availability | PRD quality/availability | Preserve operating expectations without inventing a new uptime SLA. |
| Alex as first-line support; alert recipients; staffed/out-of-hours response; emergency access | operations plus incident-response procedure | Preserve the actual support contract and configuration-based recipient extension. |
| Lowest practical cloud tiers; no fixed monthly ceiling; reuse existing accounts; commercial/API entitlement | PRD constraints and integration activation policy | The £75 alert is not an invented absolute budget. Keep entitlement requirements separate from unsupported new development bureaucracy. |
| UK South as a chosen default rather than mandatory UK residency | ADR-0015/PRD constraint clarification | Do not silently convert a chosen deployment region into a business requirement. |
| Original source labels and direct quotations | Provenance pointers attached to their destinations; original Git history | A current requirement may supersede a quotation, but its original meaning and provenance must remain recoverable. |

Create a migration ledger in the documentation-change ticket: original section/statement ID, original blob or commit, destination clause, disposition (preserved, explicitly superseded, duplicate, or unresolved) and reviewer. Only delete the file after every material statement has a disposition and all active inbound links have been repaired. A temporary retirement stub may route old links during the same migration; do not leave it as a second binding authority indefinitely.

## 8. capabilities.md and boundaries.md

### 8.1 Recount

The normalized uploaded capability file is byte-identical to the `dev` blob, so this recount applies to the pinned branch.

| Measure | Current document claim | Computed from the table |
| --- | --- | --- |
| Total / unique capability IDs | 244 / 244 | 244 / 244 — correct |
| Planned capabilities | 205 in the introductory text | **215** |
| Now | 153 | **154** |
| Next | 27 | **26** |
| Later | 35 | 35 |
| Not planned | 29 | 29 |
| Target 0.1.0-alpha.1 | 153 | **152** |
| Target 0.2.0 | 5 | **4** |
| Non-SemVer `v1` targets | Not part of declared release scheme | **2: INT-16 and EXT-12** |

The complete row-level recount is in `metrics.json`. The old 233-versus-234 finding has been repaired; the current arithmetic errors are new or surviving errors of the same class. Do not restore old counts from the September 5 review.

### 8.2 What the registry should contain

Keep stable ID, concise durable outcome and canonical owner, plus retired-ID/alias provenance where necessary. Keep allocation in one place. If Kanmer owns delivery scheduling, produce any documentation roadmap as a derived view rather than another hand-edited schedule. If a product-level target remains deliberately in the capability register, Kanmer must reference that allocation rather than maintain a contradictory copy.

Remove implementation narratives such as “not delivered,” “on stream B,” “caller-proved locally,” “released in release 6,” and “pending current composition binding.” Those statements belong to source evidence, release records or ticket status. Do not erase the underlying evidence; move its ownership.

The acceptance script means a six-to-three-column rewrite is **not** a harmless prose-only change. First change or replace the consumer's acceptance-scope contract and test it against the intended roster. A Markdown allocation table should not silently decide which independently deferred features block release.

Avoid a blind search-and-replace of all `v1` values with `1.0.0`. The desired release identity has to be selected consistently with the product's version scheme; this audit identifies the inconsistency but does not allocate a new release on the user's behalf.

### 8.3 Boundary classification

A boundary says what must not cross a line—for example, an AI tool cannot confirm a professional finding, an external caller cannot act as another Principal, or a reference cannot be reused. A capability scheduled later is not automatically such a boundary.

Classify each existing row as one of: permanent product exclusion, enduring technical constraint, currently deferred capability, activation/approval condition, or dated implementation state. The first belongs in the PRD, the second in an ADR/FRD, the third on the delivery plan, the fourth in the governing behavior/procedure, and the fifth in evidence.

Specific rows needing correction are the allegedly absent provider endpoint, OCR service/flag, vehicle lookup, blanket AI direct-mutation exclusion, production deployment and the capacity-cohort run. Direct, attributable unconfirmed working-data writes are not the same thing as an AI approving a finding or sending a report. Glass's estimates are not Glass's valuation. Internal AutoTrader scraping remains excluded even though external market-research results may return through an AI job.

Retain the unique current exclusions—no standalone Images list, no runtime template editor, no Case Scroll/Tabs switch—under their actual behavior/design owners. They do not need a second mini-PRD here.

## 9. runbook.md: procedures versus policy

The supplied runbook is 1,433 text lines and approximately 85 KB normalized; the current Git blob is 84,970 bytes. It is not exclusively a runbook. It contains domain rules, platform comparisons, configuration/reference material, evidence standards, source-state claims, release history, and repeated acceptance checklists alongside procedures.

Do not turn the entire file into one enormous skill. Keep a short human-readable runbook index that answers “what operation am I performing?” and links to exactly one procedure. The procedures can be the same Markdown reference files that agent skills load. Human recovery instructions must remain usable without a functioning AI client.

### Extraction map

| Existing content | Destination | Implementation note |
| --- | --- | --- |
| Unidentified queue definition | FRD-02 | A definition is not a troubleshooting procedure. Link to it from the relevant operations recipe. |
| Supported platform, tool contract and offline prerequisites | engineering reference plus local-development procedure | Remove dated vendor-support/capability essays from the command path. Read pinned versions from their actual configuration where feasible. |
| Offline setup; Local setup and run; Status/Smoke/Stop/Reset | Proposed `pegasus-local-development` skill/reference | Consolidate the two setup sequences and call existing lifecycle scripts. Do not introduce another local runner. |
| Locked restore/build/test; test-database troubleshooting | Proposed focused verification procedure | Share the policy from section 4; keep the actual platform variables and safe test-database ownership checks. |
| Test UI capture | Existing Razor UI skill family plus one verification reference | Reuse scoped capture/cohort controls. Do not duplicate the catalogue or maintain hand-written replacement snapshots. |
| Azure SQL runtime bootstrap; release artifacts; Worker activation | **Existing `pegasus-release` skill and references** | Merge duplicate procedure bodies and make the exact approved script/manifest the function-census owner. |
| Intake-data wipe | **Existing `pegasus-wipe-intake-data` skill** | Preserve exact-target approval, Worker-stopped maintenance, dry-run, recovery evidence, reference preservation and the receive-time cutoff. Its presence is not standing authorization to run it. |
| Provider-domain and principal-identification corpus authoring | Proposed reference-data-authoring skill/reference | Call the existing generators; preserve immutable inputs, normalized text versus raw evidence hashes, and no unintended route activation. |
| Principal inspection-mode change | Administration procedure | Remove the standing raw SQL UPDATE recipe where the accepted Administrator action exists. Distinguish dev source behavior from a deployment that does not yet contain that action. |
| Mailbox onboarding/disable/re-enable | Proposed mailbox-operations skill/reference | Preserve tenant setup versus application onboarding, approved scopes, generation/cutoff semantics and truthful Submitted/Sent distinction. |
| OAuth certificate operation | Proposed credential/certificate-operations skill/reference | Preserve exact secret versions/scopes, overlapping key rotation, token lifetime and emergency revocation. |
| Configuration reference | Architecture/engineering reference | Separate setting meaning from observed production values and actual mutation steps. |
| QDOS OfflineCandidate runner | Explicit optional/legacy evidence procedure | Do not let its 2,000-case prerequisite become a current v1 release gate through documentation. |
| Release dependency order | Kanmer dependencies, with durable constraints in ADR/FRD | Remove the historical alpha spine as a universal current schedule. |
| Release validation rules | Owning FRD acceptance clauses plus links from verification | Do not copy the complete product rulebook into the release procedure. |
| Monitoring and diagnosis | Incident/diagnostics reference, linked from release/operations | Keep evidence queries and failure isolation, not a duplicate monitoring requirement or outdated approval rule. |
| Production recovery | Existing release references or one recovery reference | Preserve forward-schema and previous-artifact constraints; no destructive down-migration is inferred. |
| Repository Git workflow | Kanmer | Do not create a second repository pipeline inside the runbook. |

### Correct before moving

The current build section is still unconditional. Its prose says Browser/SQL evidence is separate even though the shown non-corpus default selection includes those traits where available. Clarify selected tests versus separately unexercised acceptance; do not imply the same test was both included and omitted.

Other problematic sections include the old MCP-local-only dependency rule, the mix of seven and nine Worker function counts, the broad reset/readiness checklist, and source-state statements treating the Worker/outbox as future work. The current release skill already says the scripts at the released SHA own the exact Worker census; use that instead of another numeral in a guide.

Cloud read permission is inconsistent between the general approval matrix and the monitoring paragraph requiring separate authorization for every refresh. Retain exact target identification and explicitly authorized writes, but do not invent per-read approval when the repository policy permits read-only inventory.

A skill's description should explain its trigger and whether it is read-only, local-state-changing, or requires exact external-write approval. Each recipe needs prerequisites, target/ownership checks, steps, expected outcomes, stop conditions and evidence limits. No skill may treat its own discovery or invocation as authorization to send mail, mutate Box, rotate credentials or deploy.

## 10. operations.md versus current-architecture.md

These should remain two documents because they answer different questions. They should not both be “everything known about Pegasus.”

**Architecture:** source revision, four-project shape, data ownership, business-policy boundaries, composition/entry points, important flows and technical decision links. It may say an adapter exists or is conditionally registered; that does not prove the currently running environment uses it.

**Operations:** last observed deployed source/image/revision/migration, enabled ingress and Worker/mailbox state, exact environment identities, known current incidents, retained rollback basis, monitoring/recovery evidence and links to release records. Every observation has a timestamp and a stated coverage limit.

The current operations record reports release 38 and a read-only observation on 6 September. This audit did not query Azure. Therefore the defensible phrase is **last recorded production observation**, not “production definitely runs release 38 right now.” The integrated `dev` source and that older recorded deployment are not contradictory merely because they differ.

Correct the old three-stream assembly and “B binding pending” descriptions in architecture against the integrated source. Do not automatically mark the corresponding deployment or external-client acceptance complete. Replace the architecture diagram's ambiguous target arrows with structural edges and explicit composition labels; link live activation to operations.

Move long release narratives out of the main operations reading path without losing failed attempts, exact hashes, rollback artifacts and observed limitations. A compact current summary should link to their immutable records. Do not delete the public-upload incident merely because its source fix is now integrated; a source fix is not proof of a deployed repair.

Remove operations' stale deferred-implementation table, including absent EVA/VRM/DOC/MSG/webhook claims. Remove the broad statement that production release relevance depends only on `src/`: infrastructure or runtime configuration can change what is deployed too. Separate dated cost and soft-delete observations from active tasks requiring fresh inventory.

Finally, change the **producer instructions** in AGENTS/release skill. Requiring both documents to repeat exact deployed SHA/digest/migration values ensures future duplication. A release updates operations; a source architectural change updates architecture; a release affecting both updates each within its own scope.

## 11. ADR audit and missing technical records

An ADR is a durable explanation of a technical decision, not a backlog ticket. All **39 issued records** were read. ADR-0017 is unissued; the numeric gap is not evidence of a missing decision. The full per-record disposition is in `adr-and-frd-register.md`.

Important repairs are not limited to changing `status: accepted` into `superseded`. Mixed ADRs need a clear current-clause view. ADR-0002, for example, contains foundational choices that survive alongside hosting/environment and scheduling assumptions that do not. Keep the old rationale and stable ID; explicitly identify replaced clauses and the current successor. Follow transitive chains such as ADR-0032 → ADR-0033 and ADR-0037 → ADR-0039.

The converse error also exists: ADR-0013 is marked fully superseded by ADR-0029 even though the latter changes the image-origin boundary, not every login, renderer and QDOS clause in the former. Current architecture still cites its login-throttling clause. That surviving decision needs an active owner; a broad superseded badge is not a valid substitute for deciding which clauses remain in force.

### Strongly justified new or successor records

The current documented choices require explicit reconciliation for persistent OAuth certificates/per-grant attribution, mailbox occurrence/deduplication identity, image-reference Box custody, and the durable staff-send journal. These are concrete technical contracts that conflict with accepted ADR wording, not speculative extra paperwork.

Additional record candidates are the shared non-Case Box holding/logical-content/24-hour cache boundary and the per-Engineer Glass's credential/session integration boundary. First establish whether an existing decision already covers the actual technical choice. Add a record only for a durable new ownership/security/recovery decision; ordinary fields or every provider profile do not each need an ADR.

The Web minimum-replica setting also needs an explicit disposition against ADR-0015's original scale-to-zero trade-off. ADR-0033 warms the Worker; it must not be cited as though it independently decides the Web setting. A dated operational override with valid authority may be sufficient depending on what the accepted hosting decision actually constrains.

Do **not** create ADRs merely to approve Markdown formatting, moving a paragraph, generating a register, or creating a PRD/FRD. Those are governance decisions with the wrong document type.

## 12. FRD audit and coverage gaps

Twelve FRDs is not inherently too few. The question is whether every accepted behavior has one coherent contract—not whether there is one document per capability ID. All twelve were read; the detailed register gives each a disposition.

The strongest conflicts are already in the P1 register: Audit allocation in FRD-02; report generation in FRD-11; reset/review administration in FRD-04/12; upload limits; readiness invalidation; EVA resend; and valuation-adjustment deferrals. The older no-VRM Triage wording and FRD-09's undefined API contract have been corrected. Do not repeat those September 5 observations as if unchanged.

### Requirements that need a clause, not automatically another FRD

**Non-email Triage completion.** Triage may originate outside retained email, but completion requires exact reply-chain Sent evidence in its normal workflow. Define whether those origins are supported for completed Triage, what evidence is applicable, and what is deliberately unsupported. The PRD's “where applicable” does not provide an executable rule.

**MarketResearch job completion.** A job can return a findings document and valuation proposal, while other job text routes completion through named draft-estimate acceptance. Define the research-specific terminal and review behavior without pretending a valuation proposal is a repair estimate.

**Provider API Triage results.** Clarify what the result/status contract returns for accepted Triage, including its reference and absence of Case/PO. A contract that permits Triage but exposes only a Case-style result leaves clients guessing.

**Audit reports in v1.** FRD-11 still calls Audit parity future activation and excludes Audit from the initial renderer, while v1 includes native final reports. State exactly which Audit journeys are accepted for v1, their existing caller, and any genuinely outstanding acceptance. Do not claim runtime support merely by deleting the exclusion.

**Non-Case holding and cache behavior.** Strengthen FRD-05's functional contract for retained non-Case sources, logical reads, authorization, custody-pending failures, cache miss/integrity behavior, idle expiry and re-evaluation after staging deletion. Architectural placement and a 24-hour number alone do not specify recovery behavior.

**Engineering access versus mandatory global checks.** Reconcile the requirement for market valuation before Engineer eligibility with where valuation can actually be supplied/accepted in the current workflow. Define permitted explicit exceptions and their actors; do not invent an automatic bypass or leave a cyclic readiness requirement.

**Source evidence adoption.** Make the common rule explicit for third-party reports and estimating imports: issuer is not necessarily Principal; original observation is not CE acceptance; accepted fields retain document/version/hash/locator and require the proper actor/version/lease. Parts exist already in the v1 contract material and should be consolidated rather than re-invented.

Unknown repairer VAT is **not wholly missing**: current v1/FRD report material already requires explicit status/category before accepting totals. Preserve and cross-link that requirement. Signatory qualification optionality and targeted lease-clearance behavior are also recorded; their problem is inconsistent consumers, not absence everywhere.

### Sensible optional splits

FRD-06 spans source observations, inspection location, image advisories, damage, valuation, estimates and settlement. FRD-11 spans reporting, correspondence and several AI-job families. Separate an estimating/import contract and a valuation contract only if that creates independent, cohesive rule owners with clear cross-links. Do not split automatically at an arbitrary line count or create 244 thin documents for 244 IDs. Fix contradictory clauses before changing filenames.

## 13. PRD audit

The current PRD correctly records native engineering/final reports, optional EVA, staff-initiated sends and Glass's repair estimates rather than Glass's valuation. Its normal Case/PO versus later Audit reference distinction is also correct. Those newer clauses are the intended-state baseline for the conflicting documents identified above.

It still needs these improvements:

- Make the current v1 outcome complete in the PRD itself rather than relying on an operator-notes override. Clearly distinguish the original QDOS alpha from the current multi-principal v1 scope.
- Remove a requirement for independently buildable **current** source workspaces when the imports are retired. Retain only the rules for any future deliberately admitted import.
- Place permanent product exclusions here and link capability IDs to them; avoid a circular definition in which the PRD says capabilities owns every boundary while capabilities points back to the PRD.
- Retain workload, five-second p95 and cost objectives as targets with measurement scope, not passing claims. Do not resurrect the excluded capacity/soak run or deferred OPS-09 as a universal release gate.
- Preserve the distinction between local development permission and accepted terms/data handling for an external flow. An instruction not to invent new development bureaucracy does not mean an external operation is authorized merely because a tool exists.
- Consolidate support, operating-hours and commercial constraints transferred from operator-notes. Do not invent an uptime SLA, UK-only residency rule or absolute £75 monthly budget.

A second omnibus PRD is not required. A coherent current product document with an explicit acceptance model and linked behavioral owners is preferable to another top-level authority.

## 14. Results of comparing the former reviews

The September 5 review is useful evidence, but not a repair script for today's tree. Its 71 observations divide as follows in the accompanying detailed reconciliation:

| Disposition | Observations | Meaning |
| --- | ---: | --- |
| Resolved wording | 36 | The specific earlier documentation conflict is repaired; no fresh runtime acceptance is implied. |
| Partially resolved | 15 | Some cited wording is fixed, while a related or remaining part still needs work. |
| Still outstanding | 20 | The original material conflict remains. |

Examples of repaired findings include the Unidentified glossary, the exact FRD-01/QDOS Audit pair, workspace retirement in README/index, the missing-registration Triage wording, several ADR supersession headers, historical-only markings on Stitch and session plans, integration-branch proof wording, and non-PASS retirement remaining outside Done.

Examples not repaired include AI Proposal versus direct working data, parts of the architecture diagram/current-state narrative, the old EVA at-most-once rule, operator-notes' conflicting rules, some UI requirements, foreign repository assumptions in the Kanmer docs skill and missing proof frontmatter.

The undated review references retired paths such as `docs/architecture.md`, `docs/requirements.md`, `NOW.md` and removed workspaces. Its findings are separately disposed as resolved, obsolete-in-current-scope, partial or not fully reverified. The crosswalk does not pretend that an unnamed assertion or a historical worktree execution failure can be proven from the fragment alone.

## 15. Delivery plan for the documentation improvement

Keep this a bounded documentation programme, not another prolonged governance project.

**First change: authoritative behavior reconciliation.** Fix the P1 contradictions in their existing owners, preserving source statements and tests that assert the intended behavior. Include Audit identity, report generation, native handoff/readiness, upload ceilings, account reset/reviews and EVA resends. Record unresolved choices explicitly; do not silently pick whichever current implementation is easiest.

**Second change: ownership and relocation.** Adopt the question-specific owner matrix, transfer operator statements with a complete ledger, reduce capabilities/open-decisions/boundaries, and repair all inbound references. Retire operator-notes only when its ledger is complete. Change the rule that currently forbids legitimate procedure/support files.

**Third change: agent/procedure packaging.** Shorten human-owned AGENTS, reconcile upstream Kanmer assets and mirrors, reuse the release/wipe/UI skills, and extract the remaining procedures into a small number of named recipes. Update the producer instructions so later releases do not recreate duplicated content.

**Coupled executable change, where needed:** acceptance-roster parsing and any documentation path/format consumer. Treat these as code/script changes with focused regression evidence, not as editorial cleanup. The exact implementation can share a reviewed PR with its contract change if that avoids an intermediate broken tree.

A few cohesive PRs or one carefully staged documentation branch are preferable to dozens of one-line governance tickets. Kanmer should retain the actual execution plan and evidence; this audit remains the requested review artifact, not another live status ledger committed into the product tree.

### Acceptance of the cleanup

The cleanup is complete when each material transferred statement has one current owner or an explicit supersession; the active owners agree on the tested scenarios; settled questions are absent from open-decisions; delivery status is not duplicated in the capability registry; procedures have one executable owner; the changed paths/anchors and any machine consumers pass their relevant checks; and the reduced instruction chain routes representative tasks correctly.

Use representative documentation scenarios: a read-only review does not start a build; a prose edit selects documentation checks; a Razor edit finds its capture procedure; a schema change finds migration/grant evidence; a release finds exact-target permission and the real release recipe; an Audit instruction finds normal Case/PO allocation; missing Audit evidence withholds only the later Audit reference; native engineering does not require EVA; a changed upload contract reaches its parser/consumer tests; and no historical plan is treated as standing authority to merge, send or wipe.

No new blanket whole-solution run is justified merely by moving prose. Conversely, a changed script, consumer or generated UI contract must receive the evidence its executable effect requires.

## 16. External guidance research record

The user requested official OpenAI/Anthropic guidance published from 1 June 2026 onward. No qualifying dated official source was verified for the universal ten-line claim. These currently served official manuals were consulted separately for present mechanics and scope, without treating crawl dates as publication dates:

| Source | What it supports | Date qualification |
| --- | --- | --- |
| OpenAI, Custom instructions with AGENTS.md | Layered instruction discovery; configurable 32 KiB default combined budget | Living manual; publication date for this specific advice not established |
| Anthropic, Best practices for Claude Code | Short, broadly relevant startup instructions; task-specific material in skills | Living manual; not certified as post-June guidance |
| Anthropic, Skill authoring best practices | Under-500-line guidance is for SKILL.md body; supporting detail can be loaded as needed | Living manual; not a universal AGENTS.md rule or dated post-June recommendation |

```text
https://learn.chatgpt.com/docs/agent-configuration/agents-md
https://code.claude.com/docs/en/best-practices
https://platform.claude.com/docs/en/agents-and-tools/agent-skills/best-practices
```

Community line-count suggestions were not adopted as vendor policy. The proposed Pegasus size and ownership targets in this audit are recommendations based on the observed repository, not an assertion that either vendor requires those exact numbers.
