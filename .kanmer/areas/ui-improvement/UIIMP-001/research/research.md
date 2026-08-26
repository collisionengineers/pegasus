# Research — UIIMP-001: local UI mode boundary

## Question

Where can Live UI and Test UI be selected without changing the deployed Web application or the existing owned local-runtime lifecycle?

## Findings

- `scripts/Invoke-LocalDevelopment.ps1` is the supported lifecycle entry point. Its `Start` action currently calls `Start-LocalRun`, which validates initialization and exact-source artifacts, then creates an owned database, Azurite, Web, and Worker run.
- The lifecycle script already centralizes argument validation and has platform-aware process-launch helpers. Test UI must branch before `Get-Initialization` and `Start-LocalRun`; otherwise a static prototype would incorrectly require the full local stack.
- `Status`, `Smoke`, `Stop`, and `Reset` operate on owned run manifests. Test UI creates no owned runtime, so those actions remain Live-only and unchanged.
- `scripts/Build-ReleaseArtifacts.ps1` publishes only `src/Pegasus.Web` and `src/Pegasus.Worker`. Keeping Test UI under `docs/design/` and adding no project reference prevents it from entering Web publish output.
- The supported platforms are Windows and Linux (`docs/runbook.md`). Opening a local HTML file therefore needs an explicit platform branch: Windows shell-open and Linux `xdg-open`, with a clear error when the opener is unavailable.
- The agreed interface is `-UiMode Live|Test`, default `Live`. Test mode uses static scenarios and must not start SQL, authentication, migrations, Web, Worker, Azurite, or external services.

## Implications

Add `UiMode` to the existing launcher rather than creating a second command. Route `Start + Live` through the unchanged lifecycle and `Start + Test` through a small local-file opener. Reject `UiMode Test` for non-Start actions and reject Live-only failure controls in Test mode. The catalogue from [[UIIMP-002]] must land first so Test mode has a real target and this ticket can be completed coherently.

## Open questions

None.
