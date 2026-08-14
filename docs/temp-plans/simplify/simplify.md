I would run a dedicated **Pegasus Simplification Programme**: no rewrite, no stack change, and no new features while the worst complexity is removed.

The goal is not “fewer files at any cost.” It is:

> Make the shortest path from a business requirement to working production code obvious.

## Proposed target architecture

Keep the four existing runtime projects:

| Project                  | Keep? | Purpose                                                |
| ------------------------ | ----: | ------------------------------------------------------ |
| `Pegasus.Core`           |   Yes | Business rules and use cases                           |
| `Pegasus.Infrastructure` |   Yes | SQL, Graph, Box, Blob, ONNX and other external systems |
| `Pegasus.Web`            |   Yes | Razor Pages, HTTP, authentication and MCP entry points |
| `Pegasus.Worker`         |   Yes | Timers and queue-triggered background jobs             |

That is already a sensible modular monolith. I would not merge everything into one project, introduce microservices, replace Razor Pages, or change language.

The simplification should happen **inside this structure**.

---

# Phase 0: Stop the bleeding

Before refactoring, change the instructions controlling coding agents.

Add a short section to `AGENTS.md`:

```md
## Simplicity rules

Implement the smallest complete change satisfying the current requirement.

Do not introduce:

- a new project or deployment unit;
- an interface with only one non-test implementation;
- a generic framework for one workflow or provider;
- a feature flag for code that is not immediately deployed;
- compatibility code for an unreleased implementation;
- an ADR for a reversible implementation detail;
- a new persisted state unless it changes business behaviour;
- a second representation of existing documentation;
- speculative extension points.

Prefer:

- concrete classes;
- direct method calls;
- capability-local code;
- ordinary C# records and methods;
- deleting replaced code immediately;
- tests of observable behaviour.

Every new abstraction must identify the current problem it solves.
```

Also impose a PR rule:

> A simplification PR may not add more production code than it removes unless required to preserve behaviour.

This prevents GPT-5.6-sol from creating a `SimplificationOrchestrator`, `ISimplificationPolicy`, three adapters and ADR-0022.

## Deliverable

One small PR containing:

* Simplicity rules.
* Definition of the target four-project architecture.
* Explicit prohibition on stack migration and new application boundaries.
* A temporary feature freeze on architecture changes.

No production code changes yet.

---

# Phase 1: Create a factual complexity inventory

Do not immediately delete every interface. First establish what actually exists.

Generate a report with these inventories:

### Code inventory

For every interface:

* Interface name.
* Owning capability.
* Production implementations.
* Test-only implementations.
* Callers.
* External boundary or internal abstraction.
* Recommended result: keep, inline, merge or investigate.

For every feature flag:

* Flag name.
* Production status.
* Whether both enabled and disabled modes are genuinely required.
* Removal date or reason for permanence.

For every persisted state/code:

* Where written.
* Where read.
* Whether it changes business or operator behaviour.
* Whether it duplicates another state.

For every test:

* Behaviour test.
* Integration test.
* Structural/architecture test.
* Duplicate test.
* Obsolete test.
* Test coupled to implementation rather than behaviour.

For documentation:

* Canonical/current.
* Operational/runbook.
* Historical decision.
* Temporary plan.
* Duplicate.
* Stale.
* Unreferenced.

### Establish baseline measurements

Record:

* Production C# lines.
* Test C# lines.
* Number of projects.
* Number of interfaces.
* Interfaces with one production implementation.
* Number of feature flags.
* Number of ADRs.
* Number of temporary plans.
* Number of architecture tests.
* Full build duration.
* Test duration.
* Number of documentation files.
* Number of different status vocabularies.

These measurements are not targets by themselves. They show whether simplification is real or merely moves complexity around.

## Important rule

This phase produces one inventory document—not a new permanent documentation framework.

Delete that document when the cleanup programme finishes or reduce it to a short retrospective.

---

# Phase 2: Cut the documentation bureaucracy first

This is the lowest-risk and probably highest-immediate-value phase.

The current documentation appears to distribute truth across:

* `README.md`
* `NOW.md`
* `docs/index.md`
* `docs/requirements.md`
* `docs/capabilities.md`
* `docs/architecture.md`
* `docs/design.md`
* `docs/engineering.md`
* `docs/operations.md`
* `docs/open-decisions.md`
* `docs/runbook.md`
* `docs/adr/*`
* `docs/temp-plans/*`
* Workspace documentation

That is too many potential places to answer “what does Pegasus currently do?”

## Replace it with five authoritative documents

| Document               | Owns                                                        |
| ---------------------- | ----------------------------------------------------------- |
| `README.md`            | What Pegasus is and how to start                            |
| `docs/product.md`      | Current business capabilities and genuine future priorities |
| `docs/architecture.md` | Current system shape and important boundaries               |
| `docs/runbook.md`      | Build, test, deploy, migrate and recover                    |
| `docs/operations.md`   | Current deployed configuration and known operational issues |

Keep ADRs only for decisions that are:

* Expensive to reverse.
* Security-critical.
* Data-contract-critical.
* Deployment-boundary changes.
* Otherwise likely to be repeatedly relitigated.

Examples worth retaining:

* Modular monolith versus separate services.
* Database/provider choice.
* Custody model.
* Authentication authority.
* AI direct-write safety boundary.

Examples that generally do not need ADRs:

* A particular helper/library.
* Folder arrangements.
* Routine package upgrades.
* Whether one internal operation uses an interface.
* A local rendering integration detail.

## Specific cleanup

* Delete completed `docs/temp-plans/*` after preserving any current facts in the relevant canonical document.
* Move genuinely valuable historical decisions into ADRs, but do not convert every temporary plan.
* Merge `design.md` into either product or architecture.
* Replace the enormous capability matrix with:

  * Current
  * Next
  * Later
  * Explicitly excluded
* Remove release numbers from distant speculative features.
* Stop repeating “implemented does not prove deployed or accepted” everywhere.

