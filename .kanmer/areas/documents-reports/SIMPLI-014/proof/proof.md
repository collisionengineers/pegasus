# Proof — SIMPLI-014

## Verified merge

- Target branch: `dev` (repository workflow override; no `main` update).
- PR: https://github.com/collisionengineers/pegasus/pull/415
- Reviewed head: `cdb50cd2bbeb84fe69172407adaca06298a437a2`
- Merge commit verified locally and on GitHub: `b548b674e31d05de6f43eeb285a25dedd7d2a768`
- Merged: 2026-08-19T10:29:20Z.
- Local `dev` was fast-forwarded to that exact merge before verification. The pre-existing unrelated `.codex/config.toml` modification was preserved and not included in any result.

## Local merged-dev evidence

Commands were run from the primary checkout at `b548b674e31d05de6f43eeb285a25dedd7d2a768`.

- `dotnet restore --locked-mode` — passed; all seven application/test projects restored with locked dependencies.
- `dotnet build --configuration Release --no-restore` — passed in 1m21s with 0 warnings and 0 errors.
- `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter FullyQualifiedName~AssessmentReportRenderingTests` — 11/11 passed. This proves Core-owned readiness, photo-hash rejection, four closed outcome presentations, VAT-inclusive contract-repair cap, fee VAT/total arithmetic, engineer tuple gating, and typed artifact provenance.
- `pwsh tests/Pegasus.IntegrationTests/bin/Release/net10.0/playwright.ps1 install chromium` — completed successfully for the pinned browser.
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~AssessmentReportRendererTests` — 5/5 passed in 16s through real Chromium. The Web-composed Core use case produced both PDFs for Total loss, Repairable, Cash in lieu and Contract repair; representative assessment/fee-note text, PDF/page/hash/template/engine metadata and the exact Andy-only embedded resource were asserted.
- `dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build --filter FullyQualifiedName~DependencyDirectionTests` — 39/39 passed, including the Core port, single Infrastructure adapter, Web-only composition and absence of the former separate runtime boundary.
- `pwsh scripts/Test-MarkdownPlacement.ps1 -Base b548b674^1 -Head b548b674` — passed.
- `pwsh scripts/Test-DocumentationLinks.ps1` — all relative links resolved across 204 Markdown files.
- Focused `rg` over `docs/runbook.md` and `docs/design/README.md` found no live `workspaces/report-renderer`, CollisionRenderer API/CLI/Core/MCP, `render-starters` or `visual-regression` reference.

## Authoritative CI evidence

GitHub Actions run `32242081373` for corrected head `cdb50cd2` passed every required lane:

- unit — passed, 3m20s;
- browser — passed, 7m51s;
- SQL integration shards 1/2/3 — passed, 7m43s / 8m55s / 8m11s;
- SQL integration coverage — passed, 8s;
- changes, documentation, reference-data and source-workspaces — passed;
- infrastructure — correctly skipped by change detection.

The prior head's shard-3 LocalDB teardown lock did not recur, so SQL shard stability is green at the merged head.

## Acceptance and evidence tier

Verified at **integrated source + Core contract + Infrastructure adapter + Web composition + real local/CI Chromium** tier:

- no separate CollisionRenderer workspace, API, CLI, MCP/MCPB, container or deployment unit remains;
- Core owns accepted identity, outcome wording/calculations, fee arithmetic, source/custody gates and typed outputs;
- only approved rendererref1 assessment and fee-note resources are active;
- Andy Patterson is the only complete selectable tuple; Ed Mawdsley and Neil O'Reilly remain inactive;
- all four approved outcomes render through the composed application path;
- unsupported catalogue/template/wording/signature states remain unavailable and fail closed.

This proof does **not** claim an automatic complete-assessment trigger, durable report reference/custody/correction workflow, Azure deployment, production Chromium health, or a live user caller. Those remain assigned to [[DOCS-001]] and [[PLAT-007]]. No cloud or `main` write occurred.

## Verdict

PASS. The merged `dev` result satisfies SIMPLI-014 at its authorized integration evidence tier and is ready for Done/closeout.

---

## Addendum — 2026-08-20 (deployment status update, stage unchanged)

The renderer this ticket integrated is now **deployed** — it has shipped as
part of the Web image in both release 12 (`ed3be51c95bc2a055606e5210131d37de9de2dd1`,
deployed 2026-08-19) and release 13 (`2325ed4a31d7dad65a00a7ae5ea0c41ca869bfa5`,
deployed 2026-08-20). It now has a **reachable production caller**: per
[[DELIV-012]] proof's signed-in production verification, the Case assessment
page's **Report draft** panel is present and fail-closed with 22 enumerated
readiness reasons (including "Repair cost figures") — the panel this
ticket's rendererref1 assessment/fee-note contract backs.

This remains **gated on estimate import (ENG-002)**: no accepted estimate
lines exist in production yet, so the panel's fail-closed reasons list is
the observable state, not a completed render. This addendum does not change
this ticket's stage (`done`) or its original verdict — it records that the
"no live user caller" qualification in the original proof has narrowed
(a real caller now exists and is reachable) without yet being satisfied
end-to-end.
