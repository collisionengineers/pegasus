# Pre-edit rule inventory

Source: reviewed shared AGENTS.md (552 lines), checked against origin/dev ticket base 26ba4ed408317cccdb354dc1e115b0297f15df94. Managed block refresh is the shipped writer's content, not a custom rewrite.

| Existing rules | Disposition / owner |
| --- | --- |
| Managed board location, branch administration, request routing, orientation, gates, stages, questions, document reads, workspace/resume safety, scratch, integration proof, verification serialization, archives, skill routing and handoff | Retain canonical generated block; delegate procedure to skills. |
| Managed conduct 1–24: scope, ticket-before-branch, no self-merge, greenfield, reuse, single lists, relative paths, dependencies, conflicts/errors, real fixtures, callers, runtime assets, schema/grants, reachable SHAs, no stubs, honest tests/proof, no speculative gates, review dispositions, secrets, instruction updates | Retain once in generated block. |
| Second Kanmer conventions: branch administration, resume, provider env, tunnel controls | Remove exact/near duplicates. |
| Second conduct 1–24 | Remove stale duplicate. |
| Project identity and document navigation | Retain introduction. |
| Locked restore/build/test, pwsh platform parity, focused runbook commands | Retain Commands. |
| Routed Razor captures, verify, scoped example, commit catalogue | Retain Commands. |
| Directory map and dependency direction; Core one policy owner | Consolidate Architecture map. |
| Workspace no-caller imports and accepted integration contract | Consolidate Architecture map with application/solution/dynamic/deployment prohibitions. |
| Markdown H1/blank headings/table/wrapping | Retain Conventions; generated-preamble exception. |
| New Markdown restriction and simplicity pointers | Consolidate governance and Simplicity rails. |
| Windows/Linux separation and Linux release terminal | Consolidate Gotchas. |
| Corpus immutability and artifacts destination | Consolidate Gotchas. |
| Dark backend is not delivered; named inert frontend preview exception | Retain together in Product invariants. |
| Case no-delete, Created in error, linked replacement, immutable and unreused references | Retain Product invariants. |
| Operator notes protected truth and references not requirements | Consolidate Gotchas and governance authority pointer. |
| Caller evidence versus build; canonical verification requirement | Retain Verification via managed rules/engineering reference. |
| Refresh as-built/runtime docs after release/deployment in same task before merge | Retain once in Verification. |
| Proof on merged main | Remove contradiction; delegate configured integration proof to Kanmer. |
| Documentation governance owner, authority owner | Retain Documentation model and index. |
| Operator/PRD/FRD/ADR/capabilities/boundaries/current-state/runbook/engineering/design responsibilities | Condense routing table with existing meanings, including FRD design citation and downstream authority. |
| ADR stable IDs, no delete/renumber/reuse, new superseding ADR and status, YAML fields, one technical decision, template, durable content, thin index | Retain ADR conventions. |
| New Markdown placement, ticket transient docs, no ADR permission for PRD/FRD, capability owner/index link, workspace-local exception | Retain New Markdown placement. |
| Planning navigation and workflow/doc routing | Consolidate introduction and workflow; remove redundant Planning process. |
| Search before build, one list, abstractions criterion, existing conventions, fact checks, proportional plans | Retain unique details in Simplicity rails, generic principles delegated to conduct. |
| UI explanation economy (twice), readability balance, quality versus behavior changes | Retain one UI rule and engineering reference for balance/skip mechanics. |
| Relative platform paths, canonical rails, feature gating, protect other work | Consolidate Commands/Conventions/Product invariants/workflow. |
| Read-only cloud permission, exact-target write approval, do not delete named RG as first step | Retain Gotchas. |
| Real supplied test evidence permitted, no fabricated data, no unsolicited PII/DPA/DPIA/privacy/retention/licensing gates | Retain Gotchas; fixtures conduct remains generic owner. |
| Fail-closed allocation, standalone Audit distinction; immutable principal; reopening reason/gates; terminology INTK-007 | Retain Product invariants without semantic change. |
| New top-level architectural boundary requires accepted ADR | Retain Architecture map. |
| Outlook/Box alpha restrictions and approved disposable test areas | Retain Gotchas. |
| Handwritten take/branch/worktree/full pipeline/cleanup | Delegate Kanmer, remove conflicting task paths and phase duplication. |
| Overlapping capabilities/files and existing ownership coordination | Retain workflow. |
| Plan reused-code and checked-premise requirements | Consolidate Simplicity rails. |
| Simplification pass timing, lenses, dated dispositions, docs-only n/a | Retain workflow with engineering mechanics link. |
| Independent review verifies ticket/plan/pass and docs scope; reviewed green PR integration | Retain workflow with independent reviewer merge. |
| Local commits ungated, release exact-SHA and fresh MERGE AUTH GRANTED | Retain workflow and unchanged release mechanism reference. |
| DELIV-003 and DELIV-046 exceptions | Remove expired permissions; merged evidence remains on board. |
| Maintenance pushes for temporary plans/claims and orphan temporary files | Remove obsolete mechanisms; use ticket storage/closeout. |
| 48-hour/14-day reclaim-by-anyone | Remove age-only destructive authority; ownership-aware Kanmer reconciliation. |
| No shared-history rewrite/force/stash/reset/clean others/stage outside task | Retain workflow; expected-value lease in authorized release is not history rewrite permission. |
| Supplied project principles | Condense development status, real compatibility dependency, coherent replacement, normal schema mechanisms, proportionality, affected consumers and stop when complete. |

Separate follow-up: board defaults to pr.yml/verify/push while Pegasus uses ci.yml with pushes only on main. Do not alter CI or board policy in this ticket.

## Fetched-base correction before edits

origin/dev 26ba4ed40 is newer than shared checkout. It already removes duplicate Kanmer conventions/conduct, duplicate UI bullet and obsolete task workflow, and supports Windows/Linux releases under ADR-0039. Preserve these fixes; do not reintroduce plan's obsolete Linux-only assumption. Preserve current V1 remediation authority subsection and exact scoped grant, no-wipe exception, receive-time cutoff, named host verifier. Preserve added Commands safeguards for regex budgets, SQL shard timeout/concurrency, reference-data normalized-lf, PreProvision validation and capture concurrency. Preserve Git-fixture isolation, CI single infrastructure owner/no-change behavior, and explicit classifier precedence/provider-outcome/metrics rules. Move these unique engineering requirements to appropriate sections without changing them. Retired workspace provenance remains retired; do not restore independent-build claims. Existing release authorization already granted in scope persists; fresh approval only when no current task grant covers the candidate. These are current authoritative requirements, not compatibility additions.
