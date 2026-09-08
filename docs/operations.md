# Operations

This is the last recorded deployed-state and support summary. It is not a fresh
cloud observation. Exact source structure belongs in [architecture](current-architecture.md);
procedures are reached through [the runbook](runbook.md).

## Read-only production observation — 6 September 2026

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
owned by the [runbook's live-operation approval matrix](runbook.md#operational-authority).
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
[production environment Secrets record](operations.md)) — never
client-side.

The intended application staff accounts are Pegasus Identity accounts. The DevelopmentOffline profile authenticates its deterministic local Administrator fixture and enforces its Administrator role. Application staff identity initialization remains a separately controlled application operation; Entra users must not be assumed. Third-party credentials must never enter tracked settings, command-line arguments, prompts that may be retained, terminal output, telemetry, or business history.


## Support and incident response

Alex provides first-line application support and initially receives security,
availability, failure and cost alerts. Additional recipients are configuration,
not code changes. Critical incidents are acknowledged immediately while Alex is
in the staffed office; outside staffed hours, response is as soon as reasonably
possible. This records the support arrangement, not an invented 24/7 SLA.

Emergency production access is Alex initially, plus specifically designated
Administrators or Azure operators. Exact credentials and grants are not stored
in this file. Read-only inventory and external mutation have different authority.

## Retained evidence and recovery basis

The complete prior release ledger, failed attempts, hashes, rollout limitations,
rollback artifacts and old cost observations are retained in
[the baseline operations record](https://github.com/collisionengineers/pegasus/blob/af1625fae8ac8018054c95e988907f6c44fa4639/docs/operations.md).
Use the exact applicable release evidence when selecting recovery; do not infer
rollback compatibility or a deployed repair from a later source fix.

A new observation records UTC time, environment, artifact/revision, migration,
observed activation and the checks actually performed. A release can change
deployable infrastructure, dependencies, schema or runtime configuration even
when no file under `src/` changes. Update this summary with observations, not
future requirements or a second ticket-status ledger.

No fresh full-day workload, excluded capacity/soak run, external-provider
acceptance or disaster-recovery exercise is claimed by this documentation change.
Historical evidence remains historical; an unresolved incident stays explicit
until an observation establishes its disposition.

## Current development estate choices

The current Web warm settings are the operator's chosen configuration, not an
architectural minimum. Current data is disposable test data and need not be
preserved during an authorized reset. Neither fact supplies permission to
change settings or clear an estate outside the current task's authorization.
