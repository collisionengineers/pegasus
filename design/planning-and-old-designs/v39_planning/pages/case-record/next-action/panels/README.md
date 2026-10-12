# Next action and ribbon gates: panels

1. **The Next action step** (`_CaseAside.cshtml`, `.next-row`): after the send, Mark completed, greyed inside a `menu-gated` wrapper whose title is the reason while a task is open (`data-open-tasks-condition`, `data-next-mark-completed`).
2. **Tasks list** (`data-open-tasks`): a sub-panel headed "Tasks" in the report-blocker style, one row per open task (`data-open-task`) with its description and a **Tasks** button that jumps to the section.
3. **Actions menu items** (`_CaseRibbon.cshtml`): Mark completed (`data-mark-completed`) and Archive (`data-archive-case`), each greyed in a `menu-gated` wrapper with the same reason.
