---
id: ADR-0013
status: accepted
date: 2026-07-30
supersedes: []
superseded_by: []
related_capabilities: []
related_frd: [frd-01, frd-02, frd-06, frd-07, frd-08, frd-12]
tags: [qdos, contract]
---
# ADR-0013 — QDOS alpha implementation contract

**Status:** Accepted (2026-07-30).

## Context

The reviewed QDOS alpha plan exposed contradictions between retained proposals, current product requirements, and the intended implementation boundary. This decision settles only those disputed clauses. It does not accept the delivery plan as a whole, prove implementation, authorize an Azure or other external operation, or weaken any caller, evaluation, security, or operator-acceptance gate.

## Decision

1. **Image-led intake remains pre-Case.** Image-only material with a usable normalised VRM creates an Image intake and Image Intake Reference, not a Case. It may associate with one eligible instructed pre-report Case on an unambiguous normalised-VRM match without contradictory identity evidence, or by a reasoned authorised-staff decision. Otherwise it awaits definitive instruction. Association is reasonedly reversible before report delivery; both identities, source histories, and every relationship event remain permanent.
2. **Vehicle checks are global progression gates.** Vehicle identity/specification, vehicle-history/risk, and market valuation are all mandatory before staff may accept review and expose a Case in the Engineers queue. A genuinely unavailable or contractually inapplicable check may be satisfied only by a named, reasoned authorised-staff exception recorded in permanent history; no missing result is silently treated as success.
3. **Case readiness gates are mandatory.** Instruction completeness, image completeness, and staff review are three separate mandatory gates for every Case. Provider policy may define the accepted evidence for a gate but may not remove the gate. Named-Engineer assignment and reassignment remain EVA-owned through `0.1.0-alpha.1` and transfer only with the accepted `1.0.0` Engineer-workbench capability and caller evidence.
4. **Cancellation remains manual in the alpha.** Mailbox automation neither finalizes cancellation nor mutates a Case. An associated cancellation message enters a visible manual staff decision path. Authorised staff may hold, confirm `Provider cancelled`, or release only after the message is reasonedly recategorised, unlinked, or reassociated. Every classification, association, correction, actor, time, reason, and evidence item remains history.
5. **Box recovery is staff initiated.** A Box custody failure retains the immutable allocated Case/PO, leaves the Case `Not ready`, and exposes the failed target and outcome to authorised staff for idempotent retry. No scheduled background or automatic business retry is permitted.
6. **The dashboard term is `New cases today`.** It counts instructed Cases created since Europe/London midnight, including Cases later closed that day. It excludes Image intakes, Triage, `Needs sorting`, and `Blocked intake`, and remains distinct from `Due today`.
7. **Case/PO sequences have a four-digit maximum.** The normal reference uses a three-digit minimum and expands from `001`–`999` to `1000`–`9999`. Allocation fails closed after `9999`; it never wraps, starts a fabricated new year, or reuses a value.
8. **The focused EVA bundle exports all eligible images.** Pegasus includes every eligible custody-confirmed Case-vehicle image in deterministic manifest order. Staff-confirmed third-party vehicle evidence is excluded; recognizer suggestions alone never exclude an image. Pegasus exposes no alpha image-selection or ordering control. EVA owns downstream selection and ordering until its accepted replacement, when those decisions move to the Engineer report-generation surface.
9. **`AI-05` remains deferred to `1.0.0`.** It is advisory image-readiness assessment of the current Case image set, not image-based repair assessment. It neither changes Case state nor creates an AI Proposal and gains no alpha caller or surface.
10. **MCP has one non-human actor boundary.** MCP is a management/development-controlled ingress for one named, vendor-neutral Automation Actor. It uses its own authentication, authorization, scopes, rate limits, attribution, and permanent history; ordinary staff never receive MCP access and no staff identity is impersonated. Claude Desktop may be a compatible client but does not own the actor identity or Core policy.
11. **The domain action is `Send to AI`.** Provider-specific UI wording does not redefine the action. Claude is the current provider candidate for that later user-triggered action; Microsoft Foundry is the intended candidate platform for later AI query-response proposals. Exact client, model, transport, credential, evaluation, recovery, cost, and caller choices remain activation gates.
12. **Login protection is transient throttling.** The alpha uses generic authentication failure plus the accepted per-source and global request limits; it does not introduce persistent ASP.NET Identity account lockout.
13. **The local email evaluation workbench remains separate.** It may supply accepted evaluation evidence but is not a QDOS alpha product surface, route, deployment unit, or acceptance checkpoint.
14. **Azure targets remain unresolved until exact approval.** Subscription, resource group, region, Entra groups, SQL administration and migration identities, Box identity/root, alert recipients, budget scope, deployment commands, and destructive dispositions require fresh exact-target approval. No placeholder in the plan is executable authority.

## Supersession and precedence

Within this scope, these clauses supersede contrary active or retained wording that describes image-led material as a Case, makes any of the three Case gates optional, gives ordinary staff MCP access, limits the Case sequence to `999`, exposes alpha EVA image-selection controls, permits automatic cancellation or Box business retry, or labels the domain action `Send to Claude`. ADR-0011 remains the Automation Actor boundary; ADR-0012 remains the mileage-estimation policy; their other clauses are unchanged.

## Deferred capability impact

- **Named deferrals:** `AI-05` and the Pegasus Engineer workbench remain at `1.0.0`; `AI-09`, broader AI query-response proposals, EVA API/replacement, the provider API, broader mailbox management, and the local email evaluation workbench remain separately gated.
- **Preserved seams and identities:** Image Intake Reference, instructed Case/PO, source occurrence, source-to-Case relationship history, three independent vehicle-check results/exceptions, three independent Case-readiness decisions, Automation Actor/client/action identities, AI work request/proposal/review identity, and EVA package/manifest identity remain distinct.
- **Excluded:** no dormant AI or EVA control, generic model transport, ordinary-staff MCP route, automatic cancellation, automatic Box business retry, email-evaluator product route, Azure target, credential, deployment, migration, or destructive operation is created by this decision.
- **Activation evidence:** each deferred external or AI capability still requires its exact contract, identity and authorization boundary, representative evaluation where applicable, failure/recovery proof, real caller, exact-target approval, and operator acceptance.
- **Irreversible choices:** allocated Case/PO and Image Intake References are never reused; Case sequence values do not exceed `9999`; relationship and action history is append-only; the Automation Actor never impersonates staff; and accepted source evidence is not deleted or rewritten to simplify a later workflow.
