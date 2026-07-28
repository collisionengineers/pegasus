# 0002 — Modular shared Core with thin CLI/GUI/API clients

## Status

Accepted

## Context

The same documents must be produced from three very different surfaces: a command-line tool
for power users and automation, a Windows desktop application for non-technical staff, and a
cloud API for integration. If each surface owned its own rendering logic, they would drift —
a template, a density rule or a validation check fixed in one would be missed in another, and
output would differ depending on how a document was generated. For documents that go to
court, that divergence is unacceptable.

## Decision

Concentrate all rendering behaviour in one class library, `CollisionRenderer.Core`, and make
the CLI, GUI and API **thin clients** over it. Every host builds its renderer through the
single composition root, `CollisionRendererFactory`:

- `CollisionRendererFactory.CreateRenderer(IPdfEngine? engine = null)` returns the shared
  `IDocumentRenderer` pipeline (catalog → `HtmlComposer` → `PayloadValidator` →
  `ChromiumPdfEngine`).
- `CollisionRendererFactory.Catalog` exposes the shared `ITemplateCatalog`.

`CollisionRenderer.Cli` (assembly `collisionrenderer`), `CollisionRenderer.Api` and
`CollisionRenderer.Gui` contain only host concerns — argument parsing, HTTP endpoints, UI —
and delegate all document work to Core.

## Consequences

- **Parity by construction:** because every host constructs the renderer identically through
  one factory, the CLI, GUI and API cannot diverge in templates, validation, density or
  output. A change in Core reaches all three at once.
- A single, well-tested surface to maintain; host projects stay small and focused.
- Hosts cannot quietly special-case behaviour — any rendering change must go through Core,
  which is the intended constraint.
- Core must remain free of host-specific (notably Windows-only) dependencies so that the API
  and container can use it; this discipline is enforced and recorded separately.

## Alternatives considered

- **Duplicate rendering logic per host:** fastest to start, but guarantees drift and
  contradicts the core requirement of identical output. Rejected.
- **Shared code by copy/paste or a loose "utils" assembly:** no single composition root, so
  hosts could still wire the pipeline differently. Rejected in favour of one factory.
- **A rendering microservice that the desktop and CLI also call:** would force a network
  dependency on the offline desktop scenario; the GUI instead hosts Core in-process. Rejected.
