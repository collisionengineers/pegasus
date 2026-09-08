# Plan — DELIV-053: reusable Codex agents

## Objective

Pin the five approved project agents and serialize all host test/build execution without changing application behavior.

## Starting state

Source baseline 7b6aa189c2112ab3cf8df2c2e337fc9f2b0dabae; no project agent definitions. Existing ignored source .codex/config.toml contains non-secret Kanmer registration. Evidence: research/research.md@0e4315e63b85ca25; files/files.md@9992d0307d5e1c8a. Operator approved the displayed plan and said Implement the plan on 2026-09-08.

## Governing docs

Meets docs/engineering.md effect-based verification, evidence and proportionality rules. AGENTS.md owns project conduct; no PRD/FRD/ADR changes are needed for agent configuration. No new ADR, dependency or application boundary.

## Required changes

Use standalone TOML profiles with exact model/effort: pegasus-scout gpt-5.6-luna/medium, pegasus-investigator gpt-5.6-terra/high, pegasus-implementer gpt-5.6-terra/high, pegasus-reviewer gpt-5.6-sol/high, pegasus-verifier gpt-5.6-sol/medium. Include required name, description and developer_instructions. Read-only defaults for scout/investigator/reviewer; inherit existing runtime permission policy for implementer/verifier rather than broadening it.

Project [agents]: enabled=true; max_concurrent_threads_per_session=8; default_subagent_model=gpt-5.6-terra; default_subagent_reasoning_effort=medium. Preserve existing Kanmer settings exactly. Track config by deleting only its specific .gitignore rule. No legacy aliases, speculative settings, silent model fallback, recursive delegation or duplicate model matrix in AGENTS.md.

Add a compact unmanaged AGENTS.md section linking five roles and source URL/date, requesting delegation only for independent useful work. Primary owns assignment, file overlap, approvals, integration and release operations. Every child forbids recursive delegation, unrelated edits, external writes and autonomous tests/builds except the verifier with the explicit current host slot. All tests/builds including focused commands, verification scripts, capture/browser hosts and packaging serialize; static reads/git diffs can overlap. The verifier runs commands sequentially against frozen inputs, preserves failures and performs no code fixes. Primary may acquire the slot for release packaging only after explicit idle handoff. Other host sessions must coordinate; never kill foreign processes. Profiles are not permission grants or guarantees against parent runtime overrides.

## Expected files

- .gitignore
- .codex/config.toml
- .codex/agents/pegasus-scout.toml
- .codex/agents/pegasus-investigator.toml
- .codex/agents/pegasus-implementer.toml
- .codex/agents/pegasus-reviewer.toml
- .codex/agents/pegasus-verifier.toml
- AGENTS.md

## Do not modify

- src/**
- tests/**
- infra/**
- corpus/**
- docs/**
- .agents/**
- pegasus_pack/**

## Constraints

Windows PowerShell 7 host. No new package, scheduler, lock daemon, command wrapper or user-level configuration change. Preserve the managed Kanmer block and all unrelated work. Model assignments are engineering choices, not benchmark claims.

## Ordered steps

1. Create the five role TOMLs, track the existing Kanmer configuration with the approved [agents] keys, and remove its ignore rule.
2. Add the shared unmanaged AGENTS.md delegation/verification contract and concise role links without duplicating models.
3. Hand exact changes to the designated host verifier for strict configuration and live agent-discovery checks. Apply only scoped findings, preserving earlier results. Obtain independent simplification review (configuration-focused; no application code).
4. Record exact evidence and limitations, commit/push one bounded branch, open draft PR targeting dev, record it on the ticket and move to Review when gates allow. No self-review or merge.

## Acceptance checks

- Codex actually discovers the five unique project names; model/effort config matches approved values. Use client-visible configuration/session evidence, not the agents' self-report alone.
- Strict config diagnostics accept current supported keys. Project trust/model unavailability is surfaced, never auto-repaired.
- Kanmer registration remains intact, no credential/machine-absolute path appears in tracked config, and git no longer ignores .codex/config.toml.
- Non-verifier delegates request checks instead of running them; host-slot ownership is explicit and one command executes at a time.
- Unrelated source checkout changes remain untouched.

## Commands

Designated host verifier only: codex --strict-config doctor --summary; fresh trusted client read-only discovery of all five profiles and lightweight role dispatch; existing relevant documentation check if the changed instruction links require it. Static inspection: git diff --check; git diff --stat; git check-ignore -v .codex/config.toml (expected no match/exit1 after ignore removal). Do not run dotnet restore/build/test for this configuration-only change. No auto-trust or permission escalation. If the client cannot expose role/model evidence, record INCONCLUSIVE and stop at that exact missing evidence rather than claiming PASS.

## Failure and deviation rules

Stop on scope conflict, unknown configuration schema, changed local MCP settings, failed verification, unavailable models, unexpected consumer behavior or new authority. Keep all failed attempts. The implementation worker never starts a test/build; submit to the primary's one host queue.

## Stop condition

Draft PR open against dev, traceability recorded and ticket in Review with truthful verification evidence, ready for an independent reviewer. Do not merge, deploy, release the claim or start another ticket.
