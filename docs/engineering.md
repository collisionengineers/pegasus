# Engineering workflow

This document is the sole owner of repository engineering workflow. It defines how work is authorized, planned, implemented, proved, reviewed, and delivered. It does not establish product behavior, live status, deployment authority, or operator acceptance.

## Authority and source resolution

Resolve claims through [the repository source registry](index.md). Nearby files, tools, skills, plans, tests, or code must not be blended into a rule merely because they agree.

| Priority | Authority | Qualification |
| --- | --- | --- |
| 1 | Direct user instruction | A later explicit instruction amends earlier instructions only for the affected scope. |
| 2 | [Operator notes](operator-notes.md) | Own operator and business truth. Maintainers may maintain their organization and wording under standing authorization, but a material meaning change requires direct user resolution. |
| 3 | [Requirements](requirements.md) and [capability allocation](capabilities.md) | Own required product behavior and current allocation. Allocation or intent is not implementation evidence. |
| 4 | [Accepted decisions](decisions/README.md) | Own accepted current and historical technical decisions. |
| 5 | Explicitly accepted executable contracts and tests | Apply only to the exact release and rule for which they were accepted. A test written by the implementer is not automatically an independent interpretation of the rule. |
| 6 | Retrospectives and observed incidents | Constrain delivery by showing demonstrated failures; they do not define product behavior. |
| 7 | The local corpus, raw references, predecessor repositories, and imported workspaces | Show real shapes, prior behavior, and failure modes only. They are not specification authorities or source trees to migrate. See [reference handling](reference/README.md) and [workspace boundaries](../workspaces/README.md). |

A skill, tool, plan, registration, document, or repository convention is never by itself a product-rule source or authorization.

If authorities conflict or material ambiguity remains:

1. Stop the affected work.
2. Obtain direct user resolution.
3. Record the result in the appropriate canonical owner.
4. Track unresolved matters in [open decisions](open-decisions.md).
5. Keep affected work reversible.

Do not invent rules affecting references, workflow transitions, permissions, retention, external-system behavior, or operator meaning. Operator-facing UI must also follow the approved source routed through [design](../design/README.md).

## Repository lifecycle

Use one accountable lead for each route.

| Need | Required route |
| --- | --- |
| Onboard or convert the repository | Preserve source roles and every material claim in one reviewed onboarding change and pull request. Do not merge it. |
| Plan one material change | Inspect authorities, architecture, current callers, and relevant evidence; resolve decisions; persist one plan in the activated issue and change record; obtain plan acceptance; then stop before implementation. |
| Implement, fix, or remediate | Continue the same bounded branch, issue, change record, and pull request. Exercise and prove the real caller. |
| Explain behavior or feedback | Work read-only and in plain English. Distinguish intended, implemented, caller-proved, deployed, live-verified, and accepted behavior. |
| Review a pull request | Perform an independent, read-only review of the exact base and head. Do not substitute a nearby branch or working tree. |
| Inspect or operate an external service | Read current state or perform only the explicitly authorized operation against named targets. Repository work records do not grant external authority. |

## Accountability and delegation

- One lead remains accountable for integration and the final claims.
- Before repository edits or implementation delegation, the lead maintains a current, bounded execution list.
- Delegated scopes must be bounded and non-overlapping.
- Delegates report facts, inferences, limitations, and the next evidence needed.
- Read-only discovery or review may run in parallel only when it cannot overlap a writer.
- The author of a material rule implementation does not grade their own interpretation. Use an independent reviewer, a genuine live or local probe, or a second reviewer given the authoritative rule rather than the implementation rationale.

## Planning and change identity

A material change uses one delivery identity:

- one scoped branch;
- one GitHub issue when activated;
- one dated record under [changes](changes/README.md);
- one pull request when review begins.

Continue that identity from planning through remediation. Do not open parallel task systems or rewrite history into replacement records.

GitHub Project fields own live work state. The issue and change record hold scope, decisions, evidence, and concise delivery notes; they must not become a second status ledger. Reopen or regress the live state when evidence fails rather than preserving an inaccurate completion label.

