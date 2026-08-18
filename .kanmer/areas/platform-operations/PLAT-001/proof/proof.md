# Proof — PLAT-001

*Evidence gathered on merged `dev` (commit `5ab3b773`, 2026-08-18) — PR #397 merged via merge commit.*

## What was verified

The merged result of PR #397 (Claude Design UI implementation) on `origin/dev` at commit `5ab3b773` (Merge pull request #397 from collisionengineers/task/claude-design-ui).

## Evidence

### Build

```
dotnet restore ./Pegasus.slnx --locked-mode
→ All projects up-to-date for restore.

dotnet build ./Pegasus.slnx --configuration Release --no-restore
→ Build succeeded. 0 Warning(s) 0 Error(s)
→ Time Elapsed 00:01:07
```

### Test suites

```
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build
→ Passed! - Failed: 0, Passed: 580, Skipped: 0, Total: 580

dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build
→ Passed! - Failed: 0, Passed: 96, Skipped: 0, Total: 96

dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
→ Passed! - Failed: 0, Passed: 504, Skipped: 0, Total: 504

dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2
→ Passed! - Failed: 0, Passed: 32, Skipped: 0, Total: 32
```

### Mark assets

```
ls src/Pegasus.Web/wwwroot/images/marks/*.png | wc -l
→ 10

Files: access.png, accounts.png, automation.png, checkmark.png, configuration.png,
       mailboxes.png, organisations.png, pegasus-lockup.png, principals.png, roles.png
```

### Invariant checks

- No diff under `Pegasus.Core`, `Pegasus.Infrastructure`, `workspaces/`, `corpus/` — verified on merged dev
- No inline `style` attributes in server markup — 0 found
- No `asp-for` in unbound assessment sections — 0 found
- No fabricated operator data in markup — 0 found
- `aria-current="page"` present on active rail route — verified by AccessibilityTests (axe suite)

## Visual proof

Visual proof (screenshots of the rail and representative screens) was not captured in this verify pass. The browser test suite (32 tests) drives the real running application through Playwright and confirms axe accessibility compliance and operator journey completion, including:
- All authenticated routes return 200 with no axe violations
- No inline style attributes on any route
- Single H1 per page
- Navigation starts at top of viewport (blank-band guard)
- Operations usable at constrained desktop and 200% zoom
- Metric state not communicated by colour alone

Screenshot capture from a local `DevelopmentOffline` run remains as a follow-up for complete visual evidence.

## Not covered

- Corpus tests (Category=Corpus) — not run; they require the local corpus and are not part of this ticket's scope
- Visual screenshots — the browser suite exercises the real rendered application; screenshot capture is a supplement, not a substitute
- Local `DevelopmentOffline` manual run — not performed; the browser suite covers the same routes through Playwright
