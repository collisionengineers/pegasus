# ADR-0008: Focused repository workflow plugins

- Status: Superseded by [0010](../../decisions/0010-adopt-azure-workflow.md)
- Date: 2026-07-25
- Owners: Alex and the Pegasus `Next`/`unallocated` development team

## Context

The first repository-planning plugin proved durable research, review, and plan
handoff concepts, but combining them with a universal transactional lifecycle
made ordinary repository work harder to understand and maintain. The user
rejected that design as over-engineered and selected a focused plugin suite with
one small shared task convention.

The suite must support planning, implementation, independent review, testing and
validation, debugging, documentation stewardship, and UI/UX planning. Evidence
must survive agent and session handoffs without turning Markdown work records
into a second ticket system or source of product truth.

## Decision

1. Publish eight repository-local skills-only packages:
   `repoplugin-task-contracts`, `repoplugin-planning`,
   `repoplugin-implementation`, `repoplugin-review`,
   `repoplugin-validation`, `repoplugin-debugging`,
   `repoplugin-documentation`, and `repoplugin-ui-ux`.
2. Share only `.repoplugin/tasks/<task-id>/`, a small `state.json`, fixed
   lifecycle-area folders, ordinary Markdown artifacts, and small JSON handoffs.
   Attach and resume require an explicit task ID or handoff. Mutable state uses a
   sibling temporary file and move. There is no journal, exactly-once protocol,
   operation-intent log, event chain, lock service, or generation engine.
3. Planning owns repository/context research. Its standard entry point works in
   Codex Default or Plan mode, establishes the four-unknown baseline, interviews
   one material question at a time, independently reviews the draft, and loops
   until answers are incorporated and no blocking question remains. Later
   requirement changes use `RC-NNN` records and reopen only affected work.
4. A generated plan must tell its later implementer to call the actual Codex
   harness `update_plan` tool before editing or delegation. Markdown plans and
   checklists do not substitute for that tool; plan-writing agents do not call it
   merely to create the pack.
5. Implementation, review, validation, and debugging exchange concise remediation
   handoffs. Git worktrees, commits, pushes, pull requests, comments, and other
   externally visible actions remain governed by the applicable user authority
   and current repository state.
6. The documentation plugin owns reusable repository documentation and AGENTS.md
   standards, zero-loss bootstrap, query-oriented context maps, repository-wide
   contradiction checks, and maturity horizons. Only the user resolves
   contradictory repository claims.
7. The UI/UX plugin owns inventory, specification, alternative wireframes,
   independent traceability review, user approval, image generation, manual
   review, and implementation handoff. It packages only the explicitly approved
   Collision Engineers logos, four Futura weights, and sanitized internal-app
   style/accessibility guidance.
8. `.agents/plugins/marketplace.json` registers source packages only. Installing,
   enabling, trusting, or fresh-session testing them changes host configuration
   and remains a separate explicit action.

## Consequences

- Each user-facing lifecycle has one discoverable entry skill and can attach to
  the same task without a shared runtime service.
- Research and discussion survive handoff as ordinary Markdown while repository
  authorities remain the source of truth.
- The suite can be validated statically and locally without claiming host
  installation or runtime dispatch.
- Package changes must keep cross-skill names and the small task contract
  compatible, but do not require transactional replay machinery.

## Supersession and limits

This decision supersedes ADR-0007's single `plugins/repoplugin` package, hooks,
flow state machine, and immutable plan-generation design. It does not authorize
plugin installation, Git publication, pull-request activity, corpus changes,
operator-note edits, Azure mutation, deployment, or deletion of local task
history.
