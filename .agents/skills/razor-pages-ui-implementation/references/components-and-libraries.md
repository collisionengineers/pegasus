# UI components and libraries

Read this when a task involves reusable UI, a design system, Bootstrap, Web Components, a commercial suite, NuGet UI, LibMan, npm, CDN assets, or a new frontend dependency.

## First inventory the project

Identify the existing component patterns, design tokens, CSS and JavaScript organization, dependencies, package/acquisition tools, static-asset setup, browser support, and tests. Prefer the established route when it satisfies the requirement.

## Choose by need

Consider, from least to most additional surface:

1. native HTML plus existing project styles;
2. an existing local component or pattern;
3. a small new partial, View Component, Tag Helper, CSS rule, or script;
4. an existing project UI library;
5. a new external component or library when its current benefit justifies dependency and integration cost;
6. an RCL when reuse across .NET projects is a real requirement.

This is not a mandatory sequence. A mature library may be the simplest answer for a complex, well-supported need.

## Evaluate a candidate

- Does it solve the actual workflows and states, not just render attractive examples?
- Does its interaction model fit server-rendered Razor Pages?
- What JavaScript, CSS, runtime, package, initialization, and build machinery does it add?
- Is keyboard, screen-reader, zoom, forced-colour, reduced-motion, and contrast behavior documented and verified in the intended composition?
- Can it match the project's visual language without brittle overrides?
- What is the payload and effect on loading, rendering, caching, and failure behavior?
- Is it maintained, securely distributed, suitably licensed, and practical to upgrade or remove?
- Will it duplicate or conflict with the existing component system?

Library accessibility claims are useful evidence, not page-level proof. Test the configured component with its real labels, content, validation, surrounding layout, and interaction.

## Integrate deliberately

Use the repository's existing package manager or asset acquisition method. LibMan downloads selected client files but is not a general package manager. RCL static assets are exposed under `_content/{PACKAGE ID}/`. `MapStaticAssets` can fingerprint and compress build-known assets, but it does not replace every transformation or bundling tool.

Avoid casually adding a CDN dependency where the project expects pinned, locally served assets. Avoid copying an entire library when only a small supported subset is needed. Record the version and keep development and production asset paths consistent with the existing application.

## Microsoft references

- [Client-side library acquisition with LibMan](https://learn.microsoft.com/aspnet/core/client-side/libman/?view=aspnetcore-10.0)
- [Static files in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0)
- [Bundling and minification](https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification?view=aspnetcore-10.0)
- [Reusable Razor UI and static assets](https://learn.microsoft.com/aspnet/core/razor-pages/ui-class?view=aspnetcore-10.0)
- [Fluent UI Web Components with ASP.NET](https://learn.microsoft.com/fluent-ui/web-components/integrations/asp-net)
