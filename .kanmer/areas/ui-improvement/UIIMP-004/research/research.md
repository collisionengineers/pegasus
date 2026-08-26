# Research — current parity failure and reusable capture path

## Question

How can Test UI be made an exact, maintainable representation of current Razor output without creating a second UI implementation?

## Verified findings

- `origin/dev` and the shared checkout are at `93060b619ca92c2f6b3675ddba025abb724c0aa1`; the audit compared the current committed catalogue with the same current Razor source.
- The current validator checks route classification, state file presence, branch text, local references, image sources and publish isolation. It never renders Razor or compares DOM.
- Across 60 prototypes there are 95 forms but zero rendered `method` or `action` attributes, 170 controls but only nine names, zero `site.js` references, and zero SVG `<use>` elements.
- Current Live layout renders the Lucide sprite and `site.js`. Current Mail and Upload pages rely on JavaScript data hooks and hidden/live state absent from the prototypes.
- `IntakeWebApplicationFactory` already supplies a deterministic clock, isolated LocalDB, development initialization, integration authentication and repository-owned fixtures.
- `BrowserTestSupport` already exposes the real app through a loopback HTTP host and Playwright Chromium with reduced motion, configurable forced colours and viewport.
- The integration test project already references Playwright and ASP.NET Core MVC Testing; no new project, browser library, renderer or runtime is needed.

## User decisions

- Parity permits only URL rewriting needed for offline assets/navigation.
- Preserve the existing 60 named states rather than default-only or all possible code branches.
- Preserve volatile elements and attributes but normalize opaque request-specific values to stable typed placeholders.

## Implication

The handwritten prototypes must be replaced, not patched. A single scenario manifest must drive real Razor capture, the generated catalogue and drift validation. Normal validation regenerates into a temporary directory and compares; tracked snapshots are updated only by an explicit update command.
