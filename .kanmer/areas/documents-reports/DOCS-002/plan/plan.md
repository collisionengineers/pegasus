# Plan — DOCS-002: Record the Web Container App as the integrated renderer execution boundary

## Approach

Write one thin ADR using the next stable ID, ADR-0028, selecting the existing Pegasus Web Container App as the in-process Chromium execution boundary. Update only the ADR index. This records the durable choice without bundling report behavior, implementation, sizing, or deployment claims.

## Governing docs

- **New ADR:** ADR-0028 refines ADR-0015 and ADR-0025 by choosing Web rather than Worker for the integrated renderer. FRD-11 remains the behavior owner.
- **Meets ADR-0025:** uses an existing project/deployment boundary and creates no separate package/service/runtime.

## Steps

1. Add ADR-0028 with required frontmatter and Status, Context, Decision, Consequences, Options considered, and Links sections.
2. Add its accepted row to `docs/adr/README.md` and validate IDs/frontmatter/links.
3. Link ADR-0028 to DOCS-002 and TICK-215, then record docs-only simplification and verification evidence.

## Verification

Run `git diff --check`, focused frontmatter/index/link checks, and inspect the two-file diff. Proof after merge confirms the files and links on merged `dev`.

## Risks / open questions

- Risk: behavior leaks into ADR. Mitigation: keep readiness, failure, identity, and correction rules in FRD-11.
- No open operator question.
