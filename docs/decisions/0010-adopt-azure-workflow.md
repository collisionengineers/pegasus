# 0010: Adopt Azure Workflow repository standard

- Date: 2026-07-27
- Status: accepted

## Context

CollisionSpike v2 had a repository-local plugin suite, task-state format, and
documentation routes governed by ADR-0007 and ADR-0008. The tracked plugin and
marketplace implementation was removed before this onboarding, leaving dead
routes and validators. The user explicitly invoked Azure Workflow repository
onboarding to replace that repository-specific workflow without changing
business authority, application behavior, or Azure resources.

## Decision

Adopt the Azure Workflow repository standard for documentation, GitHub work
routing, change records, path-aware verification, delivery, independent review,
and explicitly approved Azure operations. Preserve CollisionSpike-specific
operator authority, product invariants, architecture, evidence, and historical
ADRs. Remove the dead repository-local plugin validation and active routes only
after their replacement owners and checks pass. ADR-0008 is superseded for
current repository workflow; ADR-0007 and ADR-0008 remain historical evidence.

## Consequences

The repository gains one neutral documentation spine, stable capability
inventory, design source/runtime map, issue forms, PR template, portable GitHub
taxonomy/Project, and one change-record workflow. GitHub owns actionable work;
the repository does not restore a task-state database or generated ledger.
Application/runtime/Azure behavior is unchanged, and all cloud actions retain
their separate explicit-approval boundary.
