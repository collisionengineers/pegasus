# Change files

| Area | Purpose and risk |
| --- | --- |
| `src/Pegasus.Infrastructure/Persistence/Migrations/` | Append-only grant reconciliation; must preserve unrelated grants and DELETE denials. |
| `src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs` | Narrow duplicate suppression without breaking concurrent idempotency. |
| `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` | Exhaustive Web/Worker role contract. |
| Focused vehicle/image integration tests | Exercise actual runtime-role writes and failure propagation. |
| `docs/current-architecture.md`, `docs/operations.md` | Refresh only after approved deployment. |

# Context files

| File | Why read it |
| --- | --- |
| `docs/frd/frd-05-documents-extraction-and-custody.md` | Image custody lifecycle behaviour. |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Vehicle lookup evidence behaviour. |
| `docs/adr/0007-direct-terminal-azure-deployment.md` | Deployment boundary and approval. |
| Existing runtime-role migrations | Established grant/deny convention. |

# Out of scope

No schema columns, DELETE authority, unrelated role grants, alert-query changes, or unapproved production writes.
