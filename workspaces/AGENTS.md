# Pegasus workspace rules

The repository-root `AGENTS.md` and canonical documentation remain authoritative. Upstream agent/plugin instructions were deliberately not imported.

- Treat every child as a non-caller source workspace. Do not add it to `Pegasus.slnx`, a project reference, application dependency, runtime load path, deployment package, or application CI job.
- Keep workspace builds, tests, package locks, and toolchains independent. Do not hoist dependencies or merge workspace projects into the Pegasus modular monolith.
- Do not copy or commit nested `.git` metadata, generated packages/output, caches, private datasets, local environment files, sample case material, corpus data, or model weights.
- Do not execute a skill, model, training job, evaluator, external connector, renderer, or document converter against operational data or an external service without the exact approval required by root policy.
- AI Centre owns AI strategy and experimentation only. `Pegasus.Core` owns business rules, human-approval gates, cases, reports, correspondence, valuation, and release policy.
- A future application integration needs a named capability, accepted contract and change record, actual caller, representative parity/security/licence evidence, migration/coexistence and recovery behavior, and operator acceptance. Until then, keep the seam documentary and the workspace unreferenced.
- Update `workspaces/README.md` provenance and content manifest whenever imported source changes. Never claim an unavailable upstream commit for the document-extraction snapshot.