Do not recreate the removed repository-local plugin suite, `.repoplugin` task database, task-folder handoffs, generated dashboards, large generated ledgers, or any parallel status database.

A branch, issue, change record, pull request, or accepted plan does not broaden permission to read or mutate cloud services, rotate credentials, deploy, alter accounts, or perform destructive operations.

## Evidence states

Report these states separately. Never collapse them into “done.”

| State | Minimum evidence | Does not prove |
| --- | --- | --- |
| Intended or planned | An authoritative requirement, capability allocation, accepted decision, or accepted change plan | Source code, a caller, deployment, or acceptance |
| Implemented | Source exists, compiles or parses, and is connected as claimed in the repository | That any genuine caller reaches it |
| Caller-proved | A genuine input traverses the real entry point and the intended implementation boundary | Azure configuration, production traffic, or operator acceptance |
| Locally verified | Applicable focused, integration, corpus, and build/test checks pass locally | Successful deployment or behavior for every provider and state |
| Deployed | The named artifact was successfully deployed to the named target | That live traffic calls it or that the workflow is correct |
| Live-verified | Authorized evidence shows the deployed real caller handled the enumerated live states | Product or operator acceptance outside those states |
| Accepted | The authorized product, operator, or user owner accepted the named result | Future releases, untested states, or unrelated capabilities |

File presence, registration, dependency injection, mocks, repository consistency, structural checks, deployment, and documentation are not caller evidence. “Registered but uncalled” is incomplete.

For each major initiative, define before scaffolding:

- what counts as alpha;
- what counts as production;
- the thinnest end-to-end slice that must survive real traffic;
- who can accept each boundary.

“Live on a mailbox” or deployed to an environment must never be reported as production without the agreed production finish line.

## Engineering invariants

Topology and accepted boundaries are owned by [architecture](architecture.md).

### One Core owner

- Every business policy belongs to one named Core use case or query.
- Web and Worker boundaries translate requests or events and orchestrate only their own boundary.
- Cross-feature work calls the target feature’s named use case or query; it must not reach into another feature’s tables.
- A business rule, classifier, allocator, parser, workflow transition, or external effect has one implementation.
- Shared code is consumed through project references, never by copying source.
- On encountering a third implementation, stop delivery and consolidate. Migrate or delete the replaced code, registration, tests, and documentation in the same bounded slice.

### Capability organization

Organize by business capability and use Collision Engineers’ business language.

Do not introduce horizontal `Common`, `Helpers`, `Utilities`, or undifferentiated `Services` folders. Names such as `V2`, `New`, `Manager`, `Helper`, or `Util` do not justify another layer.

`Audit` and `Triage` retain their reserved business meanings. Operator UI must not expose internal deployment, extraction, or orchestration mechanics.

### Abstractions and deferred capabilities

Add an interface or abstraction only when at least one of these is true:

- it represents a real external boundary;
- it has two concrete callers or implementations;
- an accepted architecture decision requires it.

A deferred capability belongs in [capability allocation](capabilities.md), an accepted [decision](decisions/README.md), or an [open decision](open-decisions.md). Leave it unbuilt until a current caller exists. Do not express deferred intent as dormant registration, an unused endpoint, a permanently disabled feature flag, a placeholder, a speculative compatibility shim, or dark destructive code.

Anything built but unwired for two weeks must gain a real caller or be deleted. A dangerous superseded capability is deleted immediately rather than merely gated.

### Classifiers, extraction, and failure semantics

- Classifier and extraction precedence must be explicit, ordered, documented, and covered by contradiction tests.
- Re-derive the complete precedence model whenever a rule is added. Append order must not become semantics.
- Prefer stronger content evidence over weaker incidental evidence according to the accepted policy; do not silently allow sender identity or a filename to veto stronger evidence.
- Every external client and catch path distinguishes `terminal`, `transient`, and `unknown`.
- Terminal outcomes park or otherwise close the affected work and stop retries.
- Unknown outcomes remain unknown; exceptions must not be converted into business truth.
- Metrics count successful effects, not attempts. A zero-error signal is meaningful only beside a heartbeat proving that work occurred.

