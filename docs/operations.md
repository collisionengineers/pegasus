# Operations

This file is the current-state record for production, releases, evidence
profiles, monitoring, and recovery. Executable setup, development, database,
testing, release, approval, monitoring, and recovery procedures are owned by
the [runbook](runbook.md). The evidence-tier ladder and repository verification
rules are owned by [engineering](engineering.md#required-evidence-tiers).

## Evidence and authority

Use these evidence states literally and independently:

`Planned` → `Implemented` → `Called` → `Locally verified` → `Deployed` → `Live verified` → `Accepted`

Compilation, registration, mocks, local execution, deployment, live-service
observation, and operator acceptance are different conclusions. The
authenticated `/Upload` POST through `ReceiveIntake` is the manual HTTP staging
caller; Worker owns queued processing; `/Received` and `/Inbox` are read-only
views. Source registration is not proof of deployed or live traffic.

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
not an accepted setting or deployment input. Secret values remain resolved only
inside the Worker through Key Vault references.

The intended application staff accounts are Pegasus Identity accounts. The DevelopmentOffline profile authenticates its deterministic local Administrator fixture and enforces its Administrator role. Application staff identity initialization remains a separately controlled application operation; Entra users must not be assumed. Third-party credentials must never enter tracked settings, command-line arguments, prompts that may be retained, terminal output, telemetry, or business history.

## Evidence profiles

The current operational baseline is the [offline development profile](runbook.md#offline-development-profile). The following
caller-scoped profile model distinguishes implemented local gates from planned
activation; installing a tool never establishes a caller.

| Profile | Current gate and planned boundary |
| --- | --- |
| `Baseline` | Windows or Linux, PowerShell 7, Git/GitHub CLI, pinned .NET 10 SDK, Azure CLI with Bicep, Azure Developer CLI, Node/npm, Python, Infisical CLI, and Box CLI for build, test, Bicep validation, and approved administration. Cloud/vendor tools remain optional in the current offline baseline. |
| `SqlServer` | The platform's supported SQL Server (LocalDB on Windows, a container on Linux) and `sqlcmd` for migrations, constraints, transactions, allocation concurrency, outbox atomicity, and local backup/restore. |
| `StorageWorker` | Repository-pinned npm Azurite and Functions Core Tools v4 for real Blob/Queue SDK, trigger, retry, poison, and restart paths. Activate only with the first real storage adapter and Worker trigger. |
| `Browser` | The `Browser` trait pins Microsoft Playwright for .NET, Chromium, and Deque axe-core. It drives the rendered DevelopmentOffline Operations, intake, Triage, administration, password-change, and case-document routes through a loopback Kestrel host, including semantic, responsive, forced-colour, and reduced-motion checks. It remains local caller evidence: Edge Stable, Narrator, manual accessibility review, external approvals, deployment, and operator/management acceptance remain separate fail-closed gates. |
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
| EVA | Exact local JSON/image-bundle contract and reconciliation metadata | Operator drag/drop acceptance and any later authorised API sandbox |
| Provider API | Not implemented: no endpoint, client, credential, or caller | Settled actor/client/authentication contract, real caller evidence, and separately approved activation |
| Automation MCP | Implemented; composition gate **enabled in production by release 9** (ADR-0026) with a Key Vault-backed client secret; integration tests drive token issuance, denial, tool calls (including the direct-write assessment tranche), and the kill switch over HTTP; live token/inventory/denial/history/kill-switch evidence recorded on 2026-08-18 under Production environment | Real external client evidence, production certificate/transport decisions, and separately approved activation |
| Send to AI channel hand-off | Implemented but composition-gated off by default (`Features:SendToAi`, DevelopmentOffline only); integration tests drive the pointer hand-off, refusal, reconcile, and the Administrator switch against a local fake connector | The recorded round-trip evidence run with a real Claude Code channel session, and any production activation, which additionally needs a non-preview transport decision (ADR-0021) |
| Direct authorised-terminal deployment | Bicep compile/lint and local configuration checks | Approved preflight, package/migration identity, deployment, health smoke, rollback |
| Backup/recovery | LocalDB backup/restore into a new disposable database | Azure SQL PITR and the one-time alpha RPO/RTO exercise |

Managed identity itself is unavailable locally. LocalDB does not prove Azure SQL Entra, throttling, backup, restore, RPO, or RTO. Azurite does not prove Azure Files, ADLS, Entra/RBAC, managed identity, durability, replication, quotas, networking, scale, or production timing.

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
`automation.assessment`) for exactly one vendor-neutral Automation client
whose identifier and secret come from configuration/user-secrets and are
never tracked or displayed. Every tool invocation is permanent action
history attributed to the Automation actor with a correlation identifier;
denials write `automation_*` security events; Administrators review both in
the Administration Automation activity view and hold an immediate kill
switch (disable refuses new tokens outright and rejects already-issued
tokens within seconds). A staff browser identity is not a substitute for
that actor and is never accepted on `/mcp`.

Every automation action is recorded exactly as a human action is (ADR-0021):
the fourteen tools wrap the same Core commands, edit lease, operation-key
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

Core contains local `ActivitySource` instrumentation. The deployed Worker registers and exports Application Insights telemetry (its live executions are observable in the production Application Insights resource), and the production budget/alert wiring is recorded under [production environment](#production-environment). The current Web host registers no in-process telemetry exporter, so correlated Web/Worker telemetry (OPS-07) remains open work; there is no live incident record or current recovery/deletion incident evidence, and historical predecessor incidents do not establish current Pegasus behavior.

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
- **Deployed evidence:** the estate currently serves **release 12**. A branch
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
- **Monitoring/cost:** 31-day retention, adaptive sampling, 0.1 GB/day
  Application Insights cap, £75 monthly budget notifying
  `digital@collisionengineers.co.uk` at actual 50/80/100% and forecast 100%.
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

Scan-like PDF OCR and the provider API are deferred caller gates whose exact targets are owned by the [capability inventory](capabilities.md); neither blocks `0.1.0-alpha.1`.

SMS, Teams, a customer portal, redaction, signatures, legal hold, subject-request workflows, and predecessor application/data migration remain exclusions until separately authorised.

### Permanent `Not planned` boundaries

Do not create an implementation, profile, fixture, port, queue, table, endpoint, dependency, configuration, release gate, topology, or cost path for:

- malware scanning or quarantine;
- multi-region or availability-zone architecture;
- private networking;
- separate staging, QA, UAT, or demo environments;
- S1 or deployment slots.

Malware scanning has no activation path. There is no scanner port, fixture, client, quarantine state, or release claim.
