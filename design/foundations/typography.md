# Typography

## Type roles and hierarchy

Use a neutral system UI sans for all application text, compact uppercase
eyebrows where useful, strong queue/metric values, and semantic heading
hierarchy. Planned body text remains 14–16px. Upstream Tw Cen/Futura files are
marketing/logo/document faces and are not needed in this internal application.

## Font sources

System stack only: `ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto,
"Helvetica Neue", Arial, sans-serif`, adapted from the upstream `--font-web`.

## Runtime consumers

All current Razor Pages use `src/Pegasus.Web/wwwroot/css/site.css`; its
shorter system fallback remains compatible. No brand font bundle is copied or
loaded.
