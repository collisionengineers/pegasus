---
name: github-researcher
description: Research public GitHub repositories and explain how they could fit an existing project. Use for repository discovery, shortlist comparisons, single-repository deep dives, or project-fit assessments that need evidence-backed, plain-language recommendations. Do not use for private-repository access, security audits, or executing third-party code.
---

# GitHub Researcher

Research public GitHub projects deeply enough to make a useful adoption decision. Inspect the target project first, gather bounded evidence with the bundled tool, and explain both the likely use case and the reasons not to adopt a candidate.

## Choose The Research Shape

Select the mode from the request:

- **Landscape** for broad discovery.
- **Comparison** for a known shortlist.
- **Deep dive** for one repository.
- **Project fit** when the main question is how a repository could help the target project.

Use `standard` depth unless the user asks for a quick scan or explicitly requests deep, exhaustive, or research-grade work. Read [references/research-variants.md](references/research-variants.md) for mode-specific evidence and depth budgets.

## Workflow

1. Resolve the target project from the user-supplied path, then the current Git root, then the current directory. Read only the relevant `AGENTS.md`, README, manifests, dependency files, configuration, and entry points. Establish the project's purpose, stack, constraints, and the capability being researched.
2. Turn the need into several short search concepts. Do not submit private code, filenames, customer names, internal hosts, or proprietary terminology to GitHub. If those details are essential, generalize them or ask before using them in an external query.
3. Run `scripts/github_research.py` from this skill directory. Use `collect` for discovery plus evidence, `inspect` for a named repository, and `search` when planning or refining queries. Prefer multiple focused searches over one long natural-language query.
4. Check candidate relevance before trusting popularity. Inspect documentation, license, recent maintenance, releases, issue/PR activity, integration surfaces, and any evidence specific to the target project.
5. Synthesize the evidence; do not simply restate the JSON. Separate observed facts from project-fit inferences, explain tradeoffs in plain language, and make a clear adopt, prototype, monitor, or reject recommendation.
6. Return the report in the conversation. Write Markdown and raw evidence JSON only when the user requests files. Read [references/reporting.md](references/reporting.md) before producing the final report.

## Search Tool

Typical commands:

```bash
python3 scripts/github_research.py search "embedded analytics" --language TypeScript --depth quick
python3 scripts/github_research.py inspect owner/repository --depth deep
python3 scripts/github_research.py collect "local-first sync engine" --depth standard --min-stars 50
python3 scripts/github_research.py collect --repo owner/one --repo owner/two --depth deep
```

The tool emits JSON to standard output and diagnostics to standard error. Use `--output <path>` only when an evidence artifact was requested. `--backend auto` prefers an authenticated GitHub CLI and otherwise uses the public REST API; `GH_TOKEN` or `GITHUB_TOKEN` raises REST limits without being printed or persisted.

Treat partial results and rate-limit metadata as report limitations. Authenticated code search is optional and only runs at deep depth when one or more `--code-term` values are supplied.

## Boundaries

- Research only public `github.com` repositories. Never weaken the visibility check or add private/enterprise access.
- Make only read-only requests. Do not clone repositories, install packages, run repository code, create issues, or modify the target project unless separately requested.
- Do not treat stars, downloads, or recent commits as proof of quality. Do not claim a security review from repository metadata.
- Cite direct GitHub pages near material claims. A search result, README assertion, or repository description is evidence of what maintainers claim, not independent verification.
- State when authentication, code search, documentation, or activity evidence was unavailable instead of filling gaps with assumptions.
