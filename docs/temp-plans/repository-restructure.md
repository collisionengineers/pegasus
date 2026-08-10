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
   `.gitattributes`, authoring scripts, live source-path literals, comments,
   tests, documentation, temp plans, CI comments, and workspace outbound links
   in the same branch. Preserve reference bytes and renderer staging assets.
   Published package bytes and applied migrations are immutable historical
   identities: do not rewrite `provider-domains-v1`, its recorded
   `docs/reference/...` source path, or either migration that carries it. Map
   that historical source identity to the moved physical workbook only at the
   authoring/test resolution boundary, without publishing the same package
   version under new bytes.
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
   `.gitignore` and `.gitattributes` entries; and preserve byte-identical
   brand/reference logo and signature placements with their distinct
   runtime-versus-evidence roles made explicit. Hash all four cross-placement
   pairs rather than inferring equality from names. Do not decide the separately
   claimable `.obsidian`, Infisical, or infrastructure-lane items.
7. Complete the queue migration in `NOW.md`: remove this branch's own Doing
   claim, retire the now-completed restructure and rule-dedupe premises, and
   rewrite the absorbed hygiene line so only independently claimable residue
   remains (`.obsidian` keep, Infisical confirm/delete, infrastructure lane, and
   any other item proved unabsorbed). Repair every other current queue path while
   preserving all other agents' claims and work.
8. Run a repository-wide live-referrer inventory, not only a changed-file scan.
   Repair every live `docs/reference`, `design/README.md`, `design/product`, and
   `operations.md#required-evidence-tiers` route, including temp plans and
   workspace/current-queue references. Retain an old literal only where it is
   immutable historical provenance or a description of the pre-move state, not
   a live owner or path.
9. No merge to `dev` is part of this task.

## Boundaries

- No `.codex/` thinning, product behaviour change, Azure/production/credential
  operation, deployment, or live data operation.
- No modification to `corpus/` or to any file beneath
  `workspaces/ai-centre/skills/`.
- Preserve `CLAUDE.md` as a mode-120000 symlink to `AGENTS.md`.
- Preserve all supplied reference evidence and report-renderer staging assets;
  moves and link/literal updates do not assert caller, deployment, or acceptance
  evidence.
- Preserve every pre-existing accepted ADR body, already-applied migration, and
  published versioned package byte-for-byte. Record current routing only in
  ADR-0023, indexes, current documentation, or explicit authoring/test mapping.
- Preserve other agents' worktrees, branches, claims, and temp plans.

## Verification

1. Run `git diff --check` and the repository documentation-link checker, then a
   separate repository-wide Markdown fragment/anchor sweep covering moved and
   newly split documents.
2. Search repository-wide for stale `docs/reference`, `design/README.md`,
   `design/product`, and `operations.md#required-evidence-tiers` paths and for
   claims that operations owns evidence classification/gates. Classify every
   remaining hit; allow only deliberate historical provenance or descriptions
   of the pre-move state where no live link/owner is implied.
3. Compare reference-tree file hashes before/after the move; hash the four
   evidence/runtime logo and signature pairs across their two placements; run `git check-attr
   -a` on representative moved PDF/PNG/XLS/XLSX/JSON/Markdown paths, and confirm
   Git reports renames rather than content rewrites.
4. Confirm `CLAUDE.md` remains mode `120000` with target `AGENTS.md`, and confirm
   `git diff` reports no paths under `.codex/`, `corpus/`, or
   `workspaces/ai-centre/skills/`.
5. Compare `provider-domains.v1.json`, both applied provider migrations, and
   ADR-0018 byte-for-byte with the PR base. Add focused provider-package tests
   proving that the historical package identity resolves to the moved authoring
   workbook without same-version republication, and prove both an existing/prior
   schema and a fresh schema converge on the same immutable package/history.
6. Run the reference-data Python tests/authoring verification, the canonical
   `dotnet restore`, `dotnet build --configuration Release`, focused tests for
   any adopted performance-test or reference-data changes, and the full test
   projects required by current repository guidance. Treat a timeout or skipped
   lane as unverified, not passed.
7. Inspect the final base/head diff, file modes, branch claim removal, absorbed
   queue-line retirement/rescoping, every remaining stale-path classification, and
   protected-tree path list before opening a PR to `dev`.
