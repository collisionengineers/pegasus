# Pegasus: strict coordinator with scoped specialist workers

## 1. Summary

Make this a **Pegasus-only setup**. The lead handles requirements, assignments, decisions and integration ownership. Workers perform source investigation, implementation, review and verification.

This accepts additional handoff latency for small tasks to minimise lead context—the reason the earlier recommendation allowed trivial lead-agent work.

Adopt the reasonably validated audit recommendations:

- Retain **49 .NET skills** and **13 Azure skills**.
- Install the three requested local skill packages into Pegasus.
- Preserve personal configuration and installed plugin caches.
- Keep the existing eight-child concurrency ceiling.

## 2. Workers and model pins

Set both `model` and `model_reasoning_effort` explicitly in every worker TOML. Pin the project lead explicitly too. These are supported custom-agent settings. [OpenAI custom-agent documentation](https://learn.chatgpt.com/docs/agent-configuration/subagents#custom-agents).

| Agent | Model | Reasoning | Responsibility |
| --- | --- | --- | --- |
| Lead | `gpt-6-astra` | `xhigh` | Coordination, decisions and integration ownership |
| `pegasus-scout` | `gpt-5.6-luna` | `medium` | Bounded file discovery and inventories |
| `pegasus-investigator` | `gpt-5.6-terra` | `high` | Causal investigation and source performance analysis |
| `pegasus-implementer` | `gpt-5.6-terra` | `high` | Assigned application, configuration and migration changes |
| `pegasus-test-author` | `gpt-5.6-terra` | `high` | Assigned tests and necessary testability changes |
| `pegasus-reviewer` | `gpt-5.6-sol` | `high` | Independent code and test review |
| `pegasus-verifier` | `gpt-5.6-sol` | `medium` | Serial verification, captures, benchmarks and binlog analysis |
| `pegasus-azure-analyst` | `gpt-5.6-terra` | `high` | Azure investigation, guidance and read-only estate diagnostics |
| `pegasus-microsoft-docs` | `gpt-5.6-terra` | `high` | Current Microsoft documentation and code samples |
| `pegasus-github-researcher` | `gpt-5.6-terra` | `medium` | Public GitHub research |
| `pegasus-git-hygienist` | `gpt-5.6-terra` | `high` | Branch/worktree audits and authorised cleanup |
| `pegasus-mcp-specialist` | `gpt-5.6-terra` | `high` | Assigned MCP design, implementation and diagnosis |

Use read-only settings for research/review roles. Every leaf sets `agents.enabled = false`. An unavailable pinned model is reported; workers do not silently substitute another model.

## 3. Capability ownership and installation

### .NET

Use the [full .NET audit](C:/Users/Alex/.codex/audits/dotnet-pegasus-20260915.md) as the retained-skill inventory. Its companion configuration is selection evidence: translate it into exclusive ownership rather than enabling retained skills at the root.

Allocate the 49 retained skills as follows:

- **Test author — 8:** `code-testing-agent`, `code-testing-extensions`, `detect-static-dependencies`, `find-untested-sources`, `generate-testability-wrappers`, `migrate-static-to-wrapper`, `scaffold-dotnet-test-project`, `testability-obstacle`.
- **Reviewer — 9:** `assertion-quality`, `coverage-analysis`, `crap-score`, `grade-tests`, `test-analysis-extensions`, `test-anti-patterns`, `test-gap-analysis`, `test-smell-detection`, `test-tagging`.
- **Verifier — 15:** test `platform-detection`, `filter-syntax`, `run-tests`; diagnostics `dotnet-trace-collect`, `dump-collect`, `microbenchmarking`; retained MSBuild skills except the five assigned below. Also owns the `binlog` MCP from `dotnet-msbuild@dotnet-agent-skills`.
- **Investigator — 1:** `dotnet-diag:analyzing-dotnet-performance`.
- **Implementer — remaining 16:** retained application, SDK and migration skills, plus MSBuild `copy-to-output-directory`, `directory-build-organization`, `item-management`, `msbuild-antipatterns`, `property-patterns`.

Disable the five audit-excluded plugins in Pegasus: Blazor, MAUI, NuGet, template engine and .NET 11. Preserve the audit’s individual exclusions within retained plugins.

Put the audit’s SDK setup corrections in implementer guidance. Require current Microsoft documentation before using the retained .NET 11 migration skill.

### Azure and Microsoft documentation

Assign the 13 retained Azure skills to `pegasus-azure-analyst`.

Split the existing `azure@azure-skills` MCP into disjoint tool owners:

- **Microsoft docs:** only `documentation`.
- **Azure analyst:** the audited 28-tool allowlist with `documentation` removed.

Both owners explicitly enable the server, supply their finite `enabled_tools` list and clear inherited `disabled_tools`. Neither owns the entire plugin.

Assign `microsoft-learn-cli` exclusively to the documentation worker. It uses the existing, successfully exercised Learn MCP first and the CLI when MCP retrieval is unavailable. Both provide documentation search, page retrieval and code samples. [Microsoft Learn developer reference](https://learn.microsoft.com/training/support/mcp-developer-reference).

Azure guidance and read-only investigation remain delegated. Release operations retain the existing Pegasus release procedure and lead ownership.

### Requested local packages

Copy the supplied sources into project-local `.agents/skills/`:

- `git-branch-cleanup` → Git hygienist.
- `github-researcher` → GitHub researcher.
- `mcp-servers` → MCP specialist.

Preserve required references, scripts, fixtures and notices. Exclude generated distribution archives and the package’s source planning file.

Inventory **14 MCP skill folders**, including nested `mcp-csharp-create`, `mcp-csharp-publish` and `mcp-csharp-test`; the package manifest lists only 11 top-level entries. Preserve its directory structure and relative references.

Installing these packages does not execute cleanup, publish packages or configure external MCP servers.

### Matched configuration

In `.codex/config.toml`, disable every specialist-owned skill and MCP surface. Enable each capability only in its owner.

Use containing skill directories as identities. Emit complete skill-state arrays in every leaf, preserving unrelated existing exclusions, so child overrides cannot reopen sibling capabilities. Resolve plugin cache paths from installed manifests and verify inherited entries that currently use `SKILL.md` paths.

Keep retained plugins loaded; scope their individual skills and MCP tools. Configuration is an ownership mechanism, not a security boundary.

## 4. Context control and delegated implementation

Put the strict coordinator rule directly in `AGENTS.md`, so it applies before skill selection.

Generate a discoverable `.agents/skills/project-workers/SKILL.md` containing:

- At most **500 words**, with metadata at most **50 words**.
- Worker selection table and essential handoff rules.
- Links to a generated reference containing full capabilities and instructions.

Use a small project-local rendering adapter that reuses `project-agent-scoping` validation and rendering. Worker TOMLs remain the source of truth; the adapter generates the compact entry and detailed reference together.

Operational rules:

- Start independent workers without inherited conversation history where supported.
- Supply a compact task packet: source root/worktree, revision, relevant requirements, allowed files, expected evidence and stop condition.
- Normally run 2–4 useful independent workers; allow up to eight.
- Require disjoint write ownership and no reversion of another worker’s changes.
- Workers normally return at most **250 words**, with conclusions, changed files, evidence links, failures and requested next steps.
- Keep raw logs, research and detailed reviews in linked artifacts.
- Route cross-specialist dependencies through the coordinator. Workers neither spawn children nor load another owner’s excluded skills.

For workflows containing internal agent stages, the worker returns the required stage request to the coordinator. Test authors request verifier execution; reviewers request missing coverage; verifier findings requiring fixes return to the implementer.

Implement the setup through separate bounded assignments:

1. Configuration and worker TOMLs.
2. Local skill package installation.
3. Routing generation after ownership is settled.
4. Independent review.
5. Serial verification and runtime proof.

The lead assigns and integrates these results without editing source or configuration itself.

## 5. Verification and acceptance

The verifier owns all checks under the existing host-slot procedure.

Require:

- TOML parsing and exclusive-scope validation across project and personal workers using the active `CODEX_HOME`.
- Complete accounting of all 49 retained .NET skills, 13 Azure skills and 14 imported MCP skill folders.
- Relevant package/script checks, reference integrity and the Markdown placement gate.
- Harmless fixture proof before relying on skill-array or plugin-tool inheritance.
- Fresh-session evidence that root and unrelated siblings cannot discover selected capabilities, while each owner can load its skills and perform a harmless applicable tool call.
- Explicit two-way Azure checks: docs worker lacks analyst tools; analyst lacks `documentation`.
- Runtime confirmation of model/effort pins and disabled recursive delegation.
- Representative routing checks for test author → verifier, Microsoft documentation retrieval, and MCP specialist selection.

Compare fresh-session lead catalogs and a bounded representative task before and after the change. Require reduced lead exposure and concise worker handoffs; report actual token measurements where available.

Static validation and runtime proof receive separate results. Ambiguous exposure, missing nested skills or unavailable runtime observations leave the affected proof incomplete. Do not compensate by re-enabling specialist capabilities for the lead.

The native audit confirmed CLI `0.154.0` and project trust; `doctor` was unavailable or timed out. No runtime isolation claim has yet been established.
