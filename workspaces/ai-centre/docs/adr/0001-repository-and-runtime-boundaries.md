# ADR 0001: Repository and runtime boundaries

- **Status:** Superseded for Pegasus integration by root ADR-0009
- **Date:** 21 July 2026

This imported decision records the source repository's former proposal only. It cannot create a
Pegasus runtime, caller, package owner, data boundary, or top-level application.
## Decision

Pegasus retains its existing application boundary: `Pegasus.Core` owns business policy;
Infrastructure implements Core ports; Web and Worker are the composition roots. This source
workspace may independently maintain Collision Brain and bounded AI evaluation/training tools, but
none is an application caller until a separately accepted root contract activates one.

The dependency direction proposed by this imported ADR is therefore not active. Any future AI
adapter must consume a Core port and cannot introduce a desktop, case-domain package, report
renderer, audit model, or connector owner here.

Private corpus inputs remain outside Git history under the root repository's ignored, immutable
`corpus/ai-centre/` boundary. Git may contain code, schemas, manifests, model cards, synthetic
fixtures, evaluation definitions, and immutable artifact references—not private source archives
or binary model payloads.

## Consequences

- Collision Brain remains a source-workspace service with no Pegasus caller.
- Production business skills remain source packs, not autonomous callers.
- Cross-boundary schemas require a separately accepted root owner, caller, versioning, and
  migration decision before activation.
