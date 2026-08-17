# Research — TICK-195: Validate new Markdown placement in CI

## Question

How can CI reject newly introduced Markdown files outside repository-approved homes without touching the active UI revamp or duplicating the existing relative-link check?

## Findings

- `AGENTS.md` and `docs/index.md#new-markdown-files` currently own the placement rule: new repository Markdown belongs under `docs/prd/`, `docs/frd/`, `docs/adr/`, or `docs/temp-plans/`; everything else updates an existing canonical file. Workspace-local documentation follows the accepted workspace contract and existing tree.
- `docs/index.md` separately identifies `reference/` as supplied evidence and `design/` as design assets. The repository already contains tracked Markdown in both trees, so a validator must distinguish governed non-documentation asset trees and existing files from unauthorized new canonical documents.
- `.github/workflows/ci.yml` has a Windows `documentation` job that runs for every pull request and push to `main`, but it currently calls only `scripts/Test-DocumentationLinks.ps1`.
- `scripts/Test-DocumentationLinks.ps1` enumerates tracked Markdown and checks relative link targets. It has no base/head comparison and cannot tell whether a Markdown file is newly introduced, so placement needs a separate diff-aware validation concern.
- The documentation job uses the default shallow checkout. A placement validator comparing the PR base and head will require the checkout to make both revisions available, following the existing `changes` job's `fetch-depth: 0` precedent.
- Git change status matters: additions and copy destinations are new files; rename destinations are also new repository locations and must be validated rather than grandfathered solely because their content existed elsewhere. Modifications and deletions do not create a placement violation.
- A testable PowerShell interface should accept explicit base/head revisions (with CI supplying the PR base SHA and checked-out head) and report every invalid path before exiting non-zero. Local tests can create temporary commits/fixtures; no application build or Web caller is involved.
- `KANMER-002` is actively claimed on `KANMER-002-repo-doc-cleanup` and its research explicitly plans to delete `docs/temp-plans/`, rewrite the new-Markdown rule in `AGENTS.md` and `docs/index.md`, and edit `scripts/Test-DocumentationLinks.ps1`. The current allow-list is therefore about to change; implementing TICK-195 against today's wording would encode stale policy.
- The active UI-revamp material is currently visible as untracked `.stitch/` and `design/planning-and-old-designs/`. EPIC-001 additionally reserves `src/Pegasus.Web/**`, UI browser/snapshot tests, `design/**`, and `.stitch/**`; none is required for this validator.

## Implications

Implement after, or explicitly rebase onto, KANMER-002's final governance shape. Add a dedicated placement script instead of expanding the concurrently claimed link checker. Wire it into the always-running documentation job with full history available, and keep the policy derived from the post-KANMER-002 repository rules. Fail closed on an unavailable/invalid comparison range and print all offending paths. Do not edit Web, design, Stitch, or UI-test paths.

## Open questions

No user-only question remains. The exact post-KANMER-002 allowed homes are a repository-state dependency to re-read immediately before planning; they must not be guessed from the current, soon-to-change rule.

## Refresh — 2026-08-17 after PR #379 and TICK-200

This section supersedes the earlier provisional findings about pending KANMER-002 work and `docs/temp-plans/`.

### Current findings

- Current `origin/dev` is `28c10422`, containing PR #379 (`6e827d19`) and TICK-200 PR #381. KANMER-002 is no longer an active dependency.
- `AGENTS.md` and `docs/index.md#new-markdown-files` now state the final rule: a new repository Markdown file must be a PRD under `docs/prd/`, an FRD under `docs/frd/`, or a technical ADR under `docs/adr/`. Transient research, plans, checklists, reviews, and proof live only in the owning Kanmer ticket documents. `docs/temp-plans/` has been deleted and must not be recreated.
- The only stated exception is workspace-local documentation governed by an accepted integration contract and the existing workspace tree. In this repository that means descendants of the two registered workspace roots, `workspaces/document-extraction/` and `workspaces/report-renderer/`; `workspaces/README.md` is the register and `workspaces/AGENTS.md` supplies their local constraints.
- Existing Markdown outside those homes is grandfathered because the rule governs a **new file**. A modification or deletion is not a placement event. Git additions, copies, and rename destinations are placement events and must be checked by destination path.
- The final TICK-200 workflow retained the always-running Windows `documentation` job with a default shallow checkout. It also introduced a tested script convention: small policy/classifier scripts under `scripts/` with executable regression scripts such as `Test-CiChangeFlags.ps1`, invoked from CI.
- The validator can remain a separate concern from `Test-DocumentationLinks.ps1`: compute added/copied/renamed Markdown destinations from explicit base/head revisions, pass them to a deterministic path-policy function or script, aggregate every violation, and exit non-zero. CI must make the base revision available (for example, full checkout history) and supply event-specific base/head SHAs.
- For a pull request, use the PR base SHA and head SHA. For a push to `main`, use the event's before SHA and current SHA. Treat an all-zero/missing base or a failed Git comparison as unverifiable and fail closed rather than silently passing.
- PR #382 added new Markdown under managed agent-skill trees after the governance rewrite. The literal canonical rule contains no tooling-tree exception. Such existing files are grandfathered, but future new `.agents/`, `.grok/`, `.codex/`, `docs/design/`, `reference/`, or root Markdown should be rejected unless governance is separately amended first.
- Current main-worktree status is clean. No UI-revamp worktree or task branch is registered locally. EPIC-001 remains the authoritative collision boundary: `src/Pegasus.Web/**`, UI-focused browser/snapshot tests, `docs/design/**` (the post-PR #379 design location), and `.stitch/**` are excluded. The validator changes none of them, although it intentionally enforces the repository rule on future new Markdown destinations.

### Current implications

Plan and implement from current `origin/dev`, after TICK-200. Keep the change to a dedicated Markdown-placement validator, its focused PowerShell regression coverage, and the TICK-200-shaped documentation job. Do not create any repository task-plan Markdown. Re-check the UI boundary and active claims before taking the ticket, but there is presently no exact UI file overlap.

### Current open questions

No user-only question remains. The canonical rule is explicit; adding an exception for tooling, design, reference, or other Markdown would be a separate governance change and is not silently within TICK-195.
