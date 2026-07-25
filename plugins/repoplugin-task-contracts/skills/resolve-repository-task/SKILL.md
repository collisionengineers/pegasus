---
name: resolve-repository-task
description: Create, attach, or resume one explicit Repoplugin repository task. Use from another Repoplugin lifecycle skill when it needs a task folder and must never guess a latest task.
---

Use `../../scripts/Invoke-RepopluginTaskOperation.ps1` from this skill directory.

- Create with a request and an optional task ID. The helper generates one when omitted.
- Attach or resume only with an explicit `-TaskId` or `-HandoffPath`; do not infer a most recent task.
- Task files live at `.repoplugin/tasks/<task-id>/` with fixed lifecycle-area folders.
- Keep requirement changes and remediation as ordinary Markdown artifacts owned by the relevant lifecycle route.