### Semantic navigation

Use Roslyn navigation for semantic C# and Razor questions. Confirm important findings in source and with build, tests, and caller evidence.

If semantic tooling is unavailable, record the limitation. Text search is discovery evidence, not a complete reference set.

## Real-caller proof

Trace the caller before changing a callee or widening a capability.

1. Name the actual entry point.
2. Identify how the input reaches that entry point in the target topology.
3. Locate the current implementation reached by that path.
4. Use genuine or genuine-shaped input through the entry point.
5. Observe the named application boundary and external effect.
6. Confirm the operator-visible result where the workflow has one.
7. Record what was traversed and what remains unproved.

Use real-shaped local data early. Genuine samples establish operational shape; controlled synthetic fixtures then cover edge cases. A fixture whose envelope is decorative or bypassed by the code is not caller proof.

For the first QDOS vertical slice, genuine local instruction material must pass through the same entry point the deployed Worker will use, create or propose the correct case identity, persist through the actual application boundary, and produce the operator-visible result. Unit tests alone are insufficient.

## Validation ladder

Run the smallest applicable rung first and widen only after it passes:

1. Compile or parse the changed artifact.
2. Run focused unit or script tests for the changed rule.
3. Run the relevant integration boundary.
4. For intake or extraction behavior, run a genuine local corpus sample through the actual caller.
5. Restore and build, then run the focused and full applicable .NET test projects directly.
6. For material domain work, have a separate reviewer compare the literal result with the authoritative rule.

Invoke restore, build, and test through the executable and solution or project that own the changed code. Verify exact commands against that owner rather than copying an unrelated command from prose.

For every check, record both what it proves and what it does not:

- Bicep compilation proves syntax and type compatibility, not quota, permissions, availability, cost, or successful deployment.
- A local Web integration test proves application composition, not Azure configuration.
- A corpus evaluation proves behavior for its sampled inputs, not every provider, forwarding pattern, document, or state.
- A green test written from the same mistaken interpretation as the implementation proves only self-consistency.
- Deployment proves artifact placement, not traffic, workflow correctness, or acceptance.

### Verified-live state matrices

A live verification must enumerate the states it covered. Any unlisted state remains unverified by definition. Include pending, partial, retrying, attachment-decision, terminal, transient, and unknown states when they are reachable.

### Documentation validation

For every changed document:

1. Read it from its canonical owner.
2. Follow each added or changed local link.
3. Verify procedures and commands against the executable or service that owns them.
4. Check that intent, implementation, deployment, and acceptance are not conflated.

Documentation-only checks do not substitute for compilation, a genuine caller, deployment, or operator acceptance. Do not add a repository-specific validator merely to make prose appear authoritative.

## Review proportionality

| Change | Minimum review |
| --- | --- |
| Local, reversible implementation detail | Focused checks and confirmation of the real caller |
| Documentation only | Canonical-owner read, local-link check, and command verification |
| Material business rule or mapping table | Independent literal comparison with the authoritative rule; read enum and mapping values directly |
| Actual pull request | Read-only review of the exact base and head |
| Broad cleanup, repository reset, migration, or architecture change | Adversarial review for omissions, reversions, deleted paths, and stale registrations |
| Destructive, irreversible, or externally mutating operation | Independent target review, read-only rehearsal, recovery proof, and explicit authority |

The larger the cleanup, the stronger the review: broad consistency checks can remain green while silently reverting or dropping behavior.

## Workspace and corpus boundary

Imported sources and workspaces are governed by [workspaces](../workspaces/README.md).

- They are evidence of shape, behavior, and failure modes, not Pegasus specification authorities.
- Do not copy predecessor implementations into the active product.
- A repository task does not automatically authorize editing an imported workspace.
- Treat genuine corpus material as evidence. Do not rewrite it to make a test pass or generalize from one sample to every provider.
- Keep genuine samples local unless an authoritative permission explicitly allows copying or export.
- Any uncertainty about reference handling, permissions, or retention must be resolved through the canonical owner rather than improvised.

