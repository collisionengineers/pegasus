# 0010 — Accept/suppress Scriban security advisories

## Status

Accepted

## Context

The templating decision (ADR 0004) uses Scriban for body templates. The Scriban package
carries NuGet security advisories (NU1901–NU1904), which surface as build warnings. Advisories
of this kind are most relevant when a template engine is used to compile **untrusted,
end-user-authored** templates at runtime, which is the scenario that makes template injection
dangerous. The question is whether that threat model applies here.

## Decision

**Accept and suppress** the Scriban advisories (NU1901–NU1904) for this solution. The
suppression is applied centrally in `Directory.Build.props`
(`<NoWarn>$(NoWarn);CS1591;NU1901;NU1902;NU1903;NU1904</NoWarn>`), with a comment recording
the rationale. The justification is that this product's threat model differs from the
advisories' concern:

- Templates are **first-party embedded artifacts** (the `.scriban` files shipped in
  `CollisionRenderer.Core`), never authored by end users at runtime.
- All payload data is **HTML-encoded and passed as values**, never compiled as template
  source — so user input cannot become executable template logic.

## Consequences

- The build is clean and not blocked by advisories that do not apply to how Scriban is used
  here; the suppression is centralised and documented rather than scattered.
- The decision is explicit and auditable: the rationale lives both in this ADR and in the
  `Directory.Build.props` comment.
- The suppression is **contingent on the threat model holding**. If the product ever allowed
  end-user-authored templates, or compiled user input as template source, this decision must
  be revisited and the advisories re-evaluated.
- Suppressing by advisory ID (rather than blanket-ignoring all warnings) keeps the scope
  narrow to the known Scriban items.

## Alternatives considered

- **Leave the warnings unsuppressed:** noisy on every build and obscures genuinely new
  warnings, without changing the (non-applicable) risk. Rejected.
- **Replace Scriban to avoid the advisories:** would discard the designer-editable,
  Jinja2-like body authoring chosen in ADR 0004 for an issue that does not apply to first-party
  templates with encoded data. Rejected.
- **Pin/patch to a fixed Scriban version:** adopted as standard hygiene where a fixed version
  exists, but it does not change this decision: the advisories are accepted because the usage
  is safe by design, and suppression keeps the build clean regardless.
