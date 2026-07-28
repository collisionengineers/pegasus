# 0003 — Unified .NET 8 stack

## Status

Accepted

## Context

ADR 0002 requires one shared engine with thin clients. ADR 0001 requires that engine to run
both on a clean Windows desktop and in a Linux container. The desktop client must be a modern,
native-feeling Windows application; the cloud client must be a small web service in a
container. The question is which technology stack can satisfy all of this without splitting
the codebase across languages or runtimes.

## Decision

Standardise on a single **.NET 8** stack for the whole solution, `CollisionRenderer.sln`:

- `CollisionRenderer.Core` — `net8.0` class library, the shared engine, with no Windows-only
  dependencies so it also runs in a Linux container.
- `CollisionRenderer.Cli` — `net8.0` console application (assembly `collisionrenderer`).
- `CollisionRenderer.Api` — `net8.0` ASP.NET Core minimal API.
- `CollisionRenderer.Gui` — `net8.0-windows`, WinUI 3 / Windows App SDK desktop app with a
  WebView2 preview.
- `tests/CollisionRenderer.Core.Tests` — xUnit, including real-Chromium integration renders.

Common build settings live in `Directory.Build.props` (nullable enabled, implicit usings,
product metadata); per-project `TargetFramework` distinguishes the WinUI host (`net8.0-windows`)
from the framework-agnostic Core/CLI/API. The repository is built with the .NET 10 SDK while
targeting `net8.0`.

## Consequences

- One language and runtime across Core, CLI, GUI and API, which is what makes true feature
  parity (ADR 0002) practical: the GUI hosts Core in-process and the API wraps the same library.
- A first-class native Windows desktop app (WinUI 3) *and* a Linux cloud container come from
  one codebase, because only the GUI targets `net8.0-windows`.
- The team needs .NET, WinUI 3 / Windows App SDK and ASP.NET Core skills, but only one
  ecosystem overall rather than, say, Python plus C# plus a separate desktop toolkit.
- Pins the solution to the .NET release cadence; targeting `net8.0` (LTS) keeps that stable.

## Alternatives considered

- **Python (continuing the `report-renderer` lineage):** good for the CSS rendering, but
  weak for a clean native Windows desktop app and would not share one engine with a C# GUI.
  Rejected.
- **A mixed stack (e.g. Python core + C#/Electron front ends):** two runtimes to ship and
  keep in parity; contradicts ADR 0002. Rejected.
- **.NET Framework / WPF:** Windows-only, so the same engine could not run in a Linux
  container without a separate port. Rejected in favour of cross-platform .NET 8.
