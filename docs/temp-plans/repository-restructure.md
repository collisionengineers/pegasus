# Repository restructure implementation plan

## Outcome

Record and implement the operator-settled repository structure without changing
product behaviour, deployment state, or protected source imports. The change
will make `docs/` prose-only, give procedure and current-state evidence separate
owners, give UI/product design one canonical prose owner, and retain supplied
reference evidence byte-for-byte at the repository root.

## Changes

1. Add accepted ADR-0023 for the target structure, ownership boundaries, and
   top-level `reference/` directory; add it to the ADR index while replacing
   repeated per-row evidence disclaimers with one blanket qualification.
2. Move `docs/reference/` to `reference/` with Git renames, update
   `.gitattributes`, authoring scripts, persisted source-path literals, comments,
   tests, documentation, temp plans, CI comments, and workspace outbound links
   in the same branch. Preserve reference bytes and renderer staging assets.
3. Split `docs/operations.md`: current production/release/evidence/monitoring/
   recovery records remain in operations; setup, local development, database,
   testing, release, approval, and recovery procedures move to a new
   `docs/runbook.md`. Move the evidence-tier ladder to `docs/engineering.md` and
   repoint every referrer.
4. Create `docs/design.md` by merging all still-applicable content from
   `design/README.md` and `design/product/`; remove those three prose files only
   after their content is represented. Keep `design/brand/`,
   `design/references/mockups/`, and `design/assets/report-renderer/` as the
   assets-only tree.
5. Apply the settled rule-ownership table: engineering owns workflow, claims,
   review, Git safety, Markdown conventions, merge authority, CI routing, and
   evidence tiers; the NOW footer owns tracker/staleness rules; the docs index
   owns the new-Markdown rule; the ADR index owns one immutability and evidence
   qualifier; requirements remains the product-invariant owner; CI details stay
   in workflow comments. Update `docs/index.md`, `AGENTS.md`, `README.md`, the NOW
   footer, workflow comments, source comments, and workspace links accordingly.
6. Resolve only the hygiene items absorbed by the ticket: either adopt the two
   orphaned performance tests into an existing compiled project or delete them
   when their assertions are already covered; consolidate the split Python
   reference-data component under one tested owner; remove stale/contradictory
   `.gitignore` and `.gitattributes` entries; and preserve differently-byte-valued
   brand/reference assets with their distinct runtime-versus-evidence roles made
   explicit. Do not decide the separately claimable `.obsidian`, Infisical, or
   infrastructure-lane items.
7. Remove this branch's own `NOW.md` claim from the PR diff after merging fresh
   `origin/dev` as required. No merge to `dev` is part of this task.

## Boundaries

- No `.codex/` thinning, product behaviour change, Azure/production/credential
  operation, deployment, or live data operation.
- No modification to `corpus/` or to any file beneath
  `workspaces/ai-centre/skills/`.
- Preserve `CLAUDE.md` as a mode-120000 symlink to `AGENTS.md`.
- Preserve all supplied reference evidence and report-renderer staging assets;
  moves and link/literal updates do not assert caller, deployment, or acceptance
  evidence.
- Preserve other agents' worktrees, branches, claims, and temp plans.

## Verification

1. Run `git diff --check` and the repository documentation-link checker, then a
   separate repository-wide Markdown fragment/anchor sweep covering moved and
   newly split documents.
2. Search for stale `docs/reference`, `design/README.md`, `design/product`, and
   `operations.md#required-evidence-tiers` paths, allowing only deliberate
   historical literals in immutable ADR bodies where a live link is not implied.
3. Compare reference-tree file hashes before/after the move, run `git check-attr
   -a` on representative moved PDF/PNG/XLS/XLSX/JSON/Markdown paths, and confirm
   Git reports renames rather than content rewrites.
4. Confirm `CLAUDE.md` remains mode `120000` with target `AGENTS.md`, and confirm
   `git diff` reports no paths under `.codex/`, `corpus/`, or
   `workspaces/ai-centre/skills/`.
5. Run the reference-data Python tests/authoring verification, the canonical
   `dotnet restore`, `dotnet build --configuration Release`, focused tests for
   any adopted performance-test or reference-data changes, and the full test
   projects required by current repository guidance. Treat a timeout or skipped
   lane as unverified, not passed.
6. Inspect the final base/head diff, file modes, branch claim removal, and
   protected-tree path list before opening a PR to `dev`.
