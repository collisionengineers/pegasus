# Plan — KANMER-002

## Chosen approach

Perform one repository-information reorganization with two safety boundaries: preserve durable planning decisions in Kanmer before deleting temporary plans, and move the entire design authority/assets/package atomically so no consumer observes a split path. Retain reference evidence except for the one byte-identical, unreferenced duplicate. Treat ignored artifacts as local housekeeping, not PR content.

This beats partial moves because `docs/design.md`, governed assets and `design/system/` form one authority surface, and it beats retaining obsolete plans because Kanmer is now the canonical work/evidence tracker.

## Ordered steps

1. Revalidate every `docs/temp-plans/` file against Git history and live Kanmer coverage; summarize the renderer integration decisions and unresolved implementation work into [[SIMPLI-015]] research.
2. Add the task root plan required by AGENTS.md while work is active, then update AGENTS.md and `docs/index.md` so future task planning lives only in Kanmer ticket documents.
3. Move `docs/design.md` to `docs/design/README.md` and move top-level `design/**` beneath `docs/design/**`.
4. Retarget every current consumer: design-sync configuration/notes, Git attributes/ignores, renderer build inputs, documentation links, workspace docs and source comments. Leave historical CHANGELOG prose intact unless it is a live link.
5. Delete the covered `docs/temp-plans/**` set, including this task's transient root plan as the final branch change, and remove the obsolete documentation-link exclusion.
6. Verify and delete only the byte-identical, unreferenced provider workbook duplicate; preserve all other reference evidence.
7. Recheck the exact local ignored artifact candidates and remove only completed/regenerable planning/audit output. Preserve active state and evidence.
8. Run exhaustive stale-path searches, JSON parsing, documentation-link validation, design-system build, renderer focused build/tests and repository docs checks; record the result in the implementation report.

## Governing docs

This is repository process and information architecture, so AGENTS.md is the canonical owner; repository rules explicitly prohibit inventing a PRD, FRD or ADR for process governance. The ticket therefore uses the `chore` profile and has no product governing-doc reference. `docs/index.md` remains the authority-chain navigation owner.

## Proof strategy

- Zero live references to `docs/temp-plans`, `docs/design.md`, or top-level `design/` outside historical text that is intentionally retained and explicitly reviewed.
- `pwsh ./scripts/Test-DocumentationLinks.ps1`.
- `npm ci && npm run build` in `docs/design/system`.
- Focused renderer restore/build/tests using the moved embedded resources.
- Hash evidence for the deleted duplicate.
- Clean Git status after commit and independent review of the exact PR diff.

## Risks and mitigations

- Path churn misses plain-text consumers: use multiple literal searches rather than only Markdown-link validation.
- Active design tooling breaks: retarget `.design-sync` and build the package from its new location.
- Historical planning knowledge is lost: migrate the renderer direction into SIMPLI-015 and rely on immutable Git history for completed task detail.
- Local artifacts contain active data: exact allowlist only; no recursive blanket deletion.
