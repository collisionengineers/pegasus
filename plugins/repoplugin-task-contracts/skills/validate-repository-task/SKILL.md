---
name: validate-repository-task
description: Complete and validate one explicit Repoplugin repository task folder. Use from another Repoplugin lifecycle skill before reporting its task artifacts as ready.
---

Use `../../scripts/Invoke-RepopluginTaskOperation.ps1` from this skill directory.

- `Complete` changes `state.json` from `active` to `completed` using a sibling temporary file and move.
- `Validate` checks task identity, state, `task.md`, and all fixed area folders.
- This helper deliberately performs only task identity, state, path, and required-folder checks; it has no transactional replay, journal, lock, or generation workflow.
