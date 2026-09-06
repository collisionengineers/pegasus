# Approved implementation inputs

User authorized delivery of this stream on 6 September 2026. The supplied pack is the reviewed research and plan, not a request to repeat planning.

Live fetch confirmed common D 3284f93fc3ea9fd3bbbea9405ec92dc7818378f2 and main 32f8679d3695e0dcab8f310a1c20f8b129d20190. The four old PR heads remain unchanged. Shared checkout is dirty and 103 commits behind: preserve it. NOW.md is absent at D; docs/index.md directs current work to Kanmer. Existing 44 worktrees remain preserved.

Owners: [[PLAT-075]] A/F, [[CASE-047]] B, [[INTK-060]] C. No fourth owner. B/C domain implementation waits for published reviewed F. Exact file ownership and residual ticket acceptance are the supplied registers.

# Confirmed scope and decisions

This package implements the approved planning task. It does not implement
Pegasus, create product worktrees, open/close PRs, or deploy anything.
No Kanmer ticket is created for this planning/consolidation task.

## Operator decisions from 6 September 2026

| ID | Binding decision |
| --- | --- |
| D01 | UI v3 HTML/specification is the target. Correct defects; the task and later operator answers override conflicting prototype details. |
| D02 | Complete engineering and final reports inside Pegasus. EVA is optional. |
| D03 | Integrate Glass’s repair estimates, not its valuation service. Per-engineer credentials in Administration; initial account alex. The operator performs all live Glass’s testing. |
| D04 | Triage remains separate from normal Case/PO allocation. Use a global increasing T-00001, T-00002 reference sequence; no yearly/principal reset or reuse. Formal instructions allocate a normal Case and link the Triage. |
| D05 | Administrators configure mailboxes. Create new mailboxes in Microsoft 365 administration, then onboard them in Pegasus UI following one-time infrastructure setup. |
| D06 | v1 includes real email sending and truthful Sent evidence. Staff initiate every report/chaser send; no unattended scheduled chasers. Agents use local substitutes and send no real emails or mutate Outlook. |
| D07 | Unknown repairer VAT requires explicit status or explicit VAT-category selection before accepting estimate totals. |
| D08 | Include address suggestions. Image Based Assessment is an address/location option and configurable principal default. Preserve the provider-controlled report-address rule; a physical address does not imply CE attendance. |
| D09 | HDUK-branded instructions in the YML samples belong to the confirmed YML route. Preserve document issuer separately from principal identity. |
| D10 | Existing application email/case data is disposable test data. Plan a clean target schema without legacy-data conversion. This does not authorize deleting source references or making an unreviewed cloud reset. |
| D11 | Absorb required work from PRs 639, 646, 670, 671, verify preservation, then close old PRs as superseded. Preserve original branches/evidence. Exactly three final open PRs target dev; none is merged. |
| D12 | Later implementation gets three properly scoped owner tickets. Map every existing ticket to a reasoned disposition. Do not create a ticket for this planning task. |
| D13 | Defer one-off customer workflow. |
| D14 | The operator's free-text deferral overrides the selected procedure option: defer additional spreadsheet-driven recipient/package/chase and garage-procedure automation to follow-up tickets. Keep the explicitly included location defaults, address suggestions and top-15 extraction. |
| D15 | Remove periodic account reviews completely. Include disable/delete access, password reset, force logout and targeted lease clearance. |
| D16 | Box is durable file storage. Azure holds processing bytes and a 24-hour idle cache. Keep minimal SQL arrival/idempotency/provenance records. |
| D17 | Preserve the four-project architecture and one Core business-policy owner. No prerelease compatibility layers, replacement platforms, generic workflow engine or speculative abstractions. |

## Current task boundaries

All authored files belong under this package. Source files and earlier review
outputs remain intact. Native Kanmer reads are allowed. There are no email,
Outlook, Box, Glass’s, EVA, Azure, database, account or credential writes.
No product build/test is represented as freshly run merely because the earlier
review contains passing results. Browser policy blocked opening the local v3
HTML; do not circumvent it or claim a new visual run.

The missing v3 verification files and exact E01–E28 Box-linked originals are
evidence limitations, not permission to invent fixtures. Use the genuine local
corpus for executable acceptance; unsupported sample-specific rules require
exact evidence before activation. Do not hold up unrelated implementation for
an external test whose required outcome and human owner are already settled.

