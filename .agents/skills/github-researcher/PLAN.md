## Project Plan: GitHub Deep Research Tool

**Goal:** Build a research-grade tool for AI agents to discover, analyze, and synthesize open-source GitHub repositories on user-specified topics, producing structured evidence and optionally a cited Markdown report.

### 1) Scope and Core Capabilities

- CLI entrypoint (and JSON I/O contract) for agent automation.
- Deep-research workflow (multi-step, not one-shot search):
  1. Discover candidate repositories from GitHub search.
  2. Expand each candidate with rich evidence collection.
  3. Rank/synthesize findings against user criteria.
  4. Export machine-readable results and optional Markdown reports.
- Accept only open/public repositories by default (non-private only).
- Enforce safe and explicit rate-limit handling for GitHub API usage.

### 2) Non-Goals for v1

- No code generation, no commit/patch application in target repos.
- No private repository scraping.
- No replacement for general web search engines or full code execution frameworks.

### 3) Architecture

Proposed modules:

- `src/ghresearch/cli.py`
  - CLI parser and command dispatch.
  - Global options: query, report path, output format, limits, filters, API token, timeout, etc.
- `src/ghresearch/github_client.py`
  - API client wrapper around GitHub REST.
  - Handles auth, pagination, retries, rate-limit/error handling.
  - Single point for endpoint-specific functions.
- `src/ghresearch/models.py`
  - Typed data models for request/response/evidence objects.
- `src/ghresearch/research.py`
  - Deep-research pipeline orchestration.
  - Query planning, scoring, synthesis, and citation linking.
- `src/ghresearch/reporting.py`
  - Markdown report generator with source citations.
- `src/ghresearch/io.py`
  - Save/load JSON and optional markdown artifacts.
- `tests/`
  - Unit tests for policy/scoring/formatting.
  - Integration tests with mocked GitHub API responses.

### 4) API and Data Inputs

- GitHub REST endpoints to use:
  - Search repositories
  - Repository metadata + contents
  - Commits, issues, pull requests, releases
  - README/license/topics
  - Code search where useful
- Query filters:
  - `is:public` enforced for base discovery.
  - `language`, `stars`, `pushed`, `topic`, `org`, `user`, `archived`, `fork` as supported by search qualifiers.

### 5) Commands

- `ghresearch discover`
  - Purpose: repository discovery list only.
  - Inputs: `--query`, `--language`, `--min-stars`, `--pushed-after`, `--limit`.
  - Output: JSON of candidate repositories plus score context.
- `ghresearch scan`
  - Purpose: evidence-enrichment pass over explicit repo list/filters.
  - Inputs: explicit repo list, targeted artifact options (`--collect-readme`, `--collect-releases`, etc.).
  - Output: per-repo evidence bundle and score.
- `ghresearch research`
  - Purpose: full deep workflow:
    discovery + scan + synthesis.
  - Inputs: research topic query plus report options.
  - Outputs:
    - JSON artifact (`report.json`) suitable for agents
    - Optional Markdown report (`report.md`) with citations.
- `ghresearch cite`
  - Optional utility to regenerate only citation format or citation digest from a prior JSON artifact.

### 6) Deep-Research Pipeline

#### 6.1 Discovery
- Build query string with explicit public-only qualifier by default.
- Execute paginated search with hard ceiling and short-circuit strategy.
- Normalize candidates; deduplicate by repo full name.

#### 6.2 Evidence Expansion
Collect for each repository (bounded by configurable depth budgets):
- Repository metadata: description, language, stars, forks, license, timestamps, topics.
- README and documentation entry points.
- Commit cadence (recent commit sample + velocity indicators).
- Issue and pull request activity (recent and recent trends).
- Releases/tags and notable maintenance indicators.
- Optional content search snippets for target terms in code/docs.

#### 6.3 Scoring and Ranking
- Weighted score dimensions (configurable in code):
  - topic relevance signals
  - recent activity
  - maintenance health
  - documentation quality
  - adoption proxy signals (stars/forks/watchers)
- Keep score transparent with explanation for top drivers.

#### 6.4 Synthesis and Reporting
- Build ranked findings with evidence groupings.
- Produce conclusions in plain language and risk/coverage caveats.
- Attach citations with exact URLs and source type.
- Include “evidence confidence” and “coverage gaps” per repository.

### 7) Report Format (Markdown)

- Sections:
  - Summary
  - Top findings (ranked)
  - Detailed repo profiles
  - Source/evidence tables
  - Limitations and next steps
- Citation style:
  - Numbered references or per-item footnotes linking to stable GitHub URLs and API-derived data.

### 8) Configuration and Safety

- Auth:
  - Use `GITHUB_TOKEN` env var and/or CLI flag.
  - Explicitly warn on unauthenticated mode (lower limits).
- Resilience:
  - Retry with exponential backoff for transient failures.
  - Honor rate-limit headers and stop/backoff on 403/429 secondary throttling.
- Guardrails:
  - Per-run total API call cap.
  - Per-repo cap and max-depth caps.
  - Optional strict mode: refuse run if limits are too strict to complete requested depth.

### 9) Testing Plan

- Unit tests:
  - query composition and qualifiers
  - score weighting/normalization
  - report rendering and citation formatting
- Integration tests (mocked HTTP):
  - search pagination
  - throttling/backoff branch
  - malformed/partial API responses
- CLI tests:
  - command argument parsing
  - output artifacts created at expected paths
- Optional smoke test:
  - run against a tiny public query with mocked responses and assert citation coverage.

### 10) Milestones

1. Core scaffolding + models + client wrapper + discover command.
2. Evidence pipelines + scoring and bounded scan mode.
3. Full research command + Markdown reporting.
4. CLI polish + robustness + tests + docs.
5. Packaging (`pyproject.toml`) and quick start guide.

### 11) Milestone Exit Criteria

- `research` command executes with valid JSON output and citations.
- Report includes enough evidence depth beyond simple title/description matches.
- Hard caps/retries prevent runaway calls under low-rate or unstable conditions.
- Tests pass for core scoring/reporting behavior.

### 12) Reference Notes

- GitHub REST v3 endpoints and search/limits docs should be used as the implementation source of truth for request/response shapes, rate-limit headers, and search qualifiers.
