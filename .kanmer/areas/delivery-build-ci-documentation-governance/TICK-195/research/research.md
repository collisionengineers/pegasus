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