## Fixed planning ownership

- F: Codex shared foundation; schema/model/configuration/snapshot/composition,
  cross-stream contracts, shared test support and governance reconciliation.
- A: Codex platform: identity, administration infrastructure, mail onboarding
  and staff-send transport, Box/cache, connector, CI and combined verification.
- B: Claude casework: Case pages/domain/combined save, calculations, Glass’s,
  report generation and delivery preparation, case-specific assets.
- C: Claude intake: principal/third-party extraction, principal directory,
  pre-case/inbox/search/work-centre/outer-shell pages and shared UI assets.
- A alone writes EF global configuration/migrations/snapshot and composition
  roots. B/C own their adapter/store method implementations after F freezes.
  Domain-specific Administration pages follow B/C ownership, not a blanket A
  directory claim. Final ownership register is authoritative.

## Package state

Authoring and independent planning review are complete. The final review and
mechanical/preservation results are linked from README. Future implementation
and operator acceptance remain the work specified by these plans.



# Shared contracts and single owners

These are the accepted implementation choices, governed by
[DECISIONS](DECISIONS.md). B/C handoff JSON files are detailed field inventories;
this file resolves their overlapping names and ownership. Foundation F01–F03
publishes the concrete C# definitions/EF mappings once, before parallel domain
work. No new project, runtime, workflow engine or generic unit-of-work layer.

## S01 — actor, concurrency and atomic Case save

Use existing server-derived `ActionActor`, version, Case lease, operation key
and reason types. Do not reduce an Automation actor to a staff ID. All Web/MCP
mutations invoke the same Core policy. Server loads readiness facts inside the
mutation transaction; posted booleans never establish readiness. Admin role
does not confer Engineer findings authority. Permission/version/hash checks
repeat at the actual side-effect boundary, not only when a page opens.

B adds one `SaveCaseWorkspace` Core command and `EfCaseWorkspaceStore` using the
existing scoped `PegasusDbContext` and one serializable transaction. It validates
the complete B-owned Case/Assessment snapshot, then updates all affected rows
and history and commits once. Refactor existing store mutation methods into
shared internal helpers as needed; do not call several independently committing
public operations. No generic transaction interface is required. Foundation adds
`ClearCaseEditLease` as a real shared Core command plus an administrative store
method on existing `EfCaseWorkflowStore`: Administrator right, selected Case
or user, expected active lease generation, actor, reason and operation key;
serializable mutation invalidates the active token without requiring the
holder’s secret. Replay returns its original outcome. F implements/tests this
small primitive before B owns later workflow edits; A owns its admin caller.
A/F02 adds its policy tests in
`tests/Pegasus.Core.Tests/Workflow/CaseEditAuthorityTests.cs` and serializable
store tests in `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs`.
The same contract-only phase handoff applies: after F freezes, B owns subsequent
workflow tests. Cover wrong Case/user/generation, no active lease, exact replay,
concurrent renew/clear and rejection of the old token.
The existing holder-token Release command is not an administrative override.
Existing
Engineer-note append and explicit accept/apply/import/generate actions remain
named commands; they must preserve unsaved workspace input or require Save/
Discard first. B owns lifecycle workflow/readiness policy and EF workflow store.

All failed/stale/conflicting edits retain posted input and commit nothing.
Accepted estimates/applied values/generated report snapshots are immutable
versions. D10 removes any need to preserve obsolete development schema behavior;
new accepted history still remains stable for real use.

## S02 — schema, migrations, contracts and composition

A Foundation owns all persistence entity declarations, model configuration,
DbContext, migrations/designer/snapshot, runtime grants/bootstrap, DI and host
entrypoints. B/C supply field inventory and own domain policy/store methods.
Freeze the minimal entities requested by B-F-01..07 and C-F01..09, adapting
existing aggregates instead of adding overlapping tables. All maps/grants ride
one reviewed schema diff. Retain the valid migration chain and add the required
v1 migration; no migration solely to convert disposable historic app data.

The known cross-stream C# records, interfaces and enums below must actually
exist in the common F commit. Publishing their prose signatures alone is not
sufficient. A authors only those definitions during F01; B/C supply the exact
field inventories and review them before freeze. Their domain commands, policy
and stores remain with the post-F owner. This is the explicit contract-only
exception in the file manifest. Add no throwing implementations, no-op services
or alternate definitions to make a standalone branch compile.

