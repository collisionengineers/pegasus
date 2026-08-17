# 0013 — Upgrade Scriban to 7.2.6; ADR-0010's advisory acceptance is obsolete

## Status

Accepted

## Context

ADR-0010 accepted and suppressed the Scriban NuGet advisories NU1901–NU1904
through a central `NoWarn` in `Directory.Build.props`. Its reasoning was that
the advisories' threat model — untrusted, end-user-authored templates compiled
at runtime — does not describe this product, because templates are first-party
embedded artefacts and payload data is HTML-encoded and passed as values.

That reasoning was sound but its own *Alternatives considered* section records
the hygiene rule it did not exercise: "Pin/patch to a fixed Scriban version:
adopted as standard hygiene where a fixed version exists".

A fixed version exists. Measured in this workspace with
`dotnet list package --vulnerable --include-transitive`:

| Version | Result |
| --- | --- |
| Scriban 5.12.1 (the previous pin) | 14 advisories: 1 Critical, 9 High, 4 Moderate |
| Scriban 7.2.6 | No vulnerable packages in any of the six projects |

The Critical is `GHSA-5wr9-m6jw-xx44` (CVSS 9.1), patched in 7.0.0: a
`TemplateContext` sandbox escape in which type accessors are cached by `Type`
alone, built from the then-current `MemberFilter`, so a reused context with a
later-tightened filter keeps exposing previously hidden members.

That advisory was not obviously inapplicable to this code. `HtmlComposer`
caches parsed templates in a `ConcurrentDictionary` and reuses composition
state across renders, which is the shape the advisory concerns. Reading the
code showed it never constructs a `TemplateContext` and never sets a
`MemberFilter`, so it was not in fact exposed — but establishing that required
reading, not assumption, and the remaining thirteen advisories still stood.

The operator decided on 2026-08-03 that Scriban is upgraded rather than
suppressed.

## Decision

Upgrade `Scriban` from `5.12.1` to `7.2.6` in
`src/CollisionRenderer.Core/CollisionRenderer.Core.csproj`, and remove
`NU1901;NU1902;NU1903;NU1904` from the workspace `Directory.Build.props`
`NoWarn`. `CS1591` is retained; it is unrelated to package advisories.

No package-advisory code is suppressed anywhere in this workspace after this
change — not centrally, not per project, not per item. Restore is left free to
report the next real advisory.

## Scope of supersession

This ADR supersedes **ADR-0010 in its entirety**. ADR-0010's decision was to
accept and suppress advisories against a specific pinned version; that version
is no longer referenced and those advisories no longer appear. There is no
advisory acceptance left to carry forward, so ADR-0010 is obsolete rather than
partially superseded. Its body is not edited.

ADR-0004 (typed model, first-party Scriban body, C#-built common shell) is
**not** superseded. The templating decision is unchanged; only the package
version moves.

The threat-model facts ADR-0010 relied on remain true and remain worth stating,
because they are the reason a future advisory can be assessed rather than
panicked over: templates are first-party embedded artefacts, end users never
author or compile runtime templates, and payload text is HTML-encoded and
passed as values. They are no longer load-bearing for a suppression, because
there is no suppression.

## Consequences

- The workspace reports no vulnerable packages, transitively, across all six
  projects.
- Render output is unchanged. Scriban's entire contribution to this renderer is
  the composed HTML string, so composed-HTML equality is an exact and directly
  attributable parity proof. Every one of the 12 template identifiers was
  composed at every one of the 3 density values, before and after the upgrade,
  and all 36 SHA-256 hashes are identical. This is a stronger proof than
  comparing rasterised PDFs, which would have interposed Chromium.
  Read the 36 honestly: they are 36 composed outputs, not 36 distinct
  documents. Only 14 are distinct, because `HtmlComposer` currently passes the
  density through to `market-valuation-evidence` alone, so the other eleven
  identifiers compose byte-identically at all three densities. All four
  `.scriban` bodies are still exercised, and every one of the 36 composed a
  real document rather than a placeholder fallback.
- No C# change was required. The whole Scriban surface this code uses is
  `Template.Parse`, `Template.HasErrors`, `Template.Messages` and
  `Template.Render(ScriptObject)`, all unchanged across the two major versions.
- No `.scriban` body changed. The four template bodies are governed design
  assets under `docs/design/assets/report-renderer/templates/` and were not touched.
- `TreatWarningsAsErrors` remains `false` in this workspace, so a future
  advisory surfaces as a warning rather than a build failure. A clean build is
  therefore **not** evidence of a clean audit; the evidence is
  `dotnet list package --vulnerable --include-transitive`.
- Because no advisory codes are masked, a later runtime uplift that turns on
  transitive auditing by default will report the full graph honestly. If new
  advisories surface, the correct response is to identify what drags them in
  and take an operator decision — not to restore a blanket suppression.
