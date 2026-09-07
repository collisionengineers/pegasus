# Operations

This file is the current-state record for production, releases, evidence
profiles, monitoring, and recovery. Executable setup, development, database,
testing, release, approval, monitoring, and recovery procedures are owned by
the [runbook](runbook.md). The evidence-tier ladder and repository verification
rules are owned by [engineering](engineering.md#required-evidence-tiers).

## Evidence and authority

### Read-only production observation — 6 September 2026

On 6 September 2026, Azure CLI on the Windows development host read the existing
`rg-pegasus-prod` Container App. The sole active revision was
`pegasus-prod-web-252ow37gij--0f0e90ae44ff`, Healthy, with 100% traffic.
Its image digest was
`sha256:b791d9587224d30d68fd6abcbd1e1d5f389f2baefc3702d9ec2d2f37398eef15`,
matching release 38 below. `Features__AutomationMcp` and
`Features__ProviderApi` were both `true`; no explicit `Features__SendToAi`
environment value was present. This read checked revision, image and those
settings only. It did not recheck SQL, provider credentials, external-client
round trips, document rendering or operator acceptance.

The v1 branches are development work and have not changed that deployment.
Their persistent OAuth keys, staff-send transport, cache, new queries and
administration pages require the separate reviewed release and activation
process. No cloud, mailbox, Box, Glass's or EVA writes were made by this task.

A Windows-only diagnostic on 6 September built a local Linux/amd64 OCI Web
archive from the A development tree at `8e6f3b21d`. Static inspection applied
all 14 layers and 56 whiteouts and confirmed the final image includes
`Pegasus.Web.dll`, Playwright 1.61.0, its Linux Node driver, Chromium revision
1228, fonts and native libraries. The 1,426,020,864-byte archive SHA-256 is
`c83e1bcb60d5b8a4b227b3b2f412493015b3b5d58504b9133114ef888fcd3614`;
its local audit is `artifacts/v1-web-diagnostic-artifact-audit.json`. This
proves packaged file presence only. No Linux browser execution, clean release
artifact, registry publication or deployment was performed by that diagnostic.

Use these evidence states literally and independently:

`Planned` → `Implemented` → `Called` → `Locally verified` → `Deployed` → `Live verified` → `Accepted`

Compilation, registration, mocks, local execution, deployment, live-service
observation, and operator acceptance are different conclusions. The
authenticated `/Upload` POST through `ReceiveIntake` is the manual HTTP staging
caller; Worker owns queued processing; `/Received` and `/Inbox` are read-only
views. Source registration is not proof of deployed or live traffic.

The repository release route is locally ready on Linux x64: .NET 10,
PowerShell 7, Azure CLI, Azure Developer CLI, Bicep, ORAS 1.3.4 and the
SqlServer module are available in WSL, and new artifacts use manifest schema 3
with a Linux `efbundle`. Azure CLI and azd are not currently authenticated in
this WSL instance, and no Linux-built artifact has been promoted or deployed.
Authentication, `MERGE AUTH GRANTED` and exact-target write approval remain
separate prerequisites for the production cutover.

The assessment renderer is **deployed with a reachable operator caller** since
release 12 (2026-08-19): the Web image carries the pinned Chromium build, and
the case assessment page offers a "Report draft" action that renders and
returns the PDF. For a live case it currently fails closed listing "Repair
cost figures" among the outstanding readiness items, because no estimate
import exists yet (ENG-002); rendered-output evidence in the deployed
container therefore remains pending a case with imported costs. The Core draft-generation use case is composed through
Infrastructure in the Web host and representative assessment plus fee-note
artifacts render through real Chromium in the Browser test lane. The published
Web container image now carries the pinned Chromium build through
`ContainerBaseImage` (`mcr.microsoft.com/playwright/dotnet`, tag-locked to the
`Microsoft.Playwright` package version) and its platform, config, exposed
port, entrypoint, and inherited Chromium/browser layers are locally verified
against the OCI archive with `oras` (ADR-0028, DELIV-012). No automatic
accepted-assessment trigger, durable report reference/custody workflow, Azure
deployment, health/capacity result, approval, issue, or sending is claimed by
that evidence; DOCS-001 and PLAT-007 own those later gates.

<a id="approved-box-integration-test-target"></a>

## Approved Box custody root

Box folder `405543781910` ("pegasus") is the production custody root: all case
folders are created only under it, and the deployed configuration carries it.
Folder `392761581105` is the only eligible controlled integration-test
boundary, confined to an approved disposable test subtree; neither folder is
standing write authority. The exact-target approval and invocation checks are
owned by the [runbook's live-operation approval matrix](runbook.md#live-operation-approval-matrix).
The activated production caller is confined to case-scoped objects under the
configured root and has no delete, move, copy, or share operation. Failed
attempts remain visible for authorised staff retry; there is no automatic
business retry.

Production server authentication uses the retained `box-config-json` JWT
configuration and `box-client-secret` Key Vault secrets. The Box SDK obtains and
refreshes short-lived authorization headers at runtime; a static access token is
not an accepted setting or deployment input. Since release 3 both hosts resolve
their own copy of these secrets server-side only — the Worker through app
setting Key Vault references and the Web through Key Vault-backed Container
Apps secrets, each via its own managed identity (see the
[production environment Secrets record](#production-environment)) — never
client-side.

The intended application staff accounts are Pegasus Identity accounts. The DevelopmentOffline profile authenticates its deterministic local Administrator fixture and enforces its Administrator role. Application staff identity initialization remains a separately controlled application operation; Entra users must not be assumed. Third-party credentials must never enter tracked settings, command-line arguments, prompts that may be retained, terminal output, telemetry, or business history.

## Evidence profiles

The current operational baseline is the [offline development profile](runbook.md#offline-development-profile). The following
caller-scoped profile model distinguishes implemented local gates from planned
activation; installing a tool never establishes a caller.

| Profile | Current gate and planned boundary |
| --- | --- |
| `Baseline` | Windows or Linux, PowerShell 7, Git/GitHub CLI, pinned .NET 10 SDK, Azure CLI with Bicep, Azure Developer CLI, Node/npm, Python, Infisical CLI, and Box CLI for build, test, Bicep validation, and approved administration. Cloud/vendor tools remain optional in the current offline baseline. |
| `SqlServer` | The platform's supported SQL Server (LocalDB on Windows, a container on Linux) and `sqlcmd` for migrations, constraints, transactions, allocation concurrency, outbox atomicity, and local backup/restore. |
| `StorageWorker` | Repository-pinned npm Azurite and Functions Core Tools v4 exercise the existing Blob/Queue adapters and Worker triggers, including retry, poison and restart paths. Local profile proof is distinct from deployed trigger execution. |
| `Browser` | The `Browser` trait pins Microsoft Playwright for .NET, Chromium, and Deque axe-core. It drives the rendered DevelopmentOffline Operations, intake, Triage, administration, password-change, and case-document routes through a loopback Kestrel host. Its semantic, keyboard, focus, responsive, 200%-equivalent, forced-colour, reduced-motion and axe assertions are the selected release accessibility evidence. They do not prove screen-reader interoperability, complete WCAG conformance, subjective usability, or operator acceptance. External approvals, deployment, and operator/management acceptance remain separate gates. |
| `Graph` | Microsoft Dev Proxy and mocked Kiota request adapters for paging, throttling, 401/403, 429, 5xx, timeout, authentication, and retry. |
| `Observability` | OpenTelemetry in-memory exporter and an optional native Collector for correlation, attributes, health signals, OTLP, and redaction. |
| `Performance` | No lane. The nightly in-process pressure probe was retired on 2026-08-18 (DELIV-007) as diagnostic-only CI that gated nothing; the trait stays reserved for a future lane with an accepted capacity claim. |
| `Security` | .NET dependency vulnerability checks and OWASP ZAP; ZAP uses the conditional container profile. |
| `Containers` | A container runtime (Docker Desktop in Linux-container mode on Windows, the native engine on Linux), conditionally for ZAP, optional telemetry, optional SQL compatibility, or a specifically approved licensed Document Intelligence container. Docker is never required merely for Azurite. On Linux the local database is a container, so a container runtime is a base prerequisite there rather than a conditional one. |
| `LiveIntegration` | The existing approved developer identity/secret tooling and exact SDK/CLI owned by the feature. Never part of the default local check. |

Storage Explorer, SSMS, and Postman are optional conveniences.

Do not add Service Bus, Event Hubs, Cosmos DB, Redis, PostgreSQL, Azure Files, ADLS, local SMTP infrastructure, Testcontainers, or related emulators without a later accepted architectural need.

`OfflineCandidate` is the only profile of
[`Invoke-QdosAlphaAcceptance.ps1`](runbook.md#qdos-offline-candidate-runner).
It is fail closed and remains unavailable
without the approved immutable dataset, caller manifest, and run-owned local
evidence required by the runbook. Its capability-coverage check is owned by the
runner script and reads the alpha roster from `docs/capabilities.md`; the
application registers no acceptance gate. It never promotes offline evidence to
deployed, live-verified, release-accepted, QDOS operator-accepted, or Collision
Engineers management-accepted evidence.

Traits currently in use are `SqlServer`, `Browser`, `Corpus`, and
`QdosAlphaAcceptance`. Additional stable planned traits (unused until their
lanes exist) are `Unit`, `Integration`, `Storage`, `FunctionsHost`,
`Performance`, `Security`, `Recovery`, and `LiveIntegration`.

A required but skipped selected trait fails. Optional inactive profiles do not block baseline work.

## Local and live evidence boundaries

| Boundary | Local evidence | Separately approved live evidence |
| --- | --- | --- |
| ASP.NET Core / Container Apps | Kestrel, `WebApplicationFactory`, Playwright, local HTTPS, local OCI-layout inspection | Linux/AMD64 Container Apps Consumption runtime, digest-pinned ACR pull, always-warm minimum replica, probes, revision restart, managed identity |
| SQL Server / Azure SQL | Disposable LocalDB for migrations, locking, allocation, rollback, backup, and restore | Entra identity, Azure SQL configuration/throttling, point-in-time restore, 15-minute RPO and four-hour RTO |
| Blob / Queue / Functions | Azurite and actual Functions host for staging, identifiers, duplicate/poison/restart behavior | Storage RBAC, managed identity, durability, Flex scale/concurrency, platform diagnostics |
| Key Vault / identity | Mock the owned port; developer credentials only for approved development resources | Deployed managed identity, least-privilege RBAC, firewall behavior |
| Application Insights / Log Analytics | In-memory OpenTelemetry and optional local Collector | Ingestion, sampling, KQL, retention, alert rules, recipient delivery |
| Graph / Exchange | Kiota fake and Dev Proxy; allowlist rejects unknown mailbox/folder/action before client call | Approved mailbox allowlist, Exchange Application RBAC, immutable IDs, delta behavior, exact Sent-item existence |
| Box | Fake SDK/HTTP contract for folder/file commands, custody, versions, idempotency, and failures; the approved Box integration-test target may also create/update controlled non-corpus artifacts for local or explicitly approved non-production deployment evidence | Real custody, permissions, versions, recovery, production target, and caller evidence |
| Document Intelligence | Candidate-routing and response-contract tests with controlled non-corpus fixtures | OCR accuracy, confidence, API drift, cost, throttling, identity; licensed disconnected containers are not the default emulator |
| DVLA/DVSA | Deterministic contracts, invalid identifiers, retries, unavailable-service outcomes | Entitlement, identity, real response behavior |
| EVA | Exact local JSON/image-bundle contract and reconciliation metadata; API submission proved against the vendor's own recorded traffic — the camelCase success envelope, the `RequestFrom` and unbound-field rejections inside HTTP 200, and the `text/plain` 500 | Operator drag/drop acceptance; a first submission to EVA from Pegasus, which has never happened in any environment; and the operator-gated live-credential swap |
| EVA API credentials | Required in Production from the release that ships EXT-04: `Eva__ClientId` and `Eva__ClientSecret` resolve from Key Vault through `EVA_CLIENT_ID_SECRET_URI` and `EVA_CLIENT_SECRET_SECRET_URI`; `Eva__BaseUri`, `Eva__RequestFrom`, `Eva__InspectionType` and `Eva__InstructionEmail` are plain settings. Web fails to start if any is missing. EVA serves test and live from one host, so the credential pair alone decides which environment a deployment talks to | Live credentials, which are a separate operator-gated change |
| Provider API | **Live from release 37**: `Features__ProviderApi=true` on the Web app, `POST/GET /api/provider/v1/submissions` mounted behind the `PegasusProviderApi` bearer scheme with a database-backed `PrincipalApiCredentials` store. Verified after deployment: an unauthenticated `POST` answers **401**, not 404 — the route exists and admits nobody. | **No credential has been issued**, so no provider can call it yet; issuing the first one is a separate exact-target approval ([capabilities](capabilities.md)). Real caller evidence still outstanding. |
| Automation MCP | Implemented; composition gate **enabled in production by release 9** (ADR-0026) with a Key Vault-backed client secret; integration tests drive token issuance, denial, tool calls (including the direct-write assessment tranche), and the kill switch over HTTP; live token/inventory/denial/history/kill-switch evidence recorded on 2026-08-18 under Production environment | Real external client evidence, production certificate/transport decisions, and separately approved activation |
| Send to AI channel hand-off | Implemented but composition-gated off by default (`Features:SendToAi`, DevelopmentOffline only); integration tests drive the pointer hand-off, refusal, reconcile, and the Administrator switch against a local fake connector | The recorded round-trip evidence run with a real Claude Code channel session, and any production activation, which additionally needs a non-preview transport decision (ADR-0031) |
| Direct authorised-terminal deployment | Bicep compile/lint and local configuration checks | Approved preflight, package/migration identity, deployment, health smoke, rollback |
| Backup/recovery | LocalDB backup/restore into a new disposable database | Azure SQL PITR and the one-time alpha RPO/RTO exercise |

Managed identity itself is unavailable locally. LocalDB does not prove Azure SQL Entra, throttling, backup, restore, RPO, or RTO. Azurite does not prove Azure Files, ADLS, Entra/RBAC, managed identity, durability, replication, quotas, networking, scale, or production timing.

A read-only Azure census on 7 September 2026 returned no
`Microsoft.CognitiveServices/accounts` resources in the current subscription.
The v1 OCR work did not create a resource, grant access, configure an endpoint
or deploy the optional Worker adapter. Provider activation and live OCR
evidence remain absent from this implementation record.

Graph Sent-item evidence does not prove recipient delivery or automatic case matching.

### Automation MCP is implemented and enabled in production

The Automation Actor ingress (MCP-01–04, MCP-06) is implemented inside `Pegasus.Web`
and composition-gated off by default: unless `Features:AutomationMcp` is
enabled, no `/mcp` endpoint, `/connect/token` route, or resource-metadata
document exists and the application keeps failing closed by exposing no such
ingress. Until ADR-0026 the flag was accepted only in the DevelopmentOffline
runtime profile; since release 9 (2026-08-18) the production Web revision
renders `Features__AutomationMcp=true` from Bicep with the Key Vault-backed
client secret, and the dated live evidence is recorded under
[Production environment](#production-environment). Migration
`20260803151159_AutomationActorOpenIddict` re-created the OpenIddict tables
(the dormant set from `20260729150000_DocumentCustodyAndRequests` had been
dropped by `20260730203833_RemoveDormantOpenIddict`) with the Web-only
least-privilege grants, and they now back the single seeded Automation
client-credentials registration.

When enabled, the ingress issues short-lived scoped access tokens
(`automation.cases`, `automation.intake`, `automation.documents`,
`automation.assessment`, `automation.mail`) for exactly one vendor-neutral Automation client
whose identifier and secret come from configuration/user-secrets and are
never tracked or displayed. Every tool invocation is permanent action
history attributed to the Automation actor with a correlation identifier;
denials write `automation_*` security events; Administrators review both in
the Administration Automation activity view and hold an immediate kill
switch (disable refuses new tokens outright and rejects already-issued
tokens within seconds). A staff browser identity is not a substitute for
that actor and is never accepted on `/mcp`.

Every automation action is recorded exactly as a human action is (ADR-0031):
the registered tools wrap the same Core commands, edit lease, operation-key
replay, and version guards as the staff app, assessment values written by
the automation carry the unconfirmed mark until staff review at manual
engineer assignment, and the migration
`20260803205759_SendToAiAssessmentToolset` adds the assessment field,
estimate line, work-request, and Send to AI control tables with the same
Web-only least-privilege grants.

The Send to AI hand-off (`Features:SendToAi`, DevelopmentOffline only) is
composed beside it. Local setup for an evidence run: generate a channel
token of at least 32 characters, store it with `dotnet user-secrets set
"SendToAi:ChannelToken" <value>` on `Pegasus.Web` (never tracked, displayed,
or logged), start the local `pegasus-claude-channel` connector on
`http://127.0.0.1:8629` with the same token, start the Claude Code session
with its channel loaded, then enable both feature flags. The assessment
page's Send to Claude panel hands off a pointer only; `Sent` maps to the
connector's forwarded claim, never to “the provider read it”; the reconcile
control reads the connector's reply record and flips the tracking state
only. The Administrator Send to AI switch on the Administration Automation
page refuses new hand-offs immediately; the Automation client kill switch
cuts the return path.

Local evidence so far is tier 2–4: green build plus focused integration
tests driving token issuance, transport and scope denials, tool calls with
action-history proof, and the kill switch over real HTTP against the
composed application. Tier-5 evidence from an external real client (for
example Claude Code presenting a bearer token), production
certificate/transport decisions, deployment, and live activation remain
separately approved work.

**Production configuration attempt — 2026-08-18.** The approved configuration-only
activation created `automation-mcp-client-secret` in the production Key Vault,
granted the Web managed identity Key Vault Secrets User on that exact secret,
and composed a new Container Apps revision with the feature setting and secret
reference. Revision `pegasus-prod-web-252ow37gij--0000002` failed startup with
`Features:AutomationMcp requires the DevelopmentOffline runtime profile`; it
never received traffic. The gate was returned to `false`, and revision
`pegasus-prod-web-252ow37gij--0000003` is healthy with `/health/live` and
`/health/ready` returning 200 and the MCP routes closed. This proves that the
deployed image requires a source change before live activation; it is not a
configuration-only gate.

**Replacement-image attempt — 2026-08-18.** Source revision
`a593bc890cf14b247841c1e878230f919e2e7f94` removed only the former
DevelopmentOffline composition check and was uploaded as Linux/AMD64 image
`sha256:e5d1d01d36039cfb220b941bd442846016baf06a670d95630797a4653ac7d072`.
Its enabled revision did not become ready: the database readiness check reported
that the configured schema is not current. No migration was applied. It was
rolled back to healthy revision `pegasus-prod-web-252ow37gij--rollbacka593b`,
using the previously deployed image with `Features__AutomationMcp=false`;
health endpoints returned 200 and `/mcp` was closed. That out-of-band image
was never deployed again: release 9 (below) applied the two pending migrations
and provisioned the promoted `main` revision with the gate enabled from Bicep.

**Live activation evidence — 2026-08-18, release 9 (revision
`pegasus-prod-web-252ow37gij--f1e116c6eb93`).** Against the production
`/connect/token` and `/mcp`: a wrong client secret → 401 `invalid_client`
(`automation_token_rejected` security event); client credentials for
`pegasus-automation` with scope `automation.cases` → Bearer token,
`expires_in` 600; `/mcp` without a token → 401 with `WWW-Authenticate: Bearer
resource_metadata=…` (`automation_access_denied`); `initialize` and
`tools/list` → the fifteen approved tools; `pegasus_case_search` → success
with a `Succeeded` ActionHistory row for ActorKind `Automation`;
`pegasus_intake_queue_list` with the cases-only token → refused, "The
'automation.intake' scope is required" (`automation_scope_denied`);
`pegasus_case_get` with an empty id → refused with a `Failed` ActionHistory
row. Administrator kill switch (Administration → Automation): disable → token
endpoint 400 `unauthorized_client` and an in-flight token refused within 12 s
("The Automation client registration is disabled."); re-enable → tokens issue
and tool calls succeed again; the registration was left enabled. Success
evidence used only read tools; no write tool was exercised against production
data. Not proved: an external MCP client (Claude Desktop/Code) session — the
operator's connector configuration is outside this repository.

**Connector flow (ADR-0027, live since release 10).** External MCP clients
authorise by OAuth 2.1 authorization code + PKCE: the browser is sent to
`/authorize`, a Pegasus Administrator with the manage-automation-clients right
signs in (the strict same-site staff cookie means one sign-in per
authorisation) and approves or refuses the connector for the requested scopes;
the code is exchanged at `/connect/token` with the client id, the client
secret and the PKCE verifier, and a refresh token is issued. Redirect URIs are
exact and administrator-managed through `AutomationMcp__RedirectUris`
(rendered from Bicep; the azd input `AUTOMATION_MCP_REDIRECT_URIS` defaults to
`https://claude.ai/api/mcp/auth_callback`). The consent decision is
permanent history (`automation_connector_authorized` / `_denied`); the
tokens act as the Automation actor with the same kill switch, rate limit and
scopes as client-credentials tokens.

## Dated evidence qualifications

The retained evidence observations are qualified as follows:

- A 2026-07-23 corpus inventory describes only the observed local scope and safety boundary; it does not prove current contents, extraction accuracy, workflow behavior, deployment, or acceptance.
- A 2026-07-23 multi-format evaluation used controlled protocol fixtures and pinned genuine samples through the historical Development-only `POST /Intake/Qdos`. The current source route is the authenticated `/Upload` POST through `ReceiveIntake`, followed separately by Worker-owned queued processing. The historical result records sampled QDOS-policy behavior and failure boundaries, not current-caller execution, complete workflow, field-level accuracy, Worker/Graph/Box/Azure behavior, or production acceptance.
- A 2026-07-23 embedded-PDF benchmark used 74 unique PDFs and 567 reported pages from an immutable local QDOS cohort through a disposable benchmark harness. It records comparative embedded-text decoding and marker coverage only; it does not prove literal field accuracy, OCR, future layouts, production runtime behavior, or operator acceptance.
- A 2026-08-03 VRM recognition evaluation accepted the automatic image-registration reading threshold (`INT-17`) at the **0.80** confidence bar with the `INT-28`/`INT-32` match rules, closing former open decision 1; the engine selection is [ADR-0019](adr/0019-in-process-onnx-vrm-recognition.md). The full-cohort run `20260803-092906` covered 2,818 cohort images at the 0.80 bar — 315 suggestions, 3.2% genuine near-misses, 13.7% correctly read third-party registrations, zero technical failures. The one-time holdout confirmation run `20260803-102921` covered 705 untouched images at the accepted 0.80 bar — 88 suggestions at 12.5%, 2 genuine near-misses at 2.3%, 14 correctly read third-party registrations, zero technical failures, consistent with the cohort. This records operator acceptance of the threshold against these cohorts; it does not prove current-caller production execution or future-layout accuracy.
- A 2026-08-17 MAIL-21 volume-cohort run (`QdosEmailCohortTests.VolumeCohortRecordsExactOutcomeCounts`) read a local immutable flat `corpus/*.eml` dump (256 files). Route dispositions: 75 accepted, 167 no-match, 13 needs-sorting, 1 unreadable. Of the 75 accepted-route messages, `qdos_mail_classification` v3 recorded 14 classified (8 `pre-instruction-emails`, 3 `new-instruction-received/audit`, 3 `new-instruction-received/inspection`), 61 unclassified, and 0 ambiguous. Labelled `extraction-corpus/QDOS/{audits,inspections,...}` folders were absent on that machine, so the accuracy/holdout facts skipped. This is local volume evidence only: it does not invent ground-truth labels, prove a labelled holdout, deploy, live-verify, or record operator acceptance.

## Planned EML evaluator

Local working-copy EML evaluation belongs to the separately owned desktop evaluator ([ADR-0016](adr/0016-standalone-desktop-email-evaluator.md)); its allocation is owned by the [capability inventory](capabilities.md) evaluator boundary. This remains an evaluator boundary, not proof that the current real caller was exercised.

EML contract evidence must cover parsing, provenance, corruption, nesting, cancellation, resource limits, deterministic failures, and content safety. Product-behavior claims require the current Web or later Worker caller; a standalone evaluator or historical endpoint is insufficient.

DOC and MSG automatic extraction remain deferred until safe local parsing fixtures and a human-reviewed genuine cohort and untouched holdout exist. An external processor requires separate selection and data-transfer approval.

## Monitoring and diagnosis

The Web exposes:

- `/health/live`;
- database-backed `/health/ready`.

Readiness requires the database and all committed migrations.

Core contains local `ActivitySource` instrumentation. Both deployed hosts
register and export Application Insights telemetry, and the production
budget/alert wiring is recorded under
[production environment](#production-environment). Correlated retention for a
full working day remains unproved; historical predecessor incidents do not
establish current Pegasus behavior.

Monitoring and diagnosis procedure is owned by the
[runbook](runbook.md#monitoring-and-diagnosis).

## Production environment

Executed 2026-08-02 (full runbook and evidence hashes: git history,
`azure-production-replacement-plan.md`):

- **Environments:** isolated local development and production only; no Azure
  dev/test/integration/staging resources (ADR-0014).
- **Production target:** subscription `e6076573-23a5-46a8-acef-7e22d264e5db`,
  tenant `858cf5b3-aa0a-47a6-9b40-4851fd0afa94`, resource group
  `rg-pegasus-prod`, region `uksouth`.
- **Compute/data:** Linux/AMD64 Razor Pages Web on Container Apps Consumption
  (single revision, 0.5 vCPU / 1 GiB, min 1 max 1 replica — no scale-to-zero,
  no cold start), FC1 .NET 10 isolated Worker, Basic ACR, S0 Azure SQL, two Standard
  LRS storage accounts, distinct Web/Worker managed identities, a Pegasus Key
  Vault, Log Analytics, and Application Insights.
- **Deployed evidence:** the estate currently serves **release 38**. A branch
  head ahead of the newest row is expected and is not a missing release:
  **a source revision is a release claim only when it changes something under
  `src/`.** Documentation-only commits build no artifact, so they ride the
  next functional release rather than justifying one.

  Every release below went through the same authorised-terminal route: build
  the immutable artifacts from a clean exact HEAD, validate the plan in
  `Artifact`, `PreUpload` and `PreMigration` modes, push the digest-pinned OCI
  image to the production ACR, apply any pending migration explicitly *before*
  the application packages, activate the single Web revision, redeploy the
  Worker package, then smoke. Smoke asserts health live/ready 200, an exact
  version and source-SHA match against the release manifest, and an anonymous
  `/Cases` 302 to the **https** sign-in route (the forwarded-headers fix was
  live-verified at release 3; earlier releases redirected to `http://`).

  | Release | Date | Source revision | Image digest | Web revision | Migration |
  |---|---|---|---|---|---|
  | 38 | 2026-09-02 | `0f0e90ae44ffda7339ca2a460310deeb98121afa` | `sha256:b791d9587224d30d68fd6abcbd1e1d5f389f2baefc3702d9ec2d2f37398eef15` | `pegasus-prod-web-252ow37gij--0f0e90ae44ff` | none (head unchanged at `20260829212237_GrantProviderSubmissionAcceptRecovery`) |
  | 37 | 2026-08-30 | `0b3ec847aae42ee1c1bee4fb99459f9192534dca` | `sha256:47f57ea5031953ef93ccb09b2eb829b30d468647c96c0dc804310cc6f368595b` | `pegasus-prod-web-252ow37gij--0b3ec847aae4` | eleven, head `20260829212237_GrantProviderSubmissionAcceptRecovery` (`AiJobs`, `GrantAiJobs`, `PrincipalApiCredentials`, `GrantPrincipalApiCredentials`, `CaseEditLeaseHolderKind`, `ProviderSubmissions`, `GrantProviderSubmissions`, `NamedEstimates`, `ProviderDeclaredInstruction`, `CaseValuations`, `GrantProviderSubmissionAcceptRecovery`) |
  | 36 | 2026-08-28 | `84132d01ccb0afca7af6c6ce519e6f3491aee160` | `sha256:5ba65f61ad754639185764ed2c7795fc06938e6e397a3a9d5c7f7fe5c01bb032` | `pegasus-prod-web-252ow37gij--84132d01ccb0` | `20260827143132_EvaApiSubmissions` and `20260827143200_GrantEvaSubmissions` |
  | 35 | 2026-08-27 | `3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f` | `sha256:694c562f9b686877b73e30015a65d35b52c05e5a4b0c455219388c157a0892c8` | `pegasus-prod-web-252ow37gij--3a1a017c8dea` | `20260827100901_ReactivateBoundApprovedMailboxes` (data-only, matched zero rows) |
  | 34 | 2026-08-27 | `1ec65dc894f121f4bb5b31ae82c818a401d08beb` | `sha256:b04bad2c2ee8109d3309eb99b3d6610aca8f1319869f92db7c12e17fcb9d2bf0` | `pegasus-prod-web-252ow37gij--1ec65dc894f1` | none (head unchanged at `20260826151807_ApprovedMailboxStableIdentityAndSubscriptions`) |
  | 33 | 2026-08-26 | `ee8067eca799eaa614a96488364d333093e21aaa` | `sha256:dd38dc6db0e4fb777fdfcfc193d800f4c46373d79dd5df2b676dcae5f3ba50d6` | `pegasus-prod-web-252ow37gij--ee8067eca799` | `20260826151807_ApprovedMailboxStableIdentityAndSubscriptions` |
  | 32 | 2026-08-26 | `cfb3e6cfd838dfdcf7ffa64aa9164bfdc2bc9223` | `sha256:bac866eeb11215c2b0dbaf949e769280aefef246c34f6cbf9436d28a486274bf` | `pegasus-prod-web-252ow37gij--cfb3e6cfd838` | none (head unchanged at `20260825145216_MailboxImageIntake`) |
  | 31 | 2026-08-25 | `7dbb7c39…` | `sha256:a10dce43…` | `pegasus-prod-web-252ow37gij--7dbb7c3952fb` | `20260825145216_MailboxImageIntake` |
  | 30 | 2026-08-25 | `eaabf311…` | `sha256:40a44edb…` | `pegasus-prod-web-252ow37gij--eaabf31130be` | `20260825105037_AssessmentAccessExportVersion`, `20260825121453_GrantWorkerImageIntakeLifecycleEvents` |
  | 29 | 2026-08-25 | `b1aa68c8…` | `sha256:cb480319…` | `pegasus-prod-web-252ow37gij--b1aa68c86063` | none (head unchanged at `20260825001401_RemoveWorkflowCompletenessWaivers`) |
  | 28 | 2026-08-25 | `7e9465b0…` | `sha256:08f5f605…` | `pegasus-prod-web-252ow37gij--7e9465b00603` | `20260824123336_DropEvaHandoffTables`, `20260825001401_RemoveWorkflowCompletenessWaivers` |
  | 27 | 2026-08-24 | `7d4c8f00…` | `sha256:1e4146df…` | `pegasus-prod-web-252ow37gij--7d4c8f005261` | `20260824090400_DropEvaHandoffProvenanceAndManifest` |
  | 26 | 2026-08-23 | `7d6a948a…` | `sha256:d64e76ba…` | `pegasus-prod-web-252ow37gij--7d6a948a2f34` | none (head unchanged at `20260822223626_BackfillVehicleLookupSuggestions`) |
  | 25 | 2026-08-23 | `75570b99…` | `sha256:e99ade3c…` | `pegasus-prod-web-252ow37gij--75570b99d713` | none (head unchanged at `20260822223626_BackfillVehicleLookupSuggestions`) |
  | 24 | 2026-08-23 | `19969404…` | `sha256:bd9d8c4a…` | `pegasus-prod-web-252ow37gij--199694040184` | `20260822223626_BackfillVehicleLookupSuggestions` |
  | 23 | 2026-08-22 | `b6d54ff6…` | `sha256:7193802c…` | `pegasus-prod-web-252ow37gij--b6d54ff6-eva` | `20260822195419_CorrectIntakePhotographSemanticRole` |
  | 22 | 2026-08-22 | `191ddf33…` | `sha256:b40244ec…` | `pegasus-prod-web-252ow37gij--191ddf334208` | none (head unchanged at `20260822044425_GrantWorkerCaseDocuments`) |
  | 21 | 2026-08-22 | `4257b841…` | `sha256:d18c64a9…` | `pegasus-prod-web-252ow37gij--4257b841b4e2` | none (head unchanged at `20260822044425_GrantWorkerCaseDocuments`) |
  | 20 | 2026-08-22 | `05fe7a7f…` | `sha256:90b58000…` | `pegasus-prod-web-252ow37gij--05fe7a7f2d86` | `20260822044425_GrantWorkerCaseDocuments` |
  | 19 | 2026-08-22 | `42125b34…` | `sha256:08aeeaed…` | `pegasus-prod-web-252ow37gij--42125b34e57a` | none (head unchanged at `20260821100623_GrantImageIntakeLifecycleUpdates`) |
  | 18 | 2026-08-22 | `1f3be493…` | `sha256:818fe360…` | `pegasus-prod-web-252ow37gij--1f3be493c8c6` | none (head unchanged at `20260821100623_GrantImageIntakeLifecycleUpdates`) |
  | 17 | 2026-08-21 | `71911734…` | `sha256:f625c947…` | `pegasus-prod-web-252ow37gij--7191173442db` | none (head unchanged at `20260821100623_GrantImageIntakeLifecycleUpdates`) |
  | 16 | 2026-08-21 | `4111ad29…` | `sha256:3b891b45…` | `pegasus-prod-web-252ow37gij--4111ad291779` | `20260820114412_ApprovedOutlookCategoryCatalogue`, `20260821095500_GrantWorkerVehicleLookupRequests`, `20260821100623_GrantImageIntakeLifecycleUpdates` |
  | 15 | 2026-08-20 | `6d04f89d…` | `sha256:07c05faa…` | `pegasus-prod-web-252ow37gij--6d04f89d4d30` | `20260820100724_RetainedMailSearchDocuments`, `20260820144004_RetainedMailFolderMoves` |
  | 14 | 2026-08-20 | `d91fd7d7…` | `sha256:949797d4…` | `pegasus-prod-web-252ow37gij--d91fd7d7835a` | `20260820034652_ImageIntakeSubmissionGroup`, `20260820040337_SendToAiConnectorSettings`, `20260820055900_ImageCaseCustody`, `20260820100056_ApprovedMailboxLogicalFolderBindings` |
  | 13 | 2026-08-20 | `2325ed4a…` | `sha256:7efa46fd…` | `pegasus-prod-web-252ow37gij--2325ed4a31d7` | `20260819234014_GrantWorkerIntakeSubmissionGroupRead` |
  | 12 | 2026-08-19 | `ed3be51c…` | `sha256:6dcf3ca1…` | `pegasus-prod-web-252ow37gij--ed3be51c95bc` | `20260819093019_RetainedMailboxInternetMessageIdentity`, `20260819101344_GroupedIntakeSubmission`, `20260819104953_MailClassificationCorrectionHistory`, `20260819112640_VersionedRepairSpecifications`, `20260819112914_ImageInitiatedLifecycle`, `20260819115323_UnidentifiedWork`, `20260819140113_ImageIntakeGroupExpectedMemberCount`, `20260819180000_GrantEvaHandoffDownloadOperations` |
  | 10 | 2026-08-18 | `d8de29cb…` | `sha256:4bd50f66…` | `pegasus-prod-web-252ow37gij--d8de29cb94f3` | none |
  | 9 | 2026-08-18 | `f1e116c6…` | `sha256:63e86324…` | `pegasus-prod-web-252ow37gij--f1e116c6eb93` | `20260814092852_AddWorkerCaseCreationGrants`, `20260814094632_DropBoxFileRequests` |
  | — | 2026-08-12/14 | `dd61ac56…`, then `aecad247…` | `sha256:04d39c20…`, then tag `azd-deploy-1786687004` | `--13m13ph`, then `--azd-1786687080` | three 2026-08-11/12 migrations, then `20260813025241_StandaloneAuditReportDecision` (un-numbered `azd deploy` deployments; no immutable manifest was retained) |
  | 8 | 2026-08-07 | `ded44fd7…` | `sha256:c993eb0e…` | `pegasus-prod-web-252ow37gij--ded44fd7be0a` | three 2026-08-05/06 migrations |
  | 7 | 2026-08-05 | `32feefa…` | `sha256:c8a0ebac…` | `pegasus-prod-web-252ow37gij--32feefacc388` | none |
  | 6 | 2026-08-05 | `474a0924…` | `sha256:b2ceaf37…` | `pegasus-prod-web-252ow37gij--474a0924a6ba` | `20260803205759_SendToAiAssessmentToolset` |
  | 5 | 2026-08-04 | `c6571f7…` | `sha256:29d4fcff…` | `pegasus-prod-web-252ow37gij--c6571f771aab` | none |
  | 4 | 2026-08-04 | `8e34078…` | `sha256:ae2cc7b8…` | — | four 2026-08-03 migrations |
  | 3 | 2026-08-03 | `ef987ac4…` | `sha256:89165ad5…` | `ef987ac49cb4` | inspection-mode |
  | 2 | 2026-08-03 | `836db05c…` | — | — | none |
  | 1 | 2026-08-02 | `94997dd0…` | — | — | initial |

  What each release proved beyond smoke:

  - **Public-upload incident, diagnosed 2026-09-03.** Read-only Application
    Insights and SQL checks confirmed that the observed public upload attempt
    in release 38 failed before reaching Box. The Web exception was the
    production `BoxDocumentContentStore` rejecting the legacy `StoreAsync`
    call because it lacked the persisted occurrence, ordinal and revision
    address. SQL work
    before the failure succeeded, then rolled back: the estate held one revoked
    link, zero accepted files and bytes, zero receipts, and zero
    request-upload document occurrences. No migration or cleanup is required.
    The PageModel rendered its generic error response as HTTP 200, so the
    request success result did not mean the upload succeeded.

    The same telemetry showed that Application Insights retained the bearer
    token in the request URL. The token is not reproduced here and the observed
    link is revoked. CASE-022 repairs both source defects by using the existing
    managed Box custody address and by canonicalising public-upload telemetry
    URLs. That branch is not deployed: release 38 remains the sole production
    revision and public uploads remain known-broken until a later authorised
    release and live verification.

  - **Release 38** (2026-09-02, source
    `0f0e90ae44ffda7339ca2a460310deeb98121afa`, image
    `sha256:b791d9587224d30d68fd6abcbd1e1d5f389f2baefc3702d9ec2d2f37398eef15`,
    manifest SHA-256
    `52E1A5AC23C2491594E79EA89740D9B5D826A3DD94258347DB91A16896F986AE`)
    deployed the sparse-Graph-message polling repair, Inbox selected-preview
    retention, and the version-five QDOS classification/reference evidence.
    The authorised exact-SHA promotion advanced both `main` and `dev` from
    `fb3f07acc8cca8d9d8b57db8a431b607772436dc` and the same candidate was read
    back from both refs. The immutable Worker ZIP SHA-256 is
    `1EF69DBEBF6BC3178E2688AF999A770319DD5ED1B3B341F0741CF9B463B83369`;
    the `config-zip` deployment id is
    `01ed553a-b6cd-4652-b043-72c88b9ca2e6`.

    No migration, bootstrap, or other database write formed part of the
    deployment. The migration head remained
    `20260829212237_GrantProviderSubmissionAcceptRecovery`. The new Web
    revision is Healthy and the sole active revision in Single mode with 100%
    traffic on the manifest digest. An immediate read during normal Container
    Apps replacement briefly observed release 37 as `Deprovisioning`; the
    authoritative follow-up read showed only release 38 active. Production
    smoke then passed live/ready health, exact version/source diagnostics,
    HTTPS authentication redirection, Worker activation and function census,
    the active Graph subscription, and Inbox polling liveness.

    Before deployment, the release-37 Worker failed every five minutes on a
    real Graph delta item that omitted `receivedDateTime`, leaving the newest
    completed poll at 2026-09-01 08:35 UTC with
    `invalid_mailbox_source`. After release 38, the 2026-09-02 12:50 UTC timer
    completed successfully in 4.323 seconds, cleared `LastFailureCode`, and
    advanced the cursor. The previously blocked emails then arrived in the
    Inbox; this natural recovery is live evidence for the sparse-message fix,
    without constructing or modifying an Outlook message. An authenticated UI
    check also moved focus to retained-mail search and the pointer away from
    the message rows; the same selected message stayed expanded and its preview
    remained visible. The malformed-message branch and the QDOS classification
    corpus remain local/artifact evidence rather than live-production cases.

    **The sixth operator-approved test-data wipe** ran before promotion. The
    dry run found 36 blobs (3,932,690 bytes), 70 non-preserved SQL tables and
    147 rows. It validated all 31 preserve-list entries, with 32 effective
    preserved tables, and recorded Case/Image/Unidentified sequences 31/7/1.
    Execution removed all 36 blobs and all 147 non-preserved rows in a checked,
    committed SQL transaction. Post-checks found zero blobs, zero wiped tables
    retaining rows, 354 preserved rows, and unchanged sequences 31/7/1. The
    authentication ring, box links, `pegtrans252ow37gij`, Outlook, Graph and Box
    were untouched. The operator authenticated to the Web UI and confirmed the
    wiped test round was absent. Emails subsequently released by the repaired
    mailbox cursor are new post-wipe ingestion, not failed wipe residue.

    Rollback retains release 37's digest
    `sha256:47f57ea5031953ef93ccb09b2eb829b30d468647c96c0dc804310cc6f368595b`
    and Worker artifact as the application basis; the database head is
    unchanged and no down-migration is required.

  - **Release 37** (2026-08-30, source
    `0b3ec847aae42ee1c1bee4fb99459f9192534dca`, image
    `sha256:47f57ea5031953ef93ccb09b2eb829b30d468647c96c0dc804310cc6f368595b`,
    manifest SHA-256
    `5DC59E80A5A5CE324D391CF8BBDCBC7C4E33DAEDEB0BD5A2C592961A6CD1E7A7`)
    carried the EPIC-011 operations workspace and opened two surfaces that had
    never been reachable in production. Measured from release 36's source
    (`84132d01`) to this one: **49 merged PRs, 405 commits, 434 files changed,
    +142,535/−17,804**.

    It applied **eleven** migrations, advancing the head from
    `20260827143200_GrantEvaSubmissions` to
    `20260829212237_GrantProviderSubmissionAcceptRecovery` and the applied count
    from 76 to 87. `AiJobs`, `PrincipalApiCredentials`, `ProviderSubmissions`
    and `CaseValuations` are new tables; `NamedEstimates` is a reshape of
    `CaseRepairSpecifications` and creates no table of its own. Both
    structurally risky migrations were harmless against live data:
    `CaseRepairSpecifications` and `CaseEstimateLines` each held zero rows, so
    the data `UPDATE` and six new check constraints touched nothing, and
    `ProviderDeclaredInstruction`'s widened `CK_CaseDataFields_FieldName` and
    `CK_CaseDataFields_SourceKind` lists are strict supersets of the thirteen
    field names and four source kinds the 98 live `CaseDataFields` rows use.
    `Invoke-AzureDatabaseBootstrap.ps1` then verified **544 catalogued
    permission/denial rows and 377 effective runtime DML rows**, up from
    526/359 at release 35 — that gate is an equality check in both directions,
    so the Worker's new `UPDATE` on `ProviderSubmissions` had to be both present
    and expected.

    **`Features__ProviderApi` is `true` in production for the first time.** The
    surface did not exist at release 36: `git grep ProviderApi -- src/` at
    `84132d01` returns nothing. Its routes, bearer scheme, flag and two of the
    eleven migrations (`ProviderSubmissions`, `PrincipalApiCredentials`) were
    all introduced inside this release's own range, and the gate was then opened
    in the same range — closed by omission rather than by any ADR restricting
    it. Verified after deployment: an
    unauthenticated `POST /api/provider/v1/submissions` answers **401, not
    404** — the route exists and admits nobody, because **no credential has been
    issued**. Issuing the first one is a separate exact-target approval.

    **Document upload links compose in production for the first time**
    (INTK-051), under the INT-31 interim limits: `int-31-interim-v1`, 10 MB
    aggregate and per file, ten files, a 168-hour token, 20 requests per token
    per ten minutes, and the seven content types
    `MimeKitPdfPigOpenXmlIntakeSourceReader` resolves. Opening that gate removed
    the middleware 404 that had been the only thing stopping an anonymous caller
    forcing a bounded body read, so the release also adds `PublicUploadLink`, a
    per-calling-address bound of 30 requests a minute on the one anonymous page.

    The sole Web revision `pegasus-prod-web-252ow37gij--0b3ec847aae4` is ready
    with **zero restarts**, 100% traffic in Single mode, and its digest matches
    the manifest. That restart count is the load-bearing evidence for the
    configuration: `RequestUploadLimits` is registered as a factory
    (`Program.cs:242`) and resolved lazily, and nothing in `src/` calls
    `UseDefaultServiceProvider`, so container validation follows the ASP.NET
    Core host default of validating on build only under `IsDevelopment()` —
    false here. A wrong `DocumentRequests` key or a missing `Eva:*` value
    therefore surfaces as a crash-loop or a 500 on first use rather than a
    startup error. The
    Worker `config-zip` deployment succeeded and read back exactly seven
    functions, all `Disabled=false`. Production smoke passed twice, fifteen
    minutes apart, with the inbox poll advancing 15:25:02Z → 15:30:03Z against a
    Graph subscription expiring 2026-09-02 10:25:00Z — the expiry is
    independently checkable in `ApprovedMailboxSubscriptions`, but **the two
    poll timestamps are an uncorroborated operator self-report**: no smoke
    artifact was retained, `LastCompletedAtUtc` advances every five minutes, and
    this is the release for which telemetry cannot back them up.

    **Row-count baseline at deploy.** All four new tables read zero rows
    (`AiJobs`, `PrincipalApiCredentials`, `ProviderSubmissions`,
    `CaseValuations`) against seven `Cases`. That is the evidence the new
    subsystems carry no production usage yet, and the number a later reader
    compares against to detect first use.

    **Where the levers are.** Both opened gates are container-app environment
    variables set from `infra/modules/platform.bicep` — `Features__ProviderApi`
    and the fifteen `DocumentRequests__*` keys — so they change by editing that
    file and re-provisioning, not by a portal edit or an azd input. Closing
    either in a hurry means setting the value and running `azd provision`;
    removing `DocumentRequests__AcceptedLimitsVersion` alone is enough to
    decompose the upload-link surface, and it is the one that admits anonymous
    internet callers.

    **Rollback position.** The previous good state is release 36: revision
    `pegasus-prod-web-252ow37gij--84132d01ccb0`, image
    `sha256:5ba65f61ad754639185764ed2c7795fc06938e6e397a3a9d5c7f7fe5c01bb032`,
    still present in the ACR, whose retention policy is disabled so nothing
    auto-purges it. **The eleven migrations are not assumed down-reversible
    against live data** — `NamedEstimates` performs a data `UPDATE` and
    `CaseEditLeaseHolderKind` alters a lease column — so a rollback is a Web
    revision rollback, not a schema rollback, and the schema stays forward. The
    release-36 `worker.zip` is **not** retained on this workstation (releases
    33-36 ran from a different machine), so a Worker rollback would have to be
    rebuilt from source at `84132d01ccb0`.

    Artifacts are retained at
    `artifacts/releases/release-37-0b3ec847` in the primary checkout — the
    manifest, `worker.zip` and `efbundle.exe`. The 1.4 GB `web-image.tar.gz` is
    not retained: the image itself is in the ACR under tag
    `0b3ec847aae42ee1c1bee4fb99459f9192534dca`, which is what a rollback pulls.
    The retained manifest hashes to the SHA-256 recorded above, so that figure
    is checkable rather than resting on the deploying operator's word.

    **Telemetry does not cover this release.** Application Insights ingestion
    stopped at 12:41Z when the workspace hit its 0.5 GB daily cap
    (`RespectQuota`, resetting 03:00Z) — roughly two hours forty before the
    deploy. Every post-deploy check above is therefore direct observation:
    replica state and restart count, HTTP probes against
    `/diagnostics/version`, `/health/live` and `/health/ready`, and the
    database-backed liveness check. Any incident before the next reset is
    invisible in telemetry.

  - **Release 36** (2026-08-28, source
    `84132d01ccb0afca7af6c6ce519e6f3491aee160`, image
    `sha256:5ba65f61ad754639185764ed2c7795fc06938e6e397a3a9d5c7f7fe5c01bb032`,
    manifest SHA-256
    `A1E3707F39187C5991D8D8512166F5F418FE0B05DC7CA9F85105908BDB435C76`)
    shipped the EXT-04 EVA API submission route. It applied
    `20260827143132_EvaApiSubmissions` and `20260827143200_GrantEvaSubmissions`,
    advancing the head to the latter; `EvaSubmissions` reads zero rows and no
    Principal has either EVA toggle on, so the deployed behaviour of every
    existing case is unchanged. Production SQL read back exactly the four
    expected grants — `SELECT` and `INSERT` on `EvaSubmissions` for both
    `pegasus_web_runtime_role` and `pegasus_worker_runtime_role`, with `UPDATE`
    and `DELETE` granted to neither. The sole Web revision
    `pegasus-prod-web-252ow37gij--84132d01ccb0` is `RunningAtMaxScale` with 100%
    traffic in Single mode and its digest matches the manifest; the Worker
    `config-zip` deployment succeeded and all eighteen of its Key Vault
    references, the two new EVA ones included, read `Resolved`. Production smoke
    matched the exact source and product version and included the inbox intake
    liveness check (last poll 2026-08-28 03:00:03Z, subscription expiring
    2026-09-02 10:25:00Z).

    Two prerequisites were created by hand for this release, and both are
    permanent: the secrets `eva-client-id` and `eva-client-secret` in
    `pegasusprodkv252ow37g`, and two `Key Vault Secrets User` grants for the Web
    identity scoped to those two secret resources. TICK-077 wired the secrets
    into the container app's `configuration.secrets` but shipped no grants, and
    the first provision failed with the Web identity unable to fetch either — a
    gap no CI gate can catch, because `Test-AzureDeploymentPlan` prohibits a
    vault-wide grant in bicep and secret-scoped grants are made outside it. A
    provision before that one failed earlier still, on a UTF-8 BOM TICK-077 left
    on `infra/main.parameters.json`, which azd's JSON decoder refuses; that is
    fixed on `main` as ENG-022. Neither failure deployed anything: production
    served release 35 unchanged throughout.

    **Pegasus has still never called EVA, in any environment.** This release
    proves deployment, schema, runtime permissions, secret resolution and
    configuration. It proves nothing about the vendor contract — that EVA
    accepts this payload, that images land, or what it returns — which remains
    an operator-held live test with `EvaManualSubmission` on a Principal.

    **Deviation recorded, not introduced by this release:** the Worker identity
    holds a `Key Vault Secrets User` grant at *vault* scope in addition to its
    six secret-scoped ones, which is how its EVA references resolved without a
    grant being added for them. That contradicts the secret-level-only posture
    described under the 2026-08-03 vault consolidation below, and is why only
    the Web identity needed new grants here.

  - **Release 35** (2026-08-27, source
    `3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f`, image
    `sha256:694c562f9b686877b73e30015a65d35b52c05e5a4b0c455219388c157a0892c8`,
    manifest SHA-256
    `CA81E6F7D9A1A63C9CC8460614E728B601E206919CB6653E7CB5A681D9EF10CF`)
    applied migration `20260827100901_ReactivateBoundApprovedMailboxes`
    (data-only): `__EFMigrationsHistory` head advanced to that id and
    `ApprovedMailboxes` still holds one row for `instructions@…` with
    `ActivatedAtUtc = 2026-08-27 10:20:33Z` unchanged, confirming the UPDATE
    matched zero rows as expected — the mailbox was already reactivated by
    an earlier operator action. Bootstrap verified the unchanged 526
    catalogued permission/denial rows and 359 effective runtime DML rows (no
    grant-carrying migration this release). `azd provision` carried the
    bicep-declared 0.5 GB daily cap to both `pegasus-prod-appi-252ow37gij`
    (`dataVolumeCap.cap`) and `pegasus-prod-logs-252ow37gij`
    (`workspaceCapping.dailyQuotaGb`) — both read back 0.5 immediately after
    provision, raising the ceiling from the 0.1 GB that had silenced
    telemetry by mid-morning since release 19. Azure read-back matched the
    immutable image digest; the sole Web revision
    `pegasus-prod-web-252ow37gij--3a1a017c8dea` is `RunningAtMaxScale` with
    100% traffic in Single mode; the Worker `config-zip` deployment
    completed and all seven released functions are enabled with
    `ApprovedInboxPollSchedule=0 */5 * * * *` unchanged. Production smoke
    matched the exact source and product version and, for the first time,
    included `Inbox intake liveness smoke passed` (Graph subscription
    `09018cc2…` read `Active`, expiring 2026-09-02 10:25:00Z, last poll
    2026-08-27 19:45:12Z), and the same subscription row read back directly
    from `ApprovedMailboxSubscriptions` matches. An `AppDependencies` query
    over the 14 minutes following the Worker deploy (19:48–20:02Z) returned
    223 Worker dependency records, all `Success = true`, with the workspace
    still `RespectQuota` (not `OverQuota`) — no successful SQL dependency
    rows appeared in that window, consistent with the deployed
    `SqlDependencyTelemetryFilter` dropping them while non-SQL (Graph
    polling) dependencies continued to report; this is a post-deploy
    observation, not a controlled before/after comparison against the
    unfiltered baseline. This proves deployment, schema (no change),
    permissions (unchanged), configuration, the new telemetry caps and
    technical health. It does not by itself prove the new 0.5 GB cap
    survives a full working day of combined Web and Worker volume
    (PLAT-034, open), nor the INTK-044 Audit-allocation recovery path or
    the Mailboxes page's Activated/Subscription columns under a live
    operator session — the latter's screenshot evidence is
    operator-supplied and was not captured by this agent.

  - **Release 34** (2026-08-27, source
    `1ec65dc894f121f4bb5b31ae82c818a401d08beb`, image
    `sha256:b04bad2c2ee8109d3309eb99b3d6610aca8f1319869f92db7c12e17fcb9d2bf0`,
    manifest SHA-256
    `B3984E24EC795C2E12A641039868341D116D3E84BC286133EF1D0031EA821CE2`)
    corrected the MAIL-015 release defect: `azd provision` carried the
    six-field `ApprovedInboxPollSchedule=0 */5 * * * *` to the live Worker,
    and the host indexed all seven functions including
    `InboxRecoveryFunction` (read back after the `config-zip` deployment).
    No migration: the head stayed at
    `20260826151807_ApprovedMailboxStableIdentityAndSubscriptions`. The only
    `src/` change was the Worker example settings file; the release otherwise
    carried the MAIL-016 test correction, the regenerated Test UI (UIIMP-004),
    the release-33 record and the restored design authority (DELIV-028) after
    the design-system removal in `9eec6dc2`. Two route traps fired again: the
    workstation's azd environment held release 26's digest and suffix and
    lacked `GRAPH_CHANGE_NOTIFICATION_CLIENT_STATE_SECRET_URI` — set from the
    live Worker's Key Vault reference before provisioning, otherwise the
    explicit `env` array would have dropped the Graph `clientState` binding.
    Azure read-back matched the immutable digest; the sole Web revision is
    Healthy with 100% traffic; production smoke matched the exact source and
    product version. **The fourth operator-approved test-data wipe**
    (PLAT-045) ran after the smoke: **65 of 97 tables, 623 rows**, preserving
    32 identity, automation-client, mailbox-configuration (now including
    `ApprovedMailboxSubscriptions`), principal, provider-reference,
    workflow-configuration, audit, schema and sequence tables, so the next
    case is **QDOS26024** and no reference is reused. `transient-intake`
    already held 0 blobs and the four queues were empty before and after;
    poll cursors and Graph subscriptions were preserved so nothing is
    re-ingested; Outlook and Box were untouched. Smoke passed again on the
    emptied estate. This proves deployment, configuration and technical
    health; it does not prove that the recovery timer fires on schedule.

  - **Release 33** (2026-08-26, source
    `ee8067eca799eaa614a96488364d333093e21aaa`, image
    `sha256:dd38dc6db0e4fb777fdfcfc193d800f4c46373d79dd5df2b676dcae5f3ba50d6`,
    manifest SHA-256
    `414D4958F5F468EE9A7AEDFA04ECDC706C0D6779C925B3CC3A31D4D3073678C1`)
    deployed stable approved-mailbox identity, Microsoft Graph change-notification
    wakes, the unified Worker work queue, and five-minute Inbox recovery. The
    migration advanced the database to
    `20260826151807_ApprovedMailboxStableIdentityAndSubscriptions`; the release
    applied the four intended subscription-table denials omitted by the migration,
    then bootstrap verified 526 catalogued permission/denial rows and 359 effective
    runtime DML rows. The new Graph `clientState` is versioned in Key Vault with
    secret-scoped Web and Worker access. Azure read-back matched the immutable image
    digest; the sole Web revision is healthy with 100% traffic; all seven released
    Worker functions are enabled. The deployed schedules are
    `PendingWorkRecoverySchedule=0 * * * * *`,
    `IntakeStagedArtifactReconciliationSchedule=*/10 * * * * *`,
    `ApprovedInboxPollSchedule=0 */5 * * * * *` — **seven fields, invalid
    NCRONTAB**, the MAIL-015 release defect; the six-field correction is
    MAIL-015 (PR #566) and reaches the live Worker only with the first
    provision after it merges — `SentEvidencePollSchedule=15 * * * * *`, and
    `DueWorkSweepSchedule=0 */5 * * * *`; production smoke matched the exact source
    and product version. This proves deployment, schema, permissions, configuration
    and technical health. It does not prove that the Inbox recovery timer fires,
    nor sub-five-second receipt of a fresh operator email. **Second release-33
    defect (found 2026-08-27):** the migration's seed diff set
    `ApprovedMailboxes.ActivatedAtUtc = NULL` on the live, identity-bound
    instructions mailbox; every intake consumer requires an activation time, so
    no poll ran and no Graph subscription was ever created
    (`ApprovedMailboxSubscriptions` stayed empty) — the Mail page read stale and
    the Graph-notification path was never exercised. The repair is MAIL-017's
    `20260827100901_ReactivateBoundApprovedMailboxes`; the interim operator
    action is re-saving the mailbox in Administration › Mailboxes, after which
    mail received before the new activation time is skipped by design.

  - **Release 32** (2026-08-26, source
    `cfb3e6cfd838dfdcf7ffa64aa9164bfdc2bc9223`, image
    `sha256:bac866eeb11215c2b0dbaf949e769280aefef246c34f6cbf9436d28a486274bf`,
    manifest SHA-256
    `C3A368CB633AE815CDC5ED51C4CF84E0AB56980DAB910EBB102A4523DACC6ECE`) deployed immediate
    post-commit intake publication and replaced the five-second dispatch timer
    with the one-minute interrupted-work recovery timer. No migration was
    required. Azure read-back matched the immutable image digest; the sole Web
    revision is healthy and carries 100% traffic; all nine Worker functions
    are present and enabled; `PendingWorkRecoverySchedule` is exactly
    `0 * * * * *`; and production smoke matched the exact source and product
    version. This proves deployment, activation and technical health. It does
    not prove the speed or displayed states of a fresh operator mailbox or
    manual-upload journey.

  - **Release 31** (2026-08-25, source `7dbb7c39`, image
    `sha256:a10dce43…`, manifest SHA-256 `2187533D…`) deployed mailbox vehicle
    images through the existing Image Intake path, near-real-time intake
    contracts, interrupted dispatched-work recovery, and reduced successful EF
    command-log volume. The migration bundle advanced the database to
    `20260825145216_MailboxImageIntake`; guarded bootstrap verified 518
    catalogued permission/denial rows and 355 effective runtime DML rows, with
    Worker `SELECT, INSERT` on both intake-submission group tables. Azure
    read-back matched the immutable image digest: the sole active Web revision
    is healthy, ready and carrying 100% traffic; all nine Worker functions are
    present and enabled; and production smoke matched the exact source and
    product version. This proves deployment, schema, permissions, activation
    and technical health. It does not prove a fresh operator mailbox or manual
    upload journey, a forced queue-message-loss recovery, a full working day of
    telemetry retention, or the separate INTK-042 sender/state-latency work.

  - **Release 30** (2026-08-25, source `eaabf311`, image
    `sha256:40a44edb…`, manifest SHA-256 `807777BC…`) deployed the assessment
    export-version persistence change and the grouped image-intake lifecycle
    reconciliation. The migration bundle advanced the database to
    `20260825121453_GrantWorkerImageIntakeLifecycleEvents`; the guarded
    bootstrap verified 516 catalogued permission/denial rows and 353 effective
    runtime DML rows. Azure read-back matched the immutable image digest and
    revision, all nine Worker functions remained present and enabled, and
    production smoke passed before and after the operator-approved reset.
    That reset deleted 415 rows from the 65 non-preserved transactional tables
    and all 39 blobs from `pegcustody252ow37gij/transient-intake`; all target
    tables and that container then read zero. The 31 explicitly preserved
    identity, configuration, mailbox-cursor, provider-reference, migration,
    and reference-sequence tables retained their row counts. Queues, Outlook,
    Box, `authentication-ring`, and `box-links` were not reset. This proves
    deployment, permissions, activation, smoke, and reset boundaries; it does
    not claim that a new authenticated operator image journey has yet been run
    against release 30.

  - **Release 29** (2026-08-25, source `b1aa68c8`, image
    `sha256:cb480319…`, manifest SHA-256 `35C73A4D…`) deployed ENG-018's
    correction to the single Export route. The obsolete
    `EvaMappingAcceptance` check and its operator-facing “EVA hand-off is not
    switched on” error are absent; the three `Eva__AcceptedMapping__*`
    Container App settings were removed by provision. Mapping identity remains
    version 2 as package/history metadata, while `Review` remains the sole
    business-readiness gate. The immutable artifact and deployment-plan checks
    passed, production smoke matched the exact source/version, the new Web
    revision is ready with 100% traffic, and all nine Worker functions remain
    enabled. No migration was required. This proves deployment and live
    configuration removal; it does not claim an authenticated operator Export
    or EVA import was performed.

  - **Release 28** (2026-08-25, source `7e9465b0`, image
    `sha256:08f5f605…`, manifest SHA-256 `C5943AFC…`) deployed ENG-016's single
    staff Export route. Export is available only while the case is in Review,
    rechecks that state under the same database lock as replay/history writes,
    records one action-history entry and the once-per-case EVA proxy, and emits
    the EVA archive without a second handoff workflow or Automation MCP tool.
    The release removed the superseded EVA handoff tables and the two obsolete
    completeness-waiver columns. Exact-SHA/version smoke passed, the Web
    revision is ready with 100% traffic, all nine Worker functions remain
    enabled, and the post-migration check verified 512 catalogued permission
    rows and 351 effective runtime DML rows. This proves deployment and smoke;
    it does not claim a real operator export or EVA import was performed.
    The first real operator attempt then exposed an ENG-016 merge regression:
    deployed configuration retained mapping version 2 while Core had reverted
    to version 1, so the remaining legacy activation check blocked every
    Export. Release 29 deployed ENG-018's removal of that obsolete check and
    configuration.

  - **Release 27** (2026-08-24, source `7d4c8f00`, image `sha256:1e4146df…`,
    manifest SHA-256 `D81BF7A9…`) collated eleven open pull requests — #525-#535
    plus the integration fix #537 — into one promotion: the Triage-from-intake
    route and its two follow-ons, the evidence tab rewrite and in-place preview,
    the EVA export format and field values, the Box round-trip reduction, and
    the two governance changes that authorised the rest.

    **Non-additive migration, named as ADR-0030 requires.**
    `20260824090400_DropEvaHandoffProvenanceAndManifest` drops `ManifestContent`,
    `ProvenanceContent` and `ProvenanceSha256` from `EvaHandoffRevisions`.
    **Affected capability: EVA hand-off generation and download (EXT-03).** The
    exposure was checked rather than assumed before applying it: the four call
    sites that materialise the whole entity are generation and download, both
    switched off with zero rows, while `GetPreparationAsync` — the one reached
    by every Case page — *projects* a summary that names none of the three
    columns. So the previous artifact keeps serving the case workspace. Roll
    forward, never back, past this migration.

    **First release to carry an infrastructure change with the code that needs
    it.** `Eva__AcceptedMapping__Version` moved 1 → 2 in `platform.bicep`
    alongside `CaseEvaMapping.MappingVersion`, because ENG-015 changed what four
    of the thirteen EVA fields mean — `Reference` became the provider's own
    claim number rather than the Pegasus case reference — and acceptance is
    checked by key, version and evidence reference alone. Left at 1, every
    deployment that accepted the old mapping would have authorised materially
    different output without anyone accepting it. Read back as `2` on the
    deployed revision.

    **The third operator-approved test-data wipe** ran *before* the promotion,
    on operator instruction to clear anything Azure-side created from an e-mail
    or an upload. It emptied **33 of the 68 targeted tables, 384 rows**, and all
    **42** `transient-intake` blobs (~64 MB), preserving the same **31** identity,
    automation-client, mailbox-configuration, principal, provider-reference,
    workflow-configuration, audit and schema tables as PLAT-040 — and the three
    sequence tables, so the next case is **QDOS26016** and no reference is
    reused. Mailbox poll cursors were preserved deliberately, and the estate was
    still empty after the Worker restarted, which is what proves it: clearing
    them would have re-ingested every message still in the mailbox. Outlook and
    Box were untouched; `authentication-ring` and `box-links` refused the
    release identity, as designed. The target list was computed as *all tables
    minus the preserve list* and asserted against the recorded 68/31 split, so a
    table added since the last wipe would have failed the assertion rather than
    being silently skipped. Constraints were re-enabled `WITH CHECK`, which
    passed — proving no preserved table referenced a deleted row.

    **Two release-route traps recorded again because both fired.** The azd
    environment still carried **release 25's** digest and revision suffix, so the
    first `azd provision` failed on `revision with suffix 75570b99d713 already
    exists` — the same lucky collision as release 16, and again the only thing
    standing between a stale environment and silently redeploying an old image.
    That failed provision had already succeeded for the Function App, so the
    Worker settings were re-read rather than assumed. The Web image still moves
    by `az acr login --expose-token` + `oras login` + `oras cp`; the digest
    `oras` reported was compared against `webImage.digest` in the manifest before
    provisioning.

    Proved live: `/diagnostics/version` returns the exact source SHA, health
    live/ready 200, the migration head is the new one with zero of the three
    columns remaining, and `Cases`/`IntakeReceipts`/`IntakeAssets` all read 0
    after the Worker came back. Smoke gained a check in this release —
    `Invoke-ProductionSmoke.ps1` now asserts the deployed
    `Content-Security-Policy`, because the security headers are added only
    outside Development and so no test in the suite had ever seen one. It
    confirmed `frame-ancestors 'self'`, without which DOCS-011's PDF preview is
    blocked in every deployment while staying green locally.

    Not proved by this release: no live instruction has been processed against
    it. Every intake, Triage and export behaviour above is proved by tests and by
    the deployed revision, not by production evidence. ENG-016 was deliberately
    **not** in this promotion — see [[ENG-016]].

  - **Release 26** (2026-08-23, source `7d6a948a`, image `sha256:d64e76ba…`)
    carried four production defects found operating release 25 against case
    `ap.QDOS26012` and unidentified item `U34`. The largest, PLAT-039: the Box
    SDK's `RetrieveTokenAsync` returns any cached token **without checking
    expiry**, re-minting only when its cache is empty and leaving 401 recovery
    to its own HTTP client — which Pegasus does not use. The single Web replica
    minted one token at start and served it for its whole life, so *every* Box
    read from the Web app failed with 401 an hour after each deploy: the case
    export, the Evidence gallery and document downloads alike. Six 401s were
    recorded that day from 10:36Z on a replica up since 01:34Z. The token is now
    held against the lifetime Box states and renewed 120 seconds ahead of it,
    with the margin derived from the client timeout it must exceed.

    Alongside it: the Evidence gallery had been sending the *document* id where
    the route resolves an *occurrence* id (two adjacent `Guid` slots filled
    positionally in the wrong order, so images 404d before Box was reached); a
    forwarded header carrying a `Cc:` line was unreadable, which is why one
    triage instruction and its photograph became U34 and its inbox row showed
    the forwarding desk; and QDOS's second triage template — five corpus
    messages from three senders, disjoint from the seven carrying the known body
    phrase — had never been classifiable at all.

    Recorded because it is the more useful lesson: the branch's own
    simplification pass **cleared** a regex that an independent pre-merge review
    then measured as a remotely triggerable hang (an 85-character subject ran
    past five seconds, on input from an approved mailbox, in both hosts). A pass
    run by hand by the author over their own diff is not the independent check
    the repository asks for.

    After the deploy smoked, the second operator-approved test-data wipe
    (PLAT-040) emptied **68 tables of 559 rows** and all **77**
    `transient-intake` blobs, preserving 31 identity, automation-client,
    mailbox-configuration, principal, provider-reference, workflow, audit and
    schema tables — and the three sequence tables, so the next case is
    QDOS26013 and no reference is reused. Mailbox poll cursors were preserved
    deliberately: clearing them would make the next poll re-ingest every message
    still in the mailbox. Outlook and Box were untouched; `authentication-ring`
    and `box-links` are not merely left alone but unreachable to the release
    identity by design. Smoke passed again against the emptied database.

  - **Release 25** (2026-08-23, source `75570b99`, image `sha256:e99ade3c…`)
    fixed the managed Box read: `VerifyFileMetadataAsync` compared the file's
    `content_type`, which is not a field of the Box v2 file object, so the check
    could never pass for any file. The first caller ever to read content back
    from Box was the case export, which is why it survived undetected.

  - **Release 24** (2026-08-23, source `19969404`, image `sha256:bd9d8c4a…`)
    put the DVSA-derived mileage on the cases that already existed. Release 23
    made a vehicle lookup fill a case's empty fields *when the lookup runs*,
    which helps only cases whose lookup is still to come — every case in the
    estate had already been looked up, so nothing changed for any of them and
    QDOS26011 still read "Not recorded" over an observation holding 121,823
    miles. The migration corrects the recorded past.

    Proved by reading the case back on the live instance: the Vehicle block now
    reads `121,823 Miles — Estimated`, screenshotted, where the same page had
    read "Not recorded" an hour earlier. QDOS26010 keeps its extracted 132,389
    as a fact with the lookup's 128,343 retained behind it as a suggestion —
    both held, one shown, which is what the operator's "two different mileages"
    report was about.

    The release also exposed, and did not fix, a defect that predates it: **no
    managed Box read has ever succeeded in production**. `VerifyFileMetadataAsync`
    compared the Box file's `content_type` against the recorded media type, and
    `content_type` is not a field of the Box v2 file object — the comparison was
    against null and could never pass. The Evidence gallery's images, the
    case-document download and the case export all failed on that one exception
    and reported it three different ways. It survived because nothing had ever
    read content back from Box: `EvaHandoffRevisions`,
    `EvaHandoffDownloadOperations` and `CaseAssessmentFields` are all empty.
    Box *writes* were never affected.

  - **Release 23** (2026-08-22, source `b6d54ff6`, image `sha256:7193802c…`)
    stopped the case page saying the same thing three times, and made Export
    produce a file. Registration had appeared in the Vehicle block, again in a
    read-only "Case detail" restatement of the whole projection, and a third
    time in "Vehicle evidence"; the restatement is gone and the seven fields
    that lived only there — contact, VAT status and the four inspection fields —
    moved into the block grid, so each fact is now read in exactly one place.
    Export had never worked: `Details.cshtml` emitted `asp-route-id` against a
    `{caseId:guid}` route, so link generation produced no `href` at all and the
    control was inert.

    Proved beyond smoke by reading the deployed stylesheet back over HTTP
    (`grid-template-columns: … 22px`, the reserved provenance track that makes
    an iconed row line up with a plain one) and by reading the production
    database after the migration: QDOS26011's eight photographs moved from
    `Instruction` to `Image`, QDOS26010's six likewise, while nine embedded
    photographs and three PDFs were left alone — matching a read-only dry run of
    the same predicate taken before the write. Until that correction every
    photograph was invisible to EVA image selection, so an export of QDOS26011
    would have produced an archive containing no images.

    Two things about the release itself are worth carrying forward. The Web
    container app is gated in `infra/modules/platform.bicep` on
    `length(webRevisionSuffix) == 12`; a 16-character suffix chosen to dodge a
    collision made `azd provision` report **SUCCESS while deploying nothing**,
    detectable only by reading the environment back. And the EVA mapping
    settings were applied through bicep rather than `az containerapp update`,
    because that file declares the container's `env` array explicitly and would
    have silently reverted anything set outside it.

  - **Release 22** (2026-08-22, source `191ddf33`, image `sha256:b40244ec…`)
    made operator notes visible. The note was written to `CaseHistory` while the
    Notes tab reads `CaseWorkflowEvents` — two different tables, so every note
    was saved, the page returned "The note was added.", and the timeline stayed
    empty with the count at zero. Nothing threw and CI stayed green, because the
    command's tests drive a recording fake and nothing asserted the note came
    back through the query the page uses. It was found by running the page
    locally under `DevelopmentOffline` and posting a note through the real form,
    and the fix was verified the same way: the tab renders `Notes 1` with the
    entry attributed and the event shown as **Note** through the operator-label
    map. `CaseNotePersistenceTests` now pins the write to the table the read
    uses. That local run also confirmed the rendered case page, Notes tab,
    Dashboard, Cases, Inbox, Triage, Search, Operations and Administration
    screens carry none of the design authority's banned vocabulary and no
    "Immutable".

  - **Release 21** (2026-08-22, source `4257b841`, image `sha256:d18c64a9…`)
    carried the current-state documentation for releases 19 and 20 and four
    release traps added to the repository release skill. No behaviour change; it
    exists so `main` and the serving revision are the same commit as the
    documents describing them.

  - **Release 20** (2026-08-22, source `05fe7a7f`, image `sha256:90b58000…`)
    repaired case custody. Since release 17 every new case had uploaded its
    evidence to Box and then reported *"Case evidence could not be stored"* over
    files that were sitting in Box. The Worker runtime role held no permission
    at all on `CaseDocuments`, `DocumentVersions` and `DocumentOccurrences`
    beyond the DELETE deny — those three tables were granted to the Web role
    only, because when the least-privilege baseline was written only Web created
    case documents. Release 17 moved document registration into the Worker's
    custody processor and it was denied from that moment on. The record write
    ran inside the custody transaction, so the rollback took the custody
    confirmation and the promotion to Review with it, and because an
    unclassified `SqlException` is terminal rather than transient, neither case
    was ever retried. `20260822044425_GrantWorkerCaseDocuments` grants the
    Worker the Web role's own permission strings; the grants were read back from
    `sys.database_permissions` after the bundle applied. Nothing in the suite
    could have caught it — the tests run full-privilege and never exercise the
    least-privilege role, which is the same blind spot behind
    `20260814092852` and `20260821095500`. Closing it is tracked separately.

  - **Release 19** (2026-08-22, source `42125b34`, image `sha256:08aeeaed…`)
    made an unclassified custody failure name the exception type that caused it,
    and instrumented the Web host for Application Insights — it had never called
    `AddApplicationInsightsTelemetry`, so the container app had emitted nothing
    since the estate was built. The Worker, contrary to what was believed while
    diagnosing, had been reporting continuously all along.

    That confusion had one cause, and it is the estate's real observability
    problem: **the Log Analytics workspace runs a 0.1 GB daily quota resetting at
    03:00Z**, and the estate exhausts it within hours. Ingestion stops for the
    rest of the day, so every check run in a UK working hour comes back empty.
    Both custody failures fell inside a capped window and left no trace, which is
    why release 20's cause had to be found by reading the permission tables
    instead of a stack trace. The two alert rules are blind for the same window.
    Raising the quota is a billing decision and is left with the operator.

    Taken on 2026-08-27 (MAIL-020), correcting the paragraph above: the
    **component** cap, not the workspace quota, was the limit actually hit —
    a second 0.1 GB limit resetting at 00:00Z (the workspace's resets at
    03:00Z), gone by ~05:30Z, with `AppDependencies` (the Worker's per-query
    SQL records) two thirds of the volume. The Worker now drops successful SQL dependency
    telemetry (`SqlDependencyTelemetryFilter`; failed calls, HTTP
    dependencies, requests, exceptions and traces are untouched), and
    `platform.bicep` declares a single 0.5 GB daily cap for both the
    component and the workspace, with the 90% warning and cap-hit
    notifications kept on. Release 35 (2026-08-27) applied it: both the
    component `dataVolumeCap.cap` on `pegasus-prod-appi-252ow37gij` and the
    workspace `dailyQuotaGb` on `pegasus-prod-logs-252ow37gij` read back 0.5
    immediately after that provision, and the deployed Worker filter is live.

  - **Release 18** (2026-08-22, source `1f3be493`, image `sha256:818fe360…`)
    carried the QDOS26009 operator findings. An automatically created case can
    now reach Review: the automatic route had recorded all four completeness
    flags false and the acceptance policy then demanded staff confirmation
    nobody would ever give, while `CaseCompleteness.IsReadyForReview` — which
    waives staff review for an automatically definitive intake — **had no
    callers at all** and two stricter Infrastructure copies had been written
    instead. An audit now carries one identity rather than two: its own
    reference holds the `a.` / `ap.` prefix and no separate Audit reference is
    allocated, which also closed a split where the Box root was created under
    the audit identity while lookups used the case reference. The case Evidence
    gallery reads the case's document records and serves them through the
    case-document route, completing the half of DOCS-007 that had been left on
    the Azure staging blob. Case History became **Notes**, carrying
    operator-written notes on the same append-only timeline as the system's own
    entries; the MOT history table and the DVSA/DVLA mechanics rows left the
    case page; the word "Immutable" and the label "Approved inbox" left every
    operator-facing page.

    **Verified in production after this release** (read from the deployed
    database for the two audits that arrived on the live pipeline): report
    mileage extracted from a multi-column `Speedo:` line — `vehicle_mileage
    132389 miles` on QDOS26010, where QDOS26009 had no mileage field at all
    before the fix; MOT tests parsing again, with `MotTestsJson` holding 702 and
    1565 bytes where every vehicle previously recorded zero; derived mileage in
    `Miles` under method version 2; the staff forward unwrapping to
    `mhitchen@qdosassist.co.uk` rather than the desk; and end-to-end latency of
    19 and 30 seconds against the 30–60s+ originally reported.

    **Two things this release did NOT fix, stated plainly.** Custody still
    fails on both production audits with `custody_unexpected_failure` after the
    files reach Box — the record write, multiple attachments, embedded
    photographs, schema constraints, the audit path and missing staging blobs
    have each been ruled out by experiment, leaving a Box-specific fault that no
    test exercises. And **telemetry still does not arrive**: the Application
    Insights packages ship in both hosts and the registration code is deployed,
    but thirty days plus this release produce zero traces, requests and
    exceptions. Two facts found while diagnosing it are recorded for whoever
    picks it up — `APPLICATIONINSIGHTS_AUTHENTICATION_STRING` is an App Service
    and Functions host convention that a plain ASP.NET Core Container App does
    not read, and the Container Apps environment sends console logs to
    `azure-monitor` with a null Log Analytics customer id.

    **The second of those was wrong, and the correction matters.** The null
    customer id means the environment does not write to a workspace *directly*;
    a diagnostic setting does it instead. `pegasus-prod-aca-diagnostics` on the
    managed environment routes `ContainerAppConsoleLogs` and
    `ContainerAppSystemLogs` to workspace `pegasus-prod-logs-252ow37gij`, where
    they are queryable by KQL, retained, and unaffected by the Application
    Insights daily cap. The table is the resource-specific
    `ContainerAppConsoleLogs` — **not** `ContainerAppConsoleLogs_CL`, which does
    not resolve. This is the route that diagnosed the release-26 Box defect
    while Application Insights was over quota, and it is strictly better than
    polling `az containerapp logs show --tail`.

  - **Release 17** (2026-08-21, source `71911734`, image
    `sha256:f625c947…`) carried the QDOS26008 live-regression remediation.
    Every MOT test DVSA returned had been discarded since the integration was
    built — `completedDate` arrives as a full instant and was parsed with
    `DateOnly.TryParse`, so a vehicle with history was indistinguishable from
    one without; the fixtures used a date-only string the API never sends,
    which is why CI stayed green. Reading none of the tests offered is now a
    named failure rather than silence, and kilometres convert to miles at the
    derived boundary. The report grammar reads a multi-column `Speedo:` line
    (production held it as `Vehicle: TOYOTA NOT RECORDED Colour: Black Speedo:
    72850 Miles`), letterhead art is separated from photographs by shape
    (measured 4.55:1 and 8.93:1 against photographs at 1.09–1.15:1) rather
    than by byte size, case files sit flat in the case/PO folder and are
    recorded as case documents, the effective sender resolves at retention so
    the inbox never shows the forwarding desk, and unlinking the email that
    created a case now cancels it as `Cancelled — email unlinked`. The EVA
    panel is hidden while the hand-off is switched off — it was verified to
    gate bundle generation only and never review or export.

    **Measured infrastructure change:** `extensions.queues.maxPollingInterval`
    was unset, so each of two queue hops idled back off to the 60s default.
    It is now `00:00:02`, and the deployed timer schedules read back after
    provisioning as `PendingWorkDispatchSchedule */5`,
    `IntakeStagedArtifactReconciliationSchedule */10`,
    `ApprovedInboxPollSchedule */15` — from `*/15`, `30 * * * * *` and
    `45 * * * * *`.

    **Two release-route facts recorded here because they cost time:** the azd
    environment carried release 15's image digest and revision suffix, so the
    first `azd provision` failed on a revision-suffix collision — which was
    the fortunate outcome, since without it the old image would have been
    redeployed silently. And there is no Docker on the release workstation, so
    the Web image moves from its OCI archive to ACR with
    `az acr login --expose-token` + `oras login` + `oras cp`. Both are now in
    the `pegasus-release` skill.

    Not proved by this release: no live instruction had been processed at the
    time of writing, so the intake behaviour above is proved by tests and by
    the deployed revision, not by production evidence.

  - **Release 16** (2026-08-21, manifest SHA-256 `D89EDF32…`) carried the
    post-release-15 intake-regression remediation and the merged open work
    (PRs #490–#502 and the reviewed #470, #473, #495–#497): instruction
    extraction reads the real QDOS letter grammar (typographic apostrophes,
    third-party-row guard, typed-date dedupe, wrapped-line subsumption) and
    the report-sourced vehicle/mileage backfill plus the circumstances
    paragraph as QDOS policy Version 4 rules; the Worker holds INSERT on
    `VehicleLookupRequests` so the automatic DVSA/DVLA sweep enqueues (a
    permission failure now fails the function visibly instead of being
    swallowed as already-done) — within one reconcile tick of the deploy
    the three stranded lookups were enqueued; both runtime roles hold
    UPDATE on `ImageIntakes` for the image-custody lifecycle; the Inbox
    message page was rebuilt on the record container with the effective
    original sender, structured forwarded headers, provider-footer
    trimming of the displayed body and list excerpt, exhaustive
    family · subtype classification labels, and CSP-compliant dialog
    binding in site.js; embedded instruction photographs (≥40 KB, hash-
    deduped) are promoted to case evidence beside the source in Box and
    render on the case Evidence tab; the Administrator-only approved
    Outlook category catalogue is live at `/Administration/MailCategories`;
    the Automation Actor gained registered Unidentified and Triage parity
    tools over the same Core owners (ingress composition remains gated);
    the application-exception alert deduplicates by operation and
    normalized signature and pages only for failed or persistent
    operations; Assessment vehicle prefills come from the shared Case
    projection. Post-deploy smoke passed (health, exact version/SHA
    `4111ad29`, anonymous-denial, https redirect, Worker
    `approved-live-worker`); the migration head, both roles' grant
    readback, and the fresh inbox poll state were verified live.
  - **Release 15** (2026-08-20, manifest SHA-256 `3D652838…`) carried the
    2026-08-20 operator feedback-round-2 remediations, each verified against
    its ticket before the cut (PRs #476, #478, #479, #481–#485, #487, #488):
    the design authority gained the binding no-explanatory-copy and
    page-economy rules and every touched page was brought under them; one
    upload submission is one decision card with image thumbnails; instruction
    extraction writes unambiguous typed-valid values as facts (auto-added case
    details) with claimant/claim-number/vehicle-description coverage measured
    on the real corpus; parallel same-receipt allocation retries are
    serialised by a per-receipt application lock (the recurring CI deadlock's
    root cause); the case page renders only populated sections with an
    edit-mode toggle and unsaved-changes dialog; every active case with a
    known registration gets an automatic DVSA/DVLA lookup via a reconciliation
    sweep and the assessment page prefills Mileage/Source from the evidence;
    Queues shows one merged Not-ready table with dropdown filters and sortable
    newest-first columns; the assessment page carries no hint sentences, a
    contained estimate grid, a clickable damage diagram that saves the
    case's impact location, and a preselected assessment method; the Inbox
    resolves automatically allocated cases from the allocation attempt so an
    allocated message can no longer read as awaiting allocation; Box custody
    writes no binding marker files (database folder id is the identity
    authority) and retains each instruction attachment beside the source —
    the eight legacy binding JSONs were deleted from the four live case
    folders after the deploy. The same day, the operator-approved test-data
    wipe removed 1,211 rows across 66 case/intake/image/mail data tables and
    all 159 transient-intake blobs, preserving identity, configuration,
    reference and sequence tables so references are never reused. Post-deploy
    smoke passed (health, exact version/SHA `6d04f89d`, anonymous-denial,
    https redirect, Worker `approved-live-worker`); live checks confirmed the
    new Queues surface, the empty wiped estate, and working staff sign-in.
  - **Release 14** (2026-08-20, manifest SHA-256 `87667CB7…`) carried the
    2026-08-20 operator-review remediations, each independently verified
    against its ticket before the cut (PRs #437–#468, #471, #472): the
    Not-ready count now includes image-initiated records and matches its rows;
    the Dashboard e-mail counter counts the mailbox channel only; Unidentified
    items resolve automatically when their receipt reaches a real destination
    (the stale U7 closed on the first post-deploy sweep; genuinely
    unidentified items stay open); one grouped upload registers one
    image-initiated record with a group-scoped operation key and a 15-second
    dispatch cadence; the post-upload page offers attach-to-case search;
    image-initiated cases carry their own Box folder work items and fold into
    the paired case on merge; case and vehicle images render as thumbnail
    galleries served inline by the staff-only image route; mailbox
    administration works by address alone with logical-folder visibility
    (MAIL-23 read-only exception) and the Sent-evidence poll completes
    cleanly; the Functions Worker no longer aborts during provisioning
    windows (deferred Box option parsing — zero exit-134 events post-deploy);
    assessment readiness collapses to one issues-count disclosure; MOT-table
    rows can no longer pollute vehicle make/model suggestions; estimate
    import (Audatex, fail-closed) and DVLA/DVSA vehicle data with MOT
    chronology and classified mileage are live; legacy `.doc`/`.msg`
    extraction runs in-process (ADR-0025); the Automation Actor gained
    mail-workspace and assessment tools; operator copy was aligned with the
    design authority (uncomposed capabilities render nothing). Send to AI and
    its connector administration remain composed only outside Production
    behind `Features:SendToAi`.
  - **Release 13** (2026-08-20, exact-SHA fast-forward, `main` = `dev` =
    `2325ed4a`, manifest SHA-256 `E40933DE…`) carried the six operator-review
    remediations of release 12, all reviewed independently before merge: the
    Approved-mailboxes layout rebuilt out of its table cell; the estate-wide
    UI-narration strip to the design rule (29 pages, lede slot removed at the
    source); Unidentified re-homed as a Queues tab with Image/E-mail filters
    and Not-ready origin filters, GUID- and "intake"-free by regression test;
    the upload flow's per-file rows, panel-wide drag target and post-upload
    confirmation step (CASE-003's 500 fixed with it); and the atomic
    image-group outcome fix for the swallowed sequence-contention race a real
    production upload exposed within hours of release 12, with the Worker's
    missing group-table SELECT granted (verified in
    `sys.database_permissions`) and a reconciliation that recovered the
    stranded production member into `U6` through the product's own escalation
    path — visible, referenced and staff-resolvable, where it had been
    invisible. Provision preview was byte-identical to release 12's except the
    revision suffix; smoke passed with `approved-live-worker`; the Sent and
    inbox polls advanced on the new Worker within minutes.

  - **Release 12** (2026-08-19, exact-SHA fast-forward, `main` = `dev` =
    `ed3be51c`, manifest SHA-256 `86360226…`) was the first release on the
    Chromium-carrying Web base image (`mcr.microsoft.com/playwright/dotnet`,
    ADR-0028): the image grew to ~1.36 GiB compressed, the Web container was
    raised to 1.0 vCPU / 2 GiB (operator decision, in-process rendering), and
    the new revision provisioned and turned Healthy on its first pull of that
    base. It applied eight migrations in one bundle — the largest set since
    release 4 — including two grant repairs: `CaseRepairSpecifications`
    (created ungranted by the same day's TICK-093 merge) and
    `EvaHandoffDownloadOperations`, which had been created with **no
    permission rows at all** by the already-applied 2026-08-11 migration, so
    the EVA hand-off download path had been failing in production since then;
    both grants were read back from `sys.database_permissions` after the
    bundle. The database bootstrap census verified 496 catalogued rows. The
    release also carried the grouped Upload flow (INTK-005/006), the
    Unidentified queue with `U<n>` references replacing `Needs sorting`
    (INTK-007), the Image-initiated Case lifecycle (INTK-008), the MAIL-02
    destination-policy caller on `/Inbox/{id}`, the repair-specification
    store's production caller, and the report-draft entry point (reachable;
    produces a PDF once an estimate import exists — ENG-002). Route facts
    recorded: `azd env get-value` for a nonexistent key returns the CLI's
    update-notice text rather than failing, and the migration bundle's
    `Box__ConfigJson` placeholder must be shape-valid Box JWT JSON.

  - **Release 10** (same day, second exact-SHA fast-forward, `main` = `dev` =
    `d8de29cb`, manifest SHA-256 `E9B28747…`) carried the release-9 docs
    refresh (PR 404) and AUTO-002 (PR 405): authorization code + PKCE for
    external MCP connectors with Administrator consent (ADR-0027). Live
    evidence the same hour: discovery advertises `authorization_endpoint`
    and `S256`; the connector's `/authorize` request redirects an anonymous
    browser to sign-in with the request preserved; the seeded Administrator's
    consent page named `https://claude.ai` and the requested scopes; approve
    → 302 to `https://claude.ai/api/mcp/auth_callback?code=…&state=…`; the
    code + PKCE verifier + client secret → access token (600 s) and refresh
    token, scope `automation.cases automation.documents offline_access`;
    `/mcp` `tools/list` → 15 tools and an intake tool refused for a scope
    outside the token; refresh issued a new token; ActionHistory
    `automation_connector_authorized` (Staff actor, "Connector
    https://claude.ai; scopes: …"). Worker redeployed by `config-zip`; smoke
    passed with `approved-live-worker` and the inbox poll state advanced. Not proved: the Claude.ai product completing the flow itself —
    the operator connects the connector from their account.

  - **Release 9** was the first exact-SHA fast-forward promotion under the
    DELIV-002 policy (`main` = `dev` = `f1e116c6`, main-push history guard
    "9 new first-parent commit(s); main head is contained in the release
    branch") and carried PRs 362–403 beyond release 8 (376–403 beyond the
    un-numbered 14 Aug deployment): the PRD/FRD/ADR documentation model, the
    ai-centre extraction, SIMPLI-001/007–011/015 (Worker-owned queued intake,
    upload status page, Case Details capability pages, renderer/extractor
    integration), BUG-001 QDOS principal from sender route, MAIL-21/22
    classification foundation and taxonomy, MCP-04 document caller evidence,
    the operator rail UI (PLAT-001), the fast-forward release policy and the
    revised main guard, the removal of the Markdown-placement gate and of the
    qdos-pressure lane, and AUTO-001 (Automation MCP enabled by configuration,
    ADR-0026). Its two migrations were applied with the immutable
    `efbundle.exe` before the packages (`Verified 459 catalogued
    permission/denial rows and 306 effective runtime DML rows` from the
    database bootstrap) and read back in `__EFMigrationsHistory`. The manifest
    SHA-256 was `67A9C17A…`.

    Four things this release found, recorded as properties of the route:

    - The local azd environment still carried the retired adopted vaults
      (`cespkboxkvv76a47`, `cespkenrichkvgi62sd`, since purged) as the six
      Box/DVLA/DVSA secret URIs. The Web already referenced
      `pegasusprodkv252ow37g` (same secret versions); the Worker still
      referenced the old vaults and **all six of its Key Vault references were
      unresolved in production** until this release re-rendered them against
      `pegasusprodkv252ow37g`, where both identities hold Key Vault Secrets
      User. After the release all six read `Resolved`.
    - `azd deploy worker --from-package` fails on this estate (remote Oryx
      build cannot detect a dotnet version in a pre-published package); the
      route that works is `az functionapp deployment source config-zip --src
      worker.zip`. The failed attempt caused a ~7-minute Functions host
      crash-loop (`dotnet exited with code 134`) that ended when the package
      landed; the host has been running since with the nine functions loaded
      and `ApprovedInboxPollStates.LastCompletedAtUtc` advancing.
    - That crash-loop, together with the release traffic, exhausted the Log
      Analytics workspace's 0.1 GB/day cap at 11:52 UTC
      (`dataIngestionStatus: OverQuota`; `quotaNextResetTime` 2026-08-19 03:00 UTC), so no
      Application Insights telemetry from any role exists after ~11:56 UTC on
      2026-08-18; the post-release watch used the Functions admin host status
      and database poll-state readbacks instead.
    - `efbundle.exe` builds the Web host, so it needs the Production process
      environment (see the runbook) and `AZURE_TOKEN_CREDENTIALS=AzureCliCredential`
      to authenticate as the release operator; without the latter the default
      credential chain stalls on the Visual Studio credential.

  - **Release 8** carried PRs 342, 356 and 357 — CASE-27 edit authority, the
    mailbox envelope bound that had been refusing real QDOS instructions, and
    manual upload creating a case with `/Inbox` becoming a mail viewer. PR 340
    rode along as `workspaces/` source no application build compiles. Its three
    migrations were applied explicitly before activation and verified against
    `__EFMigrationsHistory`.

    Two things this release found, both recorded because they are properties of
    the release route rather than of any one change:

    - **The local `azd` environment drifts from the estate and is not
      authoritative.** Provision failed because it still pointed the Box secret
      references at `cespkboxkvv76a47`, a vault soft-deleted on 2026-08-03
      during consolidation — two days *before* release 7 deployed successfully
      from the same environment. Its recorded image digest and revision suffix
      were release 3's. The running Container App was the source of truth; the
      secret versions were unchanged and only the vault host moved to
      `pegasusprodkv252ow37g`. Read the deployed resource, not the local
      environment, when a provision disagrees with a working estate.
    - **Historical release-8 bootstrap limitation (now corrected in source).** Its
      expected matrix is read from `20260729199000_RuntimeRoleReconciliation`
      alone, so every grant added by a later migration reads as unapproved
      drift. All 24 differences were `=>` — extra in the database, none
      missing — and each traces to a reviewed migration: release 6's
      `AiWorkRequests`/`SendToAiControl`/`CaseAssessmentFields`/
      `CaseEstimateLines`, and this release's `RetainedMailboxMessages`/
      `RetainedMailboxAttachments` granted at
      `20260805223036_RetainedMailboxMessages:136-145`. The principal creation
      and effective-permission guards ran before the assertion; the matrix
      comparison is what failed. **The runtime-role effective-permission check
      was therefore not completed for that release.** The current script now
      includes every later grant-carrying migration and terminal table removal.
    - **Worker case-creation hotfix (2026-08-14):** the production Worker role
      received the 40 exact grants later captured by
      `20260814092852_AddWorkerCaseCreationGrants`; live readback confirms all
      40 are present, while the migration itself is not yet recorded in
      `__EFMigrationsHistory`. The first resulting automatic case was
      `QDOS26001`, with Box folder `a.QDOS26001` under custody root
      `405543781910`. This is live data-plane evidence, not proof that the
      corresponding application commits or migrations have deployed.

  - **Release 7** carried the six defects that live verification of release 6
    found, and is the first release whose Worker redeploy and revision
    activation carried no schema change at all. `dev` and `main` have since
    advanced by documentation-only commits, which is why the branch heads sit
    ahead of this row.
  - **Release 6** carried the whole UI implementation programme. It seeded the
    temporary `claudeuiverification` Administrator (see below) and applied its
    migration explicitly before the packages, with the runtime-role matrix
    re-verified. Live browser verification of this release found six defects
    that local testing could not: an empty local database made a permanently
    zero dashboard count indistinguishable from a correct one, and the
    Europe/London workstation clock made `ToLocalTime()` look correct where
    the deployed Linux container runs UTC. Both are recorded here because they
    are properties of the verification environment, not of any one defect:
    **a count query and a rendered time cannot be proved locally.**
  - **Release 5** shipped the PR 333 CSP hotfix and was live-verified across
    all 21 authenticated routes — every one rendering from the viewport top
    with zero inline styles, zero console errors, and zero exceptions or
    sev3+ traces.
  - **Release 4** applied the four 2026-08-03 migrations with the runtime-role
    matrix re-verified, and verified the ADR-0020 premise directly: zero
    accepted cases, `CaseMatchIndex` shipped empty. It also surfaced the
    production-CSP blank-band defect that release 5 then fixed.
  - **Release 3** proved the Key Vault secret references resolved, through a
    single healthy revision at 100% traffic.
  - **Release 2** applied the Box custody root. A read-only inventory on
    2026-08-04 confirmed the pegasus custody folder `405543781910` has zero
    children, so no legacy `{reference}-{caseId}` folders exist and the
    Case/PO fail-closed gate is satisfied.
  - **Release 1** live-verified Graph Inbox/Sent processing through the
    production Worker: 83 successful executions, zero exceptions.
  - **Worker containment (2026-08-10, later reversed — see current state below):** release/package history is
    unchanged, but the exact production Worker
    `pegasus-prod-worker-252ow37gij` is intentionally not active. One scoped
    app-settings write completed at `2026-08-10T21:34:34Z` and changed exactly
    the nine `AzureWebJobs.<function>.Disabled` values from `false` to `true`.
    Immediate and final readback found the exact nine values `true`, all nine
    function definitions still discoverable, 47 total settings, and unchanged
    non-target setting names and values. The ignored azd environment continued
    to resolve `PEGASUS_WORKER_ACTIVATION=disabled`.

    Two complete one-minute schedule intervals through `21:38:00Z`, plus the
    `DueWorkSweepSchedule` boundary at `21:40:00Z`, recorded zero platform
    executions and zero Application Insights Function requests. SQL readback at
    `21:41:13Z` found no lease and no movement in poll completion, retained
    messages, staged receipts, intake receipts, work items, Cases, or Principals.
    This proves the scoped disabled containment state only. It is not a package
    repair, baseline, activation, mail receipt, Case/PO, Box-custody, or
    product-acceptance claim.

    **Current Worker state (2026-08-18, live-verified at release 10):** the
    containment above was reversed on 2026-08-13 — the production Worker
    `pegasus-prod-worker-252ow37gij` is **enabled**. All nine
    `AzureWebJobs.<function>.Disabled` settings read `false`
    (`Invoke-ProductionSmoke.ps1 -ExpectedWorkerActivation approved-live-worker`
    passed on 2026-08-18; `PEGASUS_WORKER_ACTIVATION=approved-live-worker` is the
    azd input). Beyond configuration, the release-10 package's inbox poll ran
    against the deployed estate (`ApprovedInboxPollStates.LastCompletedAtUtc`
    advanced within a minute, no failure code) and the Worker created a real
    case with Box custody on 2026-08-14 (INT-25 tier-5 evidence, TICK-012).
- **Post-release-8 deployment (observed 2026-08-12):** Production Web serves an
  un-numbered post-release-8 deployment: revision
  `pegasus-prod-web-252ow37gij--13m13ph`, source revision
  `dd61ac56840d2cf0c1f0667f995c3941cbb19fc5` (PR 370), image
  `sha256:04d39c20f1fb4494dbc26b93f151683674233e20ff6e99b76b3b9f951ac4b7f3`.
  `/health/live` and `/health/ready` returned 200, the version diagnostic
  matched that source SHA, and anonymous `/Cases` redirected to the https
  sign-in route. This source contains three post-release-8 migrations —
  `20260811063940_QdosAllocationRecovery`, `20260811122654_CaseCustodyEvaRecovery`,
  and `20260812010335_ManualInspectionAuditCustody` — and an authorised
  `__EFMigrationsHistory` readback on 2026-08-12 confirmed all three are applied.
  A further `azd deploy` on 2026-08-14 served `aecad247…` (forwarded-QDOS-Audit
  intake fix) with `20260813025241_StandaloneAuditReportDecision` applied. No
  immutable manifest was retained for either; they are recorded as the
  un-numbered row in the release table and superseded by release 9, whose
  manifest and migration transcript exist. Live verification beyond smoke and
  Worker configuration is limited to the INT-25 tier-5 case creation
  (2026-08-14) and the release-9 Automation MCP evidence above; no browser
  journey has exercised the upload-to-case path, the Inbox, or CASE-27 edit
  authority against the deployed estate.
- **Temporary verification account:** `claudeuiverification` exists on the
  production estate as an enabled Administrator, seeded by release 6 from the
  `Bootstrap:VerificationAccount` block committed to `appsettings.json`. It
  exists at the operator's request and on their stated risk assessment, so
  that interface verification does not run as the owner's own account, and
  **it must be removed before go-live.** Replacing the block with
  `{ "Removed": "claudeuiverification" }` deletes the account on next start.
  Its password is in source control; treat the account as disclosed.
- **Integrations:** Graph via the Worker managed identity scoped by Exchange
  Application RBAC to `instructions@collisionengineers.co.uk`; Box production
  custody rooted at the pegasus folder `405543781910` (applied by release 2);
  since release 3 Box is reached by both hosts — the Worker for intake-source
  custody and Web for the staff document surface and managed document
  content — through the one root-fenced client;
  official DVLA VES v1.2 and DVSA MOT History v1; EVA remains the accepted
  manual JSON/image handoff.
- **Secrets:** consolidated into the one Pegasus Key Vault
  `pegasusprodkv252ow37g` on 2026-08-03; `rg-pegasus-prod` holds no other
  vault. The six live Box/DVLA/DVSA secrets were restored into it and both
  hosts repointed to versioned target-vault URIs: the Worker's
  `Box__ConfigJson`, `Box__ClientSecret`, `Dvla__ApiKey`, `Dvsa__ClientId`,
  `Dvsa__ClientSecret`, and `Dvsa__ApiKey`, and the Web's `box-config-json`
  and `box-client-secret` Container Apps secrets. Access stays secret-level:
  exactly six Worker and two Web `Key Vault Secrets User` grants, each scoped
  to a single secret resource, held through the distinct Web/Worker
  user-assigned identities. The temporary `Key Vault Secrets Officer` created
  for the restore was removed; only a metadata-only `Key Vault Reader`
  remains at vault scope. Without those grants a Web revision fails to start
  rather than starting without custody.

  Live-verified 2026-08-04 (read-only): all six Worker Key Vault references
  report `Resolved`, both Web secrets carry the Web identity and target-vault
  versioned URIs, every referenced secret version exists and is enabled, and
  the active revision `pegasus-prod-web-252ow37gij--c6571f771aab` is
  `Provisioned`/`Healthy` (scaled to zero). No secret value was retrieved.
- **Predecessor vaults:** retired. `cespkboxkvv76a47` and
  `cespkenrichkvgi62sd` were soft-deleted 2026-08-03 once independent
  readback proved no live Pegasus reference pointed at either, and the
  then-empty `rg-collisionspike-dev` was deleted — confirmed absent
  2026-08-04. Five soft-deleted vaults now await platform purge in `uksouth`
  on **two** dates: `cespk-pg-kv-dev`, `cespkevakvufa3ci`, and
  `cespklockva7tzj2` on 2026-08-09, then `cespkboxkvv76a47` and
  `cespkenrichkvgi62sd` on 2026-08-10. No purge was attempted or authorised;
  the watch is not clear until both dates pass.
- **Predecessor retirement:** executed through the exact verified manifest;
  eight resource batches completed, 30 delete-classified role assignments
  removed, 7 retained; the archive manifest hash is recorded in the runbook
  (git history).
- **Monitoring/cost:** 31-day retention, adaptive sampling, a 0.5 GB/day
  cap declared once in `infra/modules/platform.bicep` for both the
  Application Insights component (resets 00:00Z) and the Log Analytics
  workspace (resets 03:00Z) — release 35 (2026-08-27) applied it; both live
  caps read back 0.5 GB after that provision — £75 monthly budget notifying
  `digital@collisionengineers.co.uk` at actual 50/80/100% and forecast 100%.
  Since release 16 the Sev1 application-exception scheduled-query rule
  deduplicates by operation and normalized signature over a 15-minute
  window and fires only for a failed recent operation, a signature
  persisting across ≥3 distinct operations, or ≥3 minute-buckets of
  uncorrelated telemetry; the Web 5xx rule keeps its 5-minute window.
  Alerts never stop resources.
- **Recovery:** the OPS-09 recovery proof is deferred and gates no release
  (removed as a gate 2026-08-03); the procedure remains in
  [production recovery](runbook.md#production-recovery).

## Azure activation remains fail-closed

`infra/main.bicep` remains fail-closed unless the exact
`deploymentMode=approved-live-deployment` value is supplied. The production-only
route replaced the former development/offline topology; Bicep compilation and
local plan validation do not authorize Azure.

The concrete activation gate is a separately recorded approval for the exact
subscription, resource group, principal, cost scope, data boundary, and
migration/deployment sequence, followed by a fresh authorised-terminal check of
availability, quota, pricing, role-assignment authority, target names, SQL Entra
administrator, and external credential readiness.

Apply migrations explicitly before application packages. Application startup must never silently migrate a non-Development database. Deployment does not itself prove live behavior or acceptance.

## Recovery

Current source provides no application backup/restore executable or
receipt/artifact deletion route, and no Pegasus recovery, failover, RPO, or
RTO exercise has completed. The recovery proof is deferred and gates no
release. The production Box custody adapter is deployed behind the existing
Core port and rooted at the approved custody root (see
[production environment](#production-environment)); it is not recovery-tested
or operator-accepted. Test cleanup and migration tests are narrower evidence.
The accepted method for a future exercise is in the
[runbook](runbook.md#recovery); procedure does not establish execution.

**Measured backup posture** (read-only `az` readback, 2026-08-20, database
`pegasus` on `pegasus-prod-sql-252ow37gij`, `rg-pegasus-prod`):

- Short-term retention (point-in-time restore window): 7 days; observed
  `earliestRestoreDate` 2026-08-13, exactly 7 days before the readback date —
  PITR is live, not merely configured.
- Backup storage redundancy: `Geo` (both `current` and `requested`); primary
  region `uksouth`, secondary `ukwest`. `zoneRedundant`: false.
- Long-term retention: not configured (weekly/monthly/yearly retention all
  zero) — only the 7-day short-term window exists.
- SKU: Standard `S0` (10 DTU). Database size: ~39.5 MiB used, ~48 MiB
  allocated, against a 250 GB max.
- Documented RPO for this configuration (Microsoft Learn, "Automated backups
  in Azure SQL Database"): transaction log backups approximately every 10
  minutes, restorable to any point within the retention window. This is under
  the 15-minute RPO objective with a typical ~5-minute margin, but Microsoft
  states the interval depends on compute size and activity — a documented
  typical figure, not a guaranteed bound.
- RTO: not yet measured by an exercise. Given the database's small size
  (~40 MB) and the documented same-region restore-time factors (size, compute
  size, log volume, activity replayed), a same-region restore is expected to
  complete in minutes, comfortably inside the 4-hour objective — an inference
  from documented factors, not a measured result.
- The exact restore commands and verification steps are in
  [runbook § Point-in-time restore commands](runbook.md#point-in-time-restore-commands).
  A restore drill that would measure actual RPO/RTO end to end is an Azure
  write and remains a separately approved exercise (parked, not run in this
  posture check).

## Deferred capability seams

Deferred capabilities must attach to an existing Core port and a real composition-root caller. Preserve run identifiers, stable source identities, versioned external contracts, ignored evidence directories, and transport-neutral policy boundaries. Every activation still requires settled product policy, representative evidence, licence/cost/security approval, a real caller, a production adapter, contract fixtures, a live sandbox where applicable, and rollout/rollback evidence.

| Capability | Preserved local seam | Activation boundary | Deliberately absent |
| --- | --- | --- | --- |
| Other Outlook mailboxes and mature categorisation | Graph fake/Dev Proxy, mailbox identities, delta replay, idempotency, policy-version/correction tests | Settle governance; approve named mailboxes and Exchange RBAC | Broad grants, rule engine/table/editor |
| Automated outbound email and chasers | Send contract, recipient validation, retries, delivery state, permanent action history | Approve behavior and allowlisted test mailbox | Automatic sender |
| WhatsApp | Versioned webhook/client fixtures, provenance, consent, duplicate and receipt handling | Product/provider selection and sandbox approval | Client, webhook, queue |
| EVA API or replacement | Versioned contract, reconciliation, idempotent create/update, shadow comparison | Vendor/operator approval and sandbox | Client or replacement engine |
| Estimating, valuation, invoicing, accounting, Audatex | Money/currency/source/version policy, permissions, history, contract fakes | Product, commercial, API, and sandbox approval | Finance schema/service/workflow |
| Diminution and Commercial | Explicit unsupported outcome; later lifecycle, fields, shared sequence, persistence, browser evidence | Operator-defined workflow and acceptance | Case type/state implementation |
| Guided capture, Tractable, Ravin | Mobile browser matrix, resumable upload, asset provenance/order, consent, duplicates | Vendor, licence, security, sandbox approval | Vendor client or upload service |
| AI/vision and automated VRM recognition | Deterministic fake, suggestion-only policy, confidence/provenance/correction, frozen cohort/holdout | Accuracy, model/service, licence, cost, security, data-transfer approval | Model client, endpoint, queue, feature flag, corpus upload |
| DOC/MSG extraction | Safe parsing fixtures for nesting, corruption, encryption, resource bounds | Human-reviewed genuine cohort/holdout; separately approved external service if selected | Automatic production parser |
| Address suggestions/maps | Provider fake, provenance, correction, never-auto-accept behavior | Provider/privacy approval and sandbox | Client, endpoint, stored guess |
| External/customer accounts | Deny all access; later invitation, recovery, ownership, cross-tenant isolation | Tenancy/identity decision, ADR, approved identity environment | External role, registration, tenant schema |
| Custom domain | Hostname-independent auth, local HTTPS, cookie/redirect/HSTS/callback tests | DNS/TLS/OAuth migration and rollback | Domain, certificate, hostname dependency |
| Graph webhooks | Signature, replay, expiry, duplicate-notification contracts | Approved public callback and subscription | Endpoint or subscription |
| PDF-engine replacement | Frozen cohort/holdout and contract-parity suite | Licence, security, maintenance review, single-path cutover | Parallel permanent engines |

Scan-like PDF OCR remains an unactivated caller gate; its optional Worker adapter does not establish a queued producer or live provider evidence. Provider API caller and deployment evidence are recorded separately in the [capability inventory](capabilities.md) and this document's release history.

SMS, Teams, a customer portal, redaction, signatures, legal hold, subject-request workflows, and predecessor application/data migration remain exclusions until separately authorised.

### Permanent `Not planned` boundaries

Do not create an implementation, profile, fixture, port, queue, table, endpoint, dependency, configuration, release gate, topology, or cost path for:

- malware scanning or quarantine;
- multi-region or availability-zone architecture;
- private networking;
- separate staging, QA, UAT, or demo environments;
- S1 or deployment slots.

Malware scanning has no activation path. There is no scanner port, fixture, client, quarantine state, or release claim.
