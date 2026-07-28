# Brand style

## Purpose and governed surfaces

This governs the internal Collision Engineers case-management Web UI by adapting
the shared essentials from the provided `collision-engineers-design-dev`
foundation. It does not copy the marketing website or document system and does
not treat candidate rasters/current Development proof as approval of a `0.1.0-alpha.1` shell.

## Principles

- Operational, restrained, desktop-first, and border-led rather than decorative.
- White/light-neutral ground, warm-charcoal/near-black text/navigation, and Collision red used sparingly for primary action, active navigation, focus, and urgent emphasis.
- Product states remain distinct: amber incomplete/pending, restrained navy Review, and green only for confirmed completion; these are Pegasus semantics, not imported marketing rules.
- Sharp 2px corners, rare soft shadows, system UI text, and Lucide icons only.

## Voice and UI language

Use Collision Engineers' settled terms and concise controls. `Audit`, `Triage`,
`Needs sorting`, and `Blocked intake` retain their exact meanings. Controls and
labels communicate purpose without narrating obvious actions. Do not expose
Azure, OCR, AI, queue mechanics, extraction engines, or deployment terminology.

## Source and runtime mapping

Upstream evidence is the provided `collision-engineers-design-dev` bundle;
adaptation decisions live in this directory and planned interaction authority in
`design/product/requirements.md`. Current exercised CSS/layout live in
`src/Pegasus.Web/wwwroot/css/site.css` and
`src/Pegasus.Web/Pages/Shared/_Layout.cshtml`; they have not yet adopted
the approved logo and exact adapted tokens.
