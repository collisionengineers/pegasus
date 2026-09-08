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
matching the release-38 record in the
[baseline release ledger](https://github.com/collisionengineers/pegasus/blob/af1625fae8ac8018054c95e988907f6c44fa4639/docs/operations.md). `Features__AutomationMcp` and
`Features__ProviderApi` were both `true`; no explicit `Features__SendToAi`
environment value was present. This read checked revision, image and those
settings only. It did not recheck SQL, provider credentials, external-client
round trips, document rendering or operator acceptance.

Later source changes, old workstation-readiness checks and earlier renderer
canaries are separate evidence. Their exact observations and limitations remain
in the [baseline operations record](https://github.com/collisionengineers/pegasus/blob/af1625fae8ac8018054c95e988907f6c44fa4639/docs/operations.md).
Do not infer present authentication, render readiness or deployment from them.

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
