# Staff identity, authorisation and action history

## Purpose

Establish authenticated CollisionSpike staff identities, role boundaries and permanent attributable action history before a deployed caller can accept or alter casework. This area owns actor and authorisation policy; casework owns business-transition policy.

## Authority and current boundary

- **Authority:** [source order](../../../agent-guidance/source-of-truth.md), [questionnaire §§3–4 and 10–12](../../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md), [remaining requirements §1](../../remaining-requirements.md), and [ADR-0004](../../../architecture/decisions/ADR-0004-provider-api-and-staff-mcp-authentication.md).
- **Policy owner:** planned Core `StaffAccess` actor/authorisation contracts, with Web authentication composition; business transition authority stays in Casework.
- **Current implementation:** Web calls `UseAuthorization` but registers no authentication/identity scheme; receipt-owned `IntakeReceiptEvents.Actor` accepts the development intake string. There are no staff accounts, role enforcement or permanent trusted actor derivation.
- **Real callers:** `/Intake/Upload` is the only current real intake caller and is deliberately unavailable outside Development; all authenticated staff pages, bootstrap, administration and future MCP are **planned**.
- **Persistence/adapters:** one existing EF migration stream/DbContext; planned ASP.NET Core Identity data, staff status/roles, action-history actor/event and correlated outbox records. No Entra application sign-in.
- **Dependencies:** the application spine/explicit migrations, then every casework caller. Provider/API and MCP authentication are separate later boundaries.
- **Replaces/consolidates:** replace request-supplied/free-text actor values and unauthenticated page access; do not add another account store or parallel role evaluator.

## Shared failure and observability rules

Deny by default except deliberately public technical health endpoints. Passwords are secure non-reversible hashes; public registration is disabled. Administrator, Engineer and User may perform case transitions and the pre-assignment review gate; Administrators alone manage staff accounts, principals, approved-mailbox allowlists and configuration. Trusted actor comes from authenticated context, never a request body. Permanent action history covers business mutations, downloads/exports, material denied or failed business actions, automated business results, external information actually accepted/linked/used, and account/role/disable/credential/configuration administration. Sign-ins belong in the separate security log; routine views, searches, refreshes, polling, retries, leases, heartbeats and adapter mechanics use content-safe telemetry. None records passwords, secrets, file bodies or message bodies.

## Authenticate staff and enforce role boundaries

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** questionnaire §3 and §12; remaining requirements §1; ADR-0004.
- **Confirmed facts:** staff use application-managed usernames/passwords, no external customer users/MFA requirement in first MVP, and an Administrator creates/disables/reviews accounts and assigns roles.
- **Decision required before implementation:** none. Initial bootstrap credentials must be supplied securely at execution time, never planned as a source/configuration secret.

### Owner and dependencies

- **Policy/implementation owner:** Web authentication composition plus Core `StaffActor`/role contract; existing DbContext/migration stream remains sole persistence owner.
- **Independent evaluator:** security-focused reviewer and test engineer; operator validates bootstrap/access administration.
- **Prerequisites:** explicit migration release path and deployment secret custody.
- **Consumers/unlocks:** protected staff UI/casework, authoritative action history, later MCP role evaluation.

### Caller, contract and change boundary

- **Real or intended caller:** planned sign-in/sign-out and authenticated pages; `/Intake/Upload` stays development-only until the replacement authenticated custody caller exists.
- **Input/output:** username/password yields secure cookie/session and current role, or generic refusal/lockout outcome; disabled account invalidates active access.
- **Ordered decisions and failure behavior:** authenticate; reject disabled/locked account; derive trusted actor/roles; authorise endpoint/action; refuse by default. Administrator-only account/principal/configuration routes are checked server-side.
- **Persistence/migration:** add Identity/staff role/status records through the one ordered migration stream; no startup production migration or seed password.
- **Concurrency/edit ownership:** account and role administration uses an optimistic version; stale Administrator saves are refused. Disabling an account updates its security stamp so an existing session cannot continue protected work.
- **Adapters/side effects:** one-shot existing-Web bootstrap reads an initial secret securely and forces change; it stores no seed secret. No Entra, Graph, provider or MCP call.
- **Operator surface and observability:** simple staff sign-in, account status/role administration for Administrator and generic safe failure messages; authentication/authorisation/privilege telemetry.
- **Documentation affected:** deployment/bootstrap instructions, configuration-name examples only; never credentials or operator-note edits.
- **Replaces/consolidates:** remove unauthenticated access to staff pages and free-text action-history actor inputs in the same slice.

