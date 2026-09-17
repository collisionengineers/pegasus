# Research Variants

Use the smallest mode and depth that can answer the decision. The budgets are ceilings, not targets; stop early when later candidates cannot plausibly change the recommendation.

## Modes

### Landscape

Discover the main approaches available for a capability. Group candidates by approach before ranking them, inspect the best representative of each credible group, and mention meaningful gaps in the landscape. Do not let near-duplicate repositories crowd out distinct approaches.

### Comparison

Evaluate the supplied repositories against the same criteria. Preserve the user's priorities; otherwise compare functional relevance, integration fit, maintenance, documentation, license, operational burden, and adoption risk. Include a compact decision matrix followed by plain-language interpretation.

### Deep Dive

Evaluate one repository on its own merits. Cover its purpose, architecture and extension points, installation or embedding model, maintenance, releases, license, documented limitations, and likely integration path. Identify what would need a prototype or source inspection before adoption.

### Project Fit

Start from the local project's actual architecture and capability gap. Map each candidate to concrete integration points and explain the smallest useful use case, required changes, expected effort, operational consequences, and credible alternatives. Do not recommend adoption merely because a repository solves a generally related problem.

## Depth Budgets

| Depth | Discovery | Detailed inspection | Evidence |
| --- | --- | --- | --- |
| Quick | 1 query, up to 10 candidates | Top 3 | Repository metadata, README, license, latest release |
| Standard | Up to 3 queries, 25 candidates total | Top 5 | Quick evidence plus root docs, community profile, recent commits, issues, and pull requests |
| Deep | Up to 5 queries, 50 candidates total | Top 10 | Standard evidence plus release history, contributors, larger activity samples, and optional authenticated code search |

The script applies per-command caps. Across several searches, deduplicate by case-insensitive `owner/repository` and keep the strongest evidence set.

## Query Planning

Build queries from capability, implementation approach, ecosystem, and integration surface. For example, a project needing embedded analytics might warrant separate searches for `embedded analytics`, `headless business intelligence`, and the target framework plus `dashboard`.

Use language, topic, minimum-star, or update-date filters only when they express a real project constraint. Do not use a high star threshold during initial discovery when it could hide a strong specialist project.

## Candidate Triage

Before detailed inspection, remove candidates that are clearly unrelated, mirrors, disabled, private, or unusable under the project's license constraints. Archived repositories may be inspected when explicitly supplied, but they must carry an adoption warning.

Rank semantically rather than by a false-precision formula. Use high, medium, or low judgments for:

- capability match;
- architectural and ecosystem fit;
- maintenance confidence;
- documentation and onboarding quality;
- license compatibility;
- integration and operational effort.

Explain the evidence behind each judgment. Popularity is only a supporting adoption signal.