Synthetic fixtures follow genuine samples: use them to isolate contradictions and edge conditions after the real operational envelope is understood.

## External and destructive operations

External and cloud reads and writes, deployment, credential rotation, account changes, and destructive operations require the explicit authority named in the repository’s root instructions. Apply that authority only to the named targets and exact operation.

Before any operation containing wipe, drop, purge, rebuild, migrate, replay, bulk update, or another broad or irreversible effect:

1. Enumerate the exact targets.
2. Perform a read-only dry run.
3. Verify the baseline under the correct identity and database role.
4. Prove that the proposed recovery source exists and is complete enough.
5. Record expected additions, changes, and deletions.
6. Obtain the required approval.
7. Stop if observations differ from the plan.

A zero-row baseline is not proof that a database is empty. In predecessor evidence, row-level security returned zero unless the intended administrative role was set; `SET ROLE csadmin` distinguished the real baseline from a database that merely looked wiped. Treat this as historical failure evidence, not standing permission to run the command. Follow [operations](operations.md), [Azure guidance](azure/README.md), and current authority for the actual environment.

Delete superseded destructive drivers so a later session cannot mistake “permanently off” for “available.”

## Controls and incident criteria

A permanent guard, CI gate, or governance script is admitted only when all of the following exist:

- a named owner;
- a named, observed incident or demonstrated failure;
- the concrete failure mode the control detects;
- a negative fixture or adversarial case;
- evidence that the control fails when the defect is present;
- an expiry or re-evaluation condition.

A hypothetical risk, a tool suggestion, or generalized hygiene preference is not an incident. The incident must identify an observed caller, operation, artifact, or state and the incorrect outcome or credible harm.

The author must watch every new guard fail at least once. A guard that has never fired or whose trigger no longer exists is removed at its review date.

### Process budget

- Commit no generated status or reconciliation ledgers.
- Prefer one focused local check and a few focused tools.
- Treat approximately ten governance scripts as an upper bound, not a target.
- Every CI gate must name the incident that justifies it.
- Agents do not create gates, ledgers, workflow machinery, or process layers without a human-named prior incident.
- If process-oriented commits exceed roughly 30% of total commits for two consecutive weeks, stop adding controls and delete or consolidate them.
- Do not make repository consistency a product of its own.

### Standing reviews

When the required operational authority is available:

- Weekly, inspect real operator-visible output. Human review of real output is a required defect-detection path, not an optional presentation check.
- Weekly, use `git log` or equivalent direct history evidence to compare process work with product work; do not build a generated dashboard for this.
- Weekly, review built-but-unwired surfaces and apply the two-week caller-or-delete rule.
- Monthly, reconcile the authorized cloud estate with the repository and [operations](operations.md).
- After the first month of a major generation, re-derive process controls and remove rules whose triggers never occurred.

## Failure-prevention rules

| Trigger | Required response |
| --- | --- |
| Starting a major generation or initiative | Write alpha, production, and the first real-traffic slice before scaffolding. |
| Proposing a new entity, prefix, category, taxonomy, or provider model | Resolve the domain contract in [requirements](requirements.md) or an accepted decision. Treat a later change as stop-the-line work, not an incidental refactor. |
| Writing an outbound-call catch path | Classify terminal, transient, and unknown outcomes from the first client. |
| Declaring a component complete | Name and prove its real caller with real-shaped input. |
| Creating a helper, client, wrapper, or rule implementation | Search semantically first and locate the existing owner or prove its absence. |
| Encountering the third copy | Stop, consolidate, and remove the replaced path in the same slice. |
| Adding a classifier or router branch | Re-derive and test the complete precedence order. |
| Writing “verified” | Enumerate the tested state matrix and leave unlisted states explicitly unverified. |
| Reviewing the author’s green tests | Use an independent interpretation of the authoritative rule. |
| Adding a guard | Name the incident and owner, add a negative case, and watch it fail. |
| Proposing a generated artifact or governance mechanism | Decline it unless a named incident and process budget justify it. |
| Preparing a destructive operation | Rehearse read-only, verify the correct-role baseline, and prove recovery. |
| Writing a metric | Count successful effects and pair zero-error claims with a heartbeat. |
| Disabling dangerous or superseded code | Delete it rather than retaining a dark affordance. |
| Finding an unwired capability | Give it a genuine caller within two weeks or delete it. |

