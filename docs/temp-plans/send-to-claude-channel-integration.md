# Send to Claude channel integration

Completion plan for the `Send to Claude` channels connector: the remaining
hardening that closes out `pegasus-claude-channel` development, the
Pegasus-side wiring that gives the Engineers assessment surface a real
send/return round trip behind composition gates, and the PAV slider the
operator has directed into channels scope. This plan is a specification
artifact: `design/README.md` § Deferred casework requires every deferred UI
capability to re-enter specification and review before implementation, and
nothing below is implemented until the operator approves the decisions in
§ Open decisions and product authority records the allocation change.

Amended after operator review on 2026-08-03: PAV is confirmed as
pre-accident value; content returns to Pegasus as direct Automation Actor
writes rather than staged proposals; automations are designed and
safeguarded externally (Claude Desktop; skills, prompts, and tasks built in
Cowork and run on Automations); Pegasus's obligations are a fully
comprehensive toolset plus logging parity with human actions.

Repositories touched:

- `pegasus` (this repository) — Core, Web, Infrastructure, tests, docs.
- `pegasus-claude-channel` (sibling local repository, no remote, three
  commits, clean tree) — the standalone channel receiver. It stays outside
  Pegasus by design: it is an external client, never a policy owner, and is
  not added to `Pegasus.slnx` or any deployment.

## Scope

Capability IDs: AI-09 (`Send to AI` work transport, `Later / 1.3.0`), riding
on the UI-15 assessment surface (`Later / 1.0.0`, design-only and unlinked)
and returning through the MCP-01–04 Automation Actor ingress (`Now`,
implemented, composition-gated off). ADR-0011 fixes the vocabulary: the
domain action is vendor-neutral `Send to AI`; `Send to Claude` is the one
sanctioned UI label while Claude is the sole accepted provider; a relabel
never changes the stored action identity.

In scope:

1. Channel-server close-out: idempotency, schema versioning, reply
   validation, the durability decision, `/events` filtering, operational
   setup, and its vitest additions. This ends connector development.
2. Pegasus-side wiring, gated and DevelopmentOffline-only: a Core-owned
   work-request model, a Web/Infrastructure hand-off adapter to the
   connector's localhost ingress, delivery-state display on the existing
   panel, and content return as direct Automation Actor writes through the
   assessment toolset (companion plan
   `docs/temp-plans/mcp-assessment-toolset.md`).
3. The PAV slider on the valuation/total-loss review surface.
4. Sequencing, activation preconditions, test plan, docs impact, and the
   evidence-tier claims each stage may make.

Out of scope (see § Non-goals): activation anywhere but DevelopmentOffline,
linking the assessment surface into navigation, report generation, any
Outlook or Box mutation, direct Anthropic API integration, and any
autonomous acceptance.

## Evidence state

**Planned.** Nothing in this document is implemented. Current reality:

- The connector implements `POST /send`, `GET /status`, `GET /events`
  (all bearer-authenticated, 127.0.0.1-only), the
  `notifications/claude/channel` forward, and the `pegasus_channel_reply`
  tool, with smoke tests green (`test/smoke.test.ts`). Delivery state is an
  in-memory ring buffer; `GET /events` is authenticated JSON rather than
  the docs' unauthenticated SSE — both accepted deviations.
- Pegasus has the design-only assessment and suggestions surfaces
  (`src/Pegasus.Web/Pages/Cases/Assessment/`), unlinked, empty PageModels,
  no route, Core field, or transport activated.
- The Automation Actor MCP ingress is implemented behind
  `Features:AutomationMcp` (DevelopmentOffline only), with nine tools,
  permanent action history, `automation_*` security events, the
  Administration Automation surfaces, and the kill switch; tier 2–4 local
  evidence recorded, tier-5 external-client evidence still queued in
  `NOW.md`.

## Coordination with live claims (`NOW.md` on `origin/dev`, 2026-08-03)

