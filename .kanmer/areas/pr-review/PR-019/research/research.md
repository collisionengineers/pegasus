# Research — PR-019

Whitespace is already normalized to no search. Overlong terms pass the page into Core and throw `ArgumentException`, which the page currently maps to 404 via its broad argument catch; the supported user response should instead render validation/status on the page. Empty retained results do not distinguish an active filter. Add page-owned 200-character validation and an explicit no-match branch while preserving GET scope. Source: `Index.cshtml.cs`, `Index.cshtml`, `RetainedMail.cs`.