## Historical evidence behind these rules

The predecessor is failure evidence, not a current-state report or source tree to migrate. The retrospective was compiled on 2026-07-22 by an automated agent from repository records including 950 commits, 298 tickets, 35 ADRs, 16 plans, and six review sessions. Its claims were tied to tickets, pull requests, commits, or paths. Because the retrospective was written by the same class of tool involved in several failures, it also supports independent review rather than self-grading.

No current implementation, deployment, or acceptance state may be inferred from this section.

### Evidence snapshot

- The recorded effort ran for nine weeks, with approximately six active weeks. Machine cadence reached 146, 57, 138, 246, 237, and 119 commits in successive weeks after a slow start.
- The repository grew into two TypeScript services with 253 Azure Functions, about seven Python function apps, a React SPA, 66 PostgreSQL tables, and Box, EVA, Graph, and DVLA integrations.
- Process and documentation files outnumbered product files by about 1.7 to 1, approximately 2,039 versus 1,173.
- One ticket tree contained roughly 1,402 files. Other snapshots found about 55 check scripts, 58 package scripts—about 40 governance-oriented—and 126 agent files.
- The largest generated ledger was 128,427 lines and was regenerated in 118 commits; a roughly 48,000-line sibling was regenerated in 129.
- About 52.5% of commits touched documentation and 22.5% touched product code. Approximately 30 consecutive pull requests focused on internal governance during the two highest-velocity weeks.
- At the retrospective snapshot, production readiness was recorded as 3 of 63 items. The system had not processed production traffic.
- A one-provider alpha began on 2026-07-21 at about 14:00Z and failed on its first real email about four hours later. The alpha paused, and the intake engine entered a draft rebuild.
- Historical infrastructure cost was about £51 per month on development tiers. This was evidence of cost discipline, not current cost or deployment state.
- The domain had several predecessor generations, absorbed sibling repositories with history, underwent a repository reset, and then began another core-engine rebuild. Repeated restarts were a material cost.

### Demonstrated failures and required responses

