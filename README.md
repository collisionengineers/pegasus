# CollisionSpike v2

CollisionSpike v2 is the clean-room case-management application for Collision Engineers. The repository contains its reviewed agent harness, .NET 10 modular monolith, local evaluation boundary, UI concepts, Azure replacement plan, and a provider-neutral intake proof with one contained QDOS extraction policy. That slice is development-only; the repository does not claim a completed QDOS workflow, support for a second principal's extraction rules, or an Azure deployment.

## Get started on Windows

```powershell
pwsh ./scripts/Invoke-Doctor.ps1
pwsh ./scripts/Invoke-RepoCheck.ps1
dotnet run --project ./src/CollisionSpike.Web
```

The current local route is `/Intake/Upload`; it is enabled by the checked-in Development launch profile and denied outside Development. QDOS is suggested only when the contained policy finds positive QDOS content evidence; it is never a default for other or ambiguous intake. See `docs/agent-notes/current-implementation-handoff.md` for the caller path, evidence, and limits, and `docs/plans/remaining-requirements.md` for the work still required for the first QDOS release.

For documentation, plans, and evidence, start with the [documentation map](docs/README.md). Architecture decisions are under [architecture](docs/architecture/README.md); the delivery horizon and blockers are under [plans](docs/plans/README.md), with the [feature maturity roadmap](docs/plans/feature-maturity-map.md) alongside it; supported workstation setup is in the [developer runbook](docs/runbooks/developer-workstation.md); and validation expectations are in [validation guidance](docs/agent-guidance/validation.md). Local genuine inputs remain ignored under `corpus/`.
