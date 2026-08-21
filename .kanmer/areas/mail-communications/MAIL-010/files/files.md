# Files

Committed in `7198c1c2`.

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Pages/Mail/Index.cshtml` | Three operator-facing sentences removed or trimmed; nothing written to replace them |

Three lines out, one line trimmed. No page model, no test, no CSS.

## Verification method

A scan of `src/Pegasus.Web/Pages/**/*.cshtml` for every word on the closed banned list
(`intake`, `bounded`, `projection`, `lease`, `opaque`, `ingress`, `composed`, `artifact`,
`durable`, `aggregate`, `caller`, `correlation identifier`, `bytes`), filtering out Razor
comments and C# identifiers. Before: one hit in operator-visible text
(`bounded`, `Mail/Index.cshtml:137`). After: none.

The design authority notes this ban "is a review rule, not an automated check — nothing
in CI enforces it today". That is why it shipped, and why the scan was run by hand here.
