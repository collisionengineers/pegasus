# Baseline before wave 1 (origin/dev = origin/main = 783b4b88, 2026-08-28)

Orchestrator ran the canonical commands on the main checkout (Windows, PowerShell 7):

| Step | Result |
| --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | exit 0 |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | exit 0 |
| `dotnet test … --filter "Category!=Corpus&Category!=Browser"` | Core 1042/1043 (1 failed), Architecture 100/100, Integration 956/958 (2 skipped), 17 m 17 s |

Pre-existing failure, not caused by this programme: `Pegasus.Core.Tests.Qdos.EvaBundleContractTests.TheRetainedSamplesAreTheSourceOfTheNewlineConvention` — a `\r` in the retained sample on this CRLF checkout (`core.autocrlf`); CI passes it. Every later wave compares against this baseline; that one test is the only accepted red.
