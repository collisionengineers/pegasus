# Validation ladder

Run the smallest applicable rung first and widen only when it passes:

1. Compile or parse the changed artifact.
2. Run focused unit or script tests for the changed rule.
3. Run the relevant integration boundary.
4. Run a genuine local corpus sample through the actual caller for intake/extraction behavior.
5. Run `pwsh ./scripts/Invoke-RepoCheck.ps1` before delivery.
6. Ask a separate reviewer to compare the result with the authoritative rule for material domain work.

Record what each check proves and what it does not. Bicep compilation proves syntax and type compatibility, not quota, permissions, availability, cost, or a successful deployment. A local Web integration test proves application composition, not Azure configuration. A corpus evaluation proves behavior on its sampled inputs, not every provider.
