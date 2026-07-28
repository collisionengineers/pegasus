# Tool Registry and MCP Security Gateway

Generated: 2026-05-22

**Type:** Platform component
**Priority:** Immediate

## Objective

Control which agents can call which MCP/tools, with permissions, schemas, rate limits, audit logging, and safe tool discovery.

## Why it matters for Collision Engineers

As the number of MCPs grows, exposing every tool directly to every model becomes risky and inefficient. OpenAI tooling supports MCP, hosted tools, tool search, and function tools; the internal platform still needs CE-specific permissions and audit.

## Proposed shape

A gateway registers tools, validates schemas, enforces role/workflow permissions, records invocations, and exposes only the appropriate tool subset to each skill/agent.

## Candidate tools / MCP methods / skill actions

- `register_tool(tool_schema)`
- `list_allowed_tools(user_id, context)`
- `invoke_tool_checked(tool_name, args, user_id)`
- `get_tool_audit_log(filters)`
- `disable_tool(tool_name, reason)`
- `simulate_tool_call(tool_name, args)`

## Inputs

- Tool schemas
- user identity
- work item state
- approval status
- policy config

## Outputs

- Tool call result
- audit log
- permission decision
- tool inventory

## Guardrails

- No broad shell/file/database tools exposed to general agents.
- Dangerous actions require explicit workflow state/approval.
- Secrets never returned to agents.
- Rate-limit external APIs.

## MVP implementation path

1. Inventory all tools and risk level.
2. Build gateway wrapper around MCP/function tools.
3. Add audit and permission checks.
4. Use tool search/deferred loading for large tool surfaces.

## Test / acceptance criteria

- Unauthorised tool call blocked.
- Approved tool call logged.
- Secrets not visible.
- Disabled tool cannot be invoked.

## Risks and open questions

- Gateway adds complexity.
- Need clear role definitions.
- MCP security depends on careful implementation.

## Project source basis

- phase_ai_tools.md
- MCP docs
- OpenAI Agents/tools docs

## External reference basis

- Model Context Protocol documentation: https://modelcontextprotocol.io/docs/getting-started/intro
- OpenAI tools / Agents documentation: https://developers.openai.com/api/docs/guides/tools
