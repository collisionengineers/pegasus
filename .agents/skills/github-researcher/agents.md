# Agents Instructions

## Purpose

You are building a GitHub research agent tool. This document defines the standard rules and conventions to keep work consistent, safe, and reproducible.

## Core Principles

- Prioritize correctness over breadth.
- Be transparent in methodology: every claim in reports should be tied to evidence.
- Fail fast and clearly when inputs are invalid or rate-limited.
- Prefer deterministic behavior for reproducibility and testing.

## Scope

- Public repositories only unless user explicitly requires a different scope and explicit policy allows it.
- Focus on research quality: discovery, evidence collection, synthesis, and citation.
- Exports should be machine-actionable and human-readable.

## Coding Conventions

- Language default: Python 3.12+.
- Use type hints for all public functions and dataclasses/Pydantic models for structured data.
- Use `typing` aliases for complex evidence/score structures to keep interfaces clear.
- Keep functions single-purpose and composable.
- Keep module boundaries explicit:
  - `cli.py`: input/output interfaces
  - `github_client.py`: external API calls and request policy
  - `research.py`: orchestration and workflow stages
  - `reporting.py`: citation/report rendering
- Use `ruff`/`ruff format` and `mypy` for style and static checking where available.

## Configuration and Security

- Read token from `GITHUB_TOKEN` by default; never hardcode secrets.
- Never persist personal tokens or sensitive response headers.
- Sanitize and validate user-provided repo identifiers.
- Use explicit API version and modern headers for all GitHub requests.
- Log API request errors with enough context, but redact auth tokens.

## Operational Conventions

- Respect API quotas:
  - use bounded pagination
  - apply retry/backoff on transient failures
  - enforce per-run and per-repo call limits
- Never issue unbounded loops over repositories or results.
- Keep deep research deterministic:
  - define and log effective query
  - define and log budgets (`repo_limit`, `max_pages`, `max_calls`, `depth`)
- Use UTC timestamps in generated artifacts.

## Reporting Conventions

- Every repo finding should include:
  - evidence snippets
  - explicit source URL(s)
- Markdown report:
  - clear sectioning (summary, ranked results, evidence table, caveats)
  - numbered or footnote citations
  - avoid uncited assertions.
- JSON output:
  - include metadata (`generated_at`, `query`, `params`, `version`)
  - include per-repo evidence arrays and score rationale.

## Validation Checklist

Before considering a change complete:

- CLI parsing is covered by tests.
- API request logic has tests for pagination and throttling paths.
- Reporter output has deterministic ordering and stable citation format.
- A failure mode exists for zero results and partial failures.
- README/usage examples match actual CLI flags.

## Workflow Rules

- Do not edit unrelated files unless required for the planned feature.
- Prefer local helper utilities over ad hoc string parsing.
- Keep behavior changes behind explicit feature gates where user expectations might shift.
- Add tests for every added branch in data collection and scoring logic.

## Delivery Standards

- Commit messages (when used) should be concise and imperative.
- PR summaries should include:
  - behavior change
  - API/request impact
  - test status
  - known limitations
