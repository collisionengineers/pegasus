# Checklist — <ticket id>

*One independently tickable box per ordered plan step or acceptance check. Remove examples that are not applicable; append progress notes rather than rewriting.*

- [ ] [pre-review] Name the production caller, registration, route, or composition entry when applicable.
- [ ] [pre-review] Prove required runtime dependencies ship in the artifact when applicable.
- [ ] [pre-review] For schema work, prove migration, grants/bootstrap, runtime role, and rollback handling when applicable.
- [ ] [pre-review] Run exact tests/commands without weakening assertions.
- [ ] [post-merge] Verify the merged result and generated artifacts when applicable.
- [ ] [pre-review] Stop at the approved boundary; do not merge or start another ticket.

`[pre-review]` and `[post-merge]` are plain-text labels for humans and skills. Current gates ignore these labels; use `get_doc_gates` for live gate behaviour.

## Progress notes

Append with `set_ticket_doc(doc: "checklist", append: true)`.
