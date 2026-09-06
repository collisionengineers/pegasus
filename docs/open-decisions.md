# Open decisions

This is the sole register of material unresolved decisions. Most product decisions reviewed through 2026-07-25 are not reopened here. The [requirements](prd/README.md) and [capability inventory](capabilities.md) own scope context; deliberately deferred, conditional, and `Unclear` capabilities are not current-scope questions merely because their activation evidence is recorded here.

Evidence tiers are defined once in [engineering](engineering.md#required-evidence-tiers); no stronger state is inferred below.

Accepted decisions move to an [ADR](adr/README.md) or their canonical owner. Delivery status does not belong in this register.

[ADR-0013](adr/0013-qdos-alpha-implementation-contract.md) settles checkpoint 1's clause-specific QDOS implementation and Razor/Worker/MCP caller boundary, the separately owned evaluator allocation boundary, and the post-alpha repository-policy deferral. It does not close the evidence-dependent questions below or prove implementation, a caller, deployment, live verification, or acceptance.

Staff roles and access, principal and historical case-party identity, the Case/PO and case-type rules, Triage’s normal workflow, named terminal outcomes and reasoned reopen, exclusive one-case edit actions, immutable source-occurrence/dispatch identity, and reasoned source/Case or outbound-evidence reassociation are settled. Their canonical clauses are [principal and case-party identity](frd/frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity), [source occurrence and dispatch](frd/frd-02-intake-and-source-identity.md#source-occurrence-and-dispatch-identity), [matching and reversible association](frd/frd-02-intake-and-source-identity.md#matching-conflicts-and-reversible-association), [Triage](frd/frd-03-triage.md#normal-workflow-and-completion-evidence), [case lifecycle](frd/frd-01-case-identity-and-lifecycle.md#lifecycle-closure-and-correspondence), [case edit authority](frd/frd-01-case-identity-and-lifecycle.md#case-edit-authority-and-recovery), [staff role access](frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix), and [outbound correspondence evidence](frd/frd-08-email-mailbox-and-background-processing.md#outbound-correspondence-evidence). This register may block only the named automatic predicate, transport, credential, or activation detail; it must not reopen those settled behaviors.

## Historical QDOS-alpha release sequencing

The sequence below records the earlier alpha decision. The 6 September v1
decisions in [operator authority](operator-notes.md#current-v1-decisions--6-september-2026)
supersede its EVA-dependent completion scope: Pegasus owns engineering and
final reports, EVA is optional, and every send is staff-initiated. The three
v1 PRs remain unmerged; this history is not fresh release authorization.

Decided 2026-08-02: the first live journey is the full QDOS cutover — a genuine
QDOS instruction email through intake, review, Case/PO allocation, Box custody,
and the EVA handoff bundle. This section owns the ordered critical path, the
non-blocking capability set, and the acceptance boundary (OPS-23/OPS-25 close
`0.1.0-alpha.1`). The remaining evidence gate on that path is item 3 (extraction
thresholds) below.

The ordered critical path (full QDOS cutover — every new QDOS instruction is
worked in Pegasus through to the EVA handoff; EVA keeps engineering and reports):

1. Green `main` through a PR with a passing `repository-check` run.
2. Prove the spine on one genuine QDOS email in production: mailbox intake → custody → extraction draft → principal → Case/PO minted → Box folder (INT-02/08/09/19/22/25, CASE-07, DOC-01/02) — needs the composition fix deployed.
3. Accept extraction thresholds from the reviewed cohort + holdout (INT-21); zero false case creation.
4. Production document content store live (DOC-02), then staff review path live: completeness gates and Review/Not ready/Held queues (CASE-13/14/15/16, UI-02/08).
5. EVA bundle from a real case: exact 13-key JSON + images (EXT-03), the `First sent to Engineer` proxy event (CASE-21), operator accepts every field mapping via a real drag-and-drop run.
6. Chasing live: due-by, 7-day chase schedule, copyable chasers (CASE-17/18, MAIL-18).
7. Web telemetry exporter (OPS-07) and minimum cutover alerts (Box custody failure, intake poison, chaser sweep), then the cutover date: all new QDOS instructions enter Pegasus; watch alerts and telemetry daily for the first week. **Before this date, confirm the retained rollback artifact runs against the live schema**, then the additive-migration requirement binds — the pre-cutover exemption in [ADR-0030](adr/0030-non-additive-schema-changes-before-cutover.md) ends, because from here a rollback has live case work to preserve. Ending the exemption does not by itself repair a non-additive migration shipped before it, which is why the check is a prerequisite of this step rather than a rule that starts applying to later releases.
8. Record operator acceptance and management approval (OPS-23, OPS-25) — this closes `0.1.0-alpha.1`.

Explicitly NOT on the path (allocated but non-blocking): MCP-01–04, INT-17 VRM reading, INT-31 upload links (activating at release 37 under interim limits, and still not a release gate), the EVAL evaluator cluster, live DVLA/DVSA adapters (approved replay/`Unavailable` is fine), MAIL-14/16 report-sent detection (post-report tracking starts manual via MAIL-15), and OPS-09 recovery proof (removed as a release gate 2026-08-03). The Box production custody boundary was decided 2026-08-02:
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

## Future AI Operations boundary

The AI job catalogue and durable lifecycle are settled by AI-10 and ADR-0035;
the ledger and administration viewer are implementation work under PLAT-075.
External transport, real client round-trip and production activation still need
their own evidence. Operations must not imply
that `Features:SendToAi` is production enabled — it is not, and it cannot be:
`src/Pegasus.Web/AiWork/SendToAi.cs:42` throws unless the runtime profile is
`DevelopmentOffline`, so setting it in production crash-loops the host rather
than enabling anything.

`Features:AutomationMcp` **is** production enabled, and has been since release 9
under ADR-0026; the earlier wording here paired the two flags and was wrong
about that half. `Features:ProviderApi` became production enabled at release 37,
which admits nobody until a credential is issued
([operations](operations.md#production-environment)).

## QDOS alpha activation details (migrated from the retired delivery plan)

Still-open questions preserved from the deleted
`research-and-planning/qdos-full-alpha-delivery-plan.md`; each blocks only the
step it names.

The former item 1 (`INT-17` VRM recognition thresholds) closed 2026-08-03:
the operator accepted the full-cohort evaluation at the **0.80** bar with the
accepted match rules.
[Operations § dated evidence](operations.md#dated-evidence-qualifications) owns
the accepted numbers and their qualification.

1. **`INT-31` upload-link limits** — **Partially settled 2026-08-29.** Still
   open: **one-time vs reuse**, and the **revocation/expiry error contract**.
   Settled as an interim activation, not a closure: token lifetime, aggregate
   and per-file byte limits, file count, allowed content types, and both rate
   bounds. Unchanged and still binding: hashed 256-bit token, anonymous
   `/Uploads/{token}` form, no case disclosure.

   The operator accepted the interim set below on 2026-08-29 so upload links
   compose in production from release 37 ([[INTK-051]]). It is named
   `int-31-interim-v1` so that accepting the full decision later is a version
   change rather than an untracked edit — but see the warning below about what
   a version change actually does today.

   | Setting | Interim value | Basis |
   | --- | --- | --- |
   | Aggregate bytes | 10 485 760 | the interim bound already recorded here — the same 10 MB as `IntakeEnvelopeLimits.MaximumContentLength` (`IntakeContracts.cs:13`) |
   | Per-file bytes | 10 485 760 | one file may use the whole aggregate |
   | File count | 10 | |
   | Token lifetime | 168 h (7 days) | matches the existing chase cadence (CASE-17/18, MAIL-18) |
   | Rate, per token | 20 per 10 minutes | `RequestUploadAttemptLimiter`, partitioned by token digest |
   | Rate, per address | 30 per minute | `PublicUploadLink`, partitioned by calling address, as staff sign-in, the MCP ingress and the Provider API already are |
   | Content types | `application/pdf`, `image/jpeg`, `image/png`, `…wordprocessingml.document`, `application/msword`, `message/rfc822`, `application/vnd.ms-outlook` | exactly the seven `MimeKitPdfPigOpenXmlIntakeSourceReader.DetectFormat` maps to a `SourceFormat` (`:971-1014`) |

   **Both rate bounds are needed, and the per-token one alone was not enough.**
   `RequestUploadAttemptLimiter` partitions on the token digest, and
   `RequestModel.OnPostAsync` answers `NotFound` for an unknown token before the
   limiter is consulted — so a caller holding no token spends nothing. That gap
   was unreachable while the composition gate was closed, because the middleware
   short-circuited `/Uploads` to 404 before any body was read; opening the gate
   makes the page reachable, and Razor Pages' antiforgery filter buffers the
   whole multipart body before the page can reject it. The per-address bound
   closes that, and it runs at `UseRateLimiter` — after routing, before endpoint
   execution — so a rejected caller never has its body read.

   **A version change is not yet a migration.** Two mechanisms invalidate every
   outstanding link the moment either value moves, and neither is graceful:
   `RequestUploadPolicy.Authorize` (`:372-376`) **throws**
   `InvalidOperationException` when a stored link's `LimitsVersion` differs from
   the configured one, and `HasAcceptedLifetime` (`:440-455`) requires
   `ExpiresAtUtc == CreatedAtUtc + limits.Lifetime` exactly, so changing
   `LifetimeHours` alone makes every already-issued link `Unavailable`. Harmless
   while production holds zero links; **settle the migration path before the
   second version.**

   These are **not** the integration fixture's values
   (`integration-fixture-v1`, 1-hour lifetime, 1 MB per file); those are test
   values and must never become production policy.

   No larger target is accepted. Manual upload remains **10 MiB per file**;
   the Provider API envelope stays 30 MB. [[INTK-052]] researches representative
   requirements, cost, performance and Azure constraints before an operator
   decision,
   and is a Core change rather than a configuration one — `DurableIntake`
   bounds the `ManualUpload` channel by
   `IntakeEnvelopeLimits.MaximumContentLength`, and the batch budget derived
   from it feeds a global multipart limit. Until INTK-052 lands, the
   `int-31-interim-v1` values above remain the truthful current policy.

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
   and 0.5 GB/day cap are the recorded deployed settings (release 35), as
   qualified in [operations](operations.md#production-environment). A full
   working-day workload and an accepted measured budget remain evidence work;
   this does not reopen the configured cap as an absent decision.
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

The QDOS intake-to-Triage question is **closed** (operator decision
2026-08-23, INTK-033). It waited only on the match predicates, and those are now
accepted, named, and versioned as the route's own classification predicates:
`body.triage-only-request` and `subject.engineer-triage` in
`qdos_mail_classification` v4, with their exclusions (case-exact generated
tells; the subject tell anchored past any forward or reply prefix) and their
ambiguity outcome (two matching categories are the recorded Ambiguous outcome
and open no Triage). Recognising them is one owner's job, and FRD-03 names that
owner as the accepted route classification policy, so the separate
`IIntakeTriageMatcher` — whose only implementation was ever the inactive one —
is retired rather than filled in. Activation stays deliberate: the Production
composition test now pins the active classification policy, its key and its
version, so the trigger can neither change nor disappear as a side effect of
composition.

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

## EVA API activation (`EXT-04`) — resolved 2026-08-27

**Resolved.** The operator directed activation on 2026-08-27 against EVA's test
environment. The decided operation is `POST /Instruction/Inspection` and only
that one: a case and its eligible images, submitted at most once.

What settled each of the boundaries this decision was held open for:

- **Operation and direction.** One outbound submission. No fetch, no
  create-with-children, no report-with-PDF handoff.
- **Contract and authentication.** Recorded in
  [FRD-07](frd/frd-07-eva-and-external-engineering-handoff.md#direct-eva-api-submission)
  and proved against the vendor's own recorded traffic, which differs from its
  documentation in several ways.
- **Attachments.** Inline base64 on the same request; EVA resolved the
  server-side defect that previously refused them.
- **Structured success/failure.** Four distinct outcomes — succeeded,
  rejected, partial, unknown — persisted per attempt.
- **Idempotency.** EVA provides none, so Pegasus owns it: at most one
  successful submission per case, enforced by a unique index. Unknown delivery
  is retained without automatic retry; another send requires explicit staff
  action under FRD-07.
- **Coexistence.** The manual export is unchanged and remains available to
  every Principal, from the same Send to EVA control.

Two things remain open and are tracked as their own work, not here: live
credentials (operator-gated, a credential swap only) and real EVA fields for
the inspection date and mileage, which currently travel as note lines.

No returned EVA identifier creates, selects, or alters a Pegasus case or
reference; they are recorded against the case as evidence of delivery.

## External data, submission, and report contracts

These are independent blockers, not one integration decision. `VEHICLE DATA`
observed in EVA, Parkers, and AutoTrader remain evidence rather than selected
adapters. AutoTrader market research runs outside Pegasus through the
`MarketResearch` job (D35, 2026-09-02); no scraping or AutoTrader integration
inside Pegasus is open here.

| Decision | Evidence needed | Impact | Recommended default | Decision question |
| --- | --- | --- | --- | --- |
| Glass's direct repair-estimate access | Accepted licensing, API or embedded-access terms, technical access, and cost. | Repair-estimate integration and its commercial viability cannot be established. | D03 selects per-Engineer Glass's repair estimates. Operator-owned live acceptance remains outstanding; no valuation service is selected. | D03 settles repair-estimate integration; the operator performs all live acceptance. |
| Direct valuation access | Accepted direct-access contracts and terms for CAP, Glass's, and Cazana, including the basis for selecting any adapter. | Valuation sourcing, permissions, and cost remain uncertain. | Treat all three as candidates only; do not imply that any valuation adapter is selected. | Is there an accepted direct-access and commercial contract for a selected valuation source? |
| Provider API tenancy and wire contract | An accepted client/tenant representation, exact routes, headers, schemas, attachment encoding, request limits, throttling/error contract, administration workflow, named clients, and rollout. The settled isolation boundary remains one principal-scoped client with own receipt/status/result only. | Treating an email domain, intermediary, or shared external tenant as the API principal could disclose another principal's work or create a second policy engine. | API-01 in FRD-09 defines the current contract. Use stable Pegasus Principal identity; additional tenancy, named credentials and live caller/rollout evidence remain separate. | What exact provider API contract and client/tenant representation preserves the accepted principal-scoped isolation boundary? |
| `provider_domain_key` migration or retirement | An authoritative source definition and owner; current and predecessor uses; mapping to stable Pegasus principal/route/evidence identities; collision and unknown handling; cutover, rollback, retention, and exact retirement proof. No allowed accepted source currently defines this name as a Pegasus identity. | Importing, translating, or deleting an undefined key could misattribute a principal, destroy provenance, or leave a hidden compatibility dependency. | Do not create, migrate, map, alias, or retire `provider_domain_key`. Keep provider-domain evidence versioned and separate from principal and route identity. | Is there any approved source and consumer that requires this key, and if so what reviewed migration and retirement contract applies? |
| Provider report submission and delivery | Exact provider API formats, delivery contracts, and provider identities. | Reports or work could be sent in an unsupported format or to an unproved identity. | Keep provider delivery behind review or existing supported procedures until each provider contract is accepted. | Has the exact format and identity contract been accepted for the provider being activated? |
| DVLA/DVSA vehicle and MOT lookup | Selected provider/API and licence; exact make/model/year/engine/fuel and MOT/mileage fields; credentials; limits/rates; error and stale-data behavior; target; integration of the accepted mileage-estimation contract; and caller proof. | A guessed field or stale/failed result could overwrite confirmed vehicle data or present an estimate as supplied fact. | Keep live lookup disabled. Preserve source-labelled suggestions and return `Unavailable` when approved local replay evidence is absent. | The DVLA/DVSA adapter is selected and composed; are its exact credentials, deployed caller and live response evidence accepted? |
| Post-report query and dispute lifecycle | Allowed states/transitions and actors; case/report/reply-chain evidence; correction/reopen and due/chaser interaction; response proof; closure; and dispute resolution. | A mailbox event could silently change case state, close work prematurely, lose a correction, or create a duplicate case/reference. | Preserve the correspondence against the existing case for staff review; let no Outlook adapter decide lifecycle or closure. | What exact CASE-23 lifecycle governs a received query/dispute through Engineer response and reasoned completion? |
| Audatex PDF ingestion | Representative PDF variants and accepted field-mapping evidence. | Variant layouts could produce incomplete or incorrect extraction. | Do not activate generic Audatex PDF mapping from unrepresentative examples. | Have the supported Audatex PDF variants and their mappings been accepted from representative evidence? |
| Mandatory global vehicle checks | Global requirements are settled as vehicle identity/specification, vehicle-history/risk, and market valuation. All three require a result or explicit exception before Engineers-queue eligibility. The authorised staff reviewer records each exception as a named, reasoned Case action. Each provider/route still needs its exact source, required result, and unavailable/failure contract. | A Case could proceed to an Engineer without a globally required result, or a provider-specific behavior could silently override the common baseline. | Preserve the global checks; use source-labelled `Unavailable` or approved local replay while live callers are unaccepted; retain unmet checks as `Not ready` rather than inventing a result. | What unavailable/failure contract applies to each global check for each provider/route? |
| Report wording outside the approved assessment baseline | The `rendererref1` assessment wording and exact `A Patterson | M.Inst.IAEA | andy_patterson` tuple remain accepted for draft generation. The signatory policy is settled by D31 (2026-09-02, superseding D18): reports render the Case's Sign-off Engineer tuple, delivered by `DOCS-017`. Salvage Categories A/B/N/A wording, recovery/storage wording, and a final statement of truth remain absent or unaccepted. | Unsupported reports could contain incomplete, unauthorized, or inconsistent statements. | Keep absent wording and incomplete identity tuples unavailable; fail closed and never infer them. | Has the exact missing wording or qualification been supplied and accepted? |

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
| --- | --- | --- | --- |
| Assessment markup ambiguities recorded rather than guessed: whether fee fields stay in the assessment record given EXT-11 is `1.2.0`, and where guide/external valuation figures are stored (EXT-10/EXT-13; the valuation API contract should name which figures it supplies). Betterment semantics, the estimate `guide` code meaning and approved signatory-list ownership are settled by the 2026-09-01 confirmation (EPIC-011 D17/D18): the first two are retained evidence only, owned by [FRD-06 § Professional engineering findings and correction](frd/frd-06-vehicle-and-engineering-evidence.md#professional-engineering-findings-and-correction), and the third is answered by the Case's Sign-off Engineer tuple (D31, 2026-09-02, superseding D18) in [FRD-11](frd/frd-11-reports-correspondence-and-reviewed-proposals.md#initial-renderer-activation). | Guessing either remaining item would invent business semantics the screens deliberately left unstated. | Store free text where shipped today; decide each with its owning capability. | Do fee fields stay in the assessment record, and where do valuation-service figures land when EXT-10/EXT-13 are contracted? |
| The Suggestions screen's fate and remaining PAV-slider presentation questions at UI-15 re-entry: repurpose or retire the Suggestions markup; confirm placement and step/rounding; resolve contrast; and decide threshold source. The requested value is optional, 0–80 %, no default, visibly derived from Engineer's Value, and proposal guidance only (D24, amended 2026-09-02). | Unresolved presentation decisions block staff-facing activation, not gated local work. | Decide at UI-15 re-entry; keep the control a review aid that writes nothing. | What presentation and threshold-source choices are accepted? |
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

Resolved 2026-09-02 (EPIC-012): the Case record is one scrolling page with a
sticky ribbon, action bar and section jump-nav (D29) and the Engineer
workbench is its Damage, Valuation, Estimate, Settlement and Report sections
(D30); sections as tabs remain the rule for other records, and no layout
switch ships. The signatory question is closed by D31 (the Case's Sign-off
Engineer tuple, superseding D18). Owners:
[FRD-12 § Case workspace](frd/frd-12-operator-experience.md#case-workspace),
[FRD-01 § Sign-off Engineer](frd/frd-01-case-identity-and-lifecycle.md#sign-off-engineer).

| Evidence needed | Impact | Recommended default | Decision question |
| --- | --- | --- | --- |
| Completion of the full design route for each later UI capability, using the canonical [design process](design/README.md) rather than inheriting raster details. | Treating comparison material or raster details as requirements could constrain later capabilities to an unaccepted interaction model. | Keep the operations-first alpha shell. Require later UI capabilities to re-enter complete design before activation. | Has the later UI capability completed the full design route without treating comparison evidence or raster details as accepted requirements? |

## Mail workspace freshness threshold and retention start

**Resolved 2026-09-01.** The operator confirmed both numbers on 2026-09-01
(EPIC-011 D22). The heading is kept unchanged because
[current architecture](current-architecture.md) links to this anchor.
The stale threshold is a **fixed 15 minutes** since the last successful update —
three missed `ApprovedInboxPollSchedule` recovery ticks at `0 */5 * * * *`,
recorded in `GetRetainedMailFreshness.StaleAfter` — and it is not configurable.
There is **no historical backfill**: the workspace starts at each approved
mailbox's genuine retention-start boundary, surfaces `HasUnretainedHistory` and
says the gap exists rather than reconstructing display material for messages
whose MIME was retained but never parsed for display. Delete remains a
recoverable move to Deleted Items and permanent deletion is absent.

Canonical owner:
[FRD-08](frd/frd-08-email-mailbox-and-background-processing.md#email-mailbox-and-background-processing).
Allocated to [[MAIL-031]] and [[TICK-054]]; not delivered.

## App Insights daily cap

MAIL-020 (release 35, 2026-08-27) raised the App Insights component
`dataVolumeCap.cap` on `pegasus-prod-appi-252ow37gij` and the Log Analytics
workspace `dailyQuotaGb` on `pegasus-prod-logs-252ow37gij` from 0.1 GB to
0.5 GB (one bicep variable, `telemetryDailyCapGb`, binds both), and the
deployed Worker now drops successful SQL dependency telemetry via
`SqlDependencyTelemetryFilter`. Operator billing approval was given
2026-08-27 (worst case approximately £24/month, expected approximately
£2/month). Both caps read back 0.5 immediately after the release 35
provision. This is a raised ceiling with a cut contributor, not proof the
new cap survives a full working day of combined Web and Worker volume.

| Decision | Evidence needed | Impact | Recommended default | Decision question |
|---|---|---|---|---|
| Further cap increase | A working day of ingestion volume observed at the 0.5 GB cap. | Too low and the estate still goes silent by mid-morning; too high raises billing without proven need. | Hold at 0.5 GB until PLAT-034 records a full working day under it. | Does the estate need a cap above 0.5 GB once a full day's volume at the new cap is observed? |

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
