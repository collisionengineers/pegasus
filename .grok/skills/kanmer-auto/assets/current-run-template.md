---
kind: auto-current
schema: 3
run_id: <run_id>
run_path: automation/runs/<run-id>.md
group: <EPIC-000 or HZN-000 — run host group>
scope: group
scope_selector: <ticket id, group id, area id, explicit id list, or board>
project_fingerprint: <get_status project identity>
controller: <controller slug>
status: running
updated_at: <ISO-8601 UTC timestamp>
---

# Current auto run — <group>

## Resume instruction

Open `<run_path>`, then re-read the group context, the frozen roster recorded there, and `get_doc_gates` for every roster ticket before dispatching. This pointer is written only after its complete run record has been written and read back.
