# CollisionSpike v2

CollisionSpike v2 is the clean-room case-management application for Collision Engineers. The repository currently contains its reviewed agent harness, executable .NET 10 modular-monolith scaffold, local evaluation boundary, UI concepts, and Azure replacement plan. It does not yet claim a completed intake workflow or an Azure deployment.

## Get started on Windows

```powershell
pwsh ./scripts/Invoke-Doctor.ps1
pwsh ./scripts/Invoke-RepoCheck.ps1
dotnet run --project ./src/CollisionSpike.Web
```

The first feature should use `$collisionspike-qdos-vertical-slice` to take genuine local QDOS material through the real Worker/Core/Web path.

Read [AGENTS.md](AGENTS.md) before changing the repository. Architecture decisions are under `docs/architecture/decisions/`; live Azure replacement evidence is under `docs/azure/`; local genuine inputs remain ignored under `corpus/`.