All paths in this table are under `src/Pegasus.Core/`.

| Contract file | Post-F owner | Required consumer / shared definition |
| --- | --- | --- |
| `Intake/IntakeContracts.cs` | C | B candidate/provenance projection and A retained-source routing |
| `Intake/ThirdPartyReports/ThirdPartyReportContracts.cs` | C | B typed report candidate superset |
| `Address/InspectionAddressResolution.cs` | C | B S05 query, choices and versioned selection |
| `Cases/OrganizationDirectory.cs` | C | B principal/location/contact projections |
| `Cases/ClaimSourceAdministration.cs` | C | B Claim Source identity and copied snapshot |
| `ImageIntake/ImageIntakeContracts.cs` | C | B pre-case identity/link/custody projection |
| `Triage/TriageContracts.cs` | C | B T reference/link and A/C correspondence context |
| `Documents/DocumentContracts.cs` | B | A logical content reader and C retained document/version contracts |
| `Assessment/EstimateImport.cs` | B | A MCP and B UI canonical import request/result |
| `Assessment/GlassRepairEstimates.cs` | B | A credential/session coordination and B session/gateway contracts |
| `Reports/CaseReportGeneration.cs` | B | A MI/custody and B immutable generation/artifact projections |
| `Reports/CaseReportDeliveryPreparation.cs` | B | A's final `IReportSendReadiness` check and B preparation |
| `Custody/CustodyContracts.cs` | A | B/C `ICaseArtifactCustody`, including holding material |
| `Identity/PerUserExternalCredentials.cs` | A | B `IPerUserExternalCredentialReader` |
| `Operations/StaffMailSend.cs` | A | B `IStaffReportSend`, C `IStaffMailSend` and the single S12 state enum |

F03 compiles these definitions with the existing callers. Before B/C begin,
record the exact F SHA and contract paths in all three owner tickets. A later
change to a shared signature is a common G change, while implementations and
their branch-local DI patches follow their domain steps. Do not add a separate
`Workflow/StaffCorrespondence.cs` contract. No domain feature is claimed complete
at this contract-only checkpoint.

Use decimal monetary inputs, UTC instants, stable typed IDs, expected versions,
source hashes and operation keys; don't place NatCode, VAT rates or display
labels inside an unrelated source-version string. Nullable facts mean unknown,
not zero/No/today. Unique constraints enforce operation replay, one Current
estimate per Case, active provider session and Triage reference identity.

## S03 — source candidates, third-party facts and principal identities

C owns one extraction candidate model, extending current fragments/provenance:
receipt/asset/version/hash/occurrence, document and party/reference roles, field,
raw value, normalized value/unit/currency, source label, page/cell/form/region,
reader/policy version and usable/missing/ambiguous/conflicting state. Multiple
candidates can coexist. It is not a new document aggregate or confidence engine.

B consumes `C-B01/C-B02` via a read-only candidate projection and accepts into
the existing named Case/assessment/estimate/valuation commands. Confirmed
staff facts never silently change. Third-party report totals/verdicts remain
source evidence even when arithmetically wrong; CE findings require B authority.
Instruction and third-party provenance use C's shared chips/viewer and B's
Case section binding. B cannot edit source candidates; C cannot accept findings.

Principal code has one existing reference catalog. Route identity, document
profile activation and usable extraction are separate decisions. Seed the
15 evidenced principals and their full local methods. Unproved sender domains
do not become accepted merely to claim all 15 active automatically. YML is the
principal for the confirmed HDUK family; HDUK stays document issuer. Sender,
intermediary, principal, claimant, insurer, repairer, storage business,
third-party engineer, claim source and recipient remain distinct roles.

## S04 — Triage and normal allocation

C owns Core Triage/reference rules; F owns SQL allocator/constraints. One global
positive integer formatted `T-` plus at least 5 digits, immutable/no reset/reuse.
Creation replay returns the original reference. Gaps from failed concurrent
allocation are allowed. Triage/Image Intake/Unidentified are distinct records,
never aliases for a normal Case. Definitive instruction uses the existing Case/
PO allocator, linking retained pre-case records. A known principal on Image
Intake is nullable and does not allocate or terminate it. Preserve current Audit
allocation rule: uncertain standalone Audit verdict withholds the later Audit
reference; it must not silently change normal allocation policy.

## S05 — inspection location and directory suggestions

