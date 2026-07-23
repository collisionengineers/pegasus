# Operations, cloud and security

These files are runbooks and snapshots for the predecessor estate. They must not be used to operate or change v2 or Azure. Current Azure changes still require explicit user approval and fresh inventory.

| File | Brief contents and original purpose | Current v2 comparison | Caution |
| --- | --- | --- | --- |
| [`README.md`](../docs/operations/README.md) | Index of old operational runbooks. | **Predecessor-specific.** | Navigation only. |
| [`alpha-testing.md`](../docs/operations/alpha-testing.md) | Old QDOS single-provider alpha bring-up and cutover sequence. | **QDOS alpha goal overlaps current scope.** | Commands, services, flags and acceptance state belong to the old app. |
| [`archive.md`](../docs/operations/archive.md) | Operating the old Box archive mirror, reconciliation and recovery. | **Box custody is planned, not implemented.** | “Archive” components and identifiers are not current v2. |
| [`cloud-inventory-2026-07-17.md`](../docs/operations/cloud-inventory-2026-07-17.md) | Dated Azure, Entra, Outlook and related resource inventory. | **Historical evidence only.** | It is stale by definition and must never authorise cloud changes. |
| [`live-facts.evidence.json`](../docs/operations/live-facts.evidence.json) | Machine-readable evidence behind the old live-facts registry. | **Predecessor-specific.** | Values are not current v2 deployment evidence. |
| [`live-environment.md`](../docs/operations/live-environment.md) | Old deployed resource names, URLs, gates and live-state summary. | **No current v2 deployment is proved by this file.** | Do not reuse endpoints, identities or state claims. |
| [`deployment.md`](../docs/operations/deployment.md) | Old deployment process, checks and rollback. | **Architecture conflict.** | v2 uses .NET, Bicep and azd; no deployment was authorised here. |
| [`database.md`](../docs/operations/database.md) | Old PostgreSQL migration, access, backup and restore operations. | **Conflicts with Azure SQL/EF Core direction.** | Do not run these commands against any environment. |
| [`identity-and-access.md`](../docs/operations/identity-and-access.md) | Old Entra/MSAL roles, access and authorization model. | **Conflicts with v2 staff identity.** | Cloud identities may still be used for Azure resources, not staff sign-in. |
| [`secrets.md`](../docs/operations/secrets.md) | Old secret names, locations and rotation steps. | **Review principles only.** | Secret names and ownership may be stale; never copy values or rotate from this guide. |
| [`feature-gates.md`](../docs/operations/feature-gates.md) | Catalogue of old runtime and deployment switches. | **Predecessor-specific.** | v2 must not recreate dormant feature-gate machinery. |
| [`diagnostics.md`](../docs/operations/diagnostics.md) | Old logs, KQL, failure triage and service diagnostics. | **Conceptual observability input only.** | Queries and resource names target the old estate. |
| [`operator-actions.md`](../docs/operations/operator-actions.md) | Generated list of operator-owned actions from old tickets. | **Historical delivery state.** | It is not a v2 task list. |
| [`helper-app-consolidation-assessment.md`](../docs/operations/helper-app-consolidation-assessment.md) | Assessment of combining old helper applications and storage. | **Predecessor-specific architecture.** | v2 already has a different approved boundary. |
| [`vehicle-data-rollout.md`](../docs/operations/vehicle-data-rollout.md) | Old vehicle provider rollout, flags, checks and fallback. | **Vehicle lookup planned, no v2 adapter exists.** | Provider/licence/live assumptions require fresh approval. |
| [`data-subject-rights.md`](../docs/operations/data-subject-rights.md) | Old process for access, correction, export and erasure requests. | **Potential future governance input.** | Dedicated first-MVP workflows were not required; case deletion remains prohibited. |
| [`delete-case-image.md`](../docs/operations/delete-case-image.md) | Old deploy-and-run procedure for removing a case image. | **Conflicts if treated as case deletion authority.** v2 permits audited logical evidence removal only on an open/reopened case and retains versions. | Never run the predecessor procedure. |

## What can be extracted safely

- Failure modes, monitoring questions, restore expectations and third-party dependency names can become prompts for a future v2 runbook.
- Exact resource names, URLs, role assignments, secrets, flags, deployment commands and old “live” results cannot be promoted.
- Any Azure or Microsoft 365 operation still needs current read-only inspection followed by explicit approval for the exact mutation.
