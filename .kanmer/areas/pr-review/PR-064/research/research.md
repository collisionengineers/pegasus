# Research — PR-064: remaining Test UI fidelity contradictions

## Question

What exact corrections are needed for the two reviewed semantic contradictions on [[PR-063]], and what is the smallest validator change that prevents an image element with no usable source from passing?

## Verified findings

Read-only inspection used the exact PR-063 head `1cd0c4c1` on `task/pr-063-default-fidelity`.

- `docs/design/test-ui/index.html` claims the organization edit default is a “Loaded organization with no principals or roles.” Its linked page renders organization `Web Caller Provider`, active principal `WEBP`, and a checked Work Provider role. The current Razor view has mutually exclusive empty/populated principal branches and binds both role checkboxes from the loaded organization. The page markup is a valid populated Work Provider branch; the inventory sentence is false.
- The vehicle-image detail inventory claims an awaiting-instruction record “with registered images.” Its page renders an Images section and `<img loading="lazy" alt="vehicle-front.jpg">` without `src`. Current Razor renders the gallery only when `Model.Images.Count > 0`; choosing the no-images branch permits removing the gallery without inventing or reusing unrelated evidence.
- `scripts/Test-UiCatalogue.ps1` only extracts attributes matching `href|src="nonempty"`. An `img` with absent `src` or `src=""` therefore produces no reference to validate and passes.
- Existing validator structure already scans every catalogue HTML file. A focused second regex pass over `img` start tags can require exactly one non-whitespace `src` without introducing a parser, framework, parameter, or new test project.
- The existing PR-063 and UIIMP-002 evidence claims that all 39 defaults were corrected are disproved by these two reviewed states. Their Kanmer checklist/report text must be amended to state the corrected rerun, not silently retain the earlier absolute claim.
- PR-063’s branch/worktree is current and available, and its open PR is #557 targeting the UIIMP-002 parent. PR-064 can safely branch from exact PR-063 head and target `task/pr-063-default-fidelity`.

## Implications

Keep the valid organization page markup and correct only its canonical branch claim. Select and accurately name the no-images vehicle branch, remove the invalid gallery, and add absent/blank image-source validation to the existing script. Prove both negative variants by temporarily creating invalid catalogue markup during verification and confirming the validator fails, then restore the tracked file. Re-run the complete 39-default inventory audit and correct upstream Kanmer evidence.

## Open questions

None. Deployment is `n/a`; the user explicitly authorized full completion without live deployment.