### Scope

- **Included:** local staff accounts, secure password hashing/cookies/lockout, Administrator/Engineer/User roles, disablement, bootstrap and deny-by-default staff access.
- **Excluded:** external accounts, Entra sign-in/B2B, mandatory MFA, provider credentials and staff MCP OAuth implementation.

### Implementation checklist

- [ ] Add one Identity-backed staff store/role model in the existing DbContext and configure global authenticated default policy.
- [ ] Implement Administrator-only account create/disable/role review, protected sign-in/out and one-shot secure bootstrap/change-password flow.
- [ ] Replace development caller access/actor assumptions and leave production intake unavailable until source custody and authorised acceptance are called.

### Validation checklist

- [ ] Test password hash/no credential persistence, generic failed sign-in, lockout, sign-out and disabled-session invalidation.
- [ ] Test two Administrators editing one account/role version; the stale save is refused and the newer value/action-history entry remains authoritative.
- [ ] Test all roles can reach permitted case action routes only when case policy also permits; test User/Engineer denial for account/principal/configuration administration.
- [ ] Verify anonymous access is denied except explicit health paths and actual authenticated Web caller exercises policy.
- [ ] Run migration against SQL Server before release; run `pwsh ./scripts/Invoke-RepoCheck.ps1` and record scoped result/limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Disabled staff account | Cannot create a new session or continue a protected one | integration/browser test | Organisation offboarding process |
| Engineer requests account administration | Server refuses regardless of UI | authorisation test | Case-transition permission |
| Anonymous request to staff page | Denied; health endpoint remains intentionally public | real Web caller test | Deployment network security |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** production migration/initial Administrator bootstrap needs explicit release approval and secure secret input; no cloud write is authorised by this plan.
- **Rollout/activation:** apply migration explicitly, run bootstrap once, verify sign-in and role denial, then protect staff pages before enabling case acceptance.
- **Rollback/recovery:** retain identity/action-history data; roll back application artifact only after confirming compatible schema. Use authorised reset/disable procedures, never expose credentials.
- **Irreversible risk:** accidental lockout; retain a documented, approved emergency Administrator recovery procedure with attributable action.

### Deferred-capability impact

- **Named capabilities:** external/customer accounts, staff MCP OAuth, provider API credentials, Entra Agent ID and later MFA/SSO.
- **Stable seam retained:** application staff identity, roles and trusted actor contract can be evaluated by a future OAuth/MCP adapter without copying authorisation.
- **Future migration/replacement:** token/consent/session policy and external identity linkage need separate accepted designs and migration evidence.
- **Activation boundary:** explicit product/security decision plus approved integration evidence.
- **Deliberately absent:** no Entra B2B, public registration, MFA mandate, provider client store or MCP endpoint.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Identity boundary, caller and security tests are defined | Implementation, deployment or staff acceptance |

## Attribute permanent action history and automation

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** questionnaire §§3–4 and 10 and remaining requirements §1.
- **Confirmed facts:** permanent action history records the bounded material actions above with actor, timestamp, action, structured before/after values, required reason and outcome; it is not a log of every interaction. A report-sent event requires exact Outlook Sent-item evidence from the shared approved-mailbox allowlist; automatic matching remains deferred.
- **Decision required before implementation:** None for append-only application action history. This owner records supplied exact external evidence; it does not decide or infer a Sent-item match.

### Owner and dependencies

- **Policy/implementation owner:** Core `PermanentActionHistory` contract and Infrastructure append-only writer; Web/Worker supply trusted actor/correlation.
- **Independent evaluator:** separate reviewer checks every material case/intake path derives actor from context.
- **Prerequisites:** staff actor contract; shared transaction/outbox spine.
- **Consumers/unlocks:** casework, configuration administration, source receipt/custody and later Worker/API/MCP actions.

