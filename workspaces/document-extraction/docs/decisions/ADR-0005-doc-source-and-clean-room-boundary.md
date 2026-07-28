# ADR-0005: DOC source and clean-room boundary

Status: **Accepted for internal implementation; distribution review open**

Date: 2026-07-24

## Context

Completing legacy Word extraction requires detailed interpretation of MS-DOC and supporting Microsoft Open Specifications. The repository also contains historical evidence that an unidentified read-only implementation was consulted. Unpinned sources, copied parser structure and unreviewed generated tables would make the implementation and its provenance impossible to defend.

## Decision

DOC work is specification-led against the exact revision and SHA-256 ledger in `docs/licensing/doc-source-provenance.json`. Full specification files remain ignored research inputs. Managed source, test vectors and generated metadata must identify the owning specification section and must be independently reviewable without importing implementation source.

Third-party implementations are optional differential oracles only. Before use, record their exact version, licence, acquisition path, invocation and comparator. Do not execute them in production and do not consult their source while designing or implementing the corresponding managed parser unit.

Do not commit Microsoft specification excerpts. Generated tables may be committed only as owned implementation source with the generator, exact input hashes, specification-section mapping and independent review recorded. The unidentified historical secondary implementation is quarantined as non-authoritative until its provenance is reconstructed.

## Consequences

- `DOC-R00` may close for internal research and implementation following the repository owner's 2026-07-24 direction to proceed through `DOC-I13`.
- Research notes can cite section identifiers and describe owned interpretations without reproducing substantial source text.
- Generated metadata needs its own generator, input hash, section mapping and independent review.
- Distribution remains separately blocked until the product licence and notices are authorised.

## Acceptance boundary

The repository-owner direction recorded in the task conversation accepts this technical boundary for internal implementation. A named authorised ownership/licensing reviewer must still approve product licensing, notices, patent treatment and distribution before any release or publication.
