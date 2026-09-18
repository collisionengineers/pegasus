# Reporting Guide

Write for a reader who understands their project but may not know the researched ecosystem. Prefer direct conclusions and concrete use cases over repository-by-repository data dumps.

## Required Report Content

1. **Bottom line** — the recommendation and strongest reason in a short opening.
2. **Project need** — the relevant current architecture, constraint, or missing capability.
3. **Method and coverage** — mode, depth, search concepts, candidate count, research date, and material evidence gaps.
4. **Findings** — ranked candidates or the single-repository assessment.
5. **Potential use** — the smallest valuable use case, integration point, implementation outline, expected effort, and operational effect.
6. **Risks and alternatives** — license, maintenance, maturity, lock-in, security-review needs, and credible competing approaches.
7. **Recommendation** — adopt, prototype, monitor, or reject, including the next validation step.

For a comparison, include a compact matrix before the narrative. Useful columns are fit, integration effort, maintenance confidence, license, principal advantage, and principal risk.

## Evidence Rules

- Link repository claims to the repository, README, release, issue, pull request, security policy, or relevant source page.
- Prefer a specific page over the repository root. Include the observation date for volatile statistics or activity claims.
- Label maintainers' claims as documented or advertised behavior when they were not independently verified.
- Clearly introduce project-fit conclusions with language such as “This suggests” or “For this project”.
- Give each leading candidate a high, medium, or low confidence label and explain missing evidence that affects it.
- Never imply that metadata inspection is a security audit, legal opinion, benchmark, or proof that the software works as documented.

## Plain-Language Repository Profile

For each leading repository, answer:

- What is it, in one sentence?
- Why is it relevant to this project?
- What could the project use it for?
- How would it connect to the existing architecture?
- What would adoption cost or complicate?
- Is it maintained and licensed appropriately?
- What must be tested before relying on it?

Keep raw stars, forks, dates, and activity counts subordinate to those answers. Omit low-value fields rather than translating every JSON key into prose.

## No Or Partial Result

If no credible candidate remains, say so directly and explain whether the cause was a sparse ecosystem, restrictive filters, authentication limits, rate limits, or insufficient evidence. Preserve useful partial conclusions and propose the next narrower or broader search; do not promote a weak candidate simply to fill the report.
