# Open decisions

This is the sole register of material unresolved decisions. Most product decisions reviewed through 2026-07-25 are not reopened here. The [requirements](prd/README.md) and [capability inventory](capabilities.md) own scope context; deliberately deferred, conditional, and `Unclear` capabilities are not current-scope questions merely because their activation evidence is recorded here.

Evidence tiers are defined once in [engineering](engineering.md#required-evidence-tiers); no stronger state is inferred below.

Accepted decisions move to an [ADR](adr/README.md) or their canonical owner. Delivery status does not belong in this register.

[ADR-0013](adr/0013-qdos-alpha-implementation-contract.md) settles checkpoint 1's clause-specific QDOS implementation and Razor/Worker/MCP caller boundary, the separately owned evaluator allocation boundary, and the post-alpha repository-policy deferral. It does not close the evidence-dependent questions below or prove implementation, a caller, deployment, live verification, or acceptance.

Staff roles and access, principal and historical case-party identity, the Case/PO and case-type rules, Triage’s normal workflow, named terminal outcomes and reasoned reopen, exclusive one-case edit actions, immutable source-occurrence/dispatch identity, and reasoned source/Case or outbound-evidence reassociation are settled. Their canonical clauses are [principal and case-party identity](frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity), [source occurrence and dispatch](frd/frd-02-intake-and-source-identity.md#source-occurrence-and-dispatch-identity), [matching and reversible association](frd/frd-02-intake-and-source-identity.md#matching-conflicts-and-reversible-association), [Triage](frd/frd-03-triage.md#normal-workflow-and-completion-evidence), [case lifecycle](frd/frd-01-case-identity-and-lifecycle.md#lifecycle-closure-and-correspondence), [case edit authority](frd/frd-01-case-identity-and-lifecycle.md#case-edit-authority-and-recovery), [staff role access](frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix), and [outbound correspondence evidence](frd/frd-08-email-mailbox-and-background-processing.md#outbound-correspondence-evidence). This register may block only the named automatic predicate, transport, credential, or activation detail; it must not reopen those settled behaviors.

## First production journey and release sequencing

## Future AI Operations boundary

The future AI job catalogue and AI Viewer remain unresolved and unimplemented.
Before allocation, decide the permitted job types and eligibility, request and
execution lifecycle, transcript/event wire format, retention, redaction, and
the production transport and activation evidence. Operations must not imply
that `Features:SendToAi` or `Features:AutomationMcp` is production enabled.

Decided 2026-08-02: the first live journey is the full QDOS cutover — a genuine
QDOS instruction email through intake, review, Case/PO allocation, Box custody,
and the EVA handoff bundle. [`NOW.md`](../NOW.md) "Path" owns the ordered
critical path, the non-blocking capability set, and the acceptance boundary
(OPS-23/OPS-25 close `0.1.0-alpha.1`). The remaining evidence gate on that
path is item 3 (extraction
thresholds) below. The Box production custody boundary was decided 2026-08-02:
folder `405543781910` ("pegasus") is the production custody root and all case
folders are created only under it (owner:
[operations](operations.md#approved-box-integration-test-target)).

Decided 2026-08-03 by operator direction: every allocated Case/PO has one Box
case root named exactly by its safe Case/PO, with no `caseId` prefix or suffix.
Retained intake sources and managed document versions (reports,
correspondence, and staff-added documents) are kept beneath that same root.
The application may retain Case and version UUIDs as internal identities, but
neither a separate `cases/{caseId}` tree nor a UUID-derived Box case folder is
part of the accepted custody layout (owner:
[requirements](frd/frd-05-documents-extraction-and-custody.md#documents-extraction-and-custody)). No remote
content migration is authorised by this decision; any existing-content
relocation requires a separately approved target, inventory, recovery plan,
and approval.

## QDOS alpha activation details (migrated from the retired delivery plan)

Still-open questions preserved from the deleted
`research-and-planning/qdos-full-alpha-delivery-plan.md`; each blocks only the
step it names.

The former item 1 (`INT-17` VRM recognition thresholds) closed 2026-08-03:
the operator accepted the full-cohort evaluation at the **0.80** bar with the
accepted match rules. The
[ADR-0019 index entry](adr/README.md#architecture-decision-records) and the
`INT-17` capability row own the accepted numbers and their qualification.

1. **`INT-31` upload-link limits** — Exact token lifetime, aggregate and
   per-file byte limits, file count, allowed content types, per-token/per-IP
   rate, one-time vs reuse, and revocation/expiry error contract. Interim bound:
   the existing aggregate 10 MB intake limit; hashed 256-bit token; anonymous
   `/Uploads/{token}` form; no case disclosure.
2. **External credential ownership** — For each credential (Box, DVLA/DVSA, any
   VRM service, the Exchange application RBAC grant): the named operations owner
   and the provider-specific issue/rotate/revoke/emergency-disable procedure.
   The contract shape (Key Vault URI/version only, prove-then-cut-over, no
   local fallback) is settled.
3. **QDOS extractor acceptance thresholds (`INT-21`)** — Per-field
   accuracy/coverage thresholds and truth representation for the ten fields
   (Claimant Name, Claim Number, VRM, Make, Model, Mileage, Accident
   Circumstances, Incident Date, Instruction Date, Inspection Address), from an
   operator-reviewed cohort + untouched holdout. Zero false case creation is
   invariant. Inspection Address extraction is meaningful only for
   physical-address Principals; an always-image-based Principal's Cases take
   the exact `Image Based Assessment` value from the provider setting
   (ADR-0018), not from extraction.
4. **Telemetry sampling and daily cap** — Exact sampling rate and daily
   ingestion cap (31-day interactive retention is settled), accepted from
   measured alpha workload and cost evidence; the deployed adaptive sampling
   and 0.1 GB/day cap are interim.
5. **Azure budget wiring** — Billing scope, notification contacts/Action Group,
   and budget start/end dates were wired in the executed release (£75/month
   alert-only monitoring; see
   [operations](operations.md#production-environment)). Still open: a refreshed
   UK South GBP forecast from measured alpha workload — no fixed monthly
   ceiling or accepted spend range exists
   ([operator notes](operator-notes.md)); material variance from forecast needs
   a named expenditure owner's sign-off.
   First measured evidence (2026-08-03, operator-commanded subscription
   cost reads; no resource was created or changed): `rg-pegasus-prod`'s
   first ~2 days cost £1.71 (Functions Flex worker £0.73, Storage £0.40,
   ACR Basic £0.31, Container Apps web £0.22, Monitor £0.05); SQL S0 had
   not yet billed (list ≈ £12/month — the only 24/7-provisioned line;
   every other resource is consumption or bottom tier; at that observation the
   web app was 0.5 vCPU/1 GiB scale-to-zero, max 1 replica). Trailing 30 days totalled
   £85.78, of which £85.40 was `rg-collisionspike-dev` compute/AI already
   removed by the 2026-08-02 runbook (Foundry Models £40.17, Functions
   £28.22, Storage £9.47); that group's residual cost is two Key Vaults at
   effectively £0. Projected steady state ≈ £30–35/month at alpha
   staff-hours usage, inside the £75 alert. `INT-17` needs no new
   resource: the engine runs in-process on the existing always-warm web
   container, and the cheapest non-impacting headroom change, if ONNX
   sessions pressure 1 GiB, is 2 GiB memory on the same Consumption
   billing — not a dedicated plan or external service. Watch items: the
   worker's £0.36/day near-idle Flex baseline (verify no always-ready
   instance is configured), and the web app still resolves its Box
   secrets from the legacy `cespkboxkvv76a47` vault — evidence for the
   queued vault-consolidation prerequisite. That second watch item is
   discharged: the 2026-08-03 vault consolidation repointed both Box
   secrets to `pegasusprodkv252ow37g` and retired the legacy vault, and
   `rg-collisionspike-dev` no longer exists, so its residual line is now
   £0 outright rather than two effectively-free vaults (live-verified
   read-only 2026-08-04).
6. **Performance dataset ownership** — Who supplies and approves the immutable
   2,000-case performance dataset, observed document/source distribution, and
   measured peak burst that the capacity gate needs (fabricated domain data is
   forbidden; absence blocks the gate).

## Mailbox rule activation, automatic matching, and confidence display

The [Received/Sent taxonomy, mirrored Reply rule, `Other` behavior, separation
of classification from destination, and correction/reversal audit
contract](frd/frd-08-email-mailbox-and-background-processing.md#settled-mailbox-taxonomy-and-correction) are settled
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
- QDOS direct sender identity is owned by
  [ADR-0020](adr/0020-accepted-qdos-case-association-predicates.md) decision 1
  (`qdos_mail_route` v4, the accepted three-domain set); an accepted domain
  alone classifies and associates nothing.
- The Mapped Principals spreadsheet at the opaque source citation
  `../reference/imp-docs/requirementsdocs/provider-extra-info/Mapped%20Principals.xlsx`
  identifies additional principals and route candidates beyond QDOS. Every
  listed candidate remains evidence, not an activated route.

The available evidence establishes review-visible uncertainty, but not an
accepted numeric confidence score, threshold, or alternative confidence
display. None should be inferred.

The QDOS intake-to-Triage matcher remains the inactive
`NoAcceptedIntakeTriageMatcher`: the downstream behavior is complete (accepted
match evidence creates exactly one replay-safe Triage and multiple matches fail
closed), so only the match predicates are missing. Activation needs the named
predicates, exclusions, and ambiguity outcome accepted under this section, and
is a deliberate change to a named, versioned matcher — the Production
composition test pins the inactive matcher so it can never be activated as a
side effect of composition.

The QDOS-direct automatic incoming-case matching predicates and their
conservative outcomes are accepted and owned by
[ADR-0020](adr/0020-accepted-qdos-case-association-predicates.md) (operator
decision 2026-08-03). This closes the first row's question for that one matcher
and pulls the QDOS-direct subset of `MAIL-09` to `Now / 0.1.0-alpha.1`. The
multi-rule precedence and confidence questions below stay open for
classification and for every other route, matcher, and surface; the QDOS
classification policy still records simultaneous category matches as the
ambiguity outcome with no invented winner.

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

## Send-to-AI transport and assessment toolset (`AI-09` / `MCP-06`)

`AI-09` and the Automation Actor assessment toolset are implemented gated
(ADR-0021, 2026-08-03): the direct-write model with logging parity replaced
the earlier proposal-only reading, the channel hand-off carries a pointer
only, and automation-recorded values stay unconfirmed until the engineer the
case is manually assigned to reviews them. The channels transport is a
research preview and carries local evidence runs only; production activation
needs a separate non-preview transport decision. Microsoft Foundry remains
the intended candidate, pending evaluation, for the later `1.3.0` AI
query-response proposals (`AI-07`/`AI-08`), which stay proposals.

Still open after the 2026-08-03 implementation:

| Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|
| Rate-card ownership and accepted derivation formulas (EXT-09): who owns published rate cards, and acceptance of WU÷10×rate, sundry percentages, material bands, and the VAT rule as Core policy. | Without accepted authority no estimate total, report worklist, or repair-cost-to-PAV ratio can be derived; the PAV slider names the missing costed total instead. | Keep derivation absent; raw line writes continue. | Which rate-card owner and derivation formulas are accepted for EXT-09? |
| Assessment markup ambiguities recorded rather than guessed: betterment semantics, the estimate `guide` code meaning, approved signatory-list ownership, whether fee fields stay in the assessment record given EXT-11 is `1.2.0`, and where guide/external valuation figures are stored (EXT-10/EXT-13; the valuation API contract should name which figures it supplies). | Guessing any of these would invent business semantics the screens deliberately left unstated. | Store free text where shipped today; decide each with its owning capability. | What are the accepted semantics for each recorded ambiguity, and where do valuation-service figures land when EXT-10/EXT-13 are contracted? |
| The Suggestions screen's fate and the PAV slider's parameters at the UI-15 re-entry review: repurpose the built Suggestions markup as a read-only automation-change review or retire it; confirm slider placement and step/rounding; resolve the recorded `.send-action` contrast shortfall (2.3–4.2:1 vs 4.5:1) before any activation puts the control in front of staff; ratio basis and threshold source (per-principal or per-instruction; QDOS 80% is the only evidenced example). | Unresolved presentation decisions block staff-facing activation, not gated local work. | Decide at the UI-15 re-entry review; keep the slider a review aid that writes nothing. | What does the UI-15 re-entry review accept for the Suggestions screen, the slider, the contrast fix, and the threshold source? |
| Tier-5 external-client evidence: one recorded DevelopmentOffline round-trip run — real Claude Code channel session, send → channel event → Actor read → attributed write → reply → Completed on reconcile — over the full fourteen-tool inventory, plus the connector JSONL evidence-log retention rule beyond local-only/gitignored. | Without it no activation claim can be made; the surface stays composition-gated. | Fold into the queued tier-5 MCP evidence run. | When is the recorded round-trip run performed and where is its evidence filed? |

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
| Completion of the full design route for each later UI capability, using the canonical [design process](design.md) rather than inheriting raster details. | Treating comparison material or raster details as requirements could constrain later capabilities to an unaccepted interaction model. | Keep the operations-first alpha shell. Require later UI capabilities to re-enter complete design before activation. | Has the later UI capability completed the full design route without treating comparison evidence or raster details as accepted requirements? |

## Mail workspace freshness threshold and retention start

The mail workspace ships reading retained messages. Two of its numbers are
provisional and are recorded here rather than presented as settled.

| Decision | Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|---|
| Stale threshold | Observed poll behaviour under real load: how often a tick is genuinely late, and how long an operator can act on mail without knowing polling has stopped. | Too short and the chip cries wolf on every slow tick; too long and a stopped Worker is invisible while staff work from a list that is no longer arriving. | Ship the provisional 15 minutes (fifteen missed one-minute ticks), recorded in `GetRetainedMailFreshness.StaleAfter`. | How long after the last successful poll should the workspace stop calling its data current? |
| Historical mail | Whether operators need messages received before message-level retention began, and if so what a reconstruction from retained artifacts could honestly recover. | A backfill invents display material for messages whose MIME was retained but never parsed for display, and would present reconstructed fields as if they had been read at poll time. | Start empty. The list surfaces `HasUnretainedHistory` and says the gap exists rather than presenting nothing as "nothing was received". | Should retained mail be backfilled for messages polled before retention began? |

## Manual upload in a deployed environment

[ADR-0003](adr/0003-pdfpig-for-first-qdos-slice.md) states that the manual
upload route "must not be enabled in a deployed environment until authenticated
intake and approved durable source custody are implemented". Shipped behaviour
has drifted from that: the nav item and the `/Upload` page are reachable in
Production today, and this task made that route the way a manual upload becomes
a case. The prohibition was neither honoured nor withdrawn.

Only one of its two conditions is clearly met. `UploadModel` is `[Authorize]`
with explicit roles, so authenticated intake holds — and held before this task.
Durable source custody does not: the same ADR paragraph records that the upload
path retains its assets "in ignored local content-addressed storage… not
production Blob staging, Box custody, backup, or retention", which remains true.

An ADR body is immutable and amendable only on an explicit operator
instruction, so the discrepancy is recorded here rather than edited away.

| Decision | Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|---|
| Manual upload deployment status | Which custody path a deployed manual upload actually writes to, and whether that satisfies "approved durable source custody" as ADR-0003 meant it. | The route is reachable in Production now. Leaving the contradiction unresolved means either an unenforced prohibition or an undocumented permission, and the release record cannot state which. | Neither enable nor disable it on this task's authority. Resolve the custody question first, then either amend ADR-0003 by operator instruction or gate `/Upload` to match it. | Is the manual upload route permitted in a deployed environment, and on what custody evidence? |

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
