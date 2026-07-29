# Open decisions

This is the sole register of material unresolved decisions. Most product decisions reviewed through 2026-07-25 are not reopened here. The [requirements](requirements.md) and [capability inventory](capabilities.md) own scope context; deliberately deferred, conditional, and `Unclear` capabilities are not current-scope questions merely because their activation evidence is recorded here.

Intended, implemented, caller-proved, deployed, and accepted are separate evidence states. A selected design or recommended default is not proof of implementation; implementation without a real caller is not caller proof; caller proof is not deployment; and deployment is not acceptance. No stronger state is inferred below.

Accepted decisions should move to the appropriate [decision](decisions/README.md) or [change](changes/README.md) record. Delivery status does not belong in this register.

[ADR-0014](decisions/ADR-0014-qdos-alpha-implementation-contract.md) settles checkpoint 1's clause-specific QDOS implementation and Razor/Worker/MCP caller boundary, the separately owned `DOC-CON-052` evaluator boundary, and the post-alpha repository-policy deferral. It does not close the evidence-dependent questions below or prove implementation, a caller, deployment, live verification, or acceptance.

Staff roles and access, principal and historical case-party identity, the Case/PO and case-type rules, Triage’s normal workflow, named terminal outcomes and reasoned reopen, exclusive one-case edit actions, immutable source-occurrence/dispatch identity, and reasoned source/Case or outbound-evidence reassociation are settled. Their canonical clauses are [principal and case-party identity](requirements.md#principal-reference-organisation-and-case-party-identity), [source occurrence and dispatch](requirements.md#source-occurrence-and-dispatch-identity), [matching and reversible association](requirements.md#matching-conflicts-and-reversible-association), [Triage](requirements.md#normal-workflow-and-completion-evidence), [case lifecycle](requirements.md#lifecycle-closure-and-correspondence), [case edit authority](requirements.md#case-edit-authority-and-recovery), [staff role access](requirements.md#staff-role-access-matrix), and [outbound correspondence evidence](requirements.md#outbound-correspondence-evidence). This register may block only the named automatic predicate, transport, credential, or activation detail; it must not reopen those settled behaviors.

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
branch and remains blocked because no usable EVA operation has been supplied by
the EVA development team. The retained vendor schema is non-authoritative
reference evidence: it does not select an operation or grant permission to call
EVA.

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
| DVLA/DVSA vehicle and MOT lookup | Selected provider/API and licence; exact make/model/year/engine/fuel and MOT/mileage fields; credentials; limits/rates; error and stale-data behavior; target; mileage-estimation rule; and caller proof. | A guessed field or stale/failed result could overwrite confirmed vehicle data or present an estimate as supplied fact. | Keep live lookup disabled. Preserve source-labelled suggestions and return `Unavailable` when approved local replay evidence is absent. | Is the exact lookup and mileage contract accepted for the named provider and caller? |
| Post-report query and dispute lifecycle | Allowed states/transitions and actors; case/report/reply-chain evidence; correction/reopen and due/chaser interaction; response proof; closure; and dispute resolution. | A mailbox event could silently change case state, close work prematurely, lose a correction, or create a duplicate case/reference. | Preserve the correspondence against the existing case for staff review; let no Outlook adapter decide lifecycle or closure. | What exact CASE-23 lifecycle governs a received query/dispute through Engineer response and reasoned completion? |
| Audatex PDF ingestion | Representative PDF variants and accepted field-mapping evidence. | Variant layouts could produce incomplete or incorrect extraction. | Do not activate generic Audatex PDF mapping from unrepresentative examples. | Have the supported Audatex PDF variants and their mappings been accepted from representative evidence? |
| Mandatory provider and vehicle-history checks | An exact contract defining which checks are mandatory, for which provider or route, when they run, and how failures or unavailable results are handled. | Cases could proceed without required checks or be blocked by checks that were never mandated. | Do not infer a universal mandatory-check policy. Keep activation gated on an exact contract. | Has the provider-specific mandatory-check contract, including vehicle-history handling, been accepted? |
| Report wording | Accepted wording for salvage Categories N, A, B, and N/A; recovery and storage; the final statement of truth; and named qualifications. | Reports could contain incomplete, unauthorized, or inconsistent statements. | Keep the affected wording review-gated and do not invent missing text or qualifications. | Has the complete wording and qualification set been accepted for report generation? |

## Send-to-AI transport experiment (`1.3.0` / `AI-09`)

`AI-09` is allocated to `1.3.0` and preserves one Core-owned work-request,
proposal, and review contract. The target does not activate a transport; any
transport must conform to that contract rather than weakening the queue.

A later experiment may compare:

1. attended Claude Code, Cowork, or Desktop chat consuming scoped MCP work;
2. supported scheduled Claude Desktop automation polling the MCP queue; and
3. a future Collision AI Centre harness polling the queue.

Direct Anthropic or other model API integration is neither an assumed candidate nor a fallback.

| Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|
| Actual client and tool support; OAuth and actor identity; attended versus unattended behaviour; leasing, cancellation, and recovery; proposal return; and cost. | An unsupported client could weaken actor accountability, queue recovery, or proposal review and could create an unintended direct-model dependency. | Run the experiment without changing the Core contract. Discard any Claude surface that cannot satisfy it. | Which candidate, if any, proves the complete Core queue contract with acceptable identity, recovery, proposal return, and cost? |

## Later operator UI capabilities

Operations-first is selected for the QDOS-alpha shell. Worklist-first and Case-first directions are retained only as comparison evidence and do not override the complete design requirements.

| Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|
| Completion of the full design route for each later UI capability, using the canonical [design process](../design/README.md) rather than inheriting raster details. | Treating comparison material or raster details as requirements could constrain later capabilities to an unaccepted interaction model. | Keep the operations-first alpha shell. Require later UI capabilities to re-enter complete design before activation. | Has the later UI capability completed the full design route without treating comparison evidence or raster details as accepted requirements? |

## Azure ownership and retirement targets

Azure ownership changes and retirement are separate exact-target decisions. The available evidence does not provide accepted target names. Each requires fresh inventory and explicit approval before any cloud mutation; see the canonical [Azure guidance](azure/README.md) and [operations guidance](operations.md).

| Decision | Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|---|
| Azure ownership change | Fresh inventory establishing the exact current target identities and names, current ownership, proposed ownership, and explicit approval for those targets. | An ownership mutation against an assumed or stale target could affect the wrong Azure resource. | Make no ownership mutation until the exact freshly inventoried targets are named and approved. | Which freshly inventoried and exactly named Azure targets have explicit approval for an ownership change? |
| Azure retirement | Fresh inventory establishing the exact target identities and names, dependencies, retirement scope, and explicit approval for those targets. | Retiring an assumed or stale target could remove a required service or leave dependent resources unmanaged. | Retire nothing until the exact freshly inventoried targets are named and approved. | Which freshly inventoried and exactly named Azure targets have explicit approval for retirement? |