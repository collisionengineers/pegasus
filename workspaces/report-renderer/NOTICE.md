# Notices — third-party components

Collision Renderer is an internal Collision Engineers Ltd product. It uses the
following third-party components.

| Component | Purpose | Licence |
|---|---|---|
| [Microsoft.Playwright](https://github.com/microsoft/playwright-dotnet) | Headless-Chromium PDF rendering | Apache-2.0 |
| Chromium (downloaded by Playwright) | Browser engine | BSD-3-Clause and others |
| [Scriban](https://github.com/scriban/scriban) | HTML template engine | BSD-2-Clause |
| .NET 8 / ASP.NET Core | Runtime and web host | MIT |
| xUnit | Test framework | Apache-2.0 |
| Liberation / DejaVu fonts (container only) | Arial-metric body text on Linux | OFL / GPL+exception |

## Brand assets

- The master gear-"C" logo (`design/brand/logos/logo_no_margin.png`) and the
  engineer signatures (`design/brand/signatures/`) are **Collision Engineers Ltd**
  property. Do not redraw the gear.
- The client-supplied **Tw Cen MT Std** and **Futura** typefaces (and the white
  reverse logo) were removed from the repo in July 2026 as unused — no template
  referenced them. Rendered documents use Arial (Windows) or a metric-compatible
  substitute (Liberation Sans) in the Linux container, so no proprietary font data
  ships inside generated PDFs. Keep it that way unless a font licence is confirmed
  to permit embedding.

## Reference material

`documentexamples/` and `stylexamples/` hold real customer reports (vehicle
registrations, claim details). `collision-engineers-design-dev/` and
`report-renderer/` are prior-art/design references. None of these four folders is
product source; if present locally they are **git-ignored** and must never be
committed or redistributed. See `docs/adr/0009-reference-material-handling.md`.

## Security note

The Scriban advisories (NU1901–NU1904) are suppressed at build time. Templates are
first-party, embedded artifacts and are never authored by end users at runtime; all
payload data is HTML-encoded and passed as template *values*, never compiled. See
`docs/adr/0010-accept-scriban-security-advisories.md`.
