# Research: UIOPER-001 Remove self referential dashboard link

## Investigation
- On `src/Pegasus.Web/Pages/Index.cshtml`, lines 114-119 define:
  ```html
  <nav class="drilldowns" aria-label="Other operations views">
      <a asp-page="/Operations">
          Open Operations
          <svg class="icon icon--sm" aria-hidden="true"><use href="#icon-chevron-right" /></svg>
      </a>
  </nav>
  ```
- Razor Pages tag helper requires `asp-page="/Operations/Index"` to resolve to `/Operations`. With `asp-page="/Operations"`, it fails to resolve and generates a self-referential link (`href=""` / `/`).
- Operations is already accessible in the main navigation header (`_Layout.cshtml`).
- The drilldown link is redundant and broken.

## Conclusion
Remove the `<nav class="drilldowns">` element from `Index.cshtml`.
