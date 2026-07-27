# Engineering guardrails

Use the [architecture index](../architecture/README.md) and its ADRs for
topology. These rules keep an implementation discoverable and prevent another
copy of business behaviour.

1. Trace the real caller before changing a callee, and exercise a genuine input
   through that entry point before widening a capability.
2. Keep business policy in one Core use case, with Web and Worker boundaries
   translating requests or events only. Cross-feature work goes through the
   target feature's named use case or query, not another feature's tables.
3. Keep one implementation of a business rule, classifier, allocator, parser,
   workflow transition, or external effect. A third copy stops delivery until
   consolidation; migrate or delete the replaced path, registration, tests, and
   documentation in the same bounded slice.
4. Organise by business capability. Do not add horizontal `Common`, `Helpers`,
   `Utilities`, or undifferentiated `Services` folders, or names such as `Next`/`unallocated`,
   `New`, `Manager`, `Helper`, or `Util` to justify another layer.
5. Add an interface or abstraction only for a real external boundary, two
   concrete callers or implementations, or an accepted architecture decision.
   Do not leave dormant registrations, feature flags, endpoints, placeholders,
   or speculative compatibility shims.
6. Make classifier and extraction precedence explicit. Model terminal,
   transient, and unknown outcomes separately; do not turn exceptions into
   business truth.
7. Use purpose-revealing names and Collision Engineers' business language.
   `Audit` and `Triage` have their reserved meanings; operator UI does not
   expose internal deployment or extraction mechanics.
8. Use Roslyn navigation for semantic C# and Razor questions, but confirm
   important results in source and with build, tests, and caller evidence. If
   it is unavailable, report the limitation rather than treating text search as
   a full reference set.
9. Add a guard only for an owner, concrete failure mode, negative fixture, and
   demonstrated failure. Keep the verification ladder small: focused checks,
   real-path evidence, then independent review.
10. Registration, file presence, mocks, repository consistency, or deployment
    alone never prove production behaviour. Keep status prose concise rather
    than generating a second status ledger.
11. Use real-shaped local data early; controlled synthetic fixtures cover edge
    cases after genuine inputs establish the operational shape.
12. Dry-run and enumerate exact targets before destructive or broad operations.

The predecessor evidence behind these rules is summarised in
`legacy-failure-prevention.md`.
