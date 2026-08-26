# Files — PR-063

## Change files

| Path | Change and risk |
|---|---|
| `docs/design/test-ui/index.html` | Add a reviewable branch claim to every visual state. Risk: duplicating branch ownership; mitigate by keeping it in the existing canonical inventory. |
| `docs/design/test-ui/pages/*.html` | Correct all 39 default prototypes to valid current rendered branches and exact defining interactions/copy. Risk: broad hand-edited static fidelity; validate every inventory mapping. |
| `scripts/Test-UiCatalogue.ps1` | Require branch evidence for each visual state and keep existing route/link/isolation checks. It cannot automate semantic fidelity, so do not claim that it does. |
| `docs/design/README.md` | Clarify that each state names its selected Razor branch and that branch fidelity is manually reviewed. |
| [[UIIMP-002]] checklist and post-implementation report | Correct whitespace and fidelity claims with rerun evidence. These are Kanmer documents, not repository files. |

## Context files

| Path | Why it matters |
|---|---|
| `src/Pegasus.Web/Pages/**/*.cshtml` and `*.cshtml.cs` | Current route markup, mutually exclusive branches, action gates and state vocabulary. |
| `src/Pegasus.Web/Pages/Shared/_Layout*.cshtml` | Exact authenticated, auth and external shell structure. |
| `src/Pegasus.Web/Pages/Shared/*.cshtml` and `src/Pegasus.Web/Pages/Cases/Shared/*.cshtml` | Defining partial-rendered fields, outcomes, summaries and controls. |
| `src/Pegasus.Web/wwwroot/css/site.css` | Existing styles; no parallel stylesheet is permitted. |
| `docs/frd/frd-12-operator-experience.md` | Binding state/accessibility/responsive vocabulary. |
| `docs/design/README.md` | UI source/runtime/evidence boundary and page-economy rules. |
| PR #556 / `task/uiimp-002-test-ui` | Parent implementation branch; PR-063 must be stacked into it rather than conflict with an unrelated product branch. |

## Ripple effects

PR-063 blocks [[UIIMP-002]]. The correction PR should target `task/uiimp-002-test-ui`; after it is reviewed and merged there, PR #556 receives the fixes without changing `dev` independently. No runtime caller, build artifact or deployment changes.

## Out of scope

Live Razor behavior, business policy, server-side form execution, new fixtures, a generator/framework, deployment, and reviewing or merging this ticket's own PR.