C owns principal/default/directory policy and the location suggestion query;
B owns Case selection and saving. QDOS defaults to Image Based Assessment even
with an extracted repairer address. All CE assessments are desktop; a selected
physical address must not imply attendance. Raw external requested/observed
inspection method remains evidence, independent of CE method/report location.
No physical-inspection CE workflow is introduced by copying prototype options.

Use the existing address-resolution command for expected-version selection,
accept/correct and reasoned override. Add bounded directory-backed suggestions
from current claimant/repairer/storage and principal's previously accepted
locations plus Administrator-maintained locations. Query by normalized name/
postcode prefix, at least 2 characters, default 20, maximum 20, stable ID tie-break;
return label/address/role/source/version. Selection copies the accepted address
and provenance; later directory edits do not rewrite a Case. Manual entry is
always supported. This is a local address suggestion implementation, not a
nationwide postcode-provider integration. No new vendor subscription/package.

## S06 — estimates and valuation

B alone owns `EstimateTotals.Compute`, valuation preview/apply and accepted
Engineer value. Web/MCP/report/UI never recompute monetary policy. Arithmetic,
rounding, discount/VAT order and original source comparisons are fixed by B03/
B04. Unknown repairer VAT requires explicit status or category choice. Claimant
VAT affects valuation/settlement policy separately. Named estimates remain
Draft until explicit acceptance/Use as Current; importing Glass's or another
source never changes Current automatically. Fixed additions copy versioned
preset values; a preset edit never rewrites an applied snapshot.

## S07 — Glass's credentials and durable session

A owns `IPerUserExternalCredentialReader`, the credential store and existing
Data Protection primitive/key ring. B owns the protected Glass session row and
its store; cookie/CSRF ciphertext is produced using A’s primitive. B owns `IGlassRepairEstimateSessionStore`,
`IGlassRepairEstimateGateway`, repair-estimate policy, admin Glass's page,
launch/callback and canonical estimate import. F owns entities/config/DI.
One active session per configured external account, including the same username
configured on two Pegasus users, requires a normalized provider-account key
without leaking a password. Do not lock only by Pegasus user or generation;
credential replacement invalidates an old session before a new one starts.

Store Case/user/account/generation, state/version, operation key, timestamps/
expiry, provider vehicle/ERE identity, one-use callback correlation digest,
protected resumable cookie/CSRF material, source XML/PDF artifact references and
failure/unknown outcome. Cookie state is encrypted SQL, not process memory or
sticky replica affinity. Never expose it in browser HTML, logs, MCP or reports.
Use existing Web HTTPS callback, one-use expiring nonce, server session context,
allowlisted provider return locations and identity checks. Callback is not a
generic URL fetcher. Login/session/provider acceptance is independently checked
even when HTTP says 200. No automatic retry after uncertain external creation.

Only the real configured signed-in engineer can launch or resume. Initial
alex credential entry and all live provider tests belong to the operator.
Artifacts feed the same B canonical importer through S08 and create one Draft,
never an automatic Current estimate. No Glass's valuation service.

## S08 — durable artifact metadata and cached bytes

One `ICaseArtifactCustody` write contract supplies immutable source or generated
artifact bytes: authorized actor, Case/holding destination, occurrence and
operation key, media kind, proposed name, size/hash, bounded stream. Result is
logical document/version, Box file/version, verified hash/size/media identity
or explicit pending/failed/unknown result. Reuse existing custody records and
retry semantics; do not add separate Glass/report writers. B owns generated
artifact metadata/report snapshot; A owns file custody/content bytes.

Existing `IDocumentContentStore`/source reader resolves logical versions via
the same A cache boundary. SQL source metadata no longer relies on Azure key
permanence. Pending custody cannot age out. Confirmed Box versions use 24-hour
idle cache with bounded cleanup; current account recovery retention is separately
documented. Authentication keys, queues and Function packages are excluded.
Every hit/stream download checks actor/Case/source authorization. B stores image
rotation/crop/role/order as versioned metadata, never alters original bytes.

## S09 — report snapshot, preparation and transport

B owns projection/readiness/generation/artifact metadata and preparation. A
owns `IStaffReportSend` backed by the general staff send operation; use this
single B-facing interface, not a second mail outbox. Input is server actor,
approved mailbox, Case/version, generation/artifact versions/hashes, To/CC,
subject/body, operation key and expected preparation version. B provides the
generation-freshness validator called immediately before A sends. General
correspondence uses the same transport with its explicit purpose, compose mode
New/Reply/ReplyAll/Forward, retained original-message/thread identity and authorized
attachments; it does not pretend to be a generated report.

