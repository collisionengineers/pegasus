---
status: accepted
---

# ADR-0021: Automation Actor direct-write assessment contract and the Send to AI transport slice

## Context

ADR-0011 and ADR-0013 clause 10 fixed MCP as a management/development-controlled
ingress for one named vendor-neutral Automation Actor invoking only its
approved inventory of ordinary operational Core use cases. The merged MCP-01–04
ingress implemented that contract with nine tools, per-area scopes, permanent
attributable history, and a two-layer kill switch, all behind a composition
gate limited to DevelopmentOffline evidence runs. Its settled
identity/authentication/tool-inventory contract lived only in
architecture.md/operations.md prose after its temp plan was deleted; `NOW.md`
queued promoting it to an ADR.

Two operator decisions on 2026-08-03 reshaped the remaining work
(`docs/temp-plans/mcp-assessment-toolset.md` and
`docs/temp-plans/send-to-claude-channel-integration.md`):

- The AI provider modifies the details of an assessment **directly** in order
  to prepare a report. Human review is the existing case workflow itself:
  every case is manually assigned to an engineer, and that assignment is where
  automated detail is reviewed. A staged-suggestion store with an in-app apply
  gate was considered and rejected as a duplicate of a review the workflow
  already guarantees.
- Automations are designed and safeguarded outside Pegasus (Claude Desktop;
  skills, prompts, and tasks built in Cowork and run on Automations). Pegasus
  owes exactly two things: a fully comprehensive toolset, and clear logging of
  every automation action with the same rigor as any human action.

The prior AI-09 contract wording ("a scoped worker may lease it and return
only a proposal", named-Engineer accept/amend/reject) conflicted with the
direct-write decision and is reworded by this ADR under product authority.

## Decision

1. **The Automation Actor contract is confirmed as implemented.** One seeded
   OpenIddict client-credentials registration authenticates the single
   vendor-neutral Automation client; `ActorKind.Automation` holds exactly
   `PerformCasework`; the streamable-HTTP `/mcp` surface registers only when
   `Features:AutomationMcp` enables it in the DevelopmentOffline profile;
   every tool wraps an existing Core use case behind per-area scopes with
   permanent attributable history, `mcp:` idempotency keys, and the
   Administrator kill switch (~seconds to take effect).

2. **Direct writes with logging parity.** Automation Actor assessment and
   case-detail writes go through the same Core commands, validation, edit
   lease, operation-key replay, and expected-version guards as a staff save,
   attributed to the automation identity. Every write lands in permanent
   action history with actor identity, operation key, correlation identifier,
   and per-field before/after evidence, exactly as a human action does, and
   is visible in the Administration Automation activity view. The review
   point is the engineer the case is manually assigned to.

3. **Findings are recordable, not confirmable.** The automation may record
   `assessment.outcome`, `assessment.legal_status`, salvage category and
   value, and the chosen valuation figures as unconfirmed working values. A
   staff save records confirmed values; confirmation of a professional
   finding is staff-Engineer-only (the `EngineerFindingPolicy` precedent).
   This preserves the permanent requirements.md boundary: no model, skill,
   prompt, or external source issues an *accepted* case, engineering,
   economic, legal, or report outcome.

4. **The tool inventory widens to fourteen tools and a fourth scope.**
   `pegasus_assessment_get` and `pegasus_assessment_update` under the new
   `automation.assessment` scope; the previously excluded candidates
   `pegasus_case_update_details`, `pegasus_eva_bundle_generate`, and
   `pegasus_eva_handoff_status` are reinstated under `automation.cases` —
   EVA generation pushes work *into* the human review point and records the
   CASE-21 proxy event exactly as a staff-triggered generation does.
   Structurally absent, on purpose: any finding-confirmation tool, any
   report-approval tool, and any tool that dispatches anything to a
   customer, principal, or external party. Estimate derivation (totals,
   worklists) stays absent until its formulas hold accepted EXT-09 authority.

5. **The Send to AI transport slice (AI-09) is implemented as gated work**
   on the MCP-01–04 implemented-but-gated precedent. `Features:SendToAi`
   composes it in the DevelopmentOffline profile only. The channel carries
   operator chat, never business data: the hand-off is a pointer (case
   reference, request identifier, short instruction), and content returns as
   the attributed Automation Actor writes above. The Core-owned work request
   tracks `Created → HandedOff → Completed` with `Failed`, `Cancelled`, and
   `Expired`; `HandedOff` maps to the connector's forwarded claim, never to
   "the provider read it"; completion is flipped by an operator-triggered
   reconcile reading the connector's reply record; and closing a request
   never applies or undoes case content. Cancellation takes a reason.
   Duplicate sends replay idempotently; the request identifier correlates
   the staff send, the channel delivery record, the provider reply, and —
   when the automation passes it — every ingress write of the session.
   An Administrator switch refuses new hand-offs immediately, beside the
   existing Automation client kill switch for the return path.

6. **AI-09 contract rewording.** requirements.md § Targeted sending and
   reviewed AI proposals and the AI-09 capability row are reworded from
   proposal-only worker to direct-writing worker reviewed at assignment, as
   carried in the same change as this ADR. AI-07 query proposals remain
   proposals. The `.mcpb`/channel packaging remains an external client and
   may be revisited by the report-renderer integration work without
   reopening this contract.

## Consequences

- Pegasus-side safeguards are the gates, scopes, leases, versions, closed
  field vocabulary, structural absence of confirmation/dispatch tools, and
  logging parity; automation behaviour itself is designed and safeguarded
  outside this repository, and a workspace, skill, prompt, or model never
  becomes an application policy owner.
- The unconfirmed mark and the readiness rail make automation-recorded
  values visibly pending review on the assessment surface; the UI-15
  activation task owns the staff save paths and the full review
  presentation.
- Everything remains composition-gated off outside DevelopmentOffline
  evidence runs. No tier-5 external-client evidence, deployment, activation,
  or operator acceptance is claimed by this ADR; the queued tier-5 run
  covers the full fourteen-tool inventory in one recorded session, and
  production activation would additionally need a non-preview transport
  decision under the AI-09 contract.
