# Open decisions

This is the sole register of material unresolved decisions. Most product decisions reviewed through 2026-07-25 are not reopened here. The [requirements](requirements.md) and [capability inventory](capabilities.md) own scope context; deliberately deferred, conditional, and `Unclear` capabilities are not current-scope questions merely because their activation evidence is recorded here.

Evidence tiers are defined once in [operations](operations.md#required-evidence-tiers); no stronger state is inferred below.

Accepted decisions move to an [ADR](adr/README.md) or their canonical owner. Delivery status does not belong in this register.

[ADR-0013](adr/0013-qdos-alpha-implementation-contract.md) settles checkpoint 1's clause-specific QDOS implementation and Razor/Worker/MCP caller boundary, the separately owned evaluator allocation boundary, and the post-alpha repository-policy deferral. It does not close the evidence-dependent questions below or prove implementation, a caller, deployment, live verification, or acceptance.

Staff roles and access, principal and historical case-party identity, the Case/PO and case-type rules, Triage’s normal workflow, named terminal outcomes and reasoned reopen, exclusive one-case edit actions, immutable source-occurrence/dispatch identity, and reasoned source/Case or outbound-evidence reassociation are settled. Their canonical clauses are [principal and case-party identity](requirements.md#principal-reference-organisation-and-case-party-identity), [source occurrence and dispatch](requirements.md#source-occurrence-and-dispatch-identity), [matching and reversible association](requirements.md#matching-conflicts-and-reversible-association), [Triage](requirements.md#normal-workflow-and-completion-evidence), [case lifecycle](requirements.md#lifecycle-closure-and-correspondence), [case edit authority](requirements.md#case-edit-authority-and-recovery), [staff role access](requirements.md#staff-role-access-matrix), and [outbound correspondence evidence](requirements.md#outbound-correspondence-evidence). This register may block only the named automatic predicate, transport, credential, or activation detail; it must not reopen those settled behaviors.

## First production journey and release sequencing

Decided 2026-08-02: the first live journey is the full QDOS cutover — a genuine
QDOS instruction email through intake, review, Case/PO allocation, Box custody,
and the EVA handoff bundle. [`NOW.md`](../NOW.md) "Path" owns the ordered
critical path, the non-blocking capability set, and the acceptance boundary
(OPS-23/OPS-25 close `0.1.0-alpha.1`). The remaining evidence gate on that
path is item 4 (extraction
thresholds) below. The Box production custody boundary was decided 2026-08-02:
folder `405543781910` ("pegasus") is the production custody root and all case
folders are created only under it (owner:
[operations](operations.md#approved-box-integration-test-target); the deployed
configuration applies the decided root at the next approved deployment).

## QDOS alpha activation details (migrated from the retired delivery plan)

Still-open questions preserved from the deleted
`research-and-planning/qdos-full-alpha-delivery-plan.md`; each blocks only the
step it names.

1. **VRM recognition engine (`INT-17`)** — Which recognition mechanism, if any,
   does alpha adopt: in-process model bytes (reviewed origin/licence/hash/RIDs,
   no Python service or runtime download) or one guarded external adapter (with
   an image-egress/credential/retention/latency/cost contract)? A frozen genuine
   labelled cohort + untouched holdout and preaccepted accuracy/abstention gates
   come first; if nothing meets the gate the capability blocks rather than
   falls back.
2. **`INT-31` upload-link limits** — Exact token lifetime, aggregate and
   per-file byte limits, file count, allowed content types, per-token/per-IP
   rate, one-time vs reuse, and revocation/expiry error contract. Interim bound:
   the existing aggregate 10 MB intake limit; hashed 256-bit token; anonymous
   `/Uploads/{token}` form; no case disclosure.
3. **External credential ownership** — For each credential (Box, DVLA/DVSA, any
   VRM service, the Exchange application RBAC grant): the named operations owner
   and the provider-specific issue/rotate/revoke/emergency-disable procedure.
   The contract shape (Key Vault URI/version only, prove-then-cut-over, no
   local fallback) is settled.
4. **QDOS extractor acceptance thresholds (`INT-21`)** — Per-field
   accuracy/coverage thresholds and truth representation for the ten fields
   (Claimant Name, Claim Number, VRM, Make, Model, Mileage, Accident
   Circumstances, Incident Date, Instruction Date, Inspection Address), from an
   operator-reviewed cohort + untouched holdout. Zero false case creation is
   invariant.
5. **Telemetry sampling and daily cap** — Exact sampling rate and daily
   ingestion cap (31-day interactive retention is settled), accepted from
   measured alpha workload and cost evidence; the deployed adaptive sampling
   and 0.1 GB/day cap are interim.
6. **Azure budget wiring** — Billing scope, notification contacts/Action Group,
   and budget start/end dates were wired in the executed release (£75/month
   alert-only monitoring; see
   [operations](operations.md#production-environment)). Still open: a refreshed
   UK South GBP forecast from measured alpha workload — no fixed monthly
   ceiling or accepted spend range exists
   ([operator notes](operator-notes.md)); material variance from forecast needs
   a named expenditure owner's sign-off.
7. **Performance dataset ownership** — Who supplies and approves the immutable
   2,000-case performance dataset, observed document/source distribution, and
   measured peak burst that the capacity gate needs (fabricated domain data is
   forbidden; absence blocks the gate).

## Mailbox rule activation, automatic matching, and confidence display

The [Received/Sent taxonomy, mirrored Reply rule, `Other` behavior, separation
of classification from destination, and correction/reversal audit
contract](requirements.md#settled-mailbox-taxonomy-and-correction) are settled
and are not reopened here. `new-instruction-received` is a Received family with
no confirmed Sent counterpart; that direction boundary does not decide which
rule wins when several predicates match.

The classification architecture is fixed:

- Direct-provider and intermediary routes are separate Core-owned,
  code-versioned policies.
- The applicable route is the only policy owner for provider, instruction type,
  case association, and any later accepted precedence; no unaccepted rule is
  active.
- For staff forwards, outer transport provenance is retained while the proved
  original sender drives route identification.
- Stable source identity must be retained and uncertainty exposed through the
  established review outcome.
- No generic rule engine or transport-specific second classifier is to be
  added.
- QDOS direct sender identity is the exact `@qdosassist.co.uk` suffix. That
  suffix alone does not classify message type, associate a case, or apply to an
  identified intermediary.
- The Mapped Principals spreadsheet at the opaque source citation
  `../reference/imp-docs/requirementsdocs/provider-extra-info/Mapped%20Principals.xlsx`
  identifies additional principals and route candidates beyond QDOS. Every
  listed candidate remains evidence, not an activated route.

The available evidence establishes review-visible uncertainty, but not an
accepted numeric confidence score, threshold, or alternative confidence
display. None should be inferred.

The first additional-provider route cohort is allocated to `0.2.0`; the broader
classified-email workspace and email MCP cohort is allocated to `0.3.0`.
Neither target closes this evidence gate.

Accepted source-labelled results from the separately delivered evaluator may satisfy a named cohort or holdout prerequisite. Its route, command, reviewer workflow, and UI mechanics are not QDOS callers or checkpoint evidence and do not close route activation, production-intake, Worker, Graph, or operator-acceptance proof.

| Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|
| For each proposed route: genuine examples; exact sender/intermediary identity; finite category predicates and exclusions; automatic incoming-case, Triage, and exact Sent-item matching predicates; and named no-match/conflict/ambiguity outcomes. | Premature activation could misclassify a message or associate the wrong case, Triage, or delivery evidence. | Keep the route and each automatic matcher inactive until its exact predicates and conservative outcomes are accepted. | Are the route’s category and automatic-matching predicates, exclusions, and ambiguity outcomes accepted? |
| An explicit multi-rule selection model, operator-reviewed conflict cases, and any proposed confidence display or threshold. | An invented precedence or threshold could conceal uncertainty or override the settled direction taxonomy. | Route multiple plausible matches to the established review outcome; infer no score, threshold, or winning rule. | What exact precedence and confidence/ambiguity behavior applies when more than one predicate matches? |
| Named policy author/reviewer/activator/rollback roles; version/effective-time rules; and exact cohort re-evaluation and downstream-notification behavior. | A rule change could silently reinterpret history or cause unreviewed downstream changes. | Preserve the original decision; permit no cohort re-evaluation or downstream notification until its explicit operation and scope are accepted. | Who controls a rule version, and what approved re-evaluation or notification follows a change? |
| An operator-reviewed genuine cohort and untouched holdout; accepted activation and rollback thresholds; exact mailbox/folder identities; and least-privilege Graph scopes, including any separate Sent Items access. | Unrepresentative evidence or overbroad access could activate unsafe matching or expose an unapproved mailbox/folder. | Keep activation local and non-mutating; grant no additional Graph mailbox, folder, or Sent Items scope. | Are the holdout, thresholds, mailbox/folder boundary, and exact Graph scopes accepted for this caller? |

## EVA manual handoff activation

Two observed examples establish this key order:

1. `Work Provider`
2. `VRM`
3. `Vehicle Model`
4. `Claimant Name`
5. `Reference`
6. `Incident Date`
7. `Instruction Date`
8. `Inspection Date`
9. `Inspection Address`
10. `Accident Circumstances`
11. `VAT Status`
12. `Mileage`
13. `Mileage Unit`

The examples establish the presence and order of `VRM`, but do not by themselves prove its source-field mapping, a VRM-specific confidence rule, or permission to create or alter EVA work.

| Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|
| Operator acceptance of every source-field mapping, especially whether `Reference` maps to EVA Claim No rather than Case/PO; null and empty handling; date and mileage normalization; image selection, naming, and order; treatment of uncertain VRM values; and a real drag-and-drop run. | An incorrect or guessed mapping could create or alter EVA work with the wrong claim, vehicle, dates, mileage, or images. | Keep generation review-gated. Do not allow a guessed mapping, including a guessed VRM mapping, to create or alter EVA work. | Has an operator accepted every mapping and normalization rule through a real drag-and-drop run? |

## EVA API activation (`0.7.0` / `EXT-04`)

Direct EVA API use is allocated only as an optional, non-blocking `0.7.0`
branch. Vendor test credentials exist, but the route remains blocked until EVA
developers deliver a vendor-confirmed usable operation meeting the accepted
contract. The retained vendor schema is non-authoritative reference evidence:
it does not select an operation or grant permission to call EVA.

In particular, no allowed accepted source currently establishes a proxy-only
case/vehicle/inspection fetch, a create-with-children operation, its
parent/child validation or atomicity, a separate picture-upload contract, a
report-with-PDF handoff, a structured Pegasus success/failure model, or the
meaning of any returned identifier. None of those observations may create,
select, or alter a Pegasus case/reference.

| Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|
| A vendor-confirmed usable operation and exact direction/scope; request and response contract; identity and authorization target; validation and atomicity; attachment/picture/report-PDF distinctions; correlation identifiers; structured success/failure; idempotency; recovery; coexistence or migration; and live evidence. | An assumed API could disclose, duplicate, lose, or corrupt EVA work, attach evidence to the wrong record, infer a Pegasus identity, or prematurely remove the manual path. | Continue the deterministic manual JSON/image/manifest handoff. Make no EVA call and infer no case/reference or external success from the supplied schema. | Which exact EVA operation, if any, is vendor-supported, caller-proved, and accepted with these boundaries? |

## External data, submission, and report contracts

These are independent blockers, not one integration decision. `VEHICLE DATA`
observed in EVA, Parkers, and AutoTrader remain evidence rather than selected
adapters.

| Decision | Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|---|
| Glass's direct repair-estimate access | Accepted licensing, API or embedded-access terms, technical access, and cost. | Repair-estimate integration and its commercial viability cannot be established. | Do not select or represent Glass's as an available direct estimate adapter. | Are Glass's licensing, access mode, technical contract, and cost accepted for direct repair estimates? |
| Direct valuation access | Accepted direct-access contracts and terms for CAP, Glass's, and Cazana, including the basis for selecting any adapter. | Valuation sourcing, permissions, and cost remain uncertain. | Treat all three as candidates only; do not imply that any valuation adapter is selected. | Is there an accepted direct-access and commercial contract for a selected valuation source? |
| Provider API tenancy and wire contract | An accepted client/tenant representation, exact routes, headers, schemas, attachment encoding, request limits, throttling/error contract, administration workflow, named clients, and rollout. The settled isolation boundary remains one principal-scoped client with own receipt/status/result only. | Treating an email domain, intermediary, or shared external tenant as the API principal could disclose another principal's work or create a second policy engine. | Keep the API absent. Use stable Pegasus principal identity as the isolation boundary and infer no tenancy model from provider-domain evidence. | What exact provider API contract and client/tenant representation preserves the accepted principal-scoped isolation boundary? |
| `provider_domain_key` migration or retirement | An authoritative source definition and owner; current and predecessor uses; mapping to stable Pegasus principal/route/evidence identities; collision and unknown handling; cutover, rollback, retention, and exact retirement proof. No allowed accepted source currently defines this name as a Pegasus identity. | Importing, translating, or deleting an undefined key could misattribute a principal, destroy provenance, or leave a hidden compatibility dependency. | Do not create, migrate, map, alias, or retire `provider_domain_key`. Keep provider-domain evidence versioned and separate from principal and route identity. | Is there any approved source and consumer that requires this key, and if so what reviewed migration and retirement contract applies? |
| Provider report submission and delivery | Exact provider API formats, delivery contracts, and provider identities. | Reports or work could be sent in an unsupported format or to an unproved identity. | Keep provider delivery behind review or existing supported procedures until each provider contract is accepted. | Has the exact format and identity contract been accepted for the provider being activated? |
| DVLA/DVSA vehicle and MOT lookup | Selected provider/API and licence; exact make/model/year/engine/fuel and MOT/mileage fields; credentials; limits/rates; error and stale-data behavior; target; integration of the accepted mileage-estimation contract; and caller proof. | A guessed field or stale/failed result could overwrite confirmed vehicle data or present an estimate as supplied fact. | Keep live lookup disabled. Preserve source-labelled suggestions and return `Unavailable` when approved local replay evidence is absent. | Is the exact lookup contract accepted for the named provider and caller? |
| Post-report query and dispute lifecycle | Allowed states/transitions and actors; case/report/reply-chain evidence; correction/reopen and due/chaser interaction; response proof; closure; and dispute resolution. | A mailbox event could silently change case state, close work prematurely, lose a correction, or create a duplicate case/reference. | Preserve the correspondence against the existing case for staff review; let no Outlook adapter decide lifecycle or closure. | What exact CASE-23 lifecycle governs a received query/dispute through Engineer response and reasoned completion? |
| Audatex PDF ingestion | Representative PDF variants and accepted field-mapping evidence. | Variant layouts could produce incomplete or incorrect extraction. | Do not activate generic Audatex PDF mapping from unrepresentative examples. | Have the supported Audatex PDF variants and their mappings been accepted from representative evidence? |
| Mandatory global vehicle checks | Global requirements are settled as vehicle identity/specification, vehicle-history/risk, and market valuation. All three require a result or explicit exception before Engineers-queue eligibility. The authorised staff reviewer records each exception as a named, reasoned Case action. Each provider/route still needs its exact source, required result, and unavailable/failure contract. | A Case could proceed to an Engineer without a globally required result, or a provider-specific behavior could silently override the common baseline. | Preserve the global checks; use source-labelled `Unavailable` or approved local replay while live callers are unaccepted; retain unmet checks as `Not ready` rather than inventing a result. | What unavailable/failure contract applies to each global check for each provider/route? |
| Report wording | Accepted wording for salvage Categories N, A, B, and N/A; recovery and storage; the final statement of truth; and named qualifications. | Reports could contain incomplete, unauthorized, or inconsistent statements. | Keep the affected wording review-gated and do not invent missing text or qualifications. | Has the complete wording and qualification set been accepted for report generation? |

## Send-to-AI transport experiment (`1.3.0` / `AI-09`)

`AI-09` is allocated to `1.3.0` and preserves one Core-owned work-request,
proposal, and review contract. The target does not activate a transport; any
transport must conform to that contract rather than weakening the queue.

The current direction distinguishes these tracks:

1. one named, vendor-neutral Automation Actor performing ordinary operational Core actions through approved MCP tools, with Pegasus attribution and history; Claude Desktop may supply the initial client evidence without owning that actor identity;
2. a user-triggered domain action `Send to AI` that may return only a proposed repair specification, never a report document or direct Case mutation; Claude is the current provider candidate, but provider-specific UI wording does not redefine the action; and
3. Microsoft Foundry as the intended candidate, pending evaluation, for later AI query-response proposals.

Direct Anthropic or other model API integration is neither an assumed implementation nor a fallback. Any `Send to AI` transport must satisfy the Core work-request, proposal, review, identity, recovery, and cost contract. A later provider change must not change the `Send to AI` domain action or stored identity.

| Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|
| Actual supported automation client and Foundry support; authentication and Automation Actor identity; the exact approved operational MCP inventory; user-triggered assessment selection and current provider label; leasing, cancellation, recovery, proposal return, cost, model evaluation, and named Engineer review. | An unsupported client or model could weaken actor accountability, queue recovery, proposal review, or create an unintended direct-model dependency. | Retain one vendor-neutral Core contract; permit only the documented operational MCP and proposal paths; do not activate either AI transport until it proves that contract. | Which specific client and Foundry model/transport choices prove the complete Core contract with acceptable identity, recovery, proposal return, evaluation, and cost? |

## Future custom assessor

A future fine-tuned custom assessor is an explicit unallocated deferral. Its
model choice and hosting—locally operated or rented infrastructure—remain
unresolved. No imported workspace, experiment, model, prompt, or evaluation
selects a Pegasus runtime, caller, deployment, or business-policy owner.

| Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|
| Accepted model purpose and evaluation suite; source-data and human-approval contract; selected local or rented hosting boundary; cost, licence, capacity, security, recovery, deployment, and real Pegasus-caller evidence. | A premature model or hosting choice could create an unsupported runtime, unreviewed data flow, or duplicate Core policy owner. | Preserve the deferred seam only. Do not scaffold a model integration, hosting target, or deployment unit. | Which evaluated custom-assessor model and hosting boundary should Pegasus adopt, if any? |

## Later operator UI capabilities

Operations-first is selected for the QDOS-alpha shell. Worklist-first and Case-first directions are retained only as comparison evidence and do not override the complete design requirements.

| Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|
| Completion of the full design route for each later UI capability, using the canonical [design process](../design/README.md) rather than inheriting raster details. | Treating comparison material or raster details as requirements could constrain later capabilities to an unaccepted interaction model. | Keep the operations-first alpha shell. Require later UI capabilities to re-enter complete design before activation. | Has the later UI capability completed the full design route without treating comparison evidence or raster details as accepted requirements? |

## Azure ownership and retirement targets

Azure ownership changes and retirement are separate exact-target decisions. The
production replacement runbook fixes the intended production group and the
candidate predecessor groups, but dated names are not current identity proof.
Each mutation requires fresh inventory and explicit approval for the resolved
resource IDs; see [operations](operations.md#production-environment). The
executed 2026-08-02 runbook evidence is in git history.

| Decision | Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|---|
| Azure ownership change | Fresh inventory establishing the exact current target identities and names, current ownership, proposed ownership, and explicit approval for those targets. | An ownership mutation against an assumed or stale target could affect the wrong Azure resource. | Make no ownership mutation until the exact freshly inventoried targets are named and approved. | Which freshly inventoried and exactly named Azure targets have explicit approval for an ownership change? |
| Azure retirement | Fresh inventory establishing the exact target identities and names, dependencies, retirement scope, and explicit approval for those targets. | Retiring an assumed or stale target could remove a required service or leave dependent resources unmanaged. | Retire nothing until the exact freshly inventoried targets are named and approved. | Which freshly inventoried and exactly named Azure targets have explicit approval for retirement? |
