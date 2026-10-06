# Tasks: states

Captured in the mockup:

| `?state=` | What it shows | Capture |
| --- | --- | --- |
| `edit` | The edit session: New task with Add task, and on each open row Assign, Complete and Cancel task | `current/captured/tasks-edit.html` |
| `read` | The same list with no actions column and no New task | `current/captured/tasks-edit.html`, drawn as `tasks-read` (the read fragment is `current/captured/tasks-fragment-read.html`) |

Reachable in the source but not captured:

- No tasks: the empty block "No tasks".
- No enabled named staff to choose: the open row offers Complete and Cancel task without Assign.
- A Completed or Query Case, an archived Case, a closed Case, and a Case a colleague is editing: the list without its actions (`mayChange` is false).
