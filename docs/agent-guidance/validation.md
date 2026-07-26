# Validation ladder

Run the smallest applicable rung first and widen only when it passes:

1. Compile or parse the changed artifact.
2. Run focused unit or script tests for the changed rule.
3. Run the relevant integration boundary.
4. Run a genuine local corpus sample through the actual caller for intake/extraction behavior.
5. Run `pwsh ./scripts/Invoke-RepoCheck.ps1` before delivery.
6. Ask a separate reviewer to compare the result with the authoritative rule for material domain work.

Record what each check proves and what it does not. Bicep compilation proves syntax and type compatibility, not quota, permissions, availability, cost, or a successful deployment. A local Web integration test proves application composition, not Azure configuration. A corpus evaluation proves behavior on its sampled inputs, not every provider.

## Documentation validation

Run the focused documentation guard before the full repository check:

```powershell
pwsh -NoProfile -File ./scripts/Test-Documentation.ps1
pwsh -NoProfile -File ./scripts/Invoke-RepoCheck.ps1
```

[`Test-Documentation.ps1`](../../scripts/Test-Documentation.ps1) checks
first-party local links and anchors, exact path casing, required authority
routes, the 213 literal feature triples, plan status and supersession metadata,
the public skill-entry catalogue, stray patch-marker headings, and the static
root/docs instruction chain and byte budget. It also exercises seven temporary
negative fixtures for a broken link, duplicate ID, missing ID, qualifier drift,
an invalid active route, a malformed patch-marker heading, and a missing skill
route.

To observe a guard rejecting a deliberate fixture directly:

```powershell
pwsh -NoProfile -File ./scripts/Test-Documentation.ps1 -NegativeFixture QualifierDrift
```

That command is expected to fail with `DOC-FEATURE-DRIFT`. The fixtures contain
only controlled Markdown protocol data, are created under a uniquely named
temporary directory, and are removed in `finally`; they are not operational
evidence.

The real repository caller is
[`Invoke-RepoCheck.ps1`](../../scripts/Invoke-RepoCheck.ps1), which runs the
documentation guard before restore, build, and test work. A standalone green
guard proves only its static documentation contracts. A green full check proves
repository consistency, not semantic authority, zero-loss incorporation,
current application behavior, deployment, or operator acceptance.

Static discovery of root `AGENTS.md` followed by `docs/AGENTS.md` is the
deterministic instruction gate. A non-interactive ephemeral Codex run in a
read-only sandbox may corroborate discovery, but CLI availability, output, or
authentication cannot replace the static result.
