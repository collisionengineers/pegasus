# Architecture

## System context

Collision Engineers staff use an ASP.NET Core Razor Pages Web application.
Future accepted provider calls and staff MCP calls enter through separate Web
boundaries. A .NET isolated Azure Functions Worker is the intended mailbox and
background composition root. Both entry points call shared Core policy through
Infrastructure adapters. Outlook, Box, DVLA/DVSA, EVA, Azure SQL, transient
Azure Storage, and Azure observability remain external systems with their named
authority boundaries.

## Components and ownership

| Component | Ownership |
| --- | --- |
| `src/CollisionSpike.Core/` | business use cases, invariants, models, and ports; depends on no Web, Worker, EF, Azure, Graph, or Box implementation |
| `src/CollisionSpike.Infrastructure/` | EF persistence and source/artifact adapters implementing Core ports; depends on Core |
| `src/CollisionSpike.Core/ReferenceData/` | provider-domain package validation, deterministic candidate semantics, and the catalog port; contains no workbook or EF implementation |
| `src/CollisionSpike.Web/` | Razor Pages/HTTP composition root, request translation, configuration, route gates, health endpoints |
| `src/CollisionSpike.Worker/` | isolated Functions composition root; currently telemetry host only, with no trigger or Core caller |

This four-project modular monolith is the approved production boundary. A new
project, runtime, store, migration stream, deployment unit, or top-level
application boundary requires an accepted ADR proving these owners cannot carry
the change.

## Entry points and callers

- `POST /Intake/Upload` in Development is the sole proven mutating product entry point. Its PageModel calls Core `ProcessIntake`, which uses the source reader, contained QDOS extraction policy, local artifact store, and EF receipt store.
- `/`, `/Intake/Queue`, and `/Intake/Review` query persisted receipt/draft state; the review download handler calls `IIntakeArtifactStore`.
- `/health/live` and `/health/ready` are technical endpoints; readiness calls the registered database health check.
- The Worker `Program.cs` builds and runs a Functions host but has no trigger, input, or Core call. Registration is not caller evidence.
- Graph mailbox intake, Box writes, provider API, staff MCP, vehicle lookup/recognition, EVA export, case lifecycle, and deployed Azure callers are planned or absent.

Current caller details and dated limits remain in the
[implementation handoff](agent-notes/current-implementation-handoff.md).

## Data and integrations

- DevelopmentOffline uses SQL Server Express LocalDB and the same committed migration stream as SQL Server/Azure SQL; ignored local files hold current source bytes until the local custody adapter replaces that proof.
- Azure SQL is the intended deployed application store for case workflow, identity, permanent action history, configuration, and source/file relationships. No live v2 migration has been applied.
- Box is the intended long-term original-file owner. Local artifacts and transient Blob/queues are not Box custody.
- Outlook owns mailbox content and exact sent-message evidence; the application owns accepted classifications and associations.
- EVA remains authoritative for named Engineer assignment and downstream engineering until an accepted replacement slice.
- Secrets use managed identity/RBAC where supported and Infisical or Key Vault only for unavoidable third-party credentials.
- Provider-domain reference evidence is a versioned, cumulative SQL snapshot
  seeded by the committed migration from one validated embedded package. A
  stored suffix is candidate evidence only: it activates no route, resolves no
  principal by itself, creates no inspection location/default, and maps no
  Case ID.

## Rule and configuration ownership

- Core owns intake decisions, route-policy selection, provider/type/case
  determinations, case/reference rules, lifecycle, matching/classification, and
  later shared UI/Worker/API/MCP business actions. Direct-provider policies and
  intermediary policies are separate code-versioned owners: either may resolve
  the same provider, but each applies only to its own message shape and evidence.
- Shared intake code normalizes the transport, reconstructs an original sender
  where staff forwarding is proved, extracts attachments/body/subject, invokes
  exactly one applicable route policy, and records its evidence and version. It
  does not impose a universal case-matching precedence.
