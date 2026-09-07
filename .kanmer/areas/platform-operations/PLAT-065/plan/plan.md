# Plan — PLAT-065: provision and activate qualified OCR

## Objective

Activate the existing qualified-page OCR caller using one Document Intelligence
account in the existing Pegasus estate, with Worker-only managed identity and
honest live canary evidence.

## Starting state

Evidence: research/research.md@2965aedeaa0ce107;
files/files.md@ee0cc1313c72b9a7. Source TICK-041 commit 890f656be contains
accepted ADR-0040 and the implemented source-context contract; not activated.
No live Cognitive Services account or Worker endpoint exists. Preserve
EPIC-011 and EPIC-014 membership. The current assignment authorizes research/
planning only: do not take or create a worktree until root dispatches execution.

## Governing docs

Meets linked FRD-05: retained source, positively qualified pages, one OCR owner,
confidence/structure fail closed and durable recovery. Meets linked FRD-07:
pending OCR is not an imported estimate; no background Current selection.
Meets accepted ADR-0040 (TICK-041): prebuilt-layout, GA data API 2024-11-30,
custom subdomain and resource-scoped Worker identity. Link the ADR when its
merged file is available; no new ADR or model decision. EPIC-014 records the
user's explicit provisioning/deployment approval; no invented approval system.
The exact target/cost below is the bounded proposed scope before any write.
Existing pegasus-release/runbook procedure still binds release SHA, preview
and actual environment evidence.

## Required changes

In platform.bicep add Microsoft.CognitiveServices/accounts, kind FormRecognizer,
S0/Standard, uksouth, name/customSubDomainName
pegasus-prod-ocr-252ow37gij via existing prefix/suffix. Use advertised current
ARM GA 2026-07-01; this is not the pinned data API. Set disableLocalAuth=true,
publicNetworkAccess=Enabled and existing estate tags. No managed identity on
the account: Worker posts retained bytes, so service-side storage access is
unnecessary.

Add one deterministic role assignment at the account scope, Cognitive Services
User a97b65f3-24c7-4388-baec-2e87135dc908, principal
4f4d9606-3634-4c21-a1ee-3238351cfc69 from existing workerIdentity properties,
principalType ServicePrincipal. Add DocumentIntelligence__Endpoint only to
workerApp appSettings, using the account's custom-domain endpoint. Worker
depends on assignment. No Web role or endpoint. Expose non-secret account ID/
endpoint outputs through platform/main for release readback.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | infra/modules/platform.bicep | Account, Worker role, endpoint and dependency |
| Modify | infra/main.bicep | Non-secret deployment outputs |
| Modify | tests/Pegasus.ArchitectureTests/WorkerActivationReleaseContractTests.cs | Focused existing-template contract assertions |
| Modify | docs/runbook.md | Existing OCR activation/readback/rollback paragraph |
| Modify | docs/current-architecture.md | Actual source/caller state |
| Modify | docs/operations.md | Exact dated resource/cost/deployment/live proof |

## Do not modify

- src/**
- infra/main.parameters.json
- docs/operator-notes.md
- corpus/**
- pegasus_pack/glasses-integration/glass_ref_docs/**

Only the six Expected files are writable. No new module or project.

## Constraints

Exact tenant 858cf5b3-aa0a-47a6-9b40-4851fd0afa94 and subscription
e6076573-23a5-46a8-acef-7e22d264e5db, resource group rg-pegasus-prod.
Existing Worker pegasus-prod-worker-252ow37gij/client
d7d9a0ad-a309-467d-9102-56a002fb0edc. No new runtime/package/identity/custom
role/network/secret/schema/budget change. Model cost is USD10/1,000 selected
pages; GBP7.3624/1,000 is a dated reference estimate only. No monthly volume
assumed, add-on or commitment tier. Keep existing £75 estate budget.

## Ordered steps

1. After TICK-041 integration and fresh packet/claim, implement the resource,
   resource-scoped Worker assignment, endpoint and output propagation.
   Extend the existing template test with account/S0/local-auth/scope checks,
   Worker endpoint and absence of Web OCR access. Preserve activation census.
2. Root runs focused validation below. Update canonical docs to separate
   implemented infrastructure from not-yet-activated production. Commit/PR to
   dev and stop for independent review; no author merge or cloud write.
3. Root's later authorized release, once TICK-085's actual canonical PDF caller
   is integrated, binds one exact SHA/manifest, refreshes inventory/name/role
   authority and selected Worker settings, and retains a full what-if/preview.
   Expected OCR delta is one account, one role assignment, Worker endpoint and
   outputs; other release changes require their own named ticket evidence.
   Never accept unrelated deletes, Web OCR role, implicit Worker disable,
   replacement identity, credential introduction or budget growth.
4. Through the existing release route deploy the reviewed artifact/configuration,
   read back account/SKU/auth/endpoint and actual identities, then exercise
   the approved retained-source canonical Case import with the supplied
   YL69YFO PDF (SHA256
   6918c91fce058b5365446681045056158336b90e1de89ba1bdc5a1d7394f728c,
   241526 bytes, six selected pages). Keep source bytes immutable. Retain
   operation/model/API/hash/selected pages and result evidence; resume the same
   operation to prove no second charge/submission. Readable VX21TZD PDF follows
   embedded extraction without OCR. No bulk corpus transfer or mail.
   From actual Web identity prove read-only data-plane access refused without
   printing its token; combine with exact account-scoped assignment readback.
   Update operations/current-architecture with exact live outcome and limits.

## Acceptance checks

Production caller is WorkerDependencyInjection → ProcessQueuedExternalWork →
ProcessIntakeOcr → AzureDocumentIntelligenceOcr. Existing credential excludes
all non-managed-identity sources. Worker package already contains adapter and
Azure.Identity; no additional deployed dependency is needed.
Live canary must pass TICK-085's deterministic source/printed-total oracle:
OCR alone is not import success. Timeout/throttle/unknown recovery evidence
reuses TICK-041's matching focused tests; do not create outages to repeat it.
No live Web-negative claim from configuration alone.

## Commands

Root only, ticket worktree, native PowerShell 7:
- az bicep build --file infra/main.bicep --stdout (no generated repo artifact).
- pwsh -NoProfile -File scripts/Test-AzureDeploymentPlan.ps1 -Mode Local
- dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj
  --configuration Release --no-build --filter
  "FullyQualifiedName~WorkerActivationReleaseContractTests|FullyQualifiedName~WorkerCompositionTests"

Root owns any required incremental build; reuse exact source-compatible OCR
adapter/recovery results, no stress/full-suite repetition. Author diff check only.
Before cloud writes, root follows pegasus-release including exact release
inputs, PreProvision validation and azd provision -e pegasus-prod --preview
--no-prompt. Do not run azd up or invent an independent deployment path.

## Failure and deviation rules

Stop on failed compiler/test/preview, unknown role/API support or scope mismatch.
Do not weaken checks or silently install/upgrade tools. Record the first failed
attempt and correction. Unknown provider work remains retained; never resubmit
to prove success. Roll back activation by removing the Worker endpoint through
the existing approved configuration route; leave the account/retained evidence
and local-auth disablement intact. No resource deletion or key fallback.
Exact release rollback preserves the approved prior artifact/activation state.

## Stop condition

Current assignment ends with reviewable research/files/plan/checklist, untaken
in Preparing. Execution handoff needs root's fresh dispatch. Author's later
implementation ends at PR/Review; root owns release and live acceptance.
Do not claim this ticket complete from IaC or local tests alone.
