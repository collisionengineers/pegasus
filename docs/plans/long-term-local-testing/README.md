# Long-term local testing

Status: **Ready evidence-profile plan — implementation remains caller-scoped**

Current owners: [plans index](../README.md), [validation guidance](../../agent-guidance/validation.md), and the feature-specific caller contracts. V0 includes local working-copy EML evaluation; later profiles activate only with their allocated real caller.

## Finish line

CollisionSpike has a reproducible Windows-native test environment that proves each delivered capability through its real Web or Worker entry point, separates local evidence from approved live-service evidence, and can expand with named deferred capabilities without introducing dormant services. The owned requirements and evidence matrix are in [Local testing and service evidence](platform/local-testing.md).

## Authority and boundaries

- Apply the repository [source-of-truth order](../../agent-guidance/source-of-truth.md), the settled [project questionnaire](../../../PROJECT_DISCOVERY_QUESTIONNAIRE.md), the [remaining first-release requirements](../remaining-requirements.md), and accepted [Azure architecture](../../architecture/decisions/ADR-0002-dotnet-modular-monolith-on-azure.md).
- Keep unresolved business rules in the canonical [open-decision register](../open-decisions.md); local tests must not invent normative behavior for a withheld rule.
- Preserve the material meaning of `docs/operator-notes/`; keep `corpus/` read-only. Corpus evidence remains ignored and local, and no corpus input or derivative may be uploaded to Azure, CI, or a vendor without fresh explicit permission.
- Local emulators and mocks do not prove managed identity, RBAC, vendor behavior, cloud durability, scaling, alert delivery, recovery objectives, or operator acceptance.
- No cloud mutation, billed service call, mailbox access, Box change, deployment, or credential change is authorised by this plan.
- Service Bus, Event Hubs, Cosmos DB, Redis, PostgreSQL, Azure Files, ADLS, and local SMTP infrastructure are excluded because they do not belong to the approved v2 target architecture.

## Stable invariants

- Web and Worker remain thin callers of the same Core-owned business behavior; a test-only or registered-only path is not a caller.
- SQL stores structured state and the outbox, Storage queues carry identifiers rather than file content, transient Blob content is deleted only after Box custody is confirmed, and external side effects remain idempotent.
- Every local run uses isolated databases, ports, storage state, and ignored artifact directories; cleanup acts only on resources owned by that run.
- Evidence uses the literal states `Planned`, `Implemented`, `Called`, `Locally verified`, `Deployed`, `Live verified`, and `Accepted`.

## Delivery order

| Order | Area or task | Requires | Real or intended caller | Unlocks |
|---|---|---|---|---|
| 1 | [Reproducible Windows-native test environment](platform/local-testing.md#reproducible-windows-native-test-environment) | Existing repository checks and accepted Azure architecture | Developer and Windows CI invocation; product behavior is exercised through the Web caller and later the Worker host | Pinned Azurite, LocalDB, Functions, browser, contract, performance, and security profiles |
| 2 | [Caller-backed local and live evidence gates](platform/local-testing.md#caller-backed-local-and-live-evidence-gates) | Test environment profiles and the real feature caller being present | `/Intake/Upload` today; authenticated Web/API/MCP and Worker triggers as delivered | Honest release evidence for the full QDOS workflow and later activated capabilities |

## Ownership and merge hotspots

| Boundary | Single owner | Consumers | Coordination rule |
|---|---|---|---|
| Test-tool manifest and PowerShell orchestration | Repository test infrastructure | Developers and Windows CI | One pinned tool source and one process-lifecycle owner; no parallel startup scripts |
| Test traits and repository gates | Repository check owner | Core, Infrastructure, Web, Worker, browser and corpus suites | Add a required gate only with the real caller or invariant it proves; required skipped tests fail |
| Storage/Worker composition | Worker composition root and Infrastructure storage adapters | Web outbox and Core use cases | Deliver Azurite evidence in the same slice as the real Blob/Queue/trigger path |
| External-service contract fixtures | Owning Infrastructure adapter | Web and Worker callers | One deterministic fake per existing port; live evidence remains a separate approved gate |

## Approval boundaries

| Action | Exact scope required | Approval/evidence required |
|---|---|---|
| Use a live Azure service | Named subscription, resource group, resource and operation | Explicit mutation/cost approval plus current Azure inventory and least-privilege identity |
| Read or change an Outlook mailbox | Named tenant, application, mailbox and folder | Exchange Application RBAC approval and a negative scope test before the Graph call |
| Use Box or another vendor sandbox | Named enterprise/account, folder/project and operation | Credential/data approval and controlled non-corpus input |
| Send a document to OCR, vision, AI, or another allocated external processor | Named service, region, model and input class | Data/licence/cost approval; corpus material remains prohibited unless separately authorised |
| Run deployment, restore, failover or retirement evidence | Exact non-production environment and recoverable target | Explicit operation approval, fresh inventory, rollback path and retained source data |

Malware scanning is a permanent `Never` boundary. This plan creates no scanner profile, fixture, port, quarantine state, activation gate, or release claim.

## Evidence language

Use `Planned`, `Implemented`, `Called`, `Locally verified`, `Deployed`, `Live verified`, and `Accepted` literally. Record command results and limitations in the owning task, not in this index.

## Integrated acceptance journey

An authenticated operator or authorised external caller submits genuine-shaped work through the delivered entry point; Core applies one intake and workflow policy; SQL commits source identity, state, permanent action history and outbox atomically; a real Functions trigger consumes an identifier through Azurite locally and Azure Storage in the approved live gate; adapters preserve provenance and handle bounded retries; the operator sees the persisted result; and recovery, telemetry and duplicate-delivery evidence demonstrate that no second case, reference or external side effect is created. Local completion does not replace the live-service and operator-acceptance boundaries in [the evidence matrix](platform/local-testing.md#local-and-live-service-matrix).

## Plan maintenance

Reconcile this pack whenever authoritative requirements, accepted ADRs, production callers, external contracts, supported developer platforms, or evidence boundaries change. Add a tool or profile only with its real caller or named release invariant, remove replaced test infrastructure in the same slice, and never turn this plan into a mutable status ledger.