Use three ordinary status values:

| Status   | Meaning                                  |
| -------- | ---------------------------------------- |
| Built    | Code exists and automated tests pass     |
| Live     | Deployed and enabled                     |
| Accepted | Staff have confirmed it meets their need |

“Caller-proved” should normally disappear. A feature without a caller is not complete enough to mark Built.

## Expected result

I would aim to reduce active documentation by **50–70%**, without losing:

* Business requirements.
* Security boundaries.
* Deployment instructions.
* Operational facts.
* Important decision history.

---

# Phase 3: Remove speculative and dormant features

This is where Pegasus can lose substantial complexity safely.

For each disabled or incomplete capability, choose exactly one:

1. **Finish and activate it now.**
2. **Delete it and retain a backlog item.**
3. **Keep it only where a real operational need requires both modes.**

Do not preserve half-built production frameworks merely because they might become useful.

Probable review candidates include:

* Disabled Automation MCP composition.
* Send-to-AI preview transport.
* Superseded Box File Request surface.
* Dormant provider/API boundaries.
* Imported workspaces with no current Pegasus caller.
* Old evaluator paths.
* Compatibility behaviour for unreleased states.
* Planned report-renderer integration seams.

The repository explicitly says the Box File Request UI is superseded and pending removal. That is an obvious first deletion candidate.

## Rule

A backlog entry is cheaper than dormant code.

Future capability descriptions belong in an issue or short roadmap—not in:

* Database schema.
* Dependency injection registration.
* Feature flags.
* Empty interfaces.
* Placeholder endpoints.
* Compatibility branches.
* Architecture tests.

---

# Phase 4: Collapse unnecessary abstraction layers

Perform this capability by capability, not repository-wide.

Start with a bounded vertical slice such as:

1. Case workflow.
2. Intake.
3. Image intake.
4. Email operations.
5. External work.
6. Assessment/MCP.

For each slice, draw the actual call path:

```text
Razor Page / Function
    → use case
    → persistence or external adapter
```

Anything between those steps must justify itself.

## Interface deletion test

Keep an interface when at least one is true:

* It represents an external system: Graph, Box, SQL storage, Blob, clock, queue, DVLA.
* There are two genuine production implementations.
* The same use case is called through meaningfully different runtime adapters.
* Replacing it in tests prevents slow or unsafe external effects.
* It marks an important dependency direction between Core and Infrastructure.

Otherwise, prefer a concrete class.

### Keep examples

Interfaces such as these are probably justified:

* `IClock`
* `IBoxClient` or custody boundary
* Graph mailbox client boundary
* Artifact/blob store
* Queue publisher
* External vehicle-data client
* Database-backed repository where Core must remain persistence-independent

### Likely collapse candidates

* Interface and implementation living together with one caller.
* Query interfaces created solely for dependency injection.
* “Policy” objects wrapping one conditional.
* Factories constructing one concrete type.
* Services forwarding directly to another service.
* Request/command/handler combinations where the handler contains only a few lines.
* Duplicate DTOs that carry the same fields through internal layers.
* Separate read/write models without genuinely different requirements.

## Hard rule

Never remove an interface and replace it with a service locator, static global, reflection or direct EF access from everywhere. That makes the code shorter but worse.

The desirable endpoint is still:

* Core owns business decisions.
* Infrastructure owns external details.
* Web/Worker translate input and output.

Just with fewer ceremonial objects between them.

---

# Phase 5: Simplify the state model

Pegasus appears especially vulnerable to modelling every procedural distinction as durable state.

For each status, decision code and versioned envelope, ask:

1. Does this change what the system does?
2. Does an operator need to see or act on it?
3. Is it needed for recovery or audit?
4. Can it be reliably derived from existing facts?
5. Does another field already represent substantially the same thing?

If the first three answers are no, it probably should not be persisted.

## Prefer facts over mirrored summaries

Persist facts such as:

* Intake received.
* Case created.
* External operation attempted.
* External operation succeeded or failed.
* Staff member approved a result.
* Report sent.

Derive display concepts from those facts where practical.

Avoid simultaneously persisting:

* A decision code saying a case was created.
* A processing state saying allocation succeeded.
* A receipt status saying completed.
* A separate case link proving the case exists.
* Another projection repeating the same authority.

If the case link is authoritative, other values should generally be projections or diagnostic details—not competing truths.

## Safety

Database simplification should happen last within each capability:

1. Simplify callers.
2. Confirm unused fields.
3. Add migration.
4. Verify production data.
5. Remove compatibility reads after deployment.

Do not combine widespread schema deletion into one migration.

---

# Phase 6: Reduce test ceremony while preserving confidence

Do not target a lower test count. Target fewer tests that assert internal shape.

## Keep strongly

* Business-rule unit tests.
* Contradiction and edge-case tests.
* Database integration tests.
* Authentication/authorisation tests.
* External adapter contract tests.
* Browser tests for critical staff workflows.
* Idempotency and replay tests.
* Document resource-limit and hostile-input tests.

## Review aggressively

* Tests checking exact class names.
* Tests asserting registrations rather than behaviour.
* Tests proving a method calls another method.
* Repeated tests of the same rule at four layers.
* Reflection-heavy architecture tests.
* Tests created solely because an abstraction exists.
* Tests locking down dormant feature gates.

The dependency-direction test protecting `Core → nothing`, `Infrastructure → Core`, and `Web/Worker → both` is valuable.

Architecture tests ensuring every minor composition decision remains exactly as written are less valuable. Those turn today’s implementation into permanent law.

## Testing target

For every important business behaviour, prefer:

* One focused Core test.
* One integration test at the real persistence/external boundary.
* One browser test only where the human workflow matters.

Not four nearly identical tests at every layer.

---

# Phase 7: Deal with `workspaces/`

The repository currently contains independently maintained imports for:

* Report renderer.
* Document extraction.
* AI centre.
* Possibly other tools.

These increase search noise, agent context, documentation volume and the risk that agents mistake experimental work for Pegasus production code.

