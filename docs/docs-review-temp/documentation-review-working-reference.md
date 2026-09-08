# Pegasus documentation investigation and recommendations

Temporary working reference · 8 September 2026 · not governing documentation.

Prepared at the operator's explicit request, without creating or updating a
Kanmer ticket. This file is the requested exception to the current restriction
on temporary Markdown in the repository. It records findings and proposed
amendments; it does not enact them or grant implementation/release authority.
Remove it after its useful findings and durable documentation are attached to
the existing documentation work.

## Review baseline and limits

The primary baseline is `DELIV-051-instructions` at
`af1625fae8ac8018054c95e988907f6c44fa4639`, in `.worktrees/deliv-051`.
This contains the PR702 amendments and operator skill/changelog cleanup.
The shared `dev` checkout is older (`3284f93fc`); its restored AGENTS.md must
not be mistaken for the PR's current proposal. Neither checkout was updated,
restored, committed or pushed during this investigation.

The review covers AGENTS.md, README.md, CONTEXT.md, every PRD/FRD and the ADR
register and decisions, plus the root documentation's ownership, section
structure, cross-references and conflicting claims. Design guidance and
provider companion documentation were also checked for competing ownership.
Source, CI and script reads corroborate selected findings. This is a
documentation review, not an exhaustive implementation-conformance audit or
fresh production observation. Retained deployment reports are dated evidence;
their statements are not certified as today's live state. No application
build, test suite, deployment, cloud write or board mutation was performed.

Evidence locators below are repository-relative paths with headings or line
numbers at this baseline. A recommendation to reconcile a business conflict
does not silently select a new business rule.

## Main findings

The documents have accumulated successive task amendments without replacing
the paragraphs those amendments supersede. A reader must reconstruct the
current specification from dates, ticket numbers and exceptions spread across
several files. Shortening AGENTS.md alone cannot fix that.

Recommended direction:

1. Give each kind of information one owner, with links from other documents.
2. Replace universal build instructions with consequence-based verification.
3. Move accepted business requirements out of operator-notes.md, preserving
   their meaning and provenance, then retire that competing authority.
4. Remove settled decisions and delivery history from open-decisions.md.
5. Keep useful capability identities; move scheduling and work state to Kanmer.
   Correct documentation takes priority over existing script dependencies.
6. Extract reusable procedures into a small set of repository skills, retaining
   a short human-readable runbook index and explicit prerequisite references.
7. Reconcile ADR supersession and FRD contradictions before polishing prose.
8. Keep current architecture and deployed operations separate and much smaller.

### Measured size

Bytes are file sizes on disk, not model tokens. Lines alone conceal long tables.

| File | Bytes | Lines | Assessment |
| --- | --- | --- | --- |
| AGENTS.md in shared dev | 35,386 | 552 | Older duplicated version; over 32,768 bytes by itself. |
| AGENTS.md in DELIV-051 | 27,390 | 409 | Reduced, but still carries too much always-loaded material. |
| README.md | 4,318 | 82 | Reasonable size; setup commands and deployment status duplicate owners. |
| CONTEXT.md | 9,078 | 149 | Useful glossary, but definitions contain outdated behaviour. |
| docs/index.md | 3,918 | 51 | Useful routing location; authority model needs replacement. |
| docs/engineering.md | 20,037 | 281 | Policy, procedures, history and obsolete stream coordination mixed. |
| docs/open-decisions.md | 41,536 | 450 | Many settled decisions, evidence tasks and historical costs. |
| docs/operator-notes.md | 49,712 | 665 | Competing master specification with superseded text. |
| docs/runbook.md | 86,465 | 1,433 | Procedures plus policy, architecture and old delivery gates. |
| docs/capabilities.md | 109,344 | 422 | Under 500 lines yet very large; live scheduling and normative prose. |
| docs/boundaries.md | 7,061 | 39 | Almost entirely a large deferred-work table. |
| docs/current-architecture.md | 78,091 | 868 | Current source, earlier topology, rules and delivery claims mixed. |
| docs/operations.md | 126,097 | 1,668 | Current estate embedded in a long release/evidence ledger. |

## 1. AGENTS.md

### 1a. Verification: the build request follows the current wording

AGENTS.md's Commands says to run the canonical solution commands before
delivery; Verification calls them the delivery gate. Runbook lines 312 onward
repeat the unconditional instruction. A documentation edit therefore appears
to owe a restore, Release build and full non-Corpus test selection.

The executable CI already distinguishes changes. `scripts/Get-CiChangeFlags.ps1`
does not classify ordinary AGENTS.md or prose documentation as build-relevant.
It does classify `docs/design/test-ui/`, source/test projects, selected scripts
and CI changes. `.github/workflows/ci.yml` has a separate documentation lane.
A blanket instruction in prose conflicts with that deliberate distinction.

Proposed replacement for AGENTS.md's verification instruction:

> Select verification from the effects of the change. Prose-only changes to
> instructions, documentation or links require relevant documentation checks
> and review; do not run dotnet restore, build or test solely because Markdown
> changed. Changes to application code, dependencies, build inputs, embedded
> templates/assets, or executable documentation inputs require the relevant
> build and tests. Use the existing CI change classifier and the verification
> policy linked from docs/engineering.md. Run the full solution checks when the
> affected scope, explicit task acceptance criteria or release procedure
> requires them. Record checks not run and why; reuse matching CI evidence
> where permitted. Coordinate heavy verification through the named host owner.

The full command blocks belong in the verification procedure. AGENTS.md should
contain the selection rule and its link, rather than commands presented as an
unconditional obligation. Change the runbook's duplicate wording at the same
time; otherwise the original behaviour returns when an agent follows that link.