`IReportSendReadiness` belongs to B's delivery-preparation contract. It loads
persisted Case/generation/preparation state and validates the server actor,
expected versions and exact artifact hashes immediately before A's send side
effect. Its implementation is B-owned; A consumes it. `IStaffMailSend` is C's
general correspondence entry point into the same A transport and state machine.
These interfaces have named current consumers and introduce no second outbox.

Mailbox configuration includes the verified effective encoded-message size
ceiling, verification time and actor; Send cannot enable while it is unknown.
Freeze the send state vocabulary in A03 once. B/C display it through a shared
projection; no browser-created Sent flag. Recipient edits do not stale generated
bytes; relevant accepted facts/image preparation/template change does. Preview/
generate/prepare is not send. Every report/chaser email starts with a staff
action; background reconciliation cannot generate a new send. Unknown is never
blindly retried. Optional EVA remains an explicit separate action and never
gates the complete Pegasus report. Automatic EVA/chasers are not activated as
part of v1; remove their executable v1 configuration where superseded.

## S10 — administration configuration matrix

| Surface | Owner | Supported UI controls / data source |
| --- | --- | --- |
| Accounts, roles, access | A01 | Create/edit, enable/disable/delete access, reset, logout, targeted lease clearance; existing Identity |
| Signatory identity | A01 with B report contract | Printed name, qualifications, signature, eligibility; versioned account tuple |
| Glass's | B04 with A01 secret boundary | Per-engineer username/secret replace/clear/enabled state; initial alex |
| Principal/organization/directory | C06 | Codes, contacts/roles, current policies, route evidence, location defaults and optional manual EVA |
| Workflow configuration | B02/B08 | Actual instruction/image completion policy and supported workflow settings; remove review/Confirm requirements |
| Rates, fees, valuation presets | B03/B04/B05 | Versioned rates/discount defaults/VAT choices, preset additions, fee inputs; accepted snapshots stay fixed |
| Mailboxes | A02/A03 | Identity/access test, intake/Sent/send capability, enable/disable, start boundary and status; Administrator only |
| Automation/AI | A06 | Configured transport availability, jobs/stop/failure/activity; no fake production activation or self-approved finding |
| Health and action logs | A06 | Component freshness/errors/queues/cache/send Unknown, actor/result/correlation with filters |
| Reports | A06 | Engineer activity, principal counts, true timestamps/denominators/holding age and export |
| Shared nav/layout/settings appearance | C08 | One shell/menu and persisted user display preferences; no duplicated admin navigation |

Infrastructure remains infrastructure: Microsoft 365 mailbox provisioning/scope,
Key Vault key/certificate references, Box parent IDs, Azure endpoints/deployment
settings and allowed external origins use existing infrastructure setup and
runbook. No arbitrary environment/SQL editor. Product configuration can be done
by an Administrator through UI without editing a database or code.

## S11 — shared UI and test files

C owns layout, global navigation, shared admin nav, CSS/site.js/icon sprite,
shared provenance/evidence/status primitives and labels. B owns Case partials,
Case-only CSS/JS and all Case routes; A owns its admin bodies. The three consume
one published token/partial/DOM contract; no duplicate component library.
Snapshot files follow the owner of the routed page; global catalogue/scripts/
shared test fixtures remain F. The combined checkout produces final regenerated
catalogue outputs through A, with each owner reviewing its page images.

The path-level owner manifest is authoritative. Its `foundation_writer` and
`post_foundation_writer` fields distinguish the two phases. F is a Codex/A
phase, never a fourth machine. A authors the small lease-clearance primitive
in the explicitly named workflow files before B takes their later domain
edits. A also authors only the S02 cross-stream definitions in the named B/C
contract files during F; their behavior remains B/C-owned after F. A remains
the single author of global DI/host files after F, including
the serialized branch-local patches. Schema/shared-contract phases are
A-owned even when later domain implementation is B/C-owned. Any new shared
file requires a named single owner and a concrete current caller. Cross-stream
review never grants permission to edit another owner's files directly.

## S12 — outbound attempt transitions and ambiguous draft lookup

