# Plan — UIIMP-003: Integrate approved Test UI experiments into Live Razor pages

## Approach

After [[UIIMP-002]] is complete and the user records one approved page/state and exact delta, compare that prototype with the current Razor page, PageModel, shared partials, CSS/JavaScript, and focused tests. Port only the approved presentation change into the existing owners, preserving all server behavior and removing superseded markup. Reuse the installed `razor-pages-ui-design` and `razor-pages-ui-implementation` skills; research found no current need for a generic converter or new skill.

## Governing docs

- **Meets `docs/frd/frd-12-operator-experience.md`** — the selected change must retain exact operator labels and every applicable loading, empty, stale, unavailable, partial, failed, validation, conflict, and access-denied state; preserve keyboard, pointer, screen-reader, 200% zoom, forced-colour, reduced-motion, and responsive behavior; and never present an unproved state as completed.
- The plan does not modify FRD-12. If the selected prototype conflicts with it or changes product behavior, stop and route that decision through `kanmer-docs` before implementation.

## Steps

1. After [[UIIMP-002]], record the user-approved prototype path, target Live route, applicable states, and exact accepted delta in `open-questions`; split unrelated pages or behaviors into separate linked tickets.
2. Replace placeholder paths in `files` with the exact Razor page/PageModel, shared callers, CSS/JavaScript selectors, and focused Web/browser tests; identify existing layout/partial/native-control mechanisms to reuse.
3. Implement only the approved delta in the existing Razor owners. Preserve authorization, Tag Helpers, binding, antiforgery, server validation, entered values, concurrency/version checks, redirects, and truthful server-known states; remove superseded markup and add no runtime Test UI path.
4. Reuse existing CSS/components first. Add narrowly scoped CSS or progressive JavaScript only when the approved behavior requires it, with semantic HTML and native controls preferred; update all known callers of any shared selector or partial.
5. Update focused integration and browser tests for rendered markup, permissions, forms, validation, keyboard/focus, responsive/zoom, forced-colour/reduced-motion behavior, and representative success/failure states.
6. Update `docs/design/README.md` only for durable approved rules or mappings. Run the required simplification pass over the branch diff and record reuse/simplification/efficiency/altitude findings in this plan.
7. Run canonical restore/build, focused/full tests proportionate to the touched route, browser accessibility checks, and a publish scan proving Test UI remains absent; compare the Live page against the approved prototype at each accepted state.

## Verification

Proof binds the approved prototype path and user decision to the exact Live route, test results, browser/accessibility evidence, and before/after visual comparison. It also records that authorization, antiforgery, validation, dynamic behavior, and deployment isolation still pass.

## Risks / open questions

- The selected prototype/delta is intentionally unresolved until [[UIIMP-002]] exists; implementation must not start before it is recorded.
- A prototype may imply business behavior it cannot own; any such change stops for FRD/PRD resolution rather than being inferred from pixels.
- Shared CSS or partial changes can affect many pages; enumerate and test every caller or keep the change page-local.
- A new conversion skill is out of scope unless later repeated evidence shows the two existing Razor skills cannot carry the workflow.
