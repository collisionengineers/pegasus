# Files — PR-064

## Change files

| Path | Change and risk |
|---|---|
| `docs/design/test-ui/index.html` | Correct the two canonical branch claims. Risk: a wording-only claim can still drift; compare each linked page to its Razor condition. |
| `docs/design/test-ui/pages/vehicle-images-details--default.html` | Remove the image-only section to select the valid no-images branch. Risk: retaining any image-only markup would keep the contradiction. |
| `scripts/Test-UiCatalogue.ps1` | Reject every catalogue `img` whose `src` is absent, empty, or whitespace. Risk: false positives on valid attribute order/casing; use the existing case-insensitive regex approach. |
| [[PR-063]] checklist and post-implementation report | Supersede the false absolute fidelity claim with PR-064’s corrected rerun. |
| [[UIIMP-002]] checklist and post-implementation report | Carry the same truthful upstream evidence correction. |

## Context files

| Path | Why it matters |
|---|---|
| `src/Pegasus.Web/Pages/Administration/Organizations/Edit.cshtml` and `.cshtml.cs` | Own populated/empty principal and selected-role branches. |
| `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml` and `.cshtml.cs` | Own the `Model.Images.Count > 0` gallery branch and awaiting-instruction controls. |
| `docs/design/test-ui/index.html` | Sole route/state/branch inventory. |
| `scripts/Test-UiCatalogue.ps1` | Existing structural validator to strengthen rather than replace. |
| `docs/frd/frd-12-operator-experience.md` | Governs truthful states and evidence distinctions. |
| PR #557 / `task/pr-063-default-fidelity` | Exact stacked base and target for this correction. |

## Ripple effects

PR-064 blocks [[PR-063]], which blocks [[UIIMP-002]]. The correction must land into the PR-063 branch so PR #557 becomes truthful before its own review continues. There is no application caller, release input, runtime, or deployment change.

## Out of scope

Live Razor changes, new evidence images, a generic HTML parser/test framework, route redesign, browser-side business behavior, deployment, self-review, or merge.
