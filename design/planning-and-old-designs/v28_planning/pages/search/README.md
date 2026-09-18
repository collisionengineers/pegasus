# Search

- **Mockup route:** `pegasus_search_operations_v28.html` (`so-area=search`) in [`../../current/`](../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Search/Index.cshtml`, `_CasePreview.cshtml`

- [**How it works**](how-it-works.md)

## Screenshots

- [s01-search-results-1580.png](../../current/v28-shots/s01-search-results-1580.png) · [1440](../../current/v28-shots/s01-search-results-1440.png) · [760](../../current/v28-shots/s01-search-results-760.png)
- [s02-search-vehicle-images-1580.png](../../current/v28-shots/s02-search-vehicle-images-1580.png) · [1440](../../current/v28-shots/s02-search-vehicle-images-1440.png) · [760](../../current/v28-shots/s02-search-vehicle-images-760.png)
- [s03-search-unavailable-1580.png](../../current/v28-shots/s03-search-unavailable-1580.png) · [1440](../../current/v28-shots/s03-search-unavailable-1440.png) · [760](../../current/v28-shots/s03-search-unavailable-760.png)

## Notes

The mockup keeps the "Selected Case" preview swap static (clicking another
result row shows a demo toast rather than actually swapping the preview
panel's content) — the live page swaps `_CasePreview` client-side per row via
a `<template>` on each `<tr>`. This is a deliberate mockup simplification, not
a capture of different behaviour; the panel's *content* for the selected row
is otherwise a faithful transcription.
