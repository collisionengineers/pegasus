# Pegasus

Pegasus is Collision Engineers' case-management and reporting application,
built with .NET 10. Product requirements and observed deployment are separate:
[the PRD](docs/prd/pegasus-product.md) owns intent;
[operations](docs/operations.md) records the last qualified runtime observation.

## Get started

Use PowerShell 7 on Windows or Linux, one platform per run. Follow
[local development](docs/runbook.md) for
prerequisites and initialization, then the existing lifecycle script:

```powershell
pwsh -NoProfile -File ./scripts/Initialize-LocalDevelopment.ps1
pwsh -NoProfile -File ./scripts/Invoke-LocalDevelopment.ps1 -Action Start
```

Use [verification](docs/runbook.md) for the checks selected
by the actual change. A documentation edit alone does not require a .NET build.

## Find the owner

- [Documentation index](docs/index.md): requirements, decisions and procedures.
- [Domain vocabulary](CONTEXT.md): reserved business meanings.
- [Architecture](docs/current-architecture.md): source structure and callers.
- [Runbook](docs/runbook.md): human-readable operational procedures.
- Kanmer: current work, ordering and delivery evidence.

`workspaces/` records retired source-import provenance; those imports are not
active application projects. `corpus/` is local, ignored and immutable.
Generated evaluation and build artifacts belong under ignored `artifacts/`.
