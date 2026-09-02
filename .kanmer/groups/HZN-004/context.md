# Release 38 production run

[[PLAT-067]] is the sole roster member. Run serially on Windows with PowerShell 7.

The frozen deployable candidate is `0f0e90ae44ffda7339ca2a460310deeb98121afa`. Re-fetch and stop if `origin/dev` differs or if `origin/main` is not its ancestor. The workflow-evidence waiver applies only to direct commits `5a40d15762b83a7c18ab431434cca7eba7b9a030` and `9b8f78a36151313bc6d48625edee7f13a2173127`; it does not waive build, artifact, smoke, or focused verification.

The intake-data wipe must run and be verified before promotion. Its Azure Blob and SQL writes require separate exact-target approval after a fresh dry run. The `dev` to `main` promotion requires fresh literal `MERGE AUTH GRANTED` immediately before the exact-SHA atomic push. Deployment writes require a separate immutable-manifest approval. The release operator must not self-review or self-merge the evidence PR.
