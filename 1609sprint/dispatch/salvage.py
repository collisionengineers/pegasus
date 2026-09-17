"""Mark a task whose Codex implement turn completed as `implemented` without re-running it.

Usage: python salvage.py <taskId> <worktreeDir> <threadId> "<summary>"

Commits the worktree's uncommitted edits as a WIP round (so the tree has a provable SHA) and
rewrites artifacts/sprint-1609/state.json for that task. Run only while no dispatch run is live:
a live run holds the state in memory and overwrites the file on its next transition.
"""
import json
import subprocess
import sys
from datetime import datetime, timezone

task_id, worktree, thread_id, summary = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
state_file = "C:/Users/Alex/Documents/GitHub/pegasus/artifacts/sprint-1609/state.json"


def git(*args):
    return subprocess.run(["git", "-C", worktree, *args], capture_output=True, text=True, check=True).stdout.strip()


if git("status", "--porcelain"):
    git("add", "-A")
    git("commit", "-q", "-m", f"wip({task_id}): round 1 (salvaged)")
head = git("rev-parse", "HEAD")

with open(state_file, encoding="utf-8") as f:
    state = json.load(f)
t = state["tasks"][task_id]
t.pop("lastError", None)
t.update({
    "state": "implemented", "threadId": thread_id, "headSha": head, "worktreeDir": worktree,
    "summaries": [summary], "updatedAt": datetime.now(timezone.utc).isoformat(),
})
t.setdefault("history", []).append({"at": t["updatedAt"], "state": "implemented", "note": "salvaged completed turn after a false-positive quarantine"})
with open(state_file, "w", encoding="utf-8") as f:
    json.dump(state, f, indent=2)
print(task_id, "implemented at", head, "thread", thread_id)
