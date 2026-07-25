---
name: triage-pr-feedback
description: Collect current GitHub pull-request reviews, inline review comments, and issue comments with pagination, then route actionable feedback into a Repoplugin remediation handoff. Use when a repository change has a pull request and feedback must be assessed without posting replies or changing code.
---

# Triage PR feedback

Operate read-only by default. Resolve the explicit shared task with `$repoplugin-task-contracts:resolve-repository-task` and read its PR reference; if neither a PR number nor unambiguous repository/branch reference is available, ask one short question and make no external call.

## Collect and route

1. Re-probe the PR and use the paginated read-only `gh` commands in [GitHub feedback collection](references/github-feedback-collection.md) to retrieve reviews, inline review comments, and issue comments.
2. Deduplicate by GitHub node/REST ID. Preserve author, URL, timestamp, path/line where present, and quoted request context without reproducing secrets.
3. Classify each item as actionable, clarification needed, already addressed, or non-actionable. Check it against the current diff and validated plan; do not assume a reviewer is right or wrong without evidence.
4. Write a concise `<task>/review/pr-feedback.md` and remediation handoff through `$repoplugin-task-contracts:persist-repository-task-artifact`. Assign actionable fixes to the implementation/remediation loop, then request re-review after evidence is updated.
5. Treat replies, review submission, thread resolution, issue-comment posting, pushing fixes, and PR state changes as separately authorized notification-causing actions. Do not perform them merely to acknowledge feedback.

Report collection scope, pagination result, actionable items, ambiguity, and any unavailable GitHub access. Static command availability is not proof that the host is authenticated or that a PR exists.