| Change | Appropriate default verification |
| --- | --- |
| Ordinary prose, links, AGENTS.md wording | Diff/format/link checks and review of meaning, ownership and instruction conflicts. No dotnet commands solely for this change. |
| Test UI snapshots or embedded renderer templates under docs | Relevant capture/render/browser checks and required build. These files are application/test inputs despite their directory. |
| Capability table consumed by an acceptance script | Correct the documentation first. Report any resulting obsolete consumer; amend or retire it as follow-on work rather than preserve a wrong documentation contract. |
| Application or test code | Affected build and meaningful focused tests; broaden for affected shared boundaries or explicit acceptance criteria. |
| Dependencies, solution or build tooling | Restore/build and relevant wider checks. |
| Release or an explicit request to prove all tests | The required full checks, serialized or backed by qualifying CI evidence. |

The earlier explicit request that the broader documentation revamp ensure
tests still pass remains an acceptance requirement for that work. It need not
be interpreted as rebuilding after each prose edit. This investigation itself
does not claim to satisfy that later regression requirement.

### 1b. Triage wording is ambiguous

The sentence “Triage is the only current term” omits what it is the term for.
It can reasonably be read as retiring Audit and Blocked intake. Those concepts
are explicitly distinct in the PRD and FRDs.

Proposed replacement:

> Audit, Triage, Unidentified, Image Intake and Blocked intake are distinct
> concepts. Use Triage only for the dedicated Triage workflow, never for generic
> inbox sorting. Use Unidentified for the outcome formerly called Needs
> sorting. These naming rules do not replace Audit, Blocked intake, Image
> Intake or the incomplete-Audit-evidence rules. See CONTEXT.md and the owning
> FRDs for their definitions.

After glossary repair, AGENTS.md can shrink this to a one-line instruction to
use the reserved terminology in CONTEXT.md. Do not reproduce the glossary.

There is a separate substantive conflict: FRD-03 calls Triage “a Case,” while
operator-notes.md says it does not technically count as a case and the glossary
calls it pre-Case work. Prefer “a separate work record with its own reference
and workflow” unless a product decision explicitly changes formal Case identity.

### 1c. Recent vendor guidance and a practical size target

The requested evidence window is 1 June–8 September 2026. Publication dates,
not crawl dates, determine eligibility.