### Caller, contract and change boundary

- **Real or intended caller:** planned authenticated staff actions and automated Worker actions; `/Intake/Upload` currently writes only receipt-owned local-development intake events and must migrate to permanent actor-aware business action history when that boundary is decided.
- **Input/output:** a material business action supplies trusted actor, action type, structured prior/new context, required reason where applicable, outcome and correlation and appends one permanent event in the business transaction; automation has an explicit machine/system actor and source identity. Non-material mechanics route to their owning log/telemetry boundary instead.
- **Ordered decisions and failure behavior:** authorise business action first; require entered reasons for hold/release, cancel/reject/reopen, corrections, reversals/unlinks, principal/reference or used-principal replacement changes, logical removal, overrides, and account/configuration changes; commit state/action history/outbox together. Material denied/failed actions record the safe reason/outcome; action-history write failure aborts a mutable success. Downloads/exports record actor, artifact identity/version and outcome without content. Read/query failure is surfaced without fabricating history.
- **Persistence/migration:** add the append-only business action-history owner with actor kind/identifier, timestamp, correlation and safe structured context; do not misrepresent or directly expose receipt-owned `IntakeReceiptEvents` as that catalogue, and add no update/delete route.
- **Adapters/side effects:** outbox preserves event intent for post-commit work; action history does not invoke email, Box, Graph or telemetry directly.
- **Operator surface and observability:** case/workspace history is readable to authorised staff; account/configuration history is Administrator-only; material history-write failures alert content-safely. Sign-in outcomes remain in the security log and routine interaction/retry mechanics remain telemetry.
- **Documentation affected:** action-event catalogue/evidence record after implementation, not a duplicate product specification.
- **Replaces/consolidates:** remove request-provided actor and isolated receipt-only event pattern as each caller is migrated.

### Scope

- **Included:** business mutations; downloads/exports; material denied/failed business actions; automated business results; accepted/linked/used external information; account creation, role, disablement, credential and configuration administration; trusted actor derivation; entered-reason rules; correlation and transaction/outbox boundary.
- **Excluded:** sign-ins/security-log implementation, routine views/searches/refresh, polling/retries, lease/heartbeat/adapter mechanics, message/file bodies, action-history deletion, legal holds, external SIEM implementation and automatic report-to-Sent-item matching.

### Implementation checklist

- [ ] Define one typed `PermanentActionHistory` contract with actor/source/correlation/before-after/outcome and enforce it from each named material Core action; route non-material interactions explicitly to security log or telemetry.
- [ ] Migrate existing receipt events and reject a mutable action when its permanent history cannot commit atomically.
- [ ] Expose authorised read-only case/admin history and add content-safe failure/security telemetry.

### Validation checklist

- [ ] Test hold/release, cancel/reject/reopen, correction, reversal/unlink, wrong-principal case replacement, used-principal-code replacement, logical removal, override and account/configuration changes each emit correct trusted actor/entered reason/prior-new context.
- [ ] Test request-body actor spoofing, missing required reason, material denial/failure and action-history storage failure; state must not mutate on a failed success transaction and the safe denied/failed outcome must remain attributable.
- [ ] Test download/export success/failure records artifact identity/version but no content; prove routine view/search/refresh, polling/retry, lease/heartbeat and adapter mechanics do not create permanent business history.
- [ ] Test sign-in success/failure goes to the security log and never to permanent business history.
- [ ] Test automation actor is distinguishable and replay/outbox correlation does not duplicate business effects.
- [ ] Exercise the `/Intake/Upload` caller and later one planned authenticated action; run `pwsh ./scripts/Invoke-RepoCheck.ps1`.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Authenticated held/cancel action | One immutable event with trusted actor, time, prior/new state and reason | transaction/UI history test | External report delivery |
| Spoofed actor or history failure | Action denied/rolled back; no invented action event | negative integration test | Database disaster recovery |
| Automated receipt replay | Correlated/replay-safe history without duplicate case effect | integration test | Production ingestion scale |
| Report/EVA download or export | one actor/artifact/version/outcome event and no file/message body | caller/persistence test | recipient delivery or content correctness |
| Routine view, refresh, retry or heartbeat | content-safe telemetry only; no permanent business event | negative boundary test | telemetry backend delivery |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** migration/release approval only; external telemetry/alert destinations need separate configuration approval.
- **Rollout/activation:** migrate action-history schema with caller slices, backfill only required v2 local records if accepted, then verify immutable history via actual caller.
- **Rollback/recovery:** preserve action-history records and revert application artifact compatibly; corrective events append rather than alter history.
- **Irreversible risk:** action-history records are intentionally retained; do not offer destructive rollback.

