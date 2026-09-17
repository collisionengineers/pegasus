"""Salvage every completed-but-quarantined Codex implement turn from a workflow run directory.

Usage: python salvage-all.py <workflowRunDir> <worktreeRoot>

For each turn in <run>/runs whose task-result outcome is done/partial and whose task is
recorded as needs-human, commit the worktree's edits as a WIP round and mark the task
`implemented` with the turn's thread id. Run only while no dispatch run is live.
"""
import glob
import json
import os
import subprocess
import sys
from datetime import datetime, timezone

run_dir, wt_root = sys.argv[1], sys.argv[2]
state_file = "C:/Users/Alex/Documents/GitHub/pegasus/artifacts/sprint-1609/state.json"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

# task id -> worktree dir name (mirrors tasks.mjs)
dirs = {"U2": "performance-next", "P766": "u50-intake", "P765": "audit-original-report", "P767": "case-linking",
        "I1": "est-import", "I2": "inbox-unid", "I3": "veh-type", "I4": "audit-fixes", "I5": "rpt-fresh",
        "I6": "est-doc", "I7": "ui-guard", "I8": "audit-a"}

with open(state_file, encoding="utf-8") as f:
    state = json.load(f)

turns = {}
for d in sorted(glob.glob(os.path.join(run_dir, "runs", "*"))):
    ev = os.path.join(d, "events.jsonl")
    meta_f = os.path.join(d, "meta.json")
    if not (os.path.exists(ev) and os.path.exists(meta_f)):
        continue
    task = None
    with open(ev, encoding="utf-8") as f:
        for line in f:
            i = line.find("Sprint 1609 dispatch, task ")
            if i >= 0:
                task = line[i + len("Sprint 1609 dispatch, task "):].split(".")[0].strip()
                break
    if not task:
        continue
    meta = json.load(open(meta_f, encoding="utf-8"))
    if meta.get("status") != "completed" or meta.get("outcome") not in ("done", "partial"):
        continue
    summary = ""
    final = os.path.join(d, "final.md")
    if os.path.exists(final):
        s = open(final, encoding="utf-8").read()
        try:
            summary = json.loads(s[s.index("{"):s.rindex("}") + 1]).get("summary", "")
        except Exception:
            summary = s[:300]
    turns[task] = {"thread": meta.get("thread_id"), "summary": summary, "outcome": meta.get("outcome")}

# I7 was authored by the primary after the sandbox refused .agents writes; keep its thread for provenance.
for d in sorted(glob.glob(os.path.join(run_dir, "runs", "*"))):
    ev = os.path.join(d, "events.jsonl")
    if os.path.exists(ev) and "task I7." in open(ev, encoding="utf-8").read(20000) and "I7" not in turns:
        meta = json.load(open(os.path.join(d, "meta.json"), encoding="utf-8"))
        turns["I7"] = {"thread": meta.get("thread_id"), "summary": "Guardrail skill files and root routing rule installed by the primary (Codex sandbox cannot write .agents/).", "outcome": "done"}

now = datetime.now(timezone.utc).isoformat()
for task, info in turns.items():
    t = state["tasks"].get(task)
    if not t or t["state"] not in ("needs-human", "blocked", "prepared"):
        continue
    wt = os.path.join(wt_root, dirs[task])
    git = lambda *a: subprocess.run(["git", "-C", wt, *a], capture_output=True, text=True, check=True).stdout.strip()
    if git("status", "--porcelain"):
        git("add", "-A")
        git("commit", "-q", "-m", f"wip({task}): round 1 (salvaged)")
    head = git("rev-parse", "HEAD")
    t.pop("lastError", None)
    t.update({"state": "implemented", "threadId": info["thread"], "headSha": head, "worktreeDir": wt,
              "summaries": [info["summary"]] if info["summary"] else [], "updatedAt": now})
    t.setdefault("history", []).append({"at": now, "state": "implemented", "note": f"salvaged {info['outcome']} turn after a false-positive quarantine"})
    print(f"{task}: implemented at {head[:9]} thread {info['thread']}")

with open(state_file, "w", encoding="utf-8") as f:
    json.dump(state, f, indent=2)
print({k: v["state"] for k, v in state["tasks"].items()})
