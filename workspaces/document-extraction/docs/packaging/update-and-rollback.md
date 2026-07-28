# Update and rollback policy

Release identity consists of package version, extractor identity, result/bundle schema identities, configuration identity, target framework, dependency-manifest hash and package-manifest hash. Operators must retain these values with extracted evidence.

## Update

1. Build a new candidate into a new immutable version directory; never overwrite a prior candidate.
2. Verify locked restore, build, tests, package contents, hashes and the declared format/security/performance/holdout gates.
3. Compare dependency and schema manifests. Treat any schema, outcome, ordering, stable-identity or default-limit change as a compatibility change requiring explicit review.
4. Deploy beside the current version and direct only an authorised validation cohort to it.
5. Promote only after caller-owned acceptance; do not silently fall back between extractor engines or versions within one operation.

## Rollback

Rollback selects the previously retained, hash-verified framework-dependent package as a whole. Do not mix assemblies from package versions. Preserve inputs and results already produced by the withdrawn version; their recorded extractor/schema/configuration identities remain authoritative provenance and must not be rewritten.

Rollback is required for a corrupted package, signature/hash mismatch, unexpected technical-failure increase, nondeterministic result, resource-bound regression, silent evidence loss or caller acceptance failure. Reprocessing is a separate authorised operation and produces a separately linked derivative.

No database migration or service state is owned by this repository. CollisionSpike owns adapter deployment, traffic selection and business rollback.