For each workspace, choose:

| Outcome             | Use when                                            |
| ------------------- | --------------------------------------------------- |
| Integrate           | Pegasus presently needs it and will call it         |
| Separate repository | It is genuinely an independent product/tool         |
| Delete              | It is superseded or reproducible                    |
| Keep temporarily    | It has a dated integration decision and named owner |

“Stored here because it might be useful” should not be an accepted permanent state.

My expectation:

* Report rendering likely becomes a bounded Pegasus capability or separately deployed renderer—not a permanent imported pseudo-project.
* Experimental AI/data tooling is probably better in a separate repository.
* Document-extraction code should either become the accepted extraction implementation or leave the main repository.

This alone will make agents dramatically less confused about the current system.

---

# Phase 8: Simplify one real workflow end-to-end

Before broad cleanup, use one vertical slice to prove the method.

I would choose **intake → classification → case allocation** because it is central, complicated and well tested.

For that slice:

1. Document the current call graph.
2. Identify duplicate models and states.
3. Remove unused/dormant paths.
4. Inline unjustified single-use abstractions.
5. Consolidate business rules in Core.
6. Simplify EF persistence without changing results.
7. Remove redundant tests.
8. Rewrite the corresponding documentation in plain English.
9. Compare build/test/runtime behaviour against baseline.
10. Have an operator run the genuine workflow.

Only after that succeeds should the same method spread to other capabilities.

---

# Execution as a PR sequence

I would not hand an agent “simplify Pegasus.” That would be catastrophic. Use narrow PRs:

### PR 1 — Simplification contract

* Add simplicity rules.
* Define protected architecture boundaries.
* Record baseline measurements.
* No runtime changes.

### PR 2 — Documentation consolidation

* Reduce canonical documents.
* Delete stale temporary plans.
* Simplify status vocabulary.
* No runtime changes.

### PR 3 — Dead feature removal

* Remove the superseded Box File Request path.
* Remove confirmed unreachable code.
* Remove associated registrations, flags, tests and documentation.

### PR 4 — Intake vertical-slice simplification

* Collapse unnecessary interfaces/models.
* Preserve observable behaviour.
* No schema change unless independently justified.

### PR 5 — Intake state/schema cleanup

* Remove proven duplicate persisted state.
* Apply narrow migrations.
* Verify against realistic data.

### PR 6 — Test-suite cleanup

* Remove structural duplication exposed by PRs 3–5.
* Retain behavioural, security and integration coverage.

### PR 7 — Workspace disposition

* Integrate, extract or delete each imported workspace.
* Make the main solution’s authority unmistakable.

### PR 8 onwards — Repeat per capability

* Cases/workflow.
* Email and retained mail.
* Image intake.
* Assessment and AI/MCP.
* External custody/work.
* Reporting.

Each PR should be independently reversible and deployable.

---

# Definition of done

The programme finishes when:

* A developer can identify the implementation of a business rule quickly.
* Every active capability has an obvious real caller.
* No dormant implementation exists for speculative work.
* Every interface has a stated present-day justification.
* Every persisted state affects behaviour, audit, recovery or operator decisions.
* The canonical documentation set is small enough to read.
* `workspaces/` no longer blurs product boundaries.
* Important workflow behaviour and security remain covered.
* Production behaviour is unchanged except for separately approved fixes.
* The build and test process is simpler or at least no slower.
* Agents stop generating new architectural vocabulary for ordinary code.

## Realistic reduction targets

These are directional, not arbitrary quotas:

* Active documentation: **down 50–70%**
* Temporary plans: **down 80–100%**
* One-implementation internal interfaces: **down 40–70%**
* Feature flags for dormant code: **down 80–100%**
* Structural/registration tests: **down 30–60%**
* Runtime projects: remain at **four**
* Deployment units: do not increase
* Business behaviour coverage: remain equal or improve
* Production capabilities: no accidental loss

The key is sequencing: **remove documentation duplication and dead paths first, prove the approach on intake, then simplify the rest capability-by-capability.** Do not begin with a giant namespace reorganisation, mass interface deletion or database rewrite.



Short answer: **the .NET conclusion still stands, but my cleanup plan was too aggressive and partly wrong.** It risked becoming yet another enormous “simplification framework.”

After examining the repository more deeply, Pegasus is genuinely overengineered—but the main problem is **scope and governance duplication**, not the four-project architecture.

## What I would retract

Several parts of my previous plan should not be followed as written:

