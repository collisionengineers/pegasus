# Validation ladder

Run the smallest applicable rung first and widen only when it passes:

1. Compile or parse the changed artifact.
2. Run focused unit or script tests for the changed rule.
3. Run the relevant integration boundary.
4. Run a genuine local corpus sample through the actual caller for intake/extraction behavior.
5. Restore/build and run the focused/full .NET test projects directly before delivery.
6. Ask a separate reviewer to compare the result with the authoritative rule for material domain work.

Record what each check proves and what it does not. Bicep compilation proves syntax and type compatibility, not quota, permissions, availability, cost, or a successful deployment. A local Web integration test proves application composition, not Azure configuration. A corpus evaluation proves behavior on its sampled inputs, not every provider.

## Documentation validation

Read each changed document from its canonical owner, follow every added or
changed local link, and verify commands against the executable that owns them.
Documentation-only checks do not substitute for compilation, a real caller,
deployment, or operator acceptance. Do not add a repository-specific validator
or tool route to make prose appear authoritative.