| Source | Date qualification | Supported conclusion |
| --- | --- | --- |
| [Anthropic: Steering Claude Code](https://claude.com/blog/steering-claude-code-skills-hooks-rules-subagents-and-more) | Published 18 June 2026; eligible. | Recommends under 200 lines, an owner, an overview/index, scoped conventions and procedures in skills. |
| [Anthropic: Maximizing the value of sessions](https://claude.com/blog/maximizing-the-value-of-your-claude-code-sessions) | Published 14 August 2026; eligible. | Keep specific startup instructions, move workflow-specific instructions into skills, and inspect loaded context. |
| [OpenAI: Custom instructions with AGENTS.md](https://learn.chatgpt.com/docs/agent-configuration/agents-md) | Current official page opened; no publication/update date established. Excluded as proof of a June-or-later recommendation. | Documents a configurable combined instruction-loading limit of 32 KiB by default, rather than a universal per-file Markdown maximum. |

Official-domain searches did not establish a dated June-or-later OpenAI
recommendation for either 500 lines or ten lines. No reviewed eligible official
source supports a universal ten-line rule. This is an evidence gap, not proof
that no such guidance exists. Older Anthropic articles were excluded from the
date-qualified recommendation.

Recommendation: target roughly 100–200 readable lines for the complete root
guide initially, with a short human-owned section. This is a local design
target, not a Codex limit or a demand to omit essential safeguards. Measure
bytes and actually loaded instructions as well as lines. Do not raise the
loading cap to avoid removing duplication.

The current managed Kanmer block alone is 83 lines and 8,860 bytes. A ten-line
total is incompatible with preserving that installed block. Shortening it
requires a change through Kanmer's setup/template ownership, not a Pegasus
rewrite inside managed markers. Nested instructions are useful for scoped
rules; merely splitting material that is still loaded together does not remove
the aggregate context cost.

Retain in the human-owned root guide: project orientation, concise development
principles, Core ownership, terminology and documentation routes, verification
selection, corpus protection, and pointers to live-operation rules. Move ADR
templates, detailed command examples, reference-generator mechanics, UI capture
details and temporary V1 remediation coordination to their actual owners.

## 2. Documentation ownership

### Proposed owner map

Replace the index's universal file ranking with ownership by question.
Scheduling cannot outrank an architecture decision, and passing local tests
cannot establish deployed state or operator acceptance. Current operator
instructions and accepted requirements settle intent; code establishes source
implementation; exact-artifact observations establish deployment.

| Information | Proposed owner | Other locations should do |
| --- | --- | --- |
| Ticket lifecycle, leases, gates, review/proof/closeout mechanics | Installed Kanmer and live board configuration | Link; never reproduce a second pipeline. |
| Scope of current work, sequencing, task grants, status, release evidence | Kanmer ticket/group and exact PR/CI/release evidence | Link from a current observation where useful. |
| Root agent entry instructions | AGENTS.md | Keep short and refer to scoped owners. |
| Documentation routing, ownership, placement and Markdown conventions | docs/index.md | Kanmer templates remain upstream templates; avoid copied local templates. |
| Product outcomes and deliberately unsupported product scope | PRD | Link from inventory and glossary. |
| Functional behaviour, states, inputs, limits, acceptance | Owning FRD | UI and operations reference rather than redefine it. |
| Technical mechanism and durable rationale | ADR | Current architecture identifies applicable decisions. |
| Domain vocabulary | CONTEXT.md | Short definitions with FRD links, not lifecycle specifications. |
| Capability identity to requirement mapping | capabilities.md, if retained | No delivery ledger or independently maintained release sequence. |
| Current source architecture and policy/caller locations | current-architecture.md | No live deployment claims or ticket plans. |
| Dated deployed configuration, activation and recovery observations | operations.md | No copied requirements, procedure bodies or full release history. |
| Engineering implementation/testing policy | engineering.md | No Kanmer mechanics or task-specific coordination. |
| Repeatable procedures | Repository skills, calling existing scripts | runbook.md becomes the human navigation/index page. |
| UI presentation, components, assets and visual conventions | design/README.md | FRD-12 owns functional interface behaviour. |

### 2a. engineering.md and Markdown

Markdown rules appear in engineering lines 50–59 and AGENTS.md; placement
routes through index.md back to AGENTS.md. ADR/PRD/FRD templates add further
copies. The problem is overlapping ownership, not that engineering guidance
could never reasonably discuss Markdown.

Choose docs/index.md as the sole Pegasus documentation convention owner.
Keep a short rule and links there; remove copied prose from AGENTS.md and
engineering.md. Include explicit exceptions for generated preambles, YAML
frontmatter, skills and temporary operator-requested artifacts. “H1 on line 1”
currently conflicts with required ADR YAML frontmatter, not just Kanmer's block.

Kanmer's installed `kanmer-docs` skill uses templates and configured `repoDocs`
globs. Its project guide template applies only when AGENTS.md is absent and
has five sections: Commands, Architecture map, Conventions, Gotchas,
Verification. These can remain concise sections or links. Pegasus should not
fork the skill to enforce local wrapping or duplicate the full template in
each index. The generated doc-structure asset explicitly describes itself as
non-authoritative; do not promote it into a competing local policy.

Additional engineering findings:

- Lines 70–93 mix evidence categories with required execution order and old
  release scope. Preserve evidence distinctions; separate them from the
  consequence-based verification policy. The 10 MiB limit is stale.
- “One Core owner” says one implementation but later stops at the third.
  Make the threshold consistent: a competing business-policy implementation
  is already a defect at the second copy.
- The two-week deadline for unwired code is an arbitrary cleanup trigger.
  Replace it with task ownership and evidence of a current requirement.
- “Lessons from the predecessor” contains general rules plus obsolete counts
  and anecdotes. Keep justified general rules; remove the historical narrative.
  In particular, “a guard that has never fired is deleted” is unsafe as a
  blanket rule: a valid prevention control need not have failed in production.
- “v1 shared development contracts” describes Foundation and A/B/C coordination
  as incomplete, despite AGENTS.md recording integration through PR674. Move
  surviving API ownership to architecture, signatures to code, and stream
  coordination/evidence to the owning Kanmer records.

### 2b. open-decisions.md: section dispositions

Keep only an unanswered material choice, its owner, affected requirement,
evidence needed and explicit blocking scope. A pending implementation or
verification run is work, not necessarily an undecided product requirement.

| Existing section | Recommended disposition |
| --- | --- |
| Introduction's list of settled roles, identity and lifecycle rules | Remove after checking links point directly to the owning FRDs. |
| Historical QDOS-alpha release sequencing | Remove. Current v1 supersedes EVA-dependent scope; “three v1 PRs remain unmerged” contradicts the recorded PR674 integration. Retain relevant cutover/schema decision in the applicable ADR, not the old checklist. |
| Future AI Operations boundary | Separate genuinely undecided transport from implemented/deployed gate observations. ADR-0035/FRD-10/11 own the ledger; operations owns gate observations. |
| Upload-link limits, former VRM threshold question | Move settled limits to FRD-02 and recognition policy to FRD-06; delete the resolved rows. |
| Credential ownership | Credential-operation skills plus operations owner/target references. Leave only a genuinely unassigned owner as a question. |
| QDOS extraction thresholds | Retain the unanswered acceptance choice, if still required; separate missing evaluation execution into its existing work record. |
| Telemetry cap and Azure budget | Keep any unaccepted budget decision; move measured expenditure/configuration to operations and evidence collection to Kanmer. Delete the obsolete cost narrative. |
| Performance dataset ownership | Keep only the ownership/acceptance question if unanswered; do not make the deferred soak exercise a universal delivery gate. |
| Mailbox activation/matching/confidence | Remove settled taxonomy, QDOS predicates and policy versions. Scope any unresolved selection/display question to the exact routes still lacking accepted rules. |
| EVA manual handoff | FRD-07 already contains the 13-field order and Reference mapping. Remove duplicate specification; separate genuinely missing operator acceptance from settled mappings. |
| EVA API activation — resolved | Remove after reconciliation with FRD-07/ADR-0038. The old at-most-one-success-per-Case statement contradicts explicit new staff re-send attempts. |
| Glass's repair estimates | Selection is settled; live acceptance is operator work, not an open integration-choice question. |
| Provider API tenancy/wire contract | FRD-09 owns API-01. Retain only a specifically proposed additional tenancy decision; move credential rollout evidence to work tracking. |
| provider_domain_key | Remove speculative preservation/migration question unless a current source and consumer is identified. An undefined historical name is not a requirement. |
| Valuation providers, report-submission contracts, post-report dispute lifecycle, missing report wording | Retain only the exact unaccepted contract/behaviour; link the current owner. Do not group unrelated blockers into one decision. |
| DVLA/DVSA and Audatex | Distinguish selected source mechanisms from missing live credentials or representative variant acceptance. Do not describe the entire adapter as undecided. |
| Global vehicle checks | FRD-02 owns required progression; keep only unresolved source-specific failure contracts. |
| Send-to-AI toolset/transport | Reconcile with ADR-0031 and ADR-0035; remove the superseded fourteen-tool inventory as a current acceptance scope. Separate old push transport from pull-ledger decisions. |
| Future custom assessor | Defer through Kanmer unless an active product decision actually depends on choosing it. Do not reserve hosting or interfaces. |
| Later UI capabilities | Remove resolved layout/signatory decisions. A generic obligation to design future work is engineering/design guidance, not an open decision. |
| Mail workspace freshness/retention start | Remove resolved section and update incoming anchors to FRD-08. A historical link is not a reason to retain a second owner forever. |
| App Insights daily cap | Merge any remaining cap question with the earlier telemetry item; operations owns the measured 0.5 GB configuration. |
| Manual upload in production | Re-investigate the custody premise against the relevant deployed artifact. Source has durable staging; the historical claim that it remains local-only cannot decide current deployment permission. |
| Azure ownership/retirement targets | Remove generic hypothetical questions. Exact-target inventory/authorization belongs in an actual operation's procedure and work record. |

Do not create a new durable file for every removed row. Most already have an
FRD, ADR or operations owner. Create a document only for a distinct current
contract that otherwise has no suitable home.

### 2c. Retire operator-notes.md without losing requirements

The file declares every line binding operator truth, but also says sections
are provenance only and gives later decisions precedence over earlier ones.
It is now an edited composite specification, not simply verbatim notes.
Recent Git history includes consolidation and v1 updates; this review does
not attribute every historical line to the operator or claim to have recovered
every original note.

Retirement is justified. First map each material statement to a destination,
compare meaning, resolve actual conflicts, and update inbound references.
Keep exact source provenance in existing reference material or Kanmer evidence,
not another newly created master authority file. Git retains the retired text.

| Source sections | Destination |
| --- | --- |
| Current v1 decisions | Product outcomes to PRD; behaviour to the relevant FRDs; task/deployment grants and old three-stream instructions to Kanmer. |
| Evidence and delivery states | One engineering evidence definition, linked by PRD and operations. |
| Stage 0 Triage; Unidentified | FRD-03 and FRD-02; short glossary definitions. |
| Receiving instructions/images and image-origin clarification | FRD-02/05/06; identity rules in FRD-01. |
| Chasing, inspection and post-report process | FRD-01/06/08/11, retaining staff initiation and exact Sent evidence. |
| Required instruction fields, channels, routing and taxonomy | FRD-02/08/09. |
| Inspection address | FRD-06; Principal defaults/admin action in FRD-04. |
| Principals, repairers, historical parties, references and case types | FRD-01/04; preserve snapshots and immutable references. |
| Reserved terms | CONTEXT.md, linked by AGENTS.md. |
| Staff roles and access | FRD-04; remove obsolete periodic-review wording. |
| CAP-001–CAP-022 product needs | PRD outcomes plus a traceable mapping to current capability IDs and FRDs. Do not silently drop or renumber these source IDs. |
| Environment/tools and development-data rules | Engineering and relevant procedure; retain the corpus boundary. |
| Interface language and presentation | design/README.md; functional actions/states in FRD-12. |
| External systems and storage/staging interpretation | Required behaviour to FRDs; actual configured estate to operations; mechanism to ADR where warranted. |
| Report readiness and additional operator statements | FRD-01/06/11 after clause-by-clause mapping. |
| Source provenance | Existing evidence references and the temporary migration crosswalk, later attached to Kanmer. |

The current date-precedence mechanism masks real contradictions: old tabbed
Case layout versus scrolling v1; periodic account review versus its removal;
future estimating integration versus included Glass's estimates; EVA-owned
engineering versus native Pegasus engineering. Replace old clauses at their
durable destination instead of carrying both with “historical” disclaimers.

### 2d. runbook.md: extract procedures, relocate other content

The runbook is not exclusively procedural. It contains requirements, policies,
architecture, evidence definitions, allocation-dependent acceptance and old
implementation statements. “Stable invariants” still calls the outbox a release
dependency rather than current source, despite current Worker/queue code.
Configuration still cites superseded ADR-0022 instead of the current mailbox
identity contract. The testing section permits several agents' concurrent
suites while the current root instructions designate one heavy verifier.

Extract by recurring task, not one skill per heading:

| Runbook material | Proposed destination |
| --- | --- |
| Supported platforms, checkout prerequisites, local DB, setup/start/status/stop/reset | New `pegasus-local-development` skill using Initialize/Invoke-LocalDevelopment scripts, with Windows/Linux references. |
| Build/test profiles, browser setup, focused capture, corpus evaluation | New `pegasus-verify` skill; engineering owns when each profile applies. Keep corpus acceptance distinct from synthetic protocol tests. |
| Provider-domain and principal-corpus authoring | One `pegasus-reference-data` skill calling existing authoring scripts; preserve immutable evidence and normalized-output distinctions. |
| Mailbox admission/disable and OAuth certificate rotation | A bounded `pegasus-integration-operations` skill with separately loaded mailbox and certificate procedures. It must distinguish application administration from tenant/cloud setup. |
| Release validation, artifacts, bootstrap, Worker activation and rollback | Extend the existing `pegasus-release` skill rather than create another release owner. |
| Explicit intake-data wipe | Existing `pegasus-wipe-intake-data`; preserve maintenance window, Worker stop, exact targets and receive-time cutoff. |
| Recovery/PITR and monitoring diagnosis | Release/recovery references and a focused diagnostic procedure; promote to a separate skill only if independently useful. |
| Configuration keys, custody topology and current integrations | Architecture for configuration ownership; operations for dated deployed values. |
| Stable business rules and Unidentified queue meaning | Relevant FRD. |
| Evidence tiers, corpus policy, approval policy | One engineering/operations policy owner, referenced from procedures. |
| Repository/delivery lifecycle and release dependency order | Kanmer; release skill for executable promotion procedure. |

Keep runbook.md as a short procedure directory usable by humans and all agents.
Skill bodies should contain trigger, inputs, prerequisites, bounded steps,
validation, failure handling and stop conditions; longer platform commands can
live in skill references. Do not move 86 KB into one always-read SKILL.md.

Existing canonical release skill explicitly covers Windows x64 and Linux x64
PowerShell 7 and platform-specific EF bundles. The operator-added
`SKILL (2).md` files are duplicate candidates, not additional discoverable
SKILL.md entry points. Compare and merge useful clauses into the canonical
release/wipe skills before deleting those copies. Do not remove the whole
`.agents/skills` directory: it also contains unique Pegasus/UI skills.

### 2e. capabilities.md

Retain stable IDs and concise durable outcomes with canonical requirement
links. Remove historical allocation narrative, release ladder, ticket status,
implementation anecdotes and repeated normative clauses after migration.
Kanmer should own work ordering and delivery status. A product release scope
may have a durable specification when required, but this repeatedly amended
table should not be a second scheduling engine.

Concrete drift: the table contains 244 rows: 154 Now, 26 Next, 35 Later and
29 Not planned. Its summary says 153 Now and 27 Next; historical prose and the
ordered sequence still say 205 planned capabilities, while 244 minus 29 is
215. This is already a maintenance failure, not merely a stylistic concern.

Observed dependency: `scripts/Invoke-QdosAlphaAcceptance.ps1:403–435` parses
exactly six table columns and selects the `0.1.0-alpha.1` target. It rejects
different row shapes. The operator explicitly directs that documentation
correctness, streamlining and current requirements take priority over this
consumer passing. Remove obsolete columns and scheduling when the intended
documentation structure calls for it; do not retain them for this script.
Record the resulting incompatibility as follow-on work: retire the obsolete
alpha runner, or update it to a genuinely required current acceptance input.
Its existing parsing contract is neither documentation authority nor a blocker
to this cleanup. Do not invent another hard-coded roster to preserve it.

### 2f. boundaries.md

Doing work later is not an architectural or product boundary. The table mixes
unimplemented tasks, supported architecture, explicit product exclusions,
temporary activation evidence and particular UI decisions.

Recommended disposition: move permanent exclusions to the PRD, technical
constraints to ADR/architecture, functional exclusions to their FRDs, and
deferred work/evidence to Kanmer. Retire boundaries.md if no independent
contract remains. A short boundary index is optional, but it must not repeat
the underlying normative rules.

Examples requiring correction: provider API is listed as an excluded endpoint;
OCR as an excluded service/flag/route; AI assistance excludes “direct mutation”
without distinguishing unconfirmed Actor writes; production deployment itself
appears as deferred. These collide with current source/decisions. Preserve
actual prohibitions such as autonomous sending; remove obsolete absence claims.

“Preserved seams” must not require unused ports, records, configuration or
schema for future work. Retain identities needed by current evidence, not
speculative future integrations.

### 2g. operations.md versus current-architecture.md

Keep both, with narrower purposes:

- Architecture: current source topology, dependency direction, major stores,
  policy ownership, callers, integration/configuration boundaries and decision
  links, anchored to a source revision. Update when those change.
- Operations: latest known deployed artifact/configuration, activation state,
  observed time, evidence pointer, operational owner and recovery qualification.
  Update when deployment or observation changes those facts.

Do not require an architecture edit for a deployment that changes no
architecture. Do not overwrite a dated observation with an unverified present
tense statement. Inactive deployment flags and absent source implementations
are different facts.

Both currently repeat evidence tiers, deferred integrations, release status,
configuration and policy. Architecture's opening says the running system
“now,” then describes source assemblies not deployed. Its diagram labels
Graph/Blob/Box as targets while later sections describe current callers.
The source/generated-material table still describes independently validated
workspaces after their retirement.

Operations' deferred-seams table calls DOC/MSG parsing, Graph webhooks and the
EVA client absent while other current documents describe implementations.
Its 1,668 lines include a long release ledger. Retain a compact latest-known
estate plus unresolved operational qualifications; move historical release
attempts to existing Kanmer/release evidence and let Git retain earlier text.
Preserve evidence that still controls rollback compatibility or acceptance.

## 3. ADR review

There are 39 ADR files numbered through 0040; 0017 was explicitly never issued.
Do not fill the gap, reuse IDs or delete citation targets as cosmetic cleanup.
An old decision may remain useful rationale without its obsolete clauses being
current requirements. The index's “accepted ADRs are the current architecture”
claim is unreliable while partially superseded bodies remain broad.

| ADR | Review and recommendation |
| --- | --- |
| 0001 | Already superseded by 0040. Current OCR references should target 0040; retain historical rationale only. |
| 0002 | Too broad: stack, environments, hosting, authentication, schema and polling in one decision. Status omits hosting supersession already recorded in metadata; body still advertises App Service/shared Azure development. Keep the modular-monolith rationale and make current clause owners explicit. |
| 0003 | PdfPig selection remains relevant. Initial local-only activation and dated benchmark passages must not determine current deployed custody. Route operational evidence elsewhere. |
| 0004 | Provider API boundary remains; staff MCP is superseded by 0011. Old Next/unallocated statements are not current source status. |
| 0005 | Accepted body still defers DOC/MSG to Needs sorting and limits OCR to scan-like pages. Reconcile with integrated readers/0040; identify which safety limits remain current. |
| 0006 | Provider-neutral ownership remains valuable; QDOS-only, Development-only and SQLite initial-migration clauses are obsolete as current instructions. Record partial supersession by 0008 and later source model explicitly. |
| 0007 | Keep direct-terminal rationale; traverse 0014/0015/0037/0039 for current environment, artifact and platform contract. Avoid making readers execute its old route. |
| 0008 | Route/principal separation remains current. QDOS-only allocation and no-provider-registry context are historical. Metadata omits the stated partial supersession of 0006. |
| 0009 | Four-project boundary and import provenance remain useful. Workspace existence/CI obligations are stale after integration/retirement; clarify scope with 0025 and workspace README. |
| 0010 | Already a documentation-governance tombstone. Update destination links when governance moves; no new ADR needed merely to move docs. |
| 0011 | Retain Automation Actor versus staff boundary; let FRD/design own UI provider labels and current inventory. Add current status summary for body-only readers. |
| 0012 | Already moved to FRD-06. Update current callers' links to that FRD instead of routing through the tombstone. |
| 0013 | Metadata says fully superseded by 0029, but 0029 concerns the image projection, not every remaining auth/deployment clause. Resolve partial versus whole supersession and obsolete EVA-owned readiness references. |
| 0014 | Local development plus production remains coherent. Exact operation steps belong in skills; no need to invent staging. |
| 0015 | Container Apps/OCI choice remains. Zero minimum replicas and accepted cold starts need checking against current measured topology/latency decisions; do not assume old sizing remains a permanent requirement. |
| 0016 | Windows-only standalone evaluator is a distinct local tool decision, not a global Windows-only restriction. Verify retained tool status; remove allocation mechanics from current guidance. |
| 0018 | Principal inspection mode remains; post-creation editing being deferred conflicts with FRD-04's current settings. “Fail-closed compatibility” retention also needs a concrete current dependency. |
| 0019 | ONNX mechanism remains. Universal staff confirmation conflicts with the later accepted automatic recognition/matching exception in FRD-06. Keep model choice separate from functional recognition policy and old vendor research. |
| 0020 | Already relocated to FRD-09. Open decisions and other current prose still treat the tombstone as the predicate owner; correct those links. |
| 0021 | Superseded status plus “every other clause stands” is confusing. 0031 retains most of its contract, while 0026/0027 amend activation/auth. Provide one clear current contract path and stop using fourteen tools as a current count. |
| 0022 | Superseded by 0024. Remove current runbook/architecture references to its coordinate identity and fallback as the live policy. |
| 0023 | Documentation-governance tombstone. Keep citation stability; update its destination links. |
| 0024 | Durable identity decision requires amendment: it specifies mailbox plus Graph ImmutableMessageId as receipt identity; FRD-08 now specifies mailbox plus canonical RFC Internet Message-ID. Separate immutable provider coordinates from business deduplication identity. |
| 0025 | Integrated application ownership is coherent. Old scheduling/workspace implementation claims belong in history; retain single-PDF-owner and four-project constraints. |
| 0026 | Production-capable MCP composition remains; persistent certificate configuration and current activation procedure should be linked explicitly. |
| 0027 | Material conflict: ephemeral keys make tokens die on restart here, while FRD-10 and current source require persistent signing/encryption certificates and rotation overlap. Record the changed technical decision. |
| 0028 | Renderer in existing Web container remains consistent. Retain runtime-dependency requirements and link the canonical release packaging procedure. |
| 0029 | Explicitly excludes a VRM-keyed Box root and says a future ADR is needed; FRD-05 now requires that root and its fold into Case custody. Record the missing amendment, preserving separate formal Case identity. |
| 0030 | Pre-cutover compatibility exception is attached to historical alpha step 7. Reconcile with explicit disposable-development-data direction; define any remaining real rollback requirement from supported artifacts/data, not an obsolete release plan. |
| 0031 | Keep absence of EVA export tools and Actor safeguards. Clarify what remains of the old push transport alongside 0035's pull ledger. |
| 0032 | Superseded by 0033. Its graph-wake/commit-before-publish rules need explicit retained-scope mapping; they should not be lost because warm execution changed. |
| 0033 | Unified queue and warm Worker remain relevant. Separate the five-second product target into PRD/FRD and deployed capacity measurements into operations. |
| 0034 | Correctly superseded by manual-only 0038. Remove automatic-submission remnants from active prose; retain this as historical rationale. |
| 0035 | Pull ledger remains coherent; job kinds/states belong in one FRD. Explicitly distinguish separate AI-09 transport rather than implying every AI task uses it. |
| 0036 | Sent evidence requirement remains. “No second outbound record” conflicts with the v1 durable staff-send operation in FRD-08/source; distinguish operation tracking from authoritative Sent evidence in an amendment. |
| 0037 | Correctly superseded by Windows/Linux 0039. Do not restore Linux-only instructions from this body or an older copied skill. |
| 0038 | Manual-only EVA submission matches current operator direction. Keep explicit re-send/unknown handling in FRD-07. |
| 0039 | Current Windows/Linux release choice; verify skill/script references use this decision and platform-matching bundles. No fresh Linux execution is claimed here. |
| 0040 | Current qualified prebuilt-layout OCR choice; reconcile old 0005/FRD references and keep activation evidence separate. |

Priority candidate missing ADRs/amendments: RFC-based mailbox identity,
persistent OAuth keys/grant attribution, Image Intake Box custody, and durable
staff-send operation tracking. The SQL-indexed 24-hour Azure document cache is
also a candidate durable storage decision: source and operator requirements
exist, but no dedicated decision in the reviewed ADR register clearly owns its
mechanism and eviction/custody boundary. Confirm whether an existing decision
adequately covers it before creating another file. These are documentation
gaps to resolve, not permission to implement a different architecture.

Metadata should reflect stated partial supersession and use consistent FRD
identifiers. Do not replace “accepted” with “implemented”: decision acceptance
and delivery proof are different. Preserve immutable history where required,
using new decisions for changed mechanisms and clear status/owner navigation.

## 4. FRD and PRD review

Twelve FRDs are not inherently too few. They cover broad domains; file count
does not measure completeness. Several are too broad and contain newly added
rules beside incompatible older rules. The following is a review of every FRD,
not a claim that every implementation and external integration has passed it.

| File | Findings and recommendations |
| --- | --- |
| FRD-01 | Remove residual EVA-owned Inspection+Audit workflow where superseded by native v1. Preserve normal Case/PO allocation before a later Audit suffix. Reconcile “no Administrator bypass” with explicitly authorized targeted lease clearance; distinguish revoking a lease from bypassing one. Link workflow labels rather than repeat full presentation rules. |
| FRD-02 | Fix 10 MiB versus accepted 100 MiB limits. Mandatory pre-case gates currently withhold Audit creation pending report outcome and say retained-email only, contradicting FRD-01 and Provider API Audit. Specify normal reference versus later suffix separately. Separate upload transport from grouped-image routing; preserve transactional recovery and deliberate staff association precedence. |
| FRD-03 | Replace ambiguous “Triage is a Case” with separate work-record language. Current T-reference, independent findings, cancellation/reopen and exact response evidence are substantial existing coverage; a new Triage FRD is unnecessary. |
| FRD-04 | Current account reset generates/reveals a temporary password, while FRD-12 requires the Administrator to enter/confirm it. FRD-12 also retains periodic Review after this file removes it. Clarify role restrictions versus per-Engineer credential administration. Preserve historical actor/signatory identity after access deletion. |
| FRD-05 | Stale 10 MiB and ADR-0001 references. Add the full current logical-document/custody/cache behaviour currently scattered across operator notes and architecture. Explain where Case custody staff retry differs from image-folder queued retry. Reconcile its image-folder requirement with ADR-0029. |
| FRD-06 | Distinguish suggestion-only recognition from the explicit automatic-reference/match exception. Reconcile no inferred address/default with newly included Principal defaults and suggestions. Remove LegacyUnresolved/pre-release preservation rules unless current data actually requires them. Keep repair specifications, valuation adoption, VAT and source provenance; consider splitting estimates only because they now have a large independent contract. |
| FRD-07 | Opening treats EVA as engineering handoff; native Pegasus handoff now exists. Saved data invalidating completeness indiscriminately conflicts with FRD-01's unchanged/unrelated-save rule. Remove implementation dates and unqualified “never called EVA” from normative requirements. Keep the exact vendor wire mapping, intentional re-send creating another external claim, and unknown outcome handling. Retained estimate imports fit FRD-06/05 better than EVA handoff. |
| FRD-08 | Current RFC identity conflicts with ADR-0024. General system-wide VRM/thread association versus principal-scoped intake matching needs an explicit applicability boundary and shared write precedence; otherwise agents may substitute one for the other. Durable send operation is already specified but conflicts with ADR-0036's wording. Preserve exact Sent-item proof and wipe cutoff. Separate mailbox business behaviour from Graph protocol mechanism. |
| FRD-09 | Fifteen accepted principal routes contradict QDOS-only companion/index assertions. Remove speculative provider_domain_key migration machinery. API-01 already has a substantial declared-field contract; it does not need to be rediscovered through open decisions. Keep auth, paused-read behaviour, create-only rejection and separate API envelope limits. |
| FRD-10 | Persistent keys/current stream/download/export contracts are present; corresponding ADRs lag. Remove duplicated pagination paragraph. Distinguish unconfirmed Actor changes from accepted human findings; “no direct case mutation” is too broad alongside approved commands. Keep scopes/tool inventory here once, not in operations and architecture as competing lists. |
| FRD-11 | New generation freezes and retains artifacts, while Report-draft entry says nothing is saved. Explicitly separate Preview, Generate, Approve/Prepare and Send. Remove “not delivered” annotations for curation/rate cards from normative text. Preserve immutable generations, stale detection, fee-note identity and exact Sent evidence. AI job lifecycle is a good candidate to split from reports. |
| FRD-12 | Password reset and periodic Review contradict FRD-04; lease-holder expiry display contradicts FRD-01's no countdown promise; upload limit is stale. Routes `/Admin` versus design/source `/Administration` need reconciliation. UI behaviour is repeatedly specified again in design/README.md. Keep page/actions/state behaviour here and presentation/components in design. Review permanent redirects against actual bookmark compatibility needs rather than automatically removing them. |

### Missing or insufficiently consolidated functional contracts

Prefer extending an owner before creating a file. Strong candidates for
distinct documents are canonical estimates/valuation adoption and the AI job
lifecycle, currently embedded in FRD-06/11. If split, move complete clauses and
update links; do not leave shortened competing copies behind.

FRD-05 needs consolidated logical document access, durable versus processing
storage, 24-hour idle cache, authorized metadata/download, corruption, pending
custody and recovery rules. FRD-08 needs one coherent send operation, attachment,
unknown-result, Sent-correlation and mailbox-generation contract. These are
extensions to current documents, not automatically additional FRDs.

Existing unresolved candidates include the precise post-report query/dispute
lifecycle and unaccepted report wording. Do not invent their states or wording
to make the document set look complete. Record the exact missing decision.

### PRD

There is one product PRD, which is appropriate for one product. It already
records native v1 engineering/reports, optional EVA and staff-initiated sends,
but still presents the product primarily as QDOS alpha and requires
independently buildable workspaces that have been retired. Its permanent
boundaries defer to a capability table rather than stating a self-contained
accepted product scope.

Rewrite around the current product outcomes, users, included v1 scope,
deliberately unsupported scope and measurable quality requirements. Remove
delivery status and exact test-tool mechanics. Preserve capacity and latency
targets as requirements, qualified separately from measured performance.
Reconcile the PRD's processor/retention approval clauses with the explicit
development-data instruction by distinguishing development scope from actual
external activation; do not silently delete a requirement or add a new gate.
Remove operator-notes supremacy after its statement mapping is complete.

## 5. Other documentation and specificity

CONTEXT.md is worth retaining. It should answer what a term means, not when
an old implementation shipped. Current examples requiring repair include
Unidentified's narrow email/Triage definition and Send to AI's proposal-only
phrasing beside the accepted unconfirmed direct-write model. Keep internal
domain names separate from staff-facing labels. The domain glossary should
link to FRDs rather than own transition, threshold or timing rules.

README.md should be a short product entry point and reliable getting-started
route. Link to the local-development procedure instead of maintaining separate
long command sequences. Link deployed state to operations with an observation
date, rather than carrying an evergreen release assertion.

Design guidance needs the same review even though it was not individually
listed in the brief: its 1,600-plus lines repeat shell, routes, permissions,
workspace behaviour, freshness and acceptance owned by FRD-12. Keep assets,
tokens, components, layout/presentation and snapshot procedure ownership;
replace duplicate functional descriptions with owner links.

The principal-rules-and-mappings README says all other principals remain
review-only, but FRD-09 activates fifteen profiles. Correct the status and
keep only companions that help locate a real rule. Do not create fourteen
speculative dossiers to satisfy a table. `docs/json-extraction-parity/` contains
a vendor PDF transcription; it is evidence, better placed under the existing
reference tree with source hash and link updates, not normative Pegasus docs.

Specificity is useful when it identifies an actual contract, target or input.
Remove anecdotal specificity, not the information needed for safe execution.

| Existing kind of text | Better treatment |
| --- | --- |
| CollisionSpike counts and failed first-email story | “Exercise representative retained input through the actual caller; a build or registration alone does not prove behaviour.” |
| A named retired Azure group in an evergreen safety rule | “Verify exact target identity, dependencies, ownership and recovery before an authorized retirement.” Keep exact names in the operation record. |
| Old U35 exclusion in a durable routing rule | State the current no-backfill/no-replay behaviour; keep the individual incident in its task evidence. |
| A/B/C stream names and Foundation signature tables | Durable capability/contract names and code links; coordination in Kanmer. |
| Historical account names as authorization rules | Role/permission contract; initial account data only where actually required. |
| Exact API keys, field spellings, units, versioned identity rules | Retain precise public/wire contract names and semantics; never generalize them away. |
| Exact production Box root, certificate references or release digest | Keep in dated operations/authorized task inputs, not repeated across general policies. |
| Accepted literal report wording and real evidence provenance | Preserve in its governed asset/requirement or reference; generic prose cannot replace it. |

## 6. Kanmer and cross-agent skill reconciliation

The ticket worktree removes local kanmer-* skill copies while retaining
project-specific skills. The installed plugin owns lifecycle instructions;
the root guide must not demand recreation of removed local mirrors by default.
Its current “reconcile repository-local Kanmer skill mirrors” wording needs
replacement with the chosen installation/discovery policy.

Other agents must still have a documented way to access Kanmer and repository
skills. Codex plugin availability alone does not establish availability to
Claude, Grok or OpenCode. Inventory supported providers' discovery paths and
version sources; prefer one maintained distribution and configuration links
over independently edited copies. Do not remove another provider's only
installation until the intended replacement is verified.

The installed Kanmer doc-structure asset itself still contains a proof-table
reference to merged main despite newer managed instructions resolving the
integration branch from configuration. Report that upstream inconsistency;
do not copy it into Pegasus or fork the plugin locally as this docs fix.
Live configuration and current phase skill govern the actual workflow.

Temporary grant/host-verifier details should be discoverable from the active
Kanmer group, with a short root pointer while needed. Removing their repeated
prose must not remove the ability to find the current verifier or the exact
authorized targets. Neither a permanent AGENTS file nor a skill should become
an evergreen grant to deploy, wipe data, send messages or merge unrelated work.

## 7. Recommended amendment sequence and verification

1. Accept the ownership map and record a clause-to-owner crosswalk for
   operator-notes, resolved decisions and moved procedures. Classify each
   conflict as settled by current instruction, confirmed by source only, or
   requiring an actual product decision.
2. Correct verification wording and terminology together in AGENTS, engineering
   and runbook. Reduce the human-owned AGENTS guide; preserve managed ownership.
3. Reconcile the high-impact FRD/ADR conflicts: upload/Audit identity, mailbox
   identity, OAuth persistence, image custody, staff send and report generation.
4. Move every surviving operator statement into its canonical owner. Repair
   authority links, then retire operator-notes and obsolete open-decision rows.
5. Correct the capability inventory and remove obsolete release columns and
   scheduling. Move scheduling/deferred work to existing Kanmer records when
   the investigation is attached there. Record obsolete script dependencies
   for amendment or retirement; they do not delay the documentation correction.
6. Extract the bounded skills and make runbook a procedure index. Consolidate
   the two copied SKILL (2).md files and verify cross-agent discovery.
7. Rewrite architecture as a source snapshot and operations as a dated deployed
   snapshot. Prune duplicate design/glossary/README rules and update all links.
8. Validate the actual resulting diff and remove this temporary working file
   once its evidence and durable outcomes have been attached to the owning work.

For the amendment, use applicable existing Markdown/link checks as diagnostic
evidence. A checker that enforces obsolete documentation structure must be
updated or retired, not treated as authority over the intended documentation.
The existing link checker checks tracked Markdown
paths, not anchor existence or external URLs, and excludes skill directories.
It would also skip this untracked report. Explicitly verify moved headings,
skill reference paths and newly added files rather than calling that checker
proof of semantic or anchor correctness.

Measure root instruction bytes and loaded context; inspect both root and
scoped discovery. Use representative review scenarios: an AGENTS prose edit
must not trigger dotnet; an embedded renderer edit must select relevant tests;
an operator-requested full regression must still run; a tracked ticket must
follow Kanmer; an isolated requested report must not invent a ticket.

For the broader work's explicit regression requirement, run/reuse appropriate
exact-head CI evidence with one heavy owner. Distinguish application regressions
from documentation tooling failures caused by deliberately retired contracts.
The latter are recorded for correction or retirement and do not justify
preserving incorrect documentation. If no runtime inputs change, do not invent
application tests that assert Markdown wording or add a general documentation
framework. Record failures and missing evidence honestly.

Completion means no material statement lost, no competing active owner,
correct links and discovery, current-state claims correctly qualified, and
checks appropriate to the actual diff. It does not require deleting every old
reference, creating a document for every capability, or reaching an arbitrary
ten-line target.
