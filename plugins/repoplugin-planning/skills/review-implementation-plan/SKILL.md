---
name: review-implementation-plan
description: Independently challenge an evidence-backed repository implementation-plan draft and identify material unanswered questions. Use only when explicitly routed by plan-repository-change after a draft exists and before plan-pack generation.
---

# Review Implementation Plan

Review a draft you did not author. First ask whether the change should exist. Then check source authority, real callers, policy ownership, duplicate paths, security, data safety, operational failure behavior, testability, documentation, commands and authority, worktree/parallel decisions, and deferred impact.

Persist a severity-ranked `review/round-NNN.md` and canonical `open-questions.md`. Facts, inferences, and user decisions must remain distinct. Every conflict, material uncertainty, unsupported assumption, or irreversible decision requiring the user's choice is an open question. Retain prior question IDs until an independently reviewed revision resolves them.

Do not rewrite the plan, decide user-only conflicts, implement, or call `update_plan`.
