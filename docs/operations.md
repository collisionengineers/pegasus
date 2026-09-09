# Operations

This is the last recorded deployed-state and support summary. It is not a fresh
cloud observation. Exact source structure belongs in [architecture](current-architecture.md);
procedures are reached through [the runbook](runbook.md).

## Release 40 — 9 September 2026

The approved corrective release was deployed from Windows PowerShell 7 at
source `c9bd3aa6bba06a81f35b0e4aca36ab8c01ac744b`, version `0.1.0-alpha.1`.
All ten checks in [the exact dev CI run](https://github.com/collisionengineers/pegasus/actions/runs/34312991582)
passed before the whole dev branch was promoted through [PR #675](https://github.com/collisionengineers/pegasus/pull/675).
This includes merged corrections #712, #714 and #715; unmerged saved worktree
branches are excluded.

The schema-3 manifest SHA-256 is
`2C9127990A039C49020053D46A413F1E4064FCF8D27AD6AD71D61D7F4D6C54D6`.
The sole active Web revision is
`pegasus-prod-web-252ow37gij--c9bd3aa6bba0`, observed Healthy,
RunningAtMaxScale and Provisioned at image digest
`sha256:6179de11540614449c3c70d3934e0933a872b992f5487d9d10405efd91676684`.
The manifest's linux/amd64 Web image was uploaded and its remote digest verified;
its exact Worker ZIP was staged by deployment
`d352a2ba-edda-4aeb-841e-a6616ec2e05d`. The native win-x64 `efbundle.exe`
applied the three pending migrations through
`20260909091500_RemoveCaseDocumentOcrOperations`. Runtime bootstrap verified
651 catalogued permission/denial rows and 449 effective runtime DML rows.

The maintenance sequence began with Worker Function disablement and new-package
staging at 05:26 UTC. Worker Stopped and the exact old Web revision inactive with
zero replicas were verified before the intake reset and again before migration.
The reset cleared 122 blobs (156,871,692 bytes) in
`pegcustody252ow37gij/transient-intake` and 448 intake-created rows across 87 SQL
tables. Verification found zero remaining blobs and zero populated reset tables;
34 tables were preserved. The SQL batch reported 449 affected rows including the
mail-state update. The committed receive-time cutoff is
`2026-09-09T05:31:30.9218156+00:00`. Identity, mailbox approval/activation times,
all four reference-sequence tables (zero keyed-value changes) and all five
valuation presets were preserved. The reset did not touch `authentication-ring`,
`box-links`, `pegtrans252ow37gij` contents, Outlook or Box.

Provisioning created `pegasus-prod-ocr-252ow37gij`; its successful state,
disabled local-key authentication and Worker managed-identity Cognitive Services
User assignment were verified. Web and OCR infrastructure were provisioned with
Worker disabled, then the compatible Worker was explicitly activated. The final
checks passed at 05:42:33 UTC: Worker Running, every canonical Function Disabled
setting false, exact Web version/digest and health, Graph validation handshake,
anonymous Case-access denial and schema head. Intake liveness reported a completed
poll at 05:40:40 UTC and an active subscription expiring
13 September 2026 at 17:15:12 UTC. Alex owns live behavioural acceptance; no
intake/OCR canary was uploaded by the agent and no functional acceptance is claimed.

Retained attempts and limitations: the first migration host setup stopped before
SQL because the copied azd environment lacked `BOX_HOLDING_FOLDER_ID`. The existing
Worker value `415928884118`, subsequently confirmed by Alex, was restored along
with existing signing/encryption certificate URI inputs from the old Web revision.
The migration retry succeeded. The initial post-provision Web check reported an
unexpected active revision; a fresh full inventory and the unchanged strict check
then verified the sole exact approved revision and digest. The first activation
smoke stopped on the reset's null poll-completion timestamp; the normal scheduled
poll completed and the full smoke passed on rerun. No old application bytes were
explicitly reactivated after the destructive migration.

The four hash-verified artifacts are retained locally under
`artifacts/releases/release-40-c9bd3aa6/`, with stage transcripts and retry evidence
in its `execution/` directory. The approval packet and preparation evidence are
`artifacts/corrective-release-approval-20260909.md` and
`artifacts/corrective-readiness-20260909.md`. These local artifacts are not
committed. The main checkout's ignored azd environment was synchronized to the
observed deployment inputs; its previous copy is retained with the release.

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

## Historical release-39 record — 7 September 2026

This is a qualified historical record from the unmerged [PR #676 operations
entry](https://github.com/collisionengineers/pegasus/blob/93255e5c188865e5b0dab95d6c628ab49a902d99/docs/operations.md), not a fresh D59 Azure,
artifact, telemetry or estate observation. It names source
`3da60bd0c270111d5168dc17246dc831882108ea`, image
`sha256:cc1a5077efdc761e359667a1bfc73b7cf8851e88437cd95fec2615fc41b1fe73`,
manifest SHA-256
`F9C64EF7729484EF3BD3EFBF24F0791ADA4F53857EA05F5A80E8453A549D3E08`,
and migration head `20260907100000_RemoveAutomaticEvaSubmission` as PR-recorded
release identifiers. Git history independently establishes only that source
identity. GitHub Actions [run 34132950893](https://github.com/collisionengineers/pegasus/actions/runs/34132950893)
independently shows `test-ui` failed while `browser` succeeded; it does not
confirm the PR's browser-cancelled or two-lane-waiver narrative, and D59 found
no retained waiver-authorisation receipt.

PR #676 records that Kudu rejected the manifest Worker ZIP
`DF651AD0D87850AB7ECD883D06951B8B1844CA4C93BAAD67715D8D075BF4F261` because
Linux glob packaging omitted `.azurefunctions/` and `.playwright/`. It further
records a same-source-SHA replacement Worker package
`0651B1862CA8201C5D70C51577B5614F491A08764D46D5B3536759CF6F1B6BBB` and
`config-zip` deployment `2ce3eb15-0a86-4d14-94f2-ecc1264ea25a`. The replacement
DLLs were not byte-identical to the manifest copies, so this is retained as a
provenance deviation rather than equated with the manifest artifact. The same
historical record attributes the intervening Worker failures and subsequent
smoke/telemetry statements to that release; D59 did not re-read the manifest,
either ZIP, deployment receipt, Azure telemetry or current estate. Later
source-only ZIP correction [PR #703](https://github.com/collisionengineers/pegasus/pull/703)
does not establish a release-39 artifact or deployment.

PR #676 also records seven migrations committing before `V1PlatformFoundation`
failed, leaving release 38 briefly without two `WorkflowConfigurations`
review-flag columns. It records two separately authorised test-data deletions
before the remaining six migrations and bootstrap completed: an intake wipe,
then removal of the conflicting QDOS organisation/principal/lineage and its
sequence row, resetting that seeded lineage. Those partial-migration and reset
facts remain historical PR claims requiring their own past authorisation; they
are not D59 permission to migrate, reset, or infer a present database state.

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

[The baseline operations record](https://github.com/collisionengineers/pegasus/blob/af1625fae8ac8018054c95e988907f6c44fa4639/docs/operations.md)
retains the prior release ledger, failed attempts, hashes, rollout limitations,
rollback artifacts and old cost observations through release 38. The dated
historical release-39 entry above supplements that ledger with PR #676-recorded
evidence and its explicit limitations; neither record is a fresh observation.
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
