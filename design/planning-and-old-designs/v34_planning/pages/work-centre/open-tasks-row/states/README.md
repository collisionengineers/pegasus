# Open tasks row: states

Captured in the mockup:

| `?state=` | What it shows | Capture |
| --- | --- | --- |
| `row` | One row per Case with open tasks, under Later, with the Tasks chip | `current/captured/work-centre-tasks-row.html` |

Reachable in the source but not captured:

- A Case with one open task: the bold line is the task alone, with no "(+N more)".
- An unowned row (no assignee, and a creator who was not staff): Owner reads "Unassigned", and the row is not on anyone's Mine.
- The row opened in place: its six facts (Reference, Vehicle, Principal, Owner, Due, Received) and **Open tasks**.
- A dismissed row, back again once a newer open task is created on the Case.
