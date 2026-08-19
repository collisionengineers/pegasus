# Research — package lock disposition

## Question

Does the integrated renderer need standalone workspace package-lock files?

## Findings

1. The renderer workspace has six projects and no `packages.lock.json`; its ADR-0014 explicitly deferred adding six lock files.
2. Every current Pegasus production/test project carries a project-local `packages.lock.json`, and the shared build action keys restore inputs from `src/**/packages.lock.json` and `tests/**/packages.lock.json`.
3. The approved integration folds engine mechanics into existing Pegasus production/test project boundaries and retires standalone API/CLI/MCP hosts. Therefore six renderer lock files would preserve projects that must disappear.
4. Adding Scriban, Playwright, and PDFsharp references to the existing owning project(s) naturally updates those projects' canonical lock files. Locked restore/build behavior remains owned by the repository runbook and shared action.

## Implications

- Do not add package locks under `workspaces/report-renderer`.
- Add only required renderer dependencies to existing Pegasus projects and regenerate their existing lock files through the canonical restore.
- Retired host-only dependencies (ModelContextProtocol/Hosting for renderer MCP, API/CLI host dependencies) do not enter production locks.
- Verify locked restore and dependency advisory output through existing repository commands.