### Deferred-capability impact

- **Named capabilities:** MCP, provider API, full Graph mailbox automation, external accounts and future report/EVA integration.
- **Stable seam retained:** actor kind/identifier, source identity and correlation permit future adapters while preserving one action-history authority.
- **Future migration/replacement:** OAuth/provider/Graph attribution needs adapter-specific trusted identity mapping and evidence.
- **Activation boundary:** accepted integration contract and independently verified actual caller.
- **Deliberately absent:** no external event bus, SIEM dependency, action-history deletion job or automatic report-to-Sent-item inference.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Action-history ownership, atomicity and test boundaries are defined | Implementation, deployment, live history operation or acceptance |

## Administer principals and live operational configuration

**Evidence state:** Planned

### Authority and decision gate

- **Requirement/decision:** [questionnaire role and configuration rules](../../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#3-users-and-organisations), [first-release must-haves](../../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md#15-first-release-scope), and [remaining requirements §1](../../remaining-requirements.md).
- **Confirmed facts:** Administrators alone manage principals and application configuration; QDOS principal configuration and the switchable completeness gate are first-release requirements; existing issued references must not be rewritten or reused. A used principal code is read-only and changes only through the linked-successor cutover owned by [case identity](../casework/case-identity-and-references.md#replace-a-used-principal-code-through-an-immutable-cutover).
- **Decision required before implementation:** None for the bounded QDOS principal record and completeness gate. Any later principal-specific field matrix, transport credential or external-system configuration requires its own authority.

### Owner and dependencies

- **Policy/implementation owner:** Core `PrincipalAdministration` and `OperationalConfiguration` use cases with one Infrastructure persistence owner.
- **Independent evaluator:** a separate test engineer verifies role denial, reference integration and configuration concurrency; an operator confirms the QDOS record and labels.
- **Prerequisites:** authenticated Administrator, permanent action-history writer and one explicit migration stream.
- **Consumers/unlocks:** [case identity and reference allocation](../casework/case-identity-and-references.md), intake principal resolution and Engineer-assignment completeness policy.

### Caller, contract and change boundary

- **Real or intended caller:** planned authenticated Administrator principal/configuration pages; no current caller.
- **Input/output:** an authorised Administrator creates or updates unused principal metadata, invokes the named used-code replacement command, and changes the current completeness-gate value; consumers receive stable linked principal identities, immutable used codes and versioned configuration.
- **Ordered decisions and failure behavior:** authenticate and authorise; validate uniqueness and required values; refuse direct editing of any used principal code and any case principal/reference; delegate a legitimate used-code change to the atomic linked-successor/counter-cutover use case with expected version and required reason; commit configuration change and action history together. Unknown, inactive, conflicting or stale principals cannot allocate or cut over.
- **Persistence/migration:** add principals with stable IDs, names, unique codes, active state and concurrency token plus one typed operational-configuration record; issued cases retain their principal ID, issued reference and permanent action history.
- **Adapters/side effects:** none. Provider credentials, Box folder operations and vendor settings are not principal-administration side effects.
- **Operator surface and observability:** Administrator-only list/edit pages show a used code as read-only and offer the explicit replacement flow with predecessor/successor link, cutover-year next/exhausted preview, later-year `001`, required reason and conflict feedback; privileged changes emit permanent action history and content-safe security telemetry.
- **Documentation affected:** configuration/bootstrap guidance and implementation evidence only; operator notes stay read-only.
- **Replaces/consolidates:** remove the hard-coded `QDOS` principal/code from allocation after the configured QDOS record is called; do not add a second settings store.

### Scope

- **Included:** QDOS principal creation/unused-metadata edit/activation state, used-code replacement caller, unique code consumed by allocation, Administrator-only access, action-history-backed optimistic concurrency and the on/off `Instruction complete` plus `Images complete` Engineer-assignment gate.
- **Excluded:** principal-specific field matrices, provider API credentials, Box/vendor secrets, external customer accounts, bulk predecessor import and arbitrary key/value configuration.

### Implementation checklist

- [ ] Define one typed principal and operational-configuration owner in Core, backed by the existing DbContext and migration stream.
- [ ] Deliver Administrator-only Web callers with server-side validation, optimistic concurrency and atomic action history.
- [ ] Keep used-code input read-only and call the single linked-successor/counter-cutover command; do not reimplement sequence policy in the page or principal-administration owner.
- [ ] Replace hard-coded QDOS allocation input and deployment-time completeness switches with the persisted, authorised values; remove the superseded path.

### Validation checklist

- [ ] Demonstrate the current failure before the guard: two Administrators edit the same principal/configuration version and a stale save could overwrite a newer change; then prove the stale save is refused.
- [ ] Test unique/required QDOS code, unknown/inactive principal allocation refusal and issued-reference preservation after permitted metadata changes.
- [ ] Test used-code edit refusal plus successful/stale/concurrent/exhausted replacement outcomes through the Administrator caller; assert the case-identity transaction remains the policy owner.
- [ ] Test Administrator success and Engineer/User/direct-request denial through the actual Web callers, including immutable action-history actor/reason/prior-new values.
- [ ] Toggle the completeness gate without deployment and prove Engineer assignment follows the new value through its Core caller.
- [ ] Run `pwsh ./scripts/Invoke-RepoCheck.ps1` and record the exact result and limitations.

### Acceptance criteria

| Scenario/input/boundary | Expected observable result | Evidence | Does not prove |
|---|---|---|---|
| Administrator configures QDOS | One active stable principal/code becomes available to intake/reference allocation and the change enters permanent history | Web-to-Core integration and allocation test | Production configuration correctness |
| Engineer/User or forged request changes configuration | Server refuses; no principal/configuration or success-history mutation occurs | authorisation/negative caller test | Broader network security |
| Stale configuration form | Save is rejected with a refresh/review message; newer value remains | concurrent browser/integration test | Case-edit locking |
| Completeness gate changes | Assignment is allowed or refused from the current persisted value without redeployment | Core/caller test | A principal-specific field matrix |
| Administrator replaces a used code | linked successor and sequence preview/result come from the atomic case-identity command; direct edit remains refused | Web-to-Core transaction/browser test | correctness of operator-entered replacement data |

### Approval, rollout and rollback

- **Approval-triggering action and exact scope:** production migration and initial QDOS configuration need release/operator approval; no cloud, credential or external-system write is authorised here.
- **Rollout/activation:** migrate, bootstrap the first Administrator, configure/verify QDOS, then enable reference allocation and the completeness gate consumers.
- **Rollback/recovery:** preserve principal/configuration and action-history rows; disable dependent callers or restore a prior value through a new recorded forward change.
- **Irreversible risk:** a consumed principal/year sequence and issued reference are never rolled back or reused.

### Deferred-capability impact

- **Named capabilities:** additional principals, provider API credentials, external accounts, principal-specific completeness matrices, custom integrations and predecessor-data migration.
- **Stable seam retained:** stable principal ID, unique code, active status and typed/versioned configuration can be consumed by later adapters without changing issued identities.
- **Future migration/replacement:** each new principal needs approved reference data; credentials and principal-specific workflow policy need separate contracts and migrations.
- **Activation boundary:** direct product/security decision, verified operational data and integration approval for the affected principal or setting.
- **Deliberately absent:** no generic settings bag, secret store, dormant provider client, external account, imported predecessor record or configurable field matrix.

### Completion evidence

| State/command/input | Result | Boundary exercised | Proves | Does not prove / skipped |
|---|---|---|---|---|
| Planned | Not run | Planning review | Principal/configuration caller, policy, concurrency and evidence are defined | Implementation, configured QDOS data, deployment or acceptance |
