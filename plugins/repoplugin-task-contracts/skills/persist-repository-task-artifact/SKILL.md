---
name: persist-repository-task-artifact
description: Write a contained Markdown artifact or JSON handoff for an explicit Repoplugin repository task. Use from another Repoplugin lifecycle skill after task identity is known.
---

Use `../../scripts/Invoke-RepopluginTaskOperation.ps1` from this skill directory.

- `WriteArtifact` writes Markdown under one fixed task area and adds task ID, path, owner, and timestamp frontmatter.
- `WriteHandoff` writes small JSON containing the task ID, task path, and artifact references.
- Supply relative paths only. The helper refuses absolute paths, traversal, and paths outside the named area.
- Use ordinary named Markdown artifacts for assumptions, open questions, requirement changes, research, and remediation.
