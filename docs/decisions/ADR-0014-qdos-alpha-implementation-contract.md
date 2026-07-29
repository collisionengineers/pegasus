# ADR-0014: QDOS alpha implementation contract

- Status: Accepted checkpoint 1 addendum
- Date: 2026-07-29
- Owners: Collision Engineers product owner and Pegasus development team
- Work identity: [issue #3](https://github.com/collisionengineers/pegasus/issues/3) and the existing [QDOS change record](../changes/2026-07-27-qdos-alpha-reference-corpora.md)
- Supersedes: only the clauses named under [Clause-level effect](#clause-level-effect)

## Context

The accepted QDOS product, route, caller, and evidence decisions are already
owned by the canonical requirements, capability inventory, open-decision
register, design contracts, and QDOS change record. Their implementation
boundary must now be activated without rewriting published decision bodies,
creating another delivery ledger, changing capability allocation, or promoting
a target design to caller, deployment, or acceptance evidence.

Checkpoint 1 starts from merged `main` at
`46b0328b149d7da887fa899c8aa39e01fcf159dc`. That merge has the documentation
pull request 18 merge
`536f5fc470a541281f86ebc711564d49432ed73f` as its first parent and capability
child/source head `f77e1492b25abdd5a14725f4c15129333482b743` as its second parent. These hashes
identify the accepted prerequisite ancestry; they do not prove implementation,
a caller, deployment, live behavior, or acceptance.

At that baseline, the only QDOS product mutation caller proved in the repository
is the Development-only `POST /Intake/Upload` path through `ProcessIntake` and
the contained `QdosInstructionExtractionPolicy`. The provider-domain catalog is
present but is not consulted by that caller. The Worker has no trigger or Core
caller, and no case/reference, MCP, Graph, Box, deployment, or operator evidence
is thereby proved. A separately delivered Development evaluator may exercise
the existing extraction policy against local material; that is evaluator
evidence, not a QDOS product caller or QDOS activation acceptance.

## Decision

1. Issue #3 and its existing QDOS change record remain the sole implementation,
   evidence, review, and delivery identity. This addendum is durable decision
   authority, not a second status ledger.
2. Delivery retains two mandatory stages. Complete offline development
   acceptance first through real local Web, Worker/Functions, SQL, storage,
   authentication, MCP, and operator callers without cloud credentials or
   external-service calls. Live adapters, Azure work, deployment, recovery,
   operator acceptance, management approval, and release follow only with their
   own exact-target evidence and authorization.
3. `Pegasus.Core` remains the only business-policy owner. Durable intake is split
   into `ReceiveIntake`, `ProcessIntake`, `ResolveIntake`, and `AcceptIntake`.
   Transport callers normalize evidence and invoke those Core boundaries; none
   may allocate a reference, create a case, change lifecycle, or decide policy
   independently.
4. The shared versioned Core mail policy owns route selection,
   provider/type/case evidence, the settled Received/Sent/Reply taxonomy, and
   accepted Triage/report matching. The QDOS extractor remains an inner typed
   extractor rather than a competing classifier. Direct-provider and
   intermediary policies remain separate under Decision 0011. Only an
   evidence-approved QDOS route may activate case creation for this alpha; the
   exact direct trait `@qdosassist.co.uk` proves a route candidate, not message
   type, case association, acceptance, or a fallback principal.
5. The following matrices are the QDOS implementation targets. They allocate
   callers but do not claim that those callers exist or have passed acceptance.

### Razor and HTTP caller matrix

| Caller | QDOS-alpha responsibility | Boundary and required proof |
| --- | --- | --- |
| Authenticated Razor Pages | Operations at `/`; Intake at `/Intake` and `/Intake/{id}`; Triage at `/Triage` and `/Triage/{id}`; Cases at `/Cases` and `/Cases/{id}`; authenticated upload; account, principal, configuration, and mailbox administration only for the authorised Administrator role | PageModels bind, authorize, and translate into named Core queries/actions. Browser evidence must exercise the real route, role denial, validation, stale/lease behavior, recovery, and permanent history where applicable. |
| Request-scoped unauthenticated upload | Accept only the fields and operation bound to a temporary staff-created token, then enter the normal intake and custody boundaries | Expiry, revocation, limits, replay, abuse, cross-request isolation, retry, custody, and non-disclosure require real-caller proof. The public edge exposes no case/reference or unrelated material. |
| Read-only queries and downloads | Expose the accepted queue, case, source, document, evidence, EVA-bundle, and report-evidence views through their existing Core owners and custody ports | A rendered page or registered handler is not caller proof. Downloads/exports and material failures retain the required actor/history evidence. |

The historical Development-only `/Intake/Upload` route remains source evidence
until the target callers above are exercised. It is not by itself offline-alpha
acceptance and is not retained as a parallel production policy owner.

### Worker caller matrix

| Worker trigger | Permitted responsibility | Prohibited behavior |
| --- | --- | --- |
| Local-mail Inbox poll timer, replaced by the separately approved Graph adapter at the live stage | Claim the durable mailbox cursor/lease, retain transport and source identity, and invoke the named Core receipt boundary; `instructions@collisionengineers.co.uk` is the sole live alpha mailbox | No direct case/reference allocation, mailbox-specific classifier, production mailbox use during local alpha, or acknowledgement before a durable outcome |
| SQL outbox dispatch timer | Claim persisted outbox work and dispatch stable work-item IDs | No source bytes in queue messages and no business decision in the dispatcher |
| `intake-work` queue trigger | Load retained source by ID and invoke the shared Core processing/acceptance path | No transport-specific acceptance, duplicate policy, or acknowledgement before a durable outcome |
| External-work recovery | Resume or terminally record persisted external work through the owning Core port and retry policy | No silent local fallback, unrecorded vendor call, or operation outside the approved adapter boundary |
| Due-work sweep | Invoke Core-owned due/chase actions for persisted identities | No page-, timer-, or adapter-owned schedule or lifecycle transition |
| Sent-evidence poll | Retain and offer exact immutable Sent-item/reply-chain evidence to the applicable Core matcher | No subject-, VRM-, frequency-, or transport-only completion rule and no claim of recipient delivery |

Each trigger claims persisted work, invokes one Core use case, and acknowledges
only after a durable outcome. Registration, configuration, or a hosted Worker
without an exercised trigger is not caller proof.

### MCP caller matrix

| Surface or action family | QDOS-alpha allocation | Boundary and proof |
| --- | --- | --- |
| `/mcp` transport and staff identity (`MCP-01`) | One remote Streamable HTTP endpoint in `Pegasus.Web`, using the accepted OpenIddict authorization-code flow with S256 PKCE, exact resource/audience, current staff account and role | Registration, metadata, a tool schema, or direct service invocation is not proof. Exercise the real HTTP caller, OAuth and Core denial, validation, staff attribution, account disable/role change, stale/lease behavior, and history. |
| Case actions (`MCP-02`) | The same named Core case, task, transition, assignment, evidence, EVA, and report actions available to that role through Web | No MCP-owned policy, bypass, bulk lifecycle mutation, permanent deletion, or action unavailable to the role in Web. |
| Intake and Triage actions (`MCP-03`) | The same Core intake-queue, resolution, acceptance, and Triage actions available to that role through Web | No Development evaluator, general classified-email workspace, arbitrary mailbox action, live send, or route-activation bypass. |
| Document actions (`MCP-04`) | The same Core-owned view, upload, download, export, version, and custody actions available to that role through Web | No arbitrary custody identifiers, credential access, direct adapter mutation, or permanent deletion. |
| Administration, credentials, cloud/release operations, and broader classified-email tools | Not allocated to the alpha MCP surface | Account/role, principal, configuration, mailbox, OAuth-client, credential, cloud, deployment, generic-email, and destructive operations remain absent unless a later allocation and accepted contract explicitly activate them. |

## Separate evaluator and repository-policy boundaries

The Development/local email evaluator is a separate delivery under
`DOC-CON-052`. The capability inventory retains the existing `Now /
0.1.0-alpha.1` allocations for `OPS-22`, `EVAL-01` through `EVAL-05`, and
`MAIL-20` because no replacement target was authorised. Those allocations are
not QDOS implementation commitments, QDOS caller rows, UI-acceptance gates, or
checkpoint acceptance. A separately delivered evaluator may call shared policy
and supply accepted, source-labelled cohorts or holdouts; it does not own the
shared Core policy, production intake, Graph replay/live adapters, or any QDOS
route. QDOS adds and claims no evaluator route, workspace workflow, reviewer
campaign, command, or Administrator evaluation approval.

Repository-policy enforcement is separately deferred until after
`0.1.0-alpha.1`. `scripts/Test-RepositoryPolicy.ps1`, whether invoked directly
or by `scripts/Test-RepositoryLanguage.ps1`, is a successful no-op. Its result is
**skipped/deferred**, never **passed**; it proves no repository-policy property,
is not an alpha-required gate, and cannot be cited as green evidence. Re-enabling
it requires a reviewed post-alpha change, reproducible proof inputs, a
clean-checkout pass, and independent review. This deferral does not waive other
independently operating language, build, test, caller, review, or acceptance
gates.

## Clause-level effect

| Existing decision and exact clause | Effect of this addendum | Preserved remainder |
| --- | --- | --- |
| ADR-0004, **Decision / Maturity and activation**, the paragraph that calls the provider API and broader email work `Next / unallocated` and limits alpha MCP to “intake-oriented actions” | Superseded only for current release identity and alpha MCP breadth. Provider API is allocated to `0.4.0`; broader classified-email queues/email MCP are allocated to `0.3.0`; staff MCP remains `0.1.0-alpha.1` with exactly the `MCP-01`–`MCP-04` matrix above. | Provider-principal and staff identity remain separate. The provider API remains outside QDOS alpha. ADR-0004's Streamable HTTP, OAuth, resource/audience, current-role, revocation, authorization, history, and prohibited-operation clauses remain accepted. |
| ADR-0006, **Decision item 1**, “`ProcessIntake` is the single Core intake use case” | Superseded only by the durable four-stage `ReceiveIntake` / `ProcessIntake` / `ResolveIntake` / `AcceptIntake` boundary. `ProcessIntake` remains one Core stage; no transport gains business ownership. | Provider-neutral source identity, required retention, reader outcomes, evidence, persistence, and fail-closed processing remain accepted. |
| ADR-0006, **Decision item 8**, the Development-only `/Intake/Upload` caller | Superseded as the target alpha caller allocation by the Razor/HTTP, Worker, and MCP matrices above. The old route remains dated local thin-slice evidence until replaced and is not production or acceptance proof. | The route must remain Development-only while it exists, and retired `/Intake/Qdos` remains absent rather than becoming a compatibility path. |
| ADR-0006, **Limits and deferred-capability impact**, opening paragraph | Superseded only where it places the Worker trigger, QDOS production-mail intake, authentication, staff MCP, case acceptance/reference allocation, production staging/Box custody, focused EVA work, and approved Azure release work outside the alpha implementation contract. Those are required QDOS-alpha outcomes, with live work still gated after offline acceptance. | Another principal's activation, provider API, broad mailbox management/classified-email workspace, DOC/MSG automation, scan-like PDF OCR execution, and unrelated deferred capabilities remain outside this alpha. Clean seams do not authorize dormant implementations. |

No other clause is superseded by this addendum. In particular:

- Decision 0011 remains the route-policy authority and already owns its stated,
  narrow supersession of ADR-0006's single-policy selection and
  no-provider-registry/table limits.
- ADR-0006 decision items 2 and 4–7, its provider-neutral and fail-closed
  consequences, and its migration/rollback boundaries remain accepted, subject
  only to Decision 0011 and the exact clauses above.
- ADR-0001, ADR-0003, and ADR-0005 retain their extraction-engine,
  multi-format, asset-occurrence, provenance, bounded-processing, and
  scan-candidate decisions. This addendum activates no OCR execution.
- ADR-0002 retains the modular-monolith runtime, four-project production
  dependency direction, one Core, one database, one migration stream, and Web /
  Worker composition boundaries. Decision 0013's source-workspace exception and
  ADR-0009's deployment-mechanism supersession remain unchanged.

## Evidence blockers and activation rule

This addendum accepts an implementation contract only. It creates no new
implementation, caller, deployment, live-verification, operator-acceptance, or
management-acceptance evidence. The QDOS change record remains the sole mutable
owner of delivery state and its [blocker list](../changes/2026-07-27-qdos-alpha-reference-corpora.md#blockers-and-unresolved-evidence-choices); the [open-decision register](../open-decisions.md) remains the owner of unresolved material evidence choices.

The durable activation holds are:

| Hold | Evidence required before the affected caller or release step activates |
| --- | --- |
| Mail routing and automatic matching | Executable route predicates and dispositions; genuine positive, negative, ambiguous, forwarded/reply-chain, retry, and untouched holdout evidence; accepted Triage and report matchers; exact activation/rollback behavior |
| Vehicle and EVA boundaries | A selected VRM mechanism with representative false-positive evidence; an accepted DVLA/DVSA provider, licence, field/error/limit/mileage contract; and an operator-accepted focused EVA mapping, readiness, image, naming, and recovery contract |
| Outlook and Box | Exact approved Graph tenant, mailbox, Inbox/Sent allowlist, Application RBAC and denied control; exact Box enterprise, identity, root, scopes, operations, custody, failure, and recovery evidence |
| Platform and release | Refreshed Azure inventory and predecessor dispositions; an approved isolated Pegasus target and spending/identity/RBAC boundary; migration, deployment, restore, rollback, recovery, and live-caller evidence |
| Acceptance | Clean-operator offline acceptance, then operator acceptance, Collision Engineers management approval, and separately authorised production migration, deployment, and cutover |

Missing evidence keeps only the affected policy, matcher, adapter, caller, or
release step absent or disabled and keeps the release blocked. It never permits
fabricated evidence, silent local fallback, a local evaluator counted as a QDOS
caller, a no-op policy verifier counted as passed, dormant capability, or a
reduced alpha contract.

## Consequences

- Checkpoint 1 may implement the accepted QDOS contract in dependency order
  within issue #3 and its existing change record.
- Razor Pages, Worker triggers, and staff MCP reach the same Core actions without
  creating transport-specific business policy.
- Separately delivered evaluator results can satisfy a named evidence
  prerequisite without transferring evaluator implementation or acceptance into
  QDOS scope.
- Offline implementation grants no cloud, mailbox, Box, EVA, Azure, deployment,
  or predecessor operation. Each live step still requires exact-target evidence
  and authorization.
- Current source, target caller allocation, exercised callers, deployment, and
  acceptance remain explicitly different evidence states.
