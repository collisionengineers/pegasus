# Files — PLAT-065

## Where the change lands

| Path | Why |
| --- | --- |
| infra/modules/platform.bicep | One FormRecognizer/S0 account, disabled local authentication, custom subdomain, one resource-scoped Worker role and Worker endpoint. Preserve all existing release inputs/resources. |
| infra/main.bicep | Propagate non-secret account ID and endpoint outputs for release readback; no new parameter. |
| tests/Pegasus.ArchitectureTests/WorkerActivationReleaseContractTests.cs | Extend existing template contract tests and isolated Local negative cases for the narrowly authorized OCR exception; no new test framework. |
| scripts/Test-AzureDeploymentPlan.ps1 | Permit only the planned FormRecognizer/S0 keyless account through the existing deferred-service guard; retain other service exclusions and Worker census. |
| docs/runbook.md | Existing OCR activation/recovery paragraph gets concrete endpoint, RBAC and rollback procedure. |
| docs/current-architecture.md | Distinguish implemented source from activated caller; refresh after release. |
| docs/operations.md | Record dated exact target, price/role, deployment identity and honest live results. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| docs/adr/0040-qualified-document-intelligence-ocr.md | Accepted model/source/auth boundary; no new OCR orchestration. |
| docs/frd/frd-05-documents-extraction-and-custody.md | Qualified pages only; retained source and fail-closed output. |
| docs/frd/frd-07-eva-and-external-engineering-handoff.md | PDF import and pending OCR are distinct; OCR never accepts Current. |
| src/Pegasus.Worker/WorkerDependencyInjection.cs | Real Production composition already consumes endpoint. |
| src/Pegasus.Worker/WorkerAzureClientFactory.cs | Existing Worker identity-only credential and offline rejection. |
| src/Pegasus.Infrastructure/Intake/AzureDocumentIntelligenceOcr.cs | Existing page-restricted REST adapter/API/credential scope. |
| tests/Pegasus.ArchitectureTests/WorkerCompositionTests.cs | Existing production/offline missing/invalid endpoint assertions. |
| tests/Pegasus.IntegrationTests/AzureDocumentIntelligenceOcrTests.cs | Existing deterministic HTTP/provider failure tests; do not recreate them. |
| tests/Pegasus.IntegrationTests/OcrIntakeRecoveryTests.cs | TICK-041 source-context and retained-result/restart evidence. |
| scripts/Test-AzureDeploymentPlan.ps1 | Existing Local/PreProvision release checks and exact Worker activation census. |
| infra/main.parameters.json | Current deployment/environment parameters; no OCR secret or duplicated endpoint input needed. |
| .agents/skills/pegasus-release/SKILL.md | Exact-SHA artifact, migration, preview and authorized-terminal release order. |

## Ripple effects

Endpoint activates existing Worker composition when its deployed source
contains TICK-041. Release outputs identify the target but do not prove OCR.
No schema, package, generated UI artifact or build-layout change.

## Out of scope

All application source, OCR thresholds, TICK-085 parser/UI/MCP work, new
identities/roles/runtimes/modules, private networking, mail/Box changes, bulk
corpus transmission, budget increase, stress/soak tests and unrelated estate
drift. Source originals remain immutable; only approved non-corpus supplied
canary content may be sent through the existing authorized production caller.
