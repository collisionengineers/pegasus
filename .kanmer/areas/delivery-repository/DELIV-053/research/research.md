# Research — DELIV-053

## Question

How can this repository pin reusable Codex agents without changing application behavior, losing Kanmer connectivity, or allowing concurrent host verification?

## Findings

- Source baseline 7b6aa189c2112ab3cf8df2c2e337fc9f2b0dabae: .codex/config.toml is ignored and contains only the Windows Kanmer launcher and KANMER_BOARD_BRANCH. No project agent TOMLs exist. Preserve those existing non-secret settings.
- OpenAI documentation read 2026-09-08: https://learn.chatgpt.com/docs/agent-configuration/subagents supports standalone .codex/agents/*.toml with name, description, developer_instructions, model and model_reasoning_effort. The [agents].max_concurrent_threads_per_session key excludes the primary. Configuration reference: https://learn.chatgpt.com/docs/config-file/config-reference. These are source references, not copied policy or measured model benchmarks.
- Installed codex-cli 0.153.4 exposes --strict-config and doctor --summary. Project config requires trust; current runtime permission overrides may supersede role sandbox defaults. No silent model substitution or auto-trust.
- AGENTS.md already requires independent review and one heavy verifier per host; the approved task strengthens exclusivity to all tests/builds, including focused runs, snapshots, browser hosts and packaging. The eight-thread limit alone does not enforce this.
- The source checkout has foreign principal-document moves. They are unrelated and must remain untouched. Isolate this ticket in its own recorded worktree.
- get_sources returned no declared sources. No dependency, external MCP, plugin installation or application tests are required by this configuration change.

## Implications

Use five pinned roles from the approved plan; keep primary coordination and release authority separate. Reuse existing Kanmer execution records for host ownership rather than adding a scheduler/lock service. Store role/model definitions once in TOML, with shared coordination rules in the unmanaged AGENTS.md section.

## Open questions

None. The operator approved the plan and requested implementation; live operations remain separately authorized.
