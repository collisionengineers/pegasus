# Sampling and reporting

## Minimum cohort matrix

| Dimension | Include |
|---|---|
| Provider | QDOS first; every later provider gets its own reviewed cohort |
| Message shape | direct, staff-forwarded, reply chain, missing sender signal |
| Content | instruction, images-only, instruction plus images, query/other |
| Attachments | none, one, many, container format, duplicate names |
| PDF | embedded text, mixed, scanned, encrypted/corrupt, multi-document |
| Decision | positive, contradiction, unknown, transient failure |

## Redacted run report

Record run ID, UTC timestamp, repository commit, evaluator version, corpus snapshot counts and manifest hash, sample strategy, number of human-reviewed expectations, exact entry point, configuration class excluding values, metrics by cohort, unsupported/transient counts, latency percentiles, external-call count/cost, redacted representative failures, reviewer, and unresolved ambiguity.

Keep detailed manifests in ignored artifacts. Commit only aggregate, non-identifying conclusions when useful.
