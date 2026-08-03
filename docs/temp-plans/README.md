# Temporary task plans

One `<task-slug>.md` per claimed task, written in the task's worktree right
after claiming and committed on the task branch. The slug matches the claim
line's `task/<task-slug>` branch. The full protocol is owned by
[engineering](../engineering.md#task-workflow); [ADR-0017](../adr/0017-multi-agent-task-workflow.md)
records the decision.

Contract:

- The plan states what the task will change and how it will be verified.
- Plans must not contain relative links to files that do not exist yet.
- Before the task PR merges, an agent that did not implement the task answers
  two questions against this plan: did the plan miss anything the task line
  implied, and did the implementation miss anything from the plan.
- The post-merge maintenance push deletes the plan file. A plan file with no
  matching `Doing` line in [`NOW.md`](../../NOW.md) is orphaned and deletable
  by anyone.

This directory is tracked; its contents are transient by design. Nothing here
is product documentation or a status ledger.
