# Components

| Component | Purpose | Actions/states | Design source | Runtime owner |
| --- | --- | --- | --- | --- |
| Application shell/navigation | identify the app and reach current Development routes | normal, hover, focus; local-intake link conditional | `brand/style.md`, planned `design/product/ui-spec.md` | `src/Pegasus.Web/Pages/Shared/_Layout.cshtml` |
| Queue/metric card | show persisted Development intake counts and open the exact list | value/empty link; planned stale/unavailable remains unimplemented | `foundations/colour.md`, planned UI spec | `src/Pegasus.Web/Pages/Index.cshtml`, `wwwroot/css/site.css` |
| Upload form | submit one supported local source through the real caller | validation, refusal, success | planned UI requirements | `src/Pegasus.Web/Pages/Intake/Upload.cshtml` |
| Intake queue/review | list persisted receipts and inspect source/evidence/draft/assets | filters, empty, failure detail, retained-asset download | planned UI requirements | `src/Pegasus.Web/Pages/Intake/` |

Only exercised components are listed. Planned `0.1.0-alpha.1` contracts remain in the UI
specification and do not create a speculative component library.
