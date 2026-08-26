# Plan — warm unified intake and custody route

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: one Core-owned, durable, fail-closed route for e-mail and upload.
- `docs/frd/frd-05-documents-extraction-and-custody.md`: custody remains complete, immutable and idempotent.
- ADR-0033 supersedes ADR-0032's scale-to-zero decision for the critical work path.

## Scope

This ticket removes the measured queue-function cold-start path. It does not claim to solve mailbox-provider delivery, stale UI state, image-model startup, or Box provider time; those retain their own evidence and tickets.

## Approach

Use one typed `intake-work` queue and one warm queue-trigger function. It dispatches to the existing intake or external-work Core processor, so durable records, claims, recovery, and business policy remain unchanged. The normal custody hand-off is another message on that same warm function, not an external queue or a new service.

## Steps

1. Add low-cardinality timing spans around durable intake claim, retained source processing, and allocation; retain dependency and exception telemetry.
2. Replace the two queue transports and two queue functions with the typed unified route; remove obsolete queue RBAC, settings, poison queue, function census, and local test configuration.
3. Configure one 2 GiB always-ready `UnifiedWorkFunction` in the Flex Consumption Bicep template, retaining burst scale-out.
4. Update the PRD/FRD/capability target and create ADR-0033. Leave as-built/deployed documentation unchanged until deployment proves it.
5. Verify the worker composition, deployment plan, Bicep compilation, Core tests, and configuration-startup tests.
6. Measure a deployed cohort before considering ONNX preload, concurrency, EF, MIME, or Box changes. Route mailbox wake-up to [[MAIL-013]] and truthful sender/state projection to [[INTK-001]].

## Acceptance evidence

- Both publishers use the same typed queue; the Worker routes each type to its existing owning Core processor.
- The critical queue consumer has one 2 GiB always-ready instance; the old external queue/function/settings no longer exist.
- P95 stage attribution is available without source content.
- The five-second target remains a deployment measurement, not a local-build claim.

## Risks

- Graph and Box can exceed the total target; telemetry must attribute that delay rather than hide it.
- A combined message without an explicit kind would confuse two GUID identifiers; parsing is strict and fails closed.

## Simplification pass — 2026-08-26

- Reused the two existing enqueue ports and two existing Core processors; no new business service, queue client abstraction, or retry policy was introduced.
- Kept custody as a separate durable message rather than inlining it into intake. This preserves its existing claim/recovery contract while removing the measured cold external-worker hop.
- Did not preload ONNX or parallelize retention/Box uploads: no new trace proves either is the current bottleneck, and both would add startup/concurrency risk.
- Replaced obsolete pre-release queue/function/configuration paths instead of retaining compatibility handling for bare GUID messages.

## Simplification follow-up — 2026-08-26

Independent reuse, simplification, efficiency and altitude review found the unified typed envelope, one queue and one always-ready caller proportionate. Applied two stale-reference corrections: the Worker comment now names the unified queue, and the runbook's incoming activation/smoke/rollback contract now names the exact seven functions. Retained `docs/current-architecture.md`'s nine-function description because it is the truthful deployed Release 32 snapshot until deployment; historical operations evidence is unchanged.

## Verification refresh — 2026-08-26

After merging current `origin/dev` into the ticket branch:

- `dotnet restore Pegasus.slnx --locked-mode` — PASS.
- `dotnet build Pegasus.slnx --configuration Release --no-restore --disable-build-servers` — PASS, 0 errors; the existing Functions metadata generator emitted 456 missing-optional-assembly warnings.
- Core tests — PASS, 999/999.
- Architecture tests — PASS, 99/99.
- Configuration/startup integration slice — PASS, 16/16.
- `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` — PASS, including Bicep compilation and the exact seven-function fail-closed/always-ready contract.
- `git diff --check` — PASS.
- An additional full IntegrationTests run was stopped after more than four minutes with no progress output. This was an extra check, not a plan or checklist gate; the required startup slice above passed. CI remains the full-suite authority before merge.