- Core validates exact provider-domain package tuples and owns deterministic
  `Found`, `Unknown`, `Ambiguous`, `InvalidSuffix`, `PackageNotFound`, and
  `PackageRejected` results. Infrastructure implements the catalog with one
  bounded EF query against the requested immutable version; Web and Worker have
  no direct workbook/package parser and no active caller of this catalog yet.
- Web/Worker adapters translate transport and configuration; they do not copy business policy.
- EF migrations under Infrastructure own application schema evolution. Normal Web/Worker startup never applies migrations; the explicit Development command owns LocalDB migration and a release-owned bundle/operation will own deployed migration.
- `src/CollisionSpike.Web/Program.cs` owns current Web composition, `DevelopmentOffline` isolation, and database provider selection; Development configuration uses LocalDB and an ignored local artifact root.
- `infra/` and `.azure/deployment-plan.md` own target infrastructure/release design. They do not prove a live deployment.

## Source roles and generated material

| Path | Role | Canonical source/generator | Consumer |
| --- | --- | --- | --- |
| `src/CollisionSpike.Infrastructure/Persistence/Migrations/` | live migration source | EF model and reviewed migrations | local/SQL Server schema apply procedures |
| `docs/reference/workproviders-and-repairers/initial.xlsx` | immutable v1 provider-domain source evidence | owner-supplied workbook | offline authoring command only |
| `scripts/Build-ProviderReferenceData.ps1` and `scripts/reference_data/build_provider_reference_data.py` | offline authoring tool | reviewed standard-library script and source contract | immutable package generation/verification only; never application runtime |
| `src/CollisionSpike.Infrastructure/Persistence/ReferenceData/provider-domains.v1.json` | canonical immutable v1 provider-domain package | approved workbook through the authoring tool | embedded build resource and reviewed seed migration |
| `artifacts/bicep/main.json` | ignored generated output | `az bicep build --file infra/main.bicep` | compile evidence only |
| `artifacts/test-results/` | ignored generated evidence | owning .NET test projects | local review/diagnosis |
| `artifacts/local-development/` and LocalDB databases | ignored Development state | explicit migration command and real local callers | local review only; not production custody |
| `docs/reference/` | preserved supplied evidence | not generated | planning/evaluation after authority reconciliation |
| `design/references/mockups/` | approved comparison rasters | linked candidate-direction sources | direction selection only; not runtime/requirements |

## Failure and recovery

Source limits, incomplete processing, identity ambiguity, unsupported formats,
integrity conflict, and persistence/custody failures fail closed before case or
reference creation. Transient work retries only within named bounds; terminal
failures remain visible. Local content-addressed bytes can outlive a failed SQL
write and must not be mistaken for accepted custody.

Production recovery is forward-oriented: retain prior immutable application
packages, apply explicit migrations before application deployment, use health
and smoke evidence, and restore data through the accepted backup/recovery path.
Schema recovery is not an automatic down-migration. The four-hour restoration
and 15-minute recovery-point outcomes remain unproved acceptance gates.

## Deployment topology

The intended environments are isolated local development, one shared Azure
development/integration environment, and production. Target Bicep describes a
.NET 10 Web App, .NET 10 isolated Functions Worker, Azure SQL, Storage,
Key Vault, Application Insights/Log Analytics, and managed identities. The
release owner uses an authorized Windows terminal and committed Bicep through
`azd`; GitHub Actions deployment is not planned.

The route is not runnable: immutable packages, migration bundle, identity/Entra
resolution, provenance/hashes, and remote-build removal remain gaps. No Azure
resource is provisioned or changed by repository onboarding.

## Architecture boundaries

- No duplicated rule engines, dormant integrations, generic services, speculative abstractions, or compatibility shims for unreleased behavior.
- No case deletion, reference reuse, principal mutation after allocation, or second meaning for Audit/Triage/Needs sorting/Blocked intake.
- Deferred capabilities retain only necessary stable identities and ports; they add no caller, resource, credential, route, schema, flag, or UI placeholder before activation.
- The predecessor, local corpus, references, plans, tests, and registration each have limited evidence roles and never replace an actual caller or accepted authority.

Detailed accepted decisions 0001–0009 remain in the
[legacy decision index](architecture/README.md); new durable decisions use
[docs/decisions](decisions/README.md).
