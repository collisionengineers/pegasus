# Engineering guardrails

These rules are deliberately short. Each addresses a failure observed in the predecessor.

1. Trace the real caller before changing a callee.
2. Put a new capability through a genuine input and actual entry point before widening it.
3. Keep one implementation of each business rule. A third copy stops delivery until consolidation.
4. Make precedence explicit for classifiers and extraction: higher-authority evidence wins predictably.
5. Model terminal, transient, and unknown outcomes separately. Do not turn exceptions into business truth.
6. Search before adding. Reuse or delete before suffixing with `V2`, `New`, `Manager`, `Helper`, or `Util`.
7. Add a guard only when it has an owner, a concrete failure mode, a negative fixture, and a watched failure.
8. Keep the verification ladder small: build, focused tests, real-path evidence, independent review.
9. Never claim production readiness from registration, file presence, mocks, repository consistency, or a successful deployment alone.
10. Keep status prose human-sized. Code and live evidence are not to be mirrored into generated ledgers.
11. Use real-shaped local data early. Synthetic fixtures cover edges after genuine inputs establish reality.
12. Dry-run and enumerate exact targets before destructive or broad operations.

The predecessor evidence behind these rules is summarized in `legacy-failure-prevention.md`.