| Historical evidence | Engineering consequence |
| --- | --- |
| The first real alpha instruction, a staff-forwarded QDOS email with subject `(EREF9) RTA on 19/07/2026`, was classified as `query`; no case was minted except through a fallback reconstruction. | Exercise genuine forwarded traffic through the actual caller before completion. |
| A classifier used 20 first-match rules in accumulated order. Sender identity could outrank stronger evidence, and a filename such as `Bodyshopreport-V1.pdf` could veto content typing. | Keep explicit, re-derived evidence precedence with contradiction tests. |
| The rebuilt engine was registered with no caller. Its first branch keyed on provider sender address even though all alpha instructions were staff forwards. Fixture `From:` lines were decorative because the engine never read them. | Registration and idealized fixtures are not caller proof. |
| The code contained nine managed-identity token-mint paths, four HTTP wrappers, three Box-folder creation implementations, multiple route and outbox lanes, and five overlapping Box-root settings. Some copies explicitly said they mirrored another file. | Search before writing, stop at the third copy, and share through project references. |
| A central intake orchestrator reached 804 lines with about 73 casts, and one evidence pipeline appeared three or four times with policy divergence. | Business decisions belong in Core; hot files require delete-or-generalize pressure rather than more branches. |
| A superseded triage generation remained registered “out of caution.” Other dark surfaces included a vision family, an EVA polling stub, a fail-closed no-op ingest path whose permission was never created, a zombie function app, an unrecorded registration, and an untracked live SPA. | Deferred work stays unbuilt. Built-but-unwired code gets a caller or is deleted; dangerous dark affordances are deleted immediately. |
| An implementer swapped two prefix-mapping values against the explicit rule and wrote tests asserting the same swapped values. The suite was green while the business rule was wrong. | The implementer does not grade their own rule interpretation; literal mapping values receive independent review. |
| A parity guard encoded a real defect as an allowed divergence. Another guard claimed MIME tables mirrored exactly when they did not. Three idempotency hash serializers were not byte-compatible, so reordered keys bypassed the intended guard. | Every guard needs an adversarial negative case and proof that it fails. |
| A repository reset was 57 commits behind its base and silently reverted five tables while consistency checks stayed green. The reconciliation gate was later found tautological. | Broad cleanups require exact-base/head, adversarial review; repository agreement is not product correctness. |
| A feature was marked verified-live with millisecond-level evidence and reopened four days later when an attachment-decision state broke. Several tickets followed the same reopen pattern. | Live verification requires an explicit state matrix, and regressions reopen live work state. |
| Roughly 30 consecutive governance pull requests landed while the intake engine remained untrusted. Generators had to run repeatedly to reach a fixed point, and a meta-guard guarded the guard register. | Enforce the process budget; controls must cite incidents and expire. |
| A planned wipe-and-replay assumed mailboxes retained all source mail. A read-only dry run found 117 messages for 390 processed emails, while Deleted Items held 7,081, 9,485, and 7,107 messages. Stored `.eml` coverage was only 212 of 390 and used the wrong message identifier. The operation would have destroyed about 150 cases. | Dry-run destructive work, verify recovery-source completeness, and stop when observations invalidate the plan. |
| The same investigation initially suggested 62% category corruption because the diagnostic starved the classifier of attachment signals. A proper evaluation showed approximately 94% recall for `receiving_work`, preventing a harmful reprocess. | Prefer representative evaluation over panic-grade diagnostics and record fixture limitations. |
| Row-level security made baseline queries return zero without the correct role, which looked exactly like a wiped database. | Verify baselines under the intended identity and role before any destructive decision. |
| The superseded replay driver was then deleted so a later session could not mistake it for a live option. | Delete dangerous dark affordances rather than relying on an off switch. |
| One invalid Box folder reference produced 1,896 exceptions in a day. Plain errors erased terminal/transient meaning, stacked Durable retries amplified each wake to about 12 executions, and defer backoff never reached zero. Another monitor emitted four-figure daily errors for at least six days; 2,528 of 3,630 exceptions came from one case. | Classify failures at the client boundary, stop terminal retries, and park poison work visibly. |
| A purge exhausted a development-tier connection pool, purged nothing, and still reported `purged: results.length`, counting attempts as successes. Other paths used dishonest labels or silent catches. | Metrics count successful effects; failures remain visible; zero-error dashboards need activity heartbeats. |
| A 17-ticket misclassification wave and many UX defects were found through dated operator screenshots rather than approximately 20 CI gates. | Maintain a weekly authorized review of real operator-visible output. |
| Seventeen of 25 ADRs required substantive correction in one review day. A retention feature was built, withdrawn, and scheduled for deletion; repository boundaries were split and reabsorbed; four “LIVE” feature-wave pull requests closed unmerged. | Reconcile decisions with implementation, settle domain contracts early, and count unlanded or unwanted work as carrying cost. |

### Evidence and tooling limitations

- Windows authentication broker failures, `cmd.exe` handling of `&`, App Insights CLI rejection of some multiline KQL or `order by` forms, and `-o tsv` row mangling were observed. Record a confirmed tooling quirk once in [Azure guidance](azure/README.md) rather than repeatedly rediscovering it.
- Development-tier App Insights evidence expired quickly in the predecessor. Capture authorized incident evidence on the day it occurs or state that it is no longer available.
- A repository artifact, generated report, registration, mock, or green structural check can agree with itself while the operator workflow is wrong.
- Human review of genuine output, read-only destructive rehearsals, representative evaluation, candid regression reopening, and deletion of dangerous superseded code were the highest-value controls in the predecessor. Keep those practices while avoiding its governance machinery.