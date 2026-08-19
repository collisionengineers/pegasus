# Post-implementation report — DOCS-002

## Summary

Added accepted ADR-0028 selecting the existing Pegasus Web Container App as the in-process Chromium/report-rendering execution boundary. The decision leaves the Flex Consumption Worker unchanged, rejects a separate renderer app/job/service, keeps report behaviour in FRD-11/Core, and makes no code, IaC, deployment, sizing or cloud change.

## Changes

| File | Change | Why |
|---|---|---|
| `docs/adr/0028-run-integrated-renderer-in-web-container-app.md` | Added ADR-0028 with complete frontmatter and Status, Context, Decision, Consequences, Options considered and Links sections | Records the durable Web-versus-Worker execution choice established by TICK-215 research without creating another deployment unit or duplicating report behaviour. |
| `docs/adr/README.md` | Added ADR-0028 to the accepted decision index | Keeps the derived accepted-architecture view complete and navigable. |

Commit: `169bcd5b`.

Simplification pass: **n/a — docs-only**. The diff contains one technical decision and one derived index row. It does not add behaviour, implementation, sizing, deployment state, cloud actions, or a second decision.

## Governing docs

- **New ADR-0028:** refines ADR-0015 and ADR-0025 without superseding either. It selects the existing Web Container App because that existing custom-container boundary can carry pinned Chromium/native/font dependencies.
- **ADR-0025 met:** CollisionRenderer remains integrated behind a Core port in the existing application; no package, API, MCP host, service, job, queue consumer or deployment unit is created.
- **FRD-11 preserved:** readiness, accepted inputs, immutable identity/hash, correction, approval and failure behaviour remain explicitly outside the ADR and owned by FRD-11/Core.

Kanmer document refs intended after merge: DOCS-002, TICK-215, SIMPLI-014 and PLAT-007 → `docs/adr/0028-run-integrated-renderer-in-web-container-app.md`. All four are supported by the plan/evidence: DOCS-002 authors it, TICK-215 implements against it, SIMPLI-014 composes the renderer in Web, and PLAT-007 proves the Web runtime/deployment. Pre-merge `link_doc` attempts were rejected because Kanmer validates refs against the current `dev` repo root, where a new PR file cannot exist yet; link them immediately after merge during verification.

## Risks / follow-ups

- This ADR proves architecture only. SIMPLI-014/TICK-215 own source integration and composition; PLAT-007 owns local/deployed container, capacity, health, telemetry and recovery proof.
- Azure deployment remains an external write requiring exact-target approval.
- The current Web resource allocation is not accepted as sufficient by this ADR; PLAT-007 must measure it.
- The four Kanmer refs remain an explicit post-merge verification step because the board server cannot validate a new branch-only document.

## Verification hand-off

On merged `dev`:

1. Run `git diff --check HEAD^ HEAD`; expect no whitespace errors.
2. Run `git show --stat --oneline HEAD`; expect ADR-0028 plus one ADR-index row only.
3. Run `pwsh -NoProfile -File scripts/Test-DocumentationLinks.ps1`; expect all relative Markdown links to resolve.
4. Run `pwsh -NoProfile -File scripts/Test-MarkdownPlacement.ps1 -Base HEAD^ -Head HEAD`; expect placement to pass.
5. Confirm ADR-0028 frontmatter id/status/date/relationships/tags and its accepted index row.
6. Attach the merged ADR with `link_doc` to DOCS-002, TICK-215, SIMPLI-014 and PLAT-007; clear DOCS-002 `docs_todo`.
7. Confirm no code, IaC, current-state/deployment or reference-evidence files changed.
