# Proof

Verified on merged `main` at `7afd18037acfa78927c4b4ffdf8e0f74c7ecc688`.

- PR #541 merged the two required current-state documentation updates.
- `docs/operations.md` records release 28 source `7e9465b0`, image `sha256:08f5f605…`, revision `pegasus-prod-web-252ow37gij--7e9465b00603`, and both applied migrations.
- `docs/current-architecture.md` identifies the single Export implementation as deployed since release 28.
- Production smoke passed for exact version and source SHA.
- The new Web revision was ready and serving 100% traffic; all nine Worker functions were present and enabled.
- Post-migration validation verified 512 catalogued permission/denial rows and 351 effective runtime DML rows.
- Documentation links passed across 197 files, Markdown placement passed, and applicable GitHub checks passed.

Outcome: deployed reality and both required current-state documents agree. No second application deployment was performed for the documentation-only commit.
