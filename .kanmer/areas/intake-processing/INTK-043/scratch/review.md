# Independent PR review — 2026-08-26

Reviewer: independent of the INTK-043 implementation.

## Changes

- `docs/adr/0032-near-real-time-durable-intake-triggering.md`: marks ADR-0032 superseded by ADR-0033.
- `docs/adr/0033-warm-unified-work-queue-for-five-second-intake.md`: records the warm unified queue decision and external-provider boundary.
- `docs/adr/README.md`: moves ADR-0032 to superseded and indexes ADR-0033.
- `docs/capabilities.md`: changes INT-33 from ten-second to five-second p95 and separates Box/provider delay.
- `docs/frd/frd-02-intake-and-source-identity.md`: defines the five-second Pegasus path and truthful provider attribution.
- `docs/prd/pegasus-product.md`: aligns the required quality target.
- `docs/runbook.md`: changes the incoming release contract from nine to seven Worker functions while retaining the currently deployed nine-function fact.
- `infra/modules/platform.bicep`: removes the external-work queue, poison queue, Web sender assignment and settings; routes the Worker to one queue; configures an always-ready entry.
- `scripts/Invoke-AzureDatabaseBootstrap.ps1`: updates caller-evidence wording only.
- `scripts/Invoke-ProductionSmoke.ps1`: changes live activation validation to the seven-function census.
- `scripts/Test-AzureDeploymentPlan.ps1`: changes source/compiled/smoke assertions to the seven-function census and adds an always-ready assertion.
- `src/Pegasus.Core/Intake/DurableIntake.cs`: adds low-cardinality activities for claim, artifact retention, source processing, and association/allocation.
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260819115323_UnidentifiedWork.cs`: updates a caller comment without changing schema.
- `src/Pegasus.Infrastructure/Transport/AzureQueueWorkEnqueuers.cs`: introduces the strict typed queue envelope and sends both existing work kinds to one client.
- `src/Pegasus.Web/Program.cs`: removes external queue configuration/client construction and injects the intake-work client into both publishers.
- `src/Pegasus.Worker/Functions/ExternalWorkFunctions.cs`: deletes the superseded external work and poison triggers.
- `src/Pegasus.Worker/IntakeFunctions.cs`: replaces separate intake triggers with typed unified normal and poison dispatchers.
- `src/Pegasus.Worker/WorkerAzureClientFactory.cs`: reduces production/development queue clients to one intake-work client.
- `src/Pegasus.Worker/WorkerDependencyInjection.cs`: gives both existing enqueuers that one queue client.
- `src/Pegasus.Worker/host.json`: retains Dependency and Exception telemetry outside adaptive sampling while lowering request volume.
- `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs`: covers both typed dispatcher branches, poison routing, and malformed input.
- `tests/Pegasus.ArchitectureTests/WorkerActivationReleaseContractTests.cs`: changes the function census and asserts the always-ready template.
- `tests/Pegasus.ArchitectureTests/WorkerAzureClientCompositionTests.cs`: changes production/development/provisioning assertions to one queue.
- `tests/Pegasus.ArchitectureTests/WorkerCompositionTests.cs`: proves the unified triggers construct in both profiles.
- `tests/Pegasus.IntegrationTests/ReadinessEndpointTests.cs`: removes the obsolete startup setting.

The post-implementation report accounts for every changed file and its stated rationale matches the diff. The plan deliberately disposed of unmeasured ONNX, MIME, retention-concurrency, EF, and Box optimizations instead of implementing speculative work. The simplification record is honest: it reuses both Core processors and publisher ports, removes the old transport/function path, and identifies the two small documentation corrections it applied.

## Governing-doc and plan checks

- FRD-02 remains one Core-owned, durable, fail-closed intake route; the transport does not duplicate identification, classification, extraction, allocation, or state policy.
- FRD-05 custody processing, durable claims, poison reconciliation, and recovery stay with the existing Core/Infrastructure owners.
- ADR-0033 is present and ADR-0032 is superseded consistently.
- The PR stays outside MAIL-013 Graph wake-up and INTK-001 UI-state scope.
- The deployed-state documents remain unchanged, correctly, because no deployment has happened.
- Open questions are fully resolved; no proposed review correction turns on a parked decision.

## Comments and disposition

1. **Blocking — invalid Flex always-ready scale-group name.** `infra/modules/platform.bicep` uses bare `UnifiedWorkFunction`. Microsoft Learn defines individually scaled functions outside the HTTP/Durable/blob groups as `function:<FUNCTION_NAME>`; this queue trigger therefore requires `function:UnifiedWorkFunction`. The architecture and deployment-plan tests currently lock the same invalid value, so their passing result does not prove the Azure contract. Disposition: filed [[PR-066]], which blocks INTK-043. Fix the template and its exact assertions, then rerun focused validation and required CI.
2. **Blocking — required documentation CI is red.** On unchanged head `ec39cc181ec4c7bc5c08e2a7ecbde0e23b1ee8b1`, `documentation` fails because `.grok/skills/kanmer-setup/SKILL.md` links to missing `../../../../docs/manual/greenfield.md`. The defect is already on `origin/dev` from commit `9061c4c6`, not introduced by INTK-043, but repository policy still forbids merging while required CI is red. Disposition: filed [[PR-065]], which blocks INTK-043. Repair it in its own scoped work and rerun PR #560.
3. **Non-blocking — remaining CI was still running at review decision.** Changes, local-development-scripts, and reference-data passed; infrastructure, unit, browser, and three SQL integration lanes were pending. Disposition: no merge consideration until a corrected head completes all required checks.

## Verdict

**Needs changes.** The diff substantially follows the ticket plan and report, but the central warm-capacity setting is not valid for an individually scaled queue function and required CI is not green. PR #560 was not merged and INTK-043 remains in Review. Re-review the corrected, unchanged replacement head after [[PR-066]] and [[PR-065]] land.