* **Do not add another “simplicity contract.”** Almost all those rules already exist in [AGENTS.md](https://github.com/collisionengineers/pegasus/blob/main/AGENTS.md) and [engineering.md](https://github.com/collisionengineers/pegasus/blob/main/docs/engineering.md). The problem is that the repository does not consistently obey them.
* **Do not collapse everything into five giant documents.** Requirements, operator evidence, operations and the runbook genuinely answer different questions. The problem is repetition and size, not merely file count.
* **Do not target a percentage of interfaces for deletion.** That was arbitrary.
* **Do not target a percentage of architecture tests for deletion.** Many protect real production/deployment boundaries.
* **Do not conduct a repository-wide database-state purge.** That would be dangerously broad.
* **Do not move the workspaces out merely because they create search noise.** They are deliberately excluded from `Pegasus.slnx` and governed by [ADR-0009](https://github.com/collisionengineers/pegasus/blob/main/docs/adr/0009-adopt-pegasus-monorepo-workspaces.md).
* **Do not start by refactoring everything.** Production has more important functional failures.

## What the evidence actually shows

| Signal                          |                           Current finding | Interpretation                                              |
| ------------------------------- | ----------------------------------------: | ----------------------------------------------------------- |
| Production projects             |                                         4 | Correct architecture                                        |
| Handwritten production C#       |                             ~72,300 lines | Substantial but not absurd                                  |
| Core interfaces                 |                                       209 | Excessive abstraction is plausible                          |
| Same-name interface/class pairs |                                        92 | Considerable use-case ceremony                              |
| `DetailsModel`                  | 2,010 lines, 39 dependencies, 36 handlers | Clear god object                                            |
| EF `DbSet`s                     |                                        68 | Very large application scope                                |
| Migrations                      |                                        42 | Fast schema expansion                                       |
| Capability entries              |                                       231 | Excessive roadmap granularity                               |
| Capabilities marked `Now`       |                                       131 | The current release is drastically overscoped               |
| Canonical documentation         |                             ~70,500 words | Too much for routine navigation                             |
| `NOW.md` queue                  |                          44 large entries | Defect queue mixed with status and planning                 |
| Temporary task plans            |                         19 files, ~606 KB | Mostly apparently orphaned under the repository’s own rules |
| New `dev` Kanmer tickets        |                                       219 | A third overlapping tracking representation                 |

The most important number is **131 capabilities marked Now** in [capabilities.md](https://github.com/collisionengineers/pegasus/blob/main/docs/capabilities.md).

That is why the schema, interfaces, tests and documentation exploded. The implementation is trying to deliver intake, classification, matching, Triage, complete case workflow, documents, Box, EVA, DVLA/DVSA, mail polling, sent evidence, AI, MCP, administration, recovery, accessibility, telemetry and production assurance simultaneously.

No cleanup technique can make that a small system.

## Some “ceremony” is justified

I was too dismissive of the distinction between:

* Implemented.
* Actually reachable through a caller.
* Deployed.
* Live-verified.
* Accepted by staff.

The distinction is useful here. [NOW.md](https://github.com/collisionengineers/pegasus/blob/main/NOW.md) records precisely the sort of failure it prevents: the Worker is deployed and enabled, but no current trigger execution or business result has been demonstrated. Send-to-AI and MCP are implemented but disabled everywhere. Production smoke tests pass even though no Principal exists, meaning QDOS intake cannot create a case.

**Keep the distinction. Stop repeating it in every capability row and document paragraph.**

Similarly, keep:

* The four-project modular monolith.
* Core/Infrastructure dependency direction.
* External-system interfaces.
* Worker composition and production-profile tests.
* Idempotency for externally retried operations.
* Edit leases and concurrency protection.
* Audit history for material case changes.
* Fail-closed intake and principal/reference rules.

Those address real risks.

## The clearest example of process becoming product code

[CoreAssembly.cs](https://github.com/collisionengineers/pegasus/blob/main/src/Pegasus.Core/CoreAssembly.cs) contains a `QdosAlphaAcceptanceGate` that hard-codes roughly 130 capability IDs and release-evidence gates.

The complete mechanism consists of approximately:

* 316 production Core lines.
* 167 integration-test lines.
* 574 PowerShell lines.

That is over 1,000 lines implementing release-governance machinery. I found no actual application caller; Web registers the class and integration tests resolve it.

That mechanism should not be part of `Pegasus.Core` or normal Web composition. Release evidence can be checked by a release script or CI without becoming production-domain code.

# Corrected plan

## 1. Freeze scope before touching architecture

Define one alpha acceptance journey:

```text
Real QDOS email or manual upload
→ source retained
→ document parsed
→ principal and case decision made
→ case created or visible recovery outcome
→ documents reach Box
→ case is editable
→ EVA handoff generated
→ staff can see what happened
```

Only capabilities required for that journey—or required to operate it safely—remain `Now`.

Default candidates to move out of the alpha:

* Send to AI.
* Automation MCP expansion.
* Report-renderer integration.
* AI Centre integration.
* Provider API.
* Broader email categorisation.
* Automatic sent-evidence matching.
* Additional external integrations.
* Any speculative post-report workflow not currently required.

I would **pause**, rather than immediately delete, AI/MCP because it is strategically relevant to Pegasus. After the core journey is accepted, either resume it with a real activation plan or remove it.

## 2. Establish one work tracker

The current situation risks having:

1. 231 capability rows.
2. 44 large `NOW.md` queue entries.
3. 219 Kanmer tickets on `dev`.
4. Temporary task plans.
5. ADRs and open decisions.

That is not more control; it is duplicated state.

If Kanmer is the intended tracker:

* Kanmer owns actionable work.
* `capabilities.md` remains a concise product-outcome catalogue, not another ticket system.
* `NOW.md` shrinks to active claims and genuinely current production warnings—or disappears once claims are handled elsewhere.
* Archive the Kanmer tickets that merely restate future capabilities with boilerplate.
* Keep only actionable defects, decisions and deliverable slices.
* Delete orphaned temporary plans after checking that no task branch still owns them.
* Small, local bug fixes should not require a separate temporary planning document.

Do not create 219 tickets saying “plan this capability later.” The capability catalogue already says that.

## 3. Fix the actual production blockers

Before codebase beautification:

1. Remove the temporary verification account and treat its committed credential as compromised.
2. Create the production QDOS Principal through an authorised route.
3. Add a supported way to retry allocation after the Principal exists.
4. Remove the broad exception swallowing in `AllocateCaseIfDefinitiveAsync`.
5. Persist and visibly surface allocation failures.
6. Implement real `Blocked intake` and poison/quarantine recovery—or remove the unsupported claims that these exist.
7. Fix or explicitly descope Standalone Audit case creation and its legacy `draft_ready` behaviour.
8. Update the runtime-role permission check to derive its expected state from all migrations.
9. Exercise one real Worker-triggered intake through to an operator-visible result.
10. Obtain operator acceptance of that exact journey.

These should be small, behaviour-focused PRs. No general refactoring alongside them.

## 4. Remove confirmed dead or superseded surfaces

Once the golden path is stable:

* Remove the superseded Box File Request UI, registrations and contracts.
* Move `QdosAlphaAcceptanceGate` out of Core and Web; keep any genuinely useful release validation in tooling.
* Delete expired temporary plans.
* Remove unused `IQdosAlphaAcceptanceGate`.
* Remove compatibility branches only after checking production data.
* Keep dormant AI/MCP code frozen until the explicit resume-or-delete decision.

## 5. Simplify code capability by capability

Start with intake because that is where real state contradictions and silent failures already exist.

For that slice:

* Define the authoritative facts:

  * Was a receipt created?
  * Was processing completed?
  * Does an actual case link exist?
  * Is human intervention required?
  * Can it be retried?
* Make the case link authoritative for case existence.
* Stop using decision codes as competing proof that a case exists.
* Normalize the remaining `draft_ready` compatibility path.
* Consolidate `Blocked`, poison, quarantine and retry behaviour.
* Delete duplicated state only after production-data inspection.

Then apply a modest abstraction rule:

* External boundary: keep an interface.
* Multiple genuine implementations: keep an interface.
* Core-to-Infrastructure port: keep an interface.
* Single concrete use case injected only for DI ceremony: inject the concrete class when that capability is already being changed.

Do not launch a repository-wide “remove 92 interfaces” campaign.

## 6. Deal with the god pages later

[Cases/Details.cshtml.cs](https://github.com/collisionengineers/pegasus/blob/main/src/Pegasus.Web/Pages/Cases/Details.cshtml.cs) is an undeniable problem: 2,010 lines, 39 constructor dependencies and 36 handlers.

After correctness stabilises, keep the visible single case workspace but route mutations through capability-specific Razor endpoints:

* Case workflow.
* Tasks and chasing.
* Documents and requests.
* Vehicle operations.
* EVA handoff.
* Closure/reopening.

`DetailsModel` should primarily load and display the case. Forms can post to the smaller capability endpoints and redirect back to the same visible workspace.

That reduces coupling without introducing MediatR, a command bus or another framework.

`Program.cs` and `DependencyInjection.cs` can similarly be divided into ordinary profile/capability registration methods—no new interfaces required.

## 7. Keep the valuable tests

Do not pursue a test-reduction quota.

Keep:

* Core rule and contradiction tests.
* SQL transaction/concurrency tests.
* Production-versus-offline composition tests.
* Worker activation safety.
* Authentication and authorisation.
* Document limits and hostile-input handling.
* Idempotency/replay.
* Real browser journeys.
* Genuine corpus evaluation.

Delete a test only when:

* Its corresponding feature is deleted.
* It duplicates another test at the same boundary.
* It pins an implementation shape that is intentionally being removed.
* It proves registration without testing reachable behaviour.

## Recommended PR order

1. Tracker and orphan-plan cleanup.
2. Verification-account removal and production bootstrap correction.
3. Visible intake allocation failures and retry.
4. Blocked/poison recovery.
5. Standalone Audit fix-or-descope.
6. One live golden-path verification.
7. Remove Box File Request.
8. Extract the acceptance gate from production code.
9. Intake state simplification.
10. Case-details decomposition.
11. Resume-or-delete decision for AI/MCP.

## Final verdict

The corrected assessment is:

* **C#/.NET:** still the right stack.
* **Four-project architecture:** still right.
* **Safety boundaries:** mostly justified.
* **Current release scope:** far too large.
* **Documentation and tracking:** significantly overengineered.
* **Use-case interface pattern:** overused, but not worth a mass rewrite.
* **Previous cleanup proposal:** directionally correct, operationally too broad.

The first objective should not be “reduce lines and interfaces.” It should be:

> Make one real business journey work reliably in production, and remove everything currently competing with that objective.

That is the simplification Pegasus actually needs.


Short answer: **the .NET conclusion still stands, but my cleanup plan was too aggressive and partly wrong.** It risked becoming yet another enormous “simplification framework.”

After examining the repository more deeply, Pegasus is genuinely overengineered—but the main problem is **scope and governance duplication**, not the four-project architecture.

## What I would retract

Several parts of my previous plan should not be followed as written:

* **Do not add another “simplicity contract.”** Almost all those rules already exist in [AGENTS.md](https://github.com/collisionengineers/pegasus/blob/main/AGENTS.md) and [engineering.md](https://github.com/collisionengineers/pegasus/blob/main/docs/engineering.md). The problem is that the repository does not consistently obey them.
* **Do not collapse everything into five giant documents.** Requirements, operator evidence, operations and the runbook genuinely answer different questions. The problem is repetition and size, not merely file count.
* **Do not target a percentage of interfaces for deletion.** That was arbitrary.
* **Do not target a percentage of architecture tests for deletion.** Many protect real production/deployment boundaries.
* **Do not conduct a repository-wide database-state purge.** That would be dangerously broad.
* **Do not move the workspaces out merely because they create search noise.** They are deliberately excluded from `Pegasus.slnx` and governed by [ADR-0009](https://github.com/collisionengineers/pegasus/blob/main/docs/adr/0009-adopt-pegasus-monorepo-workspaces.md).
* **Do not start by refactoring everything.** Production has more important functional failures.

## What the evidence actually shows

| Signal                          |                           Current finding | Interpretation                                              |
| ------------------------------- | ----------------------------------------: | ----------------------------------------------------------- |
| Production projects             |                                         4 | Correct architecture                                        |
| Handwritten production C#       |                             ~72,300 lines | Substantial but not absurd                                  |
| Core interfaces                 |                                       209 | Excessive abstraction is plausible                          |
| Same-name interface/class pairs |                                        92 | Considerable use-case ceremony                              |
| `DetailsModel`                  | 2,010 lines, 39 dependencies, 36 handlers | Clear god object                                            |
| EF `DbSet`s                     |                                        68 | Very large application scope                                |
| Migrations                      |                                        42 | Fast schema expansion                                       |
| Capability entries              |                                       231 | Excessive roadmap granularity                               |
| Capabilities marked `Now`       |                                       131 | The current release is drastically overscoped               |
| Canonical documentation         |                             ~70,500 words | Too much for routine navigation                             |
| `NOW.md` queue                  |                          44 large entries | Defect queue mixed with status and planning                 |
| Temporary task plans            |                         19 files, ~606 KB | Mostly apparently orphaned under the repository’s own rules |
| New `dev` Kanmer tickets        |                                       219 | A third overlapping tracking representation                 |

The most important number is **131 capabilities marked Now** in [capabilities.md](https://github.com/collisionengineers/pegasus/blob/main/docs/capabilities.md).

That is why the schema, interfaces, tests and documentation exploded. The implementation is trying to deliver intake, classification, matching, Triage, complete case workflow, documents, Box, EVA, DVLA/DVSA, mail polling, sent evidence, AI, MCP, administration, recovery, accessibility, telemetry and production assurance simultaneously.

No cleanup technique can make that a small system.

## Some “ceremony” is justified

I was too dismissive of the distinction between:

* Implemented.
* Actually reachable through a caller.
* Deployed.
* Live-verified.
* Accepted by staff.

The distinction is useful here. [NOW.md](https://github.com/collisionengineers/pegasus/blob/main/NOW.md) records precisely the sort of failure it prevents: the Worker is deployed and enabled, but no current trigger execution or business result has been demonstrated. Send-to-AI and MCP are implemented but disabled everywhere. Production smoke tests pass even though no Principal exists, meaning QDOS intake cannot create a case.

**Keep the distinction. Stop repeating it in every capability row and document paragraph.**

Similarly, keep:

* The four-project modular monolith.
* Core/Infrastructure dependency direction.
* External-system interfaces.
* Worker composition and production-profile tests.
* Idempotency for externally retried operations.
* Edit leases and concurrency protection.
* Audit history for material case changes.
* Fail-closed intake and principal/reference rules.

Those address real risks.

## The clearest example of process becoming product code

[CoreAssembly.cs](https://github.com/collisionengineers/pegasus/blob/main/src/Pegasus.Core/CoreAssembly.cs) contains a `QdosAlphaAcceptanceGate` that hard-codes roughly 130 capability IDs and release-evidence gates.

The complete mechanism consists of approximately:

* 316 production Core lines.
* 167 integration-test lines.
* 574 PowerShell lines.

That is over 1,000 lines implementing release-governance machinery. I found no actual application caller; Web registers the class and integration tests resolve it.

That mechanism should not be part of `Pegasus.Core` or normal Web composition. Release evidence can be checked by a release script or CI without becoming production-domain code.

# Corrected plan

## 1. Freeze scope before touching architecture

Define one alpha acceptance journey:

```text
Real QDOS email or manual upload
→ source retained
→ document parsed
→ principal and case decision made
→ case created or visible recovery outcome
→ documents reach Box
→ case is editable
→ EVA handoff generated
→ staff can see what happened
```

Only capabilities required for that journey—or required to operate it safely—remain `Now`.

Default candidates to move out of the alpha:

* Send to AI.
* Automation MCP expansion.
* Report-renderer integration.
* AI Centre integration.
* Provider API.
* Broader email categorisation.
* Automatic sent-evidence matching.
* Additional external integrations.
* Any speculative post-report workflow not currently required.

I would **pause**, rather than immediately delete, AI/MCP because it is strategically relevant to Pegasus. After the core journey is accepted, either resume it with a real activation plan or remove it.

## 2. Establish one work tracker

The current situation risks having:

1. 231 capability rows.
2. 44 large `NOW.md` queue entries.
3. 219 Kanmer tickets on `dev`.
4. Temporary task plans.
5. ADRs and open decisions.

That is not more control; it is duplicated state.

If Kanmer is the intended tracker:

* Kanmer owns actionable work.
* `capabilities.md` remains a concise product-outcome catalogue, not another ticket system.
* `NOW.md` shrinks to active claims and genuinely current production warnings—or disappears once claims are handled elsewhere.
* Archive the Kanmer tickets that merely restate future capabilities with boilerplate.
* Keep only actionable defects, decisions and deliverable slices.
* Delete orphaned temporary plans after checking that no task branch still owns them.
* Small, local bug fixes should not require a separate temporary planning document.

Do not create 219 tickets saying “plan this capability later.” The capability catalogue already says that.

## 3. Fix the actual production blockers

Before codebase beautification:

1. Remove the temporary verification account and treat its committed credential as compromised.
2. Create the production QDOS Principal through an authorised route.
3. Add a supported way to retry allocation after the Principal exists.
4. Remove the broad exception swallowing in `AllocateCaseIfDefinitiveAsync`.
5. Persist and visibly surface allocation failures.
6. Implement real `Blocked intake` and poison/quarantine recovery—or remove the unsupported claims that these exist.
7. Fix or explicitly descope Standalone Audit case creation and its legacy `draft_ready` behaviour.
8. Update the runtime-role permission check to derive its expected state from all migrations.
9. Exercise one real Worker-triggered intake through to an operator-visible result.
10. Obtain operator acceptance of that exact journey.

These should be small, behaviour-focused PRs. No general refactoring alongside them.

## 4. Remove confirmed dead or superseded surfaces

Once the golden path is stable:

* Remove the superseded Box File Request UI, registrations and contracts.
* Move `QdosAlphaAcceptanceGate` out of Core and Web; keep any genuinely useful release validation in tooling.
* Delete expired temporary plans.
* Remove unused `IQdosAlphaAcceptanceGate`.
* Remove compatibility branches only after checking production data.
* Keep dormant AI/MCP code frozen until the explicit resume-or-delete decision.

## 5. Simplify code capability by capability

Start with intake because that is where real state contradictions and silent failures already exist.

For that slice:

* Define the authoritative facts:

  * Was a receipt created?
  * Was processing completed?
  * Does an actual case link exist?
  * Is human intervention required?
  * Can it be retried?
* Make the case link authoritative for case existence.
* Stop using decision codes as competing proof that a case exists.
* Normalize the remaining `draft_ready` compatibility path.
* Consolidate `Blocked`, poison, quarantine and retry behaviour.
* Delete duplicated state only after production-data inspection.

Then apply a modest abstraction rule:

* External boundary: keep an interface.
* Multiple genuine implementations: keep an interface.
* Core-to-Infrastructure port: keep an interface.
* Single concrete use case injected only for DI ceremony: inject the concrete class when that capability is already being changed.

Do not launch a repository-wide “remove 92 interfaces” campaign.

## 6. Deal with the god pages later

[Cases/Details.cshtml.cs](https://github.com/collisionengineers/pegasus/blob/main/src/Pegasus.Web/Pages/Cases/Details.cshtml.cs) is an undeniable problem: 2,010 lines, 39 constructor dependencies and 36 handlers.

After correctness stabilises, keep the visible single case workspace but route mutations through capability-specific Razor endpoints:

* Case workflow.
* Tasks and chasing.
* Documents and requests.
* Vehicle operations.
* EVA handoff.
* Closure/reopening.

`DetailsModel` should primarily load and display the case. Forms can post to the smaller capability endpoints and redirect back to the same visible workspace.

That reduces coupling without introducing MediatR, a command bus or another framework.

`Program.cs` and `DependencyInjection.cs` can similarly be divided into ordinary profile/capability registration methods—no new interfaces required.

## 7. Keep the valuable tests

Do not pursue a test-reduction quota.

Keep:

* Core rule and contradiction tests.
* SQL transaction/concurrency tests.
* Production-versus-offline composition tests.
* Worker activation safety.
* Authentication and authorisation.
* Document limits and hostile-input handling.
* Idempotency/replay.
* Real browser journeys.
* Genuine corpus evaluation.

Delete a test only when:

* Its corresponding feature is deleted.
* It duplicates another test at the same boundary.
* It pins an implementation shape that is intentionally being removed.
* It proves registration without testing reachable behaviour.

## Recommended PR order

1. Tracker and orphan-plan cleanup.
2. Verification-account removal and production bootstrap correction.
3. Visible intake allocation failures and retry.
4. Blocked/poison recovery.
5. Standalone Audit fix-or-descope.
6. One live golden-path verification.
7. Remove Box File Request.
8. Extract the acceptance gate from production code.
9. Intake state simplification.
10. Case-details decomposition.
11. Resume-or-delete decision for AI/MCP.

## Final verdict

The corrected assessment is:

* **C#/.NET:** still the right stack.
* **Four-project architecture:** still right.
* **Safety boundaries:** mostly justified.
* **Current release scope:** far too large.
* **Documentation and tracking:** significantly overengineered.
* **Use-case interface pattern:** overused, but not worth a mass rewrite.
* **Previous cleanup proposal:** directionally correct, operationally too broad.

The first objective should not be “reduce lines and interfaces.” It should be:

> Make one real business journey work reliably in production, and remove everything currently competing with that objective.

That is the simplification Pegasus actually needs.



Yes on .NET; no, I no longer agree with the previous plan exactly as written.

After reviewing current [`main` at `75f39c7`](https://github.com/collisionengineers/pegasus/commit/75f39c70a34305e96a2a529e807fed28f9ed8833) and `dev` in depth, the strategic direction survives, but the priority order changes materially.

| Earlier conclusion                             | Revised verdict                          |
| ---------------------------------------------- | ---------------------------------------- |
| Keep .NET/C#                                   | Correct                                  |
| Keep the modular monolith                      | Correct                                  |
| “131 Now” proves severe scope bloat            | Too simplistic                           |
| Allocation failure/retry must be built         | Wrong; it already exists                 |
| Documentation/tracking are duplicating reality | Correct                                  |
| Broad interface cleanup should be a priority   | Only opportunistically                   |
| Fix production spine before adding features    | Correct                                  |
| Biggest immediate architecture problem         | Actually the dual Web/Worker intake path |

## Stack verdict

.NET is probably the best fit for this particular product.

Pegasus is an internal, form-heavy, transactional system using ASP.NET Identity, Razor Pages, EF Core/Azure SQL, Azure Functions, Graph, Blob Storage and Box. The existing four-project structure—Core, Infrastructure, Web and Worker—is a sensible modular monolith, as described in [ADR-0002](https://github.com/collisionengineers/pegasus/blob/75f39c70a34305e96a2a529e807fed28f9ed8833/docs/adr/0002-dotnet-modular-monolith-on-azure.md).

The concrete .NET advantages here are:

* Strong compiler-assisted refactoring for a large business model.
* Excellent SQL transactions, concurrency and schema migration support.
* One language across Web, background functions and domain code.
* Mature Identity, Graph and Azure integration.
* Razor Pages suit a small staff application better than a frontend/API split.
* Good long-term maintenance characteristics for audit-heavy code.

TypeScript, Python or Rails might have produced less ceremony initially, but changing now would trade known structural problems for a rewrite, weaker compile-time refactoring and a less natural fit for the existing Azure worker model. Java would be lateral. Go would be a worse fit.

The problem is not C#. C# merely makes it easy to build interface walls, gigantic DI registration lists and “clean architecture” theatre. Those were design choices.

## The most important finding

The manual-upload path currently has two competing owners:

```text
Browser upload
    ↓
Web stages work as Dispatched — without enqueuing it
    ↓
Web directly executes ProcessQueuedIntake and polls SQL
    ↓ failure/timeout
“Queued; the Worker will finish it”

Separately:
Dispatcher → Storage Queue → Worker → ProcessQueuedIntake
```

You can see this composition in [Upload.cshtml.cs](https://github.com/collisionengineers/pegasus/blob/75f39c70a34305e96a2a529e807fed28f9ed8833/src/Pegasus.Web/Pages/Upload.cshtml.cs) and [DurableIntake.cs](https://github.com/collisionengineers/pegasus/blob/75f39c70a34305e96a2a529e807fed28f9ed8833/src/Pegasus.Core/Intake/DurableIntake.cs).

That creates three concrete problems:

1. The intended least-privilege SQL model makes inline Web processing impossible. Web lacks `INSERT` permission on intake receipts, assets and evaluations, while Worker has it, according to [RuntimeRoleReconciliation](https://github.com/collisionengineers/pegasus/blob/75f39c70a34305e96a2a529e807fed28f9ed8833/src/Pegasus.Infrastructure/Persistence/Migrations/20260729199000_RuntimeRoleReconciliation.cs). Yet the Web calls the code that performs those inserts. Unless production has broader direct grants than the repository declares, inline processing must fail.

2. There is a durability hole. `ReceiveForProcessingAsync` creates an unleased `Dispatched` item without publishing a queue message. If Web dies before `ClaimProcessingAsync`, the dispatcher will never select it—it only selects `pending` and `retry_scheduled`—and lease recovery will never select it because it has no processing/dispatch lease. That item can remain stranded indefinitely in [EfIntakeWorkStore](https://github.com/collisionengineers/pegasus/blob/75f39c70a34305e96a2a529e807fed28f9ed8833/src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs).

3. `IntakeExceptionPolicy` classifies virtually every exception as recoverable. That turns permission failures, invalid states and likely programming errors into background retries, concealing the ownership mistake.

This, not the interface count, is the first architectural fix I would make.

## Corrections to my earlier assessment

Allocation recovery is already implemented. [IntakeAllocation.cs](https://github.com/collisionengineers/pegasus/blob/75f39c70a34305e96a2a529e807fed28f9ed8833/src/Pegasus.Core/Intake/IntakeAllocation.cs) contains durable attempts, immutable retry commands, concurrency protection and recoverable versus blocked failures. [Intake/Details](https://github.com/collisionengineers/pegasus/blob/75f39c70a34305e96a2a529e807fed28f9ed8833/src/Pegasus.Web/Pages/Intake/Details.cshtml) already displays failures and offers “Retry case creation.” Any NOW item saying otherwise is stale; we should reconcile it, not build the feature again.

Likewise, 131 `Now` capabilities do not mean 131 independent alpha features. They are mostly fine-grained acceptance properties. The catalogue explicitly says some `Now` items are separately owned or non-blocking. The actual defect is that `Now` conflates allocation, implementation, activation and release blocking.

There is measurable drift: [capabilities.md](https://github.com/collisionengineers/pegasus/blob/75f39c70a34305e96a2a529e807fed28f9ed8833/docs/capabilities.md) claims 230 capabilities and 41 `Later`; its table contains 231 unique capability rows and 42 `Later`. The release gate also still requires retired `DOC-06`.

## The plan I would actually execute

1. Truth and scope PR

   * Freeze AI, MCP, report-renderer and other non-cutover work.
   * Treat the eight journeys in [NOW’s Path](https://github.com/collisionengineers/pegasus/blob/75f39c70a34305e96a2a529e807fed28f9ed8833/NOW.md) as the alpha release definition.
   * Correct the capability counts and stale allocation claims.
   * Choose one operational tracker. Do not merge `dev`’s mechanical 219-ticket Kanmer expansion as-is.
   * Archive orphaned temporary plans after checking ownership.
   * Move `QdosAlphaAcceptanceGate` out of Core/Web and into release tooling. Preserve the validator, but stop registering a test-only manifest checker in the running application. Its current implementation in [CoreAssembly.cs](https://github.com/collisionengineers/pegasus/blob/75f39c70a34305e96a2a529e807fed28f9ed8833/src/Pegasus.Core/CoreAssembly.cs) even contains retired `DOC-06`.

2. Make queued uploads visible

   * Add a status page/read model keyed by staged receipt ID.
   * Show `Received`, `Processing`, `Complete` or `Failed`.
   * Auto-refresh while processing and redirect/link to the resulting case or receipt.
   * Change the existing queued branch to land there instead of returning to an upload page with no identifiable work item.

3. Make Worker the sole intake processor

   * Stage every source as `Pending`.
   * Let the dispatcher publish the queue message.
   * Let Worker alone invoke `ProcessQueuedIntake`.
   * Remove `ExecuteInlineAsync`, `ReceiveForProcessingAsync`, `ProcessIntakeSubmission`, Web’s worker registration and the two ten-second polling loops.
   * Repair any existing unleased `dispatched` records with a one-time, idempotent operation.
   * Replace “almost every exception is recoverable” with explicit transient infrastructure failures; log and surface unexpected faults.
   * Test duplicate queue delivery, crash-after-stage, lease expiry, poison handling and the actual Web/Worker permission boundary.

4. Complete the production cutover path

   * Remove the committed temporary verification account.
   * Fix the runtime-role verification/bootstrap script.
   * Verify or create the QDOS Principal using the existing administration UI.
   * Verify exact deployed source and Worker activation.
   * With approval, run one genuine QDOS email through intake, custody, extraction, allocation, Box, staff review and EVA.
   * Fix only failures exposed by that journey.
   * Add minimum actionable alerts and record operator acceptance.

5. Simplify locally after the spine is accepted

   * Keep one visual Case workspace, but split the 2,010-line, 39-dependency, 36-handler [Cases/DetailsModel](https://github.com/collisionengineers/pegasus/blob/75f39c70a34305e96a2a529e807fed28f9ed8833/src/Pegasus.Web/Pages/Cases/Details.cshtml.cs) into cohesive coordinators: view composition, edits/leases, workflow, tasks, documents/custody and vehicle/EVA.
   * Remove single-implementation use-case interfaces when touching those areas.
   * Keep interfaces around SQL stores, clocks, Graph, Box, Blob and other genuine boundaries.
   * Retain behavioural, security, concurrency and idempotency tests; relax tests that merely freeze constructor shapes or class names.
   * Do not introduce MediatR, a generic repository, microservices or an SPA.

So the corrected conclusion is: keep .NET, keep the modular monolith, stop feature expansion, and fix the split intake ownership before undertaking cosmetic architecture cleanup.

I could not execute the test suite because this environment lacks the .NET SDK. The findings above come from static control-flow, persistence, permission, test and documentation inspection; the new intake and role-boundary tests should be the first runtime verification.
