# 0004 — Templating: Scriban bodies + C# letterhead shell + embedded brand CSS

## Status

Accepted

## Context

The brand's prior renderer used Jinja2 HTML templates, and that authoring style is familiar
and proven for these documents. Two kinds of content must be produced: the **body** of each
document (which varies per template and benefits from a designer-editable, logic-light
template language) and the **letterhead shell** that surrounds every document (the master
gear-"C" logo, the Our/Your Ref/Date block, the centred title and section furniture), which
is highly structured and shared across all templates. The body and shell both need to draw on
the same brand stylesheet, and the engine must be self-contained — no loose files on disk at
render time.

## Decision

Compose each document from three parts:

- **Scriban body templates** — one `.scriban` per template
  (`market_valuation_evidence.scriban`, `advert_evidence_pack.scriban`, `fee_note.scriban`,
  `expert_report.scriban`), close in spirit to the prior Jinja2 templates and editable by a
  designer.
- **A C#-built letterhead shell** — assembled by `HtmlComposer`, which wraps the rendered
  body in the common letterhead, ref block, title and running furniture.
- **Embedded brand CSS** — the canonical top-level stylesheet
  (`docs/design/assets/report-renderer/templates/report.css`) plus the canonical logo and
  signature assets, all linked and embedded in `CollisionRenderer.Core`.

Templates, stylesheet and assets are embedded, so the engine ships as a single self-contained
library. Adding a template means adding a model record, a `.scriban` body, a
`TemplateDescriptor` in `TemplateCatalog`, and a sample JSON — no engine change.

## Consequences

- Bodies stay designer-editable and logic-light, mirroring the familiar Jinja2 approach and
  easing the transition from the prior renderer.
- The shared letterhead lives in one place (`HtmlComposer`), so every document gets identical
  furniture without each `.scriban` repeating it.
- The engine is self-contained: templates, CSS and brand assets are embedded resources, so
  there are no external file dependencies at render time.
- Two authoring surfaces (Scriban for bodies, C# for the shell) must be understood together,
  but each is used where it is strongest.
- Scriban brings package advisories; these are addressed in ADR 0010.

## Alternatives considered

- **Keep Jinja2 / a Python template engine:** would re-introduce the Python runtime and split
  the stack, contradicting ADR 0003. Rejected.
- **Razor (`.cshtml`) for bodies:** powerful, but heavier and less approachable for a
  designer than a small, sandboxed template language, and pulls in more framework surface.
  Rejected for body templates.
- **Build the whole document, including bodies, in C#:** maximal control but discards the
  designer-editable, Jinja2-like body authoring that is a deliberate goal. Rejected; C# is
  used only for the shared shell.
