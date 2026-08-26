# Razor Pages implementation patterns

Use this reference to resolve a real implementation choice, not as a checklist for every page.

| Need | Usually consider | Key distinction |
| --- | --- | --- |
| Shared document shell | Layout | Owns common structure; `RenderBody` and optional sections accept page content. |
| Shared directives | `_ViewImports.cshtml` | Centralizes namespaces and Tag Helper imports. |
| Shared startup choice | `_ViewStart.cshtml` | Runs before full pages/views; partials do not run it. |
| Repeated prepared markup | Partial view | Caller supplies the model/data; keep business and data-access logic outside. |
| Repeated UI with its own rendering/data work | View Component | Renders a fragment and can receive dependencies; it is not an HTTP endpoint. |
| HTML-oriented server behavior | Tag Helper | Enhances or generates elements while keeping markup readable. |
| Cross-project packaged UI | Razor Class Library | Can carry pages, views, View Components, models, and static web assets. |

For forms, use the Form Tag Helper and model binding conventions, render labels and validation messages, inspect `ModelState`, and redirect after successful state-changing posts when the application's flow calls for it. Do not disable antiforgery as a styling convenience.

CSS isolation is available for Razor pages and views, but use it only when it fits the project's styling organization. A single established stylesheet may be clearer for a small or deliberately shared design system.

## Microsoft references

- [Razor Pages architecture and concepts](https://learn.microsoft.com/aspnet/core/razor-pages/?view=aspnetcore-10.0)
- [Layouts](https://learn.microsoft.com/aspnet/core/mvc/views/layout?view=aspnetcore-10.0)
- [Partial views](https://learn.microsoft.com/aspnet/core/mvc/views/partial?view=aspnetcore-10.0)
- [View Components](https://learn.microsoft.com/aspnet/core/mvc/views/view-components?view=aspnetcore-10.0)
- [Tag Helpers](https://learn.microsoft.com/aspnet/core/mvc/views/tag-helpers/intro?view=aspnetcore-10.0)
- [Forms and form Tag Helpers](https://learn.microsoft.com/aspnet/core/mvc/views/working-with-forms?view=aspnetcore-10.0)
- [Model validation](https://learn.microsoft.com/aspnet/core/mvc/models/validation?view=aspnetcore-10.0)
- [Reusable UI with Razor Class Libraries](https://learn.microsoft.com/aspnet/core/razor-pages/ui-class?view=aspnetcore-10.0)
