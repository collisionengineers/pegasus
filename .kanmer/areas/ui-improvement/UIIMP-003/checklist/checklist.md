# Checklist — UIIMP-003

- [ ] Record the user-approved Test UI page/state, target Live route, applicable states, and exact delta after [[UIIMP-002]].
- [ ] Split unrelated approved pages or behaviors into separate linked tickets.
- [ ] Replace the file-map placeholders with exact Razor, shared UI, CSS/JavaScript, and test callers.
- [ ] Implement only the approved delta while preserving authorization, binding, antiforgery, validation, concurrency, redirects, and server-known states.
- [ ] Reuse existing layouts, partials, controls, classes, and progressive behavior; remove superseded markup.
- [ ] Update focused Web/browser tests for behavior, states, accessibility, keyboard/focus, responsive use, zoom, forced colour, and reduced motion.
- [ ] Update durable design documentation only where the approved change requires it.
- [ ] Run and record the required simplification pass over the branch diff.
- [ ] Run canonical restore/build, focused/full tests, browser accessibility checks, and Test UI publish-isolation scan.
- [ ] Compare every accepted Live state with the approved prototype and record results for the post-implementation report and proof.

## Progress notes
