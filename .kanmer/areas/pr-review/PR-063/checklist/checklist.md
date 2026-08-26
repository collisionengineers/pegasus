# Checklist — PR-063

- [x] Create and take the stacked PR-063 branch/worktree from the UIIMP-002 head.
- [x] Add and validate branch claims for every visual inventory state.
- [x] Correct all Administration default mappings and defining interactions.
- [x] Correct all Account, dashboard, queue, inbox and operations default mappings.
- [x] Correct all case, intake, triage, unidentified and image-intake default mappings.
- [x] Correct all upload, status and external default mappings.
- [x] Restore defining shared-shell navigation/user controls without runtime behavior.
- [x] Remove whitespace errors and pass catalogue/mapping validation.
- [x] Pass representative browser, keyboard/focus, width, zoom and forced-colour checks.
- [x] Pass PowerShell parse, documentation, locked restore/build and diff checks.
- [x] Complete and record the four-lens simplification pass.
- [x] Update UIIMP-002 evidence, write the implementation report, push, open the stacked PR and move PR-063 to Review.

## Progress notes

- 2026-08-26: Created the isolated correction branch from UIIMP-002 commit `63ce690`; the PR targets that parent branch rather than `dev`.
- 2026-08-26: Audited all 39 visual defaults and documented concrete conditions for all 60 visual states.
- 2026-08-26: Corrected invalid/combined default branches and normalized 34 authenticated default shells to the current user-control structure.
- 2026-08-26: Removed 45 reported EOF whitespace errors. Catalogue, documentation, PowerShell, Markdown placement, locked restore/Release build and diff checks pass.
- 2026-08-26: Inspected authenticated dashboard at 200%, sign-in under forced colours and external upload at 1280×900. All authenticated default shells have skip links and focusable main targets.
- 2026-08-26: Independent simplification pass recommended no behavior-preserving structural changes. Its two correctness findings—generic branch claims and overstated validator wording—were applied.

## PR-064 evidence correction — 2026-08-26

This supersedes PR-063’s earlier statement that its first rerun had corrected all 39 defaults. Review found two remaining contradictions: organization-edit’s inventory claimed no principals/roles despite its populated Work Provider markup, and vehicle-image detail claimed registered images while rendering an image with no source. [[PR-064]] corrected both, rechecked all 39 defaults against the PR-063 mapping/current Razor owners, and found no additional contradiction. The validator now also rejects absent, empty, and whitespace-only image sources.
