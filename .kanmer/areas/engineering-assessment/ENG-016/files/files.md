# Files — ENG-016 final amended PR

## Product and governing documents
- `docs/operator-notes.md`, `docs/frd/frd-07-eva-and-external-engineering-handoff.md`, `docs/current-architecture.md`, `docs/capabilities.md`: one Review/Export rule and three engineering routes.
- `docs/adr/0021-*.md`, `docs/adr/0031-*.md`, `docs/adr/README.md`, `docs/frd/frd-11-*.md`, `docs/design/README.md`, `docs/operations.md`: supersede the deleted EVA MCP tool promise and reconcile current citations.

## Removed duplicate hand-off
- Core EVA contracts/policy/mapping and case projection files; Infrastructure DI, proxy, entities, model configuration, context and store; Web EVA pages, Vehicle handler, workflow partial and MCP handlers: remove the second generate/download/status act while retaining the one Export port and first-send proxy.
- `20260824123336_DropEvaHandoffTables*`, snapshot, bootstrap and platform files: remove the three obsolete tables/columns/grants under ADR-0030; comments state roll-forward recovery.

## Final Export and readiness
- `EvaHandoffStore.cs`, Export/Details page models/views and lifecycle/completeness configuration files: server-side Review gate, operation-key history, first proxy, Content-Digest, batch image reads, unconditional completeness and operator demotion notice.
- Administration policy entity/store/model/UI plus `20260825001401_RemoveWorkflowCompletenessWaivers*`: remove the two obsolete completeness waiver settings; retain staff-review settings.

## Tests
- Core readiness/EVA/QDOS tests cover suggestions, optional/default fields and unconditional completeness.
- Architecture test pins one Core owner and the batch read.
- Integration administration, workflow, export, MCP, browser, migration, composition and support fixtures cover removed surfaces, replay concurrency, batch content and the smaller configuration contract.
- `EvaHandoffPersistenceTests.cs` is deleted with the duplicate hand-off owner.

Every path in `gh pr diff 539 --name-only` belongs to one group above. Reference EVA sample files are not part of the PR or this work.