Persist an attempt stage (`CreateDraft`, `Attach`, `Send`, `ObserveSent`),
mailbox generation, operation ID, payload hash, draft immutable ID if learned,
upload-session reference/expiry if present, requested-at and last error beside
the single send operation. Protect provider session/upload URL material and
never log its token. This is bounded recovery data, not another event system.

| From | Condition | To / permitted next action |
| --- | --- | --- |
| Prepared | Authorized staff confirms exact frozen payload | DraftCreating; one create request |
| DraftCreating | Definitive created draft identity retained | DraftReady; attach missing exact artifacts |
| DraftCreating | Definitely rejected before creation | Failed; staff may correct into a new operation |
| DraftCreating | Possible creation, identity unknown | Unknown/CreateDraft; lookup only, no second create |
| DraftReady | All attachment IDs/hashes verified, freshness rechecked | Sending; one send request |
| DraftReady | Ambiguous attachment upload | Unknown/Attach; inspect recorded draft attachments/session, no blind repeat |
| Sending | Provider returns 202 Accepted | Submitted; observe Sent only |
| Sending | Possible side effect, response lost | Unknown/Send; observe Sent/draft identity, never automatically send again |
| Sending | Definitive pre-send rejection and draft still proved unsent | Failed; staff may explicitly retry the same draft after correction/recheck |
| Submitted or Unknown | Exactly one matching Sent message/operation/artifacts | Sent; immutable observed evidence |
| Prepared or DraftReady | Staff cancels before possible send | Cancelled; recorded remote draft remains, no implied Graph deletion |
| Unknown/Send or Submitted | Staff requests cancellation | Refused as outcome uncertain; reconcile first |
| Sent | Any repeat/retry | Return prior result; no new message |

For uncertain draft creation, enumerate the approved mailbox's Drafts folder
over a bounded requested-at window, page size 50, cap 500 per reconciliation sweep,
retaining continuation if more candidates exist. Select custom internet headers
or read candidate MIME using the existing Graph client. Match
`x-pegasus-operation-id` plus the frozen mailbox/generation identity and payload
marker. Exactly one binds its immutable draft ID; zero stays Unknown; multiple
is conflict requiring staff investigation. A capped/incomplete search is never
treated as proof that no draft exists. Verify the supported Graph request shape
with adapter contract tests and later operator proof; do not assume server-side
filtering by an arbitrary custom header. No second draft creation after an
ambiguous zero-result lookup. Sent polling reads the same marker and matches
message/generation/attachment identities; internet message ID alone is not the
whole contract. Lost correlation remains visible and cannot assert Sent.

## S13 — retained analysis, scanned pages and Claim Sources

C01 implements `AnalyzeRetainedInstruction` as a real production command from
the Received page and the existing intake path. It reads A04's exact logical
source version, runs the applicable C03 document profile, and persists typed
candidates before principal allocation. All fifteen profiles are reachable
through this command. A document match proposes identity; only staff
confirmation or a separately accepted sender route can authorize Case/PO
allocation. Replays use receipt/source version and operation key.

C02 uses the existing Worker external-work path for page-qualified OCR.
`IProcessIntakeOcr` calls C's `AzureDocumentIntelligenceOcr`, using the existing
HTTP/Azure.Identity dependencies and `prebuilt-layout` REST API `2024-11-30`.
Retain the source hash, selected page numbers, operation identity, reader
version and page/region locators. Resume a known operation after a restart;
uncertain submission is reconciled rather than blindly repeated. Feed the
result into the same candidate model and analysis command, without a second
policy owner or a new runtime. Missing genuine OCR output is INCONCLUSIVE.
A owns endpoint/identity configuration, global schema/grants and Worker routing;
no live provider call or infrastructure change is authorized by this package.

C06 maintains the explicit Claim Source directory (stable ID, name, contact,
telephone, email, notes, active/version/audit). B selects its ID and copies the
accepted contact snapshot into the Case. It stays distinct from principal,
sender, insurer and third-party engineer. A's F02 migration/bootstrap seeds the
fifteen principal IDs/codes supplied by C03; document methods and route-evidence
activation remain separate configuration facts.

F freezes these contracts and entities. Once C03's real profiles and C02's
adapter exist on the recorded C head, A supplies the hash-recorded branch-local
DI/Worker patch described in COORDINATION. Before the final C head is accepted,
routed tests must use that production registration, not just a test container.