- **AI-09 implementation is this task's assignment** (operator statement,
  2026-08-03): this plan is its specification, and the implementation claim
  line goes onto `NOW.md` when stage C begins — workflow mechanics, not a
  pending approval. **task/ui-alpha-design-pass** (live, PR #326) carries
  the UI-15/AI-09 design route: the panel and assessment surface this plan
  wires. Stages D and F build on its merged markup, and the re-entry review
  happens against that task's output.
- **task/report-renderer-integration** (live) plans "promotion of the
  renderer's pre-existing MCP server as the replacement for the current
  `.mcpb` packaging (MCP-01–04 follow-ups)". If that direction is accepted,
  stage A's "ends connector development" claim may need revisiting: the
  close-out list stands on its merits, but whether `pegasus-claude-channel`
  remains the long-term packaging becomes that task's question. Flagged for
  the operator rather than resolved here.
- **`NOW.md` Next queue** already holds the Automation Actor ADR promotion
  (stage B's vehicle) and the tier-5 MCP evidence run (stage G folds into
  it).

## Architecture of the round trip

Three transports, one policy owner:

| Leg | Transport | Carries | Trust stance |
| --- | --- | --- | --- |
| Hand-off out | Pegasus Web → connector `POST /send` (localhost, bearer) | Pointer only: `case_reference`, `request_id`, instruction ≤ 500 chars | Token authenticates the local sender; the text is untrusted in front of Claude |
| Work | Claude session → Pegasus `/mcp` (Automation Actor, OAuth client credentials) | Case reads and direct assessment/case-detail writes via the Automation Actor toolset (existing nine tools plus the assessment tranche) | Actor identity, scopes, permanent history, kill switch |
| Status back | Claude → `pegasus_channel_reply`; Pegasus server-side reads connector `/status`/`/events` | Delivery/handling status and a short confirmation message | Diagnostic only; never a business ingress |

The deciding rule, restated from the connector's own README and ADR-0011:
**business data never returns through the channel.** The channel carries
operator chat (pointers out, confirmations back). The content itself lands
in Pegasus as direct Automation Actor writes — scoped, lease-guarded,
attributed, idempotent — through the assessment toolset defined in the
companion plan (operator decision 2026-08-03; this supersedes the earlier
staged-proposal return: `pegasus_proposal_submit` is not built and no
`automation.proposals` scope is created). Routing payload through
`pegasus_channel_reply` remains rejected: it would create a second,
unattributed ingress for business data, bypass Core
idempotency/attribution, contradict the connector's security stance, and
make a shared local token a write path into Pegasus.

`Pegasus.Core` owns the work-request lifecycle and the assessment write
policy (companion plan). `Pegasus.Infrastructure` implements the connector
HTTP adapter against a Core port. `Pegasus.Web` composes both behind a gate
and translates transport. The connector and the `workspaces/` skills never
own policy; automation behaviour itself (skills, prompts, tasks) is
designed outside Pegasus and is outside this repository's scope.

## Pegasus side

### Core slice (policy owner)

New capability folder in Core (business-language naming, e.g.
`src/Pegasus.Core/AiWork/`), owning:

- **Work request.** `CreateAiWorkRequest`: staff actor (Engineer roles per
  the access matrix), one case, captured case version/revision stamp,
  capability scope (`assessment_review` initially), idempotent operation
  key, generated `request_id` (GUID, satisfies the connector's
  `^[A-Za-z0-9][A-Za-z0-9_\-]{0,63}$`), permanent action history entry.
  States: `Created → HandedOff → Completed` with `Failed`, `Cancelled`,
  `Expired` as visible terminal outcomes. AI-09 rules kept where they
  still apply: duplicate or expired requests are idempotent/inert; staff
  cancellation takes a reason; cancelling or expiring a request never
  undoes writes already made — it closes the tracking record only.
- **Completion.** `Completed` is flipped when the connector reply reports
  `done` (read server-side via `/status` / `/events` on the reconcile
  action). It is a tracking state about the hand-off, not a claim about
  the writes — those are independently visible in action history the
  moment they happen. `Failed` mirrors a `failed` reply or a transport
  failure.
- **Ports.** `IAiWorkRequestStore` (persistence), `IAiHandOffTransport`
  (one method: hand off a pointer, returning accepted/refused/unreachable
  as a typed outcome — `terminal`/`transient`/`unknown` per the
  architecture invariants).

There is no proposal record, suggestion store, or accept/apply command in
this plan: content arrives as ordinary attributed writes (companion plan),
and review happens where it always does — with the engineer the case is
assigned to. Dependency, stated honestly: those writes need the Core
assessment model from the companion plan; until it lands, a handed-off
session can read the case and update the overlapping case-data fields, but
assessment-specific fields have nowhere to go. Stage F below is therefore
sequenced behind the assessment Core model, and the interim behaviour is
Open decision 6.

### Web adapter, configuration, and gating

Mirror the `Features:AutomationMcp` pattern exactly:

- `Features:SendToAi` composition gate, default off. `TryCreate`-style
  options validation refuses any profile other than DevelopmentOffline —
  same fail-closed startup rule as `AutomationMcpOptions.TryCreate`
  (`src/Pegasus.Web/Mcp/AutomationMcp.cs`). Gate off ⇒ no transport
  registration, no POST handler behaviour, panel renders `unavailable`.
- Options: `SendToAi:ChannelBaseUrl` (default `http://127.0.0.1:8629`),
  `SendToAi:ChannelToken` (≥ 32 bytes, user-secrets only — never tracked,
  displayed, or logged, same custody rule as `AutomationMcp:ClientSecret`),
  `SendToAi:TimeoutSeconds` (small, bounded). The token lives server-side
  only; the browser never sees it.
- Adapter: an `Infrastructure` (or Web-composition) HTTP client
  implementing `IAiHandOffTransport` against the connector contract,
  sending `schema_version`, treating 401/415/413 as terminal
  configuration failures, connection-refused/5xx as transient visible
  failures, and never retrying past the bounded budget. It makes no other
  outbound call.

### Assessment panel wiring and delivery-state mapping

`Index.cshtml.cs` gains a real antiforgery-protected POST handler (roles
per the access matrix) that: checks the gate, runs the Core readiness rule
(Open decision 5 names the eligibility inputs), calls
`CreateAiWorkRequest`, invokes the transport, and PRGs back. The confirm
dialog stays. Panel state mapping (server-rendered; status is Core-owned
work-request state, not a live connector poll):

| Panel state (existing markup) | Driven by |
| --- | --- |
| `unavailable` — "Not available" | Gate off, transport not composed, or readiness rule fails (readiness list names why) |
| `in-flight` — "Sending." | POST accepted, transport call in progress (transient render during PRG) |
| `sent` — "Sent. Suggestions appear here when they return." | Connector returned 202 and `forwarded`; work request `HandedOff` |
| `failed` — "Nothing was sent" | Connector refused/unreachable or forward_failed; work request `Failed`; case unchanged |
| **new** `completed` — "Claude has finished. Review the changes on this case." with a link to the activity view | Work request `Completed` (reconcile reads the connector reply; the writes themselves are already visible in history) |

The `completed` state is a small markup addition to the panel and must go
through design conformance (state text, not colour alone; no new tokens).
An operator-triggered reconcile control (the manual-refresh idiom — start
feedback, no auto-poll) queries connector `/status` and
`/events?request_id=` server-side for the diagnostic view: `replied` with
`failed` status surfaces as a visible failure with Claude's short message.
The connector remains diagnostic truth for delivery; Core remains business
truth for the work request. "Sent" claims map to `forwarded` (written to
the transport), never to "Claude read it" — consistent with the
requirement never to present attempted work as delivered.

### Correlation, attribution, activity, kill switch

- Correlation identifier = the work-request `request_id`, used in: the
  staff action-history entry at send, the connector's delivery event and
  channel meta, Claude's `pegasus_channel_reply`, and — when the
  automation passes it (companion plan D3, optional binding) — every
  Automation Actor write the session makes. The existing
  `/Administration/Automation/Activity` correlation filter then shows the
  whole round trip in one query; denials keep writing `automation_*`
  security events through the existing auditor.
- Kill-switch story, three independent cut points, all fail closed:
  1. Pegasus outbound: `Features:SendToAi` off (composition absent) plus
     an Administration toggle mirroring the Automation client pattern
     (reason + operation key + history; disable refuses new hand-offs
     immediately).
  2. Return path: the existing Automation kill switch — disabling the
     automation client refuses new tokens and rejects in-flight tokens
     within seconds, so Claude can neither read case data nor submit.
  3. Connector: rotate/remove `CHANNEL_TOKEN` and restart, or end the
     Claude Code session (the listener dies with its parent).

### Content return: the Automation Actor assessment toolset

Superseded by the operator's 2026-08-03 direct-write decision: there is no
proposal tool and no `automation.proposals` scope. The return path is the
assessment tranche of the Automation Actor toolset —
`pegasus_assessment_update`, `pegasus_case_update_details`,
`pegasus_eva_bundle_generate` and companions — specified in
`docs/temp-plans/mcp-assessment-toolset.md`. Every write is lease-guarded,
version-checked, idempotent, and logged with the same rigor as a human
action; that logging parity is the Pegasus-side safeguard, while automation
behaviour is safeguarded externally where it is designed. The inventory
widening is still recorded in the new ADR and in the seeded client's scope
grant (`automation.assessment`).

## Channel server close-out

These changes end development on `pegasus-claude-channel` (version bump to
`0.2.0`). All are in `src/server.ts` plus tests and README.

1. **Idempotent `/send`.** A repeated `request_id` returns 202 with the
   existing event's status and `duplicate: true`, and emits no second
   notification. Aligns the connector with AI-09's idempotent-retry rule
   and makes Pegasus's bounded retry safe.
2. **Schema versioning.** `schema_version: 1` required on the `/send`
   body; echoed in responses, `/status`, `/events` records, and the
   notification `meta`. Unknown major version ⇒ 400 with a stable error
   code. The reply tool result names the version it recorded against.
3. **Durability decision.** Adopt the append-only JSONL evidence log (the
   alternative already offered): every delivery transition and reply is
   appended to a dated `state/events-YYYYMMDD.jsonl` (path overridable via
   `CHANNEL_STATE_DIR`, directory gitignored). The in-memory ring buffer
   remains the serving store for `/status`/`/events`; the JSONL is the
   evidence artifact that survives restart. Recorded constraint: the file
   contains case references, so it is local-only material — never
   committed, published, or synced, the same discipline as `corpus/`.
   This revises the current "no filesystem writes" line in the README,
   which is rewritten to scope the guarantee ("no filesystem writes other
   than the local evidence log").
4. **Reply validation hardening.** Keep the status enum and request-id
   pattern; additionally record every reply in the JSONL (latest wins in
   memory), reject replies against unknown request ids (existing), and
   keep the reply message cap and sanitization. No payload extension —
   the reply stays operator chat.
5. **`/events` filtering.** `?request_id=` and `?limit=` query support so
   the Pegasus reconcile action fetches one record cheaply.
6. **Content-Type check.** `POST /send` requires `application/json`
   (415 otherwise).
7. **Tests (vitest).** Duplicate `request_id` idempotency; schema-version
   rejection; JSONL append and restart survival; `/events` filtering;
   415; a stdio-level `tools/call` test driving `pegasus_channel_reply`
   through JSON-RPC (the current suite only asserts the notification
   write); sanitization edge cases (bidi controls, angle lookalikes) as
   explicit cases.
8. **README/operational setup.** Token generation and `.env` ACL
   (existing), `claude mcp add` with absolute paths (existing),
   `claude --dangerously-load-development-channels
   server:pegasus-claude-channel` (existing), plus: the Pegasus side
   (`dotnet user-secrets set "SendToAi:ChannelToken" …` on
   `Pegasus.Web`), startup order, the one-machine-one-port rule, and a
   pinned Claude Code version for evidence runs (the channels contract is
   a research preview and may drift; the wire format was confirmed
   2026-08-03).

Explicitly retained deviations: authenticated JSON `/events` (not SSE);
no `claude/channel/permission` declaration, ever.

## PAV slider

Operator direction places the PAV slider in channels scope; it was not in
the assessment markup. Repository evidence, gathered rather than invented:

- PAV is **pre-accident value** — confirmed by the operator on 2026-08-03
  (former Open decision 8, now closed), matching all repository evidence:
  the protected skills
  (`workspaces/ai-centre/skills/vehicle-assessment/references/total-loss-and-salvage-routing.md`:
  "The pre-accident value comes from the engineer's instruction or from
  `vehicle-valuation` … never invented"), EVA's salvage screen (`% of
  PAV`, `Equity in repair`, and the `PAMV From`/`PAMV To` pre-accident
  market value range, `docs/reference/eva_information/eva_information.md`),
  and the renderer spec ("Recommended Settlement (PAV − salvage)",
  `docs/reference/rendererref1/DESIGN_SPEC.md`).
- **Figure source, per operator direction 2026-08-03:** external API calls
  to valuation services will be added to obtain valuation figures, and
  those figures are what the slider is based upon. That is EXT-10
  (versioned vehicle-valuation evidence) / EXT-13 (independently licensed
  valuation-source adapters) territory — both `Later / 1.0.0`, neither
  contracted yet, and each adapter needs its own accepted access/terms.
  Until an adapter lands, the recorded guide evidence figures
  (CAP/Glass's/Cazana) entered on the Valuation tab are the interim range
  source; the slider's data contract is written against "recorded
  valuation evidence" so an API adapter slots in without a UI change.
- The economics the slider illustrates: repair-cost-to-PAV ratio decides
  total-loss candidacy at an instruction-dependent 66–80% threshold; the
  known QDOS instruction sets an 80% ceiling that caps authorisation,
  never costing; settlement on a total loss = PAV − salvage; EVA's
  observed `Equity in repair` (£134.99) equals 0.80 × PAV − gross repair
  cost on the recorded figures, evidencing the 80% arithmetic. The skill
  rule "a ratio without a costed repair total is not evidence" becomes a
  UI rule below. (The observed `% of PAV` of 77% does not exactly match
  any recomputation from the visible figures; the ratio basis is Open
  decision 9, not something to guess.)

Design scope:

- **What it is.** A review-time sensitivity aid, not data entry. The
  engineer drags a PAV value across the case's candidate range; read-only
  derived figures update live: repair-cost-to-PAV percentage, position
  against the applicable threshold/ceiling (as text, not colour), equity
  in repair, and — on total-loss outcomes — the indicative settlement
  (PAV − salvage). It shows how sensitive the recorded outcome is to
  the PAV opinion.
- **Where it sits.** The assessment surface's Valuation section — the
  place the assigned engineer reviews valuation and outcome, which is
  where the operator locates review of automated detail. Rendered when
  the case holds valuation evidence and a costed repair total. Whether a
  copy also renders anywhere else is Open decision 10.
- **Data.** Slider range from the recorded valuation evidence — external
  valuation-service figures once the EXT-10/EXT-13 adapters exist, guide
  evidence figures (CAP/Glass's/Cazana retail and trade) until then —
  plus the chosen figures; default thumb at the engineer's value. Inputs:
  repair cost total (derived from estimate lines — never typed), salvage
  value, chosen figures, valuation evidence, threshold (source unresolved
  — Open decision 9). No figure is ever invented; absent evidence renders
  the missing-evidence state, never a guess.
- **Review aid only.** The slider writes nothing. The mutation paths
  remain the ordinary assessment save actions, actor-attributed. The
  slider's value is never auto-copied into an input.
- **Missing-evidence states.** No costed repair total ⇒ no ratio is shown
  (the skill's rule verbatim); the panel names the missing item instead.
  No guide evidence ⇒ no range ⇒ the slider is absent with a named
  missing-evidence state. Never a magnitude guess.
- **Conformance.** `input type="range"` paired with a labelled numeric
  input (keyboard and screen-reader parity), tabular numerals, visible
  threshold text, no animation, no new colour tokens, reduced-motion and
  forced-colours safe. It is an ordinary Pegasus control — the
  `.send-action` divergence does not extend to it.

## Sequencing and activation preconditions

Stages, each independently reviewable; A is independent, B gates C–G:

- **A — Connector close-out** (external repo, can start immediately):
  § Channel server close-out items 1–8. Ends connector development.
- **B — Specification and authority** (this plan's review): the
  remaining operator decisions below; the AI-09 pull-forward is decided
  (2026-08-03, the MCP-01–04 implemented-but-gated precedent) and is
  recorded via a `NOW.md` claim at take-up and a new ADR covering: the Send to AI transport slice, the channel boundary
  (pointer out / content back as direct Actor writes), the direct-write
  model with logging parity, the widened Actor tool inventory and
  `automation.assessment` scope, the required AI-09 contract rewording
  in `docs/requirements.md`/`docs/capabilities.md`, and the
  DevelopmentOffline-only gate. No Pegasus code before B completes.
- **C — Core slice**: work-request lifecycle, ports, unit tests,
  migration for its table.
- **D — Web adapter and panel wiring**: gate, options, transport
  adapter, POST handler, panel states incl. `completed`, Administration
  toggle, activity records, integration tests.
- **E — Actor assessment toolset**: implemented under the companion plan
  `docs/temp-plans/mcp-assessment-toolset.md` (its slices 1–3); this
  plan's round trip depends on it but does not duplicate it.
- **F — Review aids and PAV slider**: the `completed` panel state's
  activity link, the review presentation of unconfirmed automation
  values, and the slider component on the Valuation section. **Blocked**
  behind the companion plan's assessment Core model and the UI-15
  re-entry review (Open decision 6 governs the interim).
- **G — Round-trip evidence run**: one recorded DevelopmentOffline run —
  real Claude Code session with `--dangerously-load-development-channels`,
  both `Features:SendToAi` and `Features:AutomationMcp` enabled, send →
  channel event → Actor case read → attributed assessment write →
  channel reply → `Completed` on reconcile — recorded per
  `docs/operations.md`, folding into the queued tier-5 MCP
  external-client evidence item in `NOW.md`.

What can honestly be built/tested now versus what waits: A now; C–D after
B, fully testable behind gates without any live service; E under the
companion plan; F after the assessment Core model and the UI-15 re-entry
review; G after C–E. **Activation** — linking the surface into navigation, enabling
any profile beyond DevelopmentOffline, or claiming AI-09 delivered —
waits for: the ADR and allocation change, operator acceptance of the
surface (including the recorded `.send-action` contrast shortfall,
2.3–4.2:1 against the required 4.5:1 — it does not block gated local
work, but it must be resolved or explicitly accepted before the control
is put in front of staff, because acceptance is a browser/accessibility
evidence claim), and a decision on whether a research-preview channels
transport can ever carry more than local evidence runs (recorded
position: it cannot; production activation would need a non-preview
transport decision under the AI-09 contract).

## Test plan

Pegasus side (parallel LocalDB pattern, per-test
`IntakeWebApplicationFactory`, `[Trait("Category", "SqlServer")]`, no
collection fixtures — matching `AutomationMcpIngressTests`):

- `Pegasus.Core.Tests` (tier 2): work-request lifecycle including
  idempotent create, cancel-with-reason, expiry; completion/failure
  transitions from typed reply outcomes; cancellation never undoes
  recorded writes (it closes the tracking record only).
- `Pegasus.IntegrationTests` (tiers 4–5): gate-off exposes no send
  behaviour (mirror of `GateOffExposesNoAutomationSurface`); gate-on
  send creates the work request, action history, and calls a
  `FakeChannelReceiver` test double (127.0.0.1:0, asserting bearer,
  schema_version, body shape); connector 401/refused/5xx ⇒ `Failed`,
  case unchanged, visible panel state; duplicate operation key replays
  idempotently; reconcile against a fake connector reply flips
  `Completed` and writes history with the correlation id;
  Administration toggle refuses new hand-offs and records reason.
  (Actor write-path tests live in the companion plan.)
- Browser/accessibility: the assessment routes remain outside the axe
  theory (no seeded case fixture) — manual verification per the design
  pass, re-run for the `completed` state and the slider; no acceptance
  claimed.

Connector side (vitest): § Channel server close-out item 7.

## Docs impact

| File | Change |
| --- | --- |
| `NOW.md` | New task claim line(s) per stage taken |
| `docs/adr/` | New ADR (stage B): transport slice, channel boundary, direct-write model and logging parity, Actor tool inventory widening, AI-09 contract rewording, gate |
| `docs/capabilities.md` | AI-09 activation-note update recording the gated implementation, on product authority only |
| `docs/architecture.md` | Send to AI boundary: Core owner, adapter, the external connector as a non-owned client |
| `docs/operations.md` | Evidence-boundary row for the channel connector; setup runbook; evidence-tier record for stage G |
| `docs/open-decisions.md` | The § Open decisions items that stay open after review |
| `design/README.md` | `completed` panel state and PAV slider component contract; § Tokens divergence record untouched unless the contrast decision changes it |
| `pegasus-claude-channel/README.md` | Schema, evidence log, idempotency, Pegasus configuration cross-reference, version pinning |

## Non-goals

- Claude never sends an assessment outward and never confirms a
  professional finding: automation writes land as attributed, unconfirmed
  values reviewed by the engineer the case is assigned to; report
  approval and outward dispatch remain human acts.
- No business payload through the channel; no
  `claude/channel/permission`; no second policy engine; the connector
  and workspace skills own no Pegasus policy.
- No activation outside DevelopmentOffline; no navigation link for the
  assessment surface; no production deployment claim.
- No Outlook mailbox or Box mutation anywhere in this work.
- No direct Anthropic (or other) model API integration; no report
  generation; no EVA replacement; no `AI Assessor` surface.
- No new colour tokens; `Send to Claude` remains the only
  provider-branded label, scoped as already recorded.

## Evidence-tier claims

Per `docs/operations.md` § Required evidence tiers: stage A yields
connector-repo test evidence (outside Pegasus tiers); C–E yield tier 2
(Core), tier 4 (LocalDB persistence/migration), and tier 5 (real HTTP
Web/MCP caller) local evidence; G yields one recorded
DevelopmentOffline integrated run with a real external client. None of
these is a deployment, activation, browser-acceptance, or
operator-acceptance claim — a registration, a green build, and a
deployed feature remain different claims, and each stage's PR names
exactly which tier it traversed.

## Open decisions

Decided 2026-08-03 (recorded, no longer open): **1** — AI-09
implementation proceeds now as gated work; it is this task's assignment
(the ADR still records the AI-09 contract rewording: proposal-only worker →
direct-writing worker reviewed at assignment). **2** — content returns as
direct Automation Actor writes; both the proposal tool and payload-in-reply
are off the table. **8** — PAV is pre-accident value.
3. Interim `Failed`/retry budget and work-request expiry duration —
   no repository source defines them.
4. Naming: `Features:SendToAi` and `SendToAi:*` configuration keys
   (vendor-neutral, recommended) versus Claude-branded keys.
5. The readiness rule that makes a case eligible to send (which
   outstanding requirements block the panel).
6. Stage F interim before the assessment Core model lands: panel links
   to the activity view only (recommended), or hold stage F entirely.
7. Connector JSONL evidence-log retention and location beyond
   "local-only, gitignored".
9. PAV ratio basis (gross repair cost inc/ex VAT — EVA's observed 77%
   does not recompute cleanly) and the threshold source (per-principal
   setting? per-instruction? QDOS 80% is the only evidenced example; no
   Core field exists today). The planned valuation-service integration
   (EXT-10/EXT-13) should name which figures the API supplies —
   retail/trade/PAV — when it is contracted.
10. PAV slider placement: the assessment Valuation section is now the
    recommended home (review at assignment); confirm, plus its
    step/rounding. Related: companion plan D10 decides the Suggestions
    screen's fate.
11. The `.send-action` contrast shortfall (2.3–4.2:1 vs 4.5:1): deepen
    the gradient, take dark text, or accept as recorded — required
    before activation puts the control in front of staff.
12. Panel copy: the built markup says "Sent. Suggestions appear here
    when they return." — with direct writes the second sentence is
    wrong and needs design-conformance rewording (e.g. "Sent. Changes
    will appear on this case for your review."), alongside the existing
    pointer-versus-content wording question.
