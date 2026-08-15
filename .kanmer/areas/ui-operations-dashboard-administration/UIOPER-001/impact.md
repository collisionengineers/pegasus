# Impact: UIOPER-001 Remove self referential dashboard link

## Touched Files
- `src/Pegasus.Web/Pages/Index.cshtml`: Remove the `.drilldowns` navigation element.
- `src/Pegasus.Web/wwwroot/css/site.css`: Remove unused `.drilldowns` styling.

## Downstream Impact
- Navigating to Operations remains fully supported through the primary header navigation (`_Layout.cshtml`).
- No backend logic or data contracts are affected.
- Test suites continue to pass.
