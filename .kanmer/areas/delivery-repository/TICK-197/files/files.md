# Files — TICK-197

## Where the change lands

| Path | Why |
|---|---|
| `.github/workflows/ci.yml` | Add infrastructure path detection and a credential-free validation job that runs the existing Local mode. Preserve least-privilege `contents: read` and avoid any Azure authentication or deployment action. |
| `scripts/Test-AzureDeploymentPlan.ps1` | Correct the stale Web replica assertion so the existing validator matches the committed one-warm-replica infrastructure before it becomes a required CI lane. No live-mode behavior should change. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `.kanmer/groups/EPIC-001/context.md` | The group excludes Web/UI, browser/snapshot, design, and Stitch surfaces and requires a fresh overlap check before implementation. |
| `docs/engineering.md` | Static/build/architecture evidence requires Bicep compilation; this resolves the ticket toward a lane and limits what its green result proves. |
| `docs/runbook.md` | Bicep compilation proves syntax/type consistency only; GitHub Actions deployment is not planned; credentials and live operations remain separately approved. |
| `docs/operations.md` | Current state distinguishes local Bicep compile/configuration checks from approved preflight, deployment, health, and rollback evidence. |
| `infra/main.bicep` and `infra/modules/platform.bicep` | These are the compiled deployment entry point and module; the platform module establishes the current one-to-one replica invariant. |
| `azure.yaml` and `infra/main.parameters.json` | The existing validator reads these configuration inputs and asserts their fail-closed mappings. |
| `scripts/Invoke-ProductionSmoke.ps1` | Local validation reads this script text for production safety invariants but must never execute its live Azure path. |
| `scripts/Invoke-Doctor.ps1` and `scripts/PegasusPlatform.ps1` | Existing tool discovery/install guidance for Azure CLI/Bicep; do not introduce a second version policy in the workflow without reconciling these owners. |

## Ripple effects

- Pull requests that modify infrastructure inputs or their validator will gain a required executable signal rather than relying on local/release-time execution.
- Workflow path detection must include its own definition and every source consumed by Local mode, or relevant changes can bypass the lane.
- A failing Bicep compile or invariant assertion will block the PR but will not prove or alter Azure state.
- TICK-200 may later optimize wall-clock time; this ticket should keep the lane narrow and path-scoped so it does not impose work on unrelated UI-only changes.
- Verification should include a successful Local run after the stale assertion is repaired and a controlled negative fixture/mutation proving the job fails on invalid Bicep or a broken invariant without touching tracked source.

## Out of scope

- `src/Pegasus.Web/**`, UI-focused tests, `design/**`, and `.stitch/**`.
- Azure login, OIDC, credentials, subscriptions, resource groups, `azd`, what-if against a live target, provisioning, deployment, smoke tests, or any other cloud read/write.
- Changes to Bicep topology or production capacity; the ticket validates the committed design rather than redesigning it.
- Broad documentation cleanup or replacement of retired `NOW.md` citations owned by KANMER-001/KANMER-002.
- CI wall-clock optimization beyond keeping this lane path-scoped; TICK-200 owns wider optimization.
