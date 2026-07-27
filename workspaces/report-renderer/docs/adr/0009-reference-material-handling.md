# 0009 — Local reference material is git-ignored, never committed

## Status

Accepted

## Context

Development and design work sometimes needs local reference material: actual customer reports
that show the house style in practice, styling references, the CSS-native design-system source,
and the prior Python/WeasyPrint renderer. In early development these appeared in four root
folders: `documentexamples/`, `stylexamples/`, `collision-engineers-design-dev/` and
`report-renderer/`.

`documentexamples/` and `stylexamples/` contain genuine customer data — vehicle plates, claim
details and other personally identifiable information (PII) — and large document binders. The
design-system and prior-renderer folders are not PII by themselves, but they are still reference
inputs rather than product source. None of the four folders is required by the build.

## Decision

Treat all four folders as **local reference material only** and **git-ignore them so they are
never committed**. A clean checkout does not contain them. If a developer keeps them locally,
they should live outside the repo root; the `.gitignore` and `.dockerignore` entries remain as
defensive guards if the folders are copied into the working tree.

They are not inputs to the build: the engine is self-contained via embedded resources (ADR 0004),
so nothing in the build depends on these folders being present.

## Consequences

- No customer PII is committed to the repository or its history, satisfying data-protection
  obligations.
- The build does not depend on these folders, so clones and CI work without them.
- Reference material can remain available to developers locally for fidelity work, preferably
  outside the repo root, while staying out of source control and container build contexts.
- The exclusion relies on `.gitignore` discipline; contributors must not force-add ignored paths,
  and review should guard against accidental inclusion of customer data or prior-art source.

## Alternatives considered

- **Commit the examples for convenience:** would place PII in Git history permanently and is
  unacceptable on data-protection grounds. Rejected outright.
- **Commit redacted/synthetic copies instead:** useful in principle, but redaction is error-prone
  for binders of real reports and risks leaking residual PII; the canonical sample payloads
  needed for tests are instead embedded as sanitised samples in Core. Rejected for the reference
  folders, which stay local and ignored.
- **Vendor the design-system and prior-renderer folders:** would blur product source with prior
  art and make the root harder to understand. The required assets and CSS have already been
  brought into Core deliberately. Rejected.
- **Store the material in a separate private repository or secure store:** appropriate if the
  reference set becomes operationally important, but heavier than needed for local design
  reference. For now, keep it out of this repository and rely on ignored local storage.
