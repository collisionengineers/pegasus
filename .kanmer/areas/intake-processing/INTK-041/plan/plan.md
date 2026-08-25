# Plan — define near-real-time two-stage durable intake

## Chosen approach

Replace tight polling as the ordinary scheduling mechanism with two durable, simple triggers: publish each newly committed intake/custody work identifier immediately, and use Microsoft Graph basic notifications to wake mailbox delta processing. Keep existing timers only as slow recovery. This reuses the current Core intake policy, SQL work records, queue identifiers, Worker processing, mailbox cursor, Web host, and Azure resources.

This is smaller and safer than combining the Web, mailbox, extraction, and case-creation functions into one process. Combining them would make an external webhook wait on slow/untrusted document work, weaken durable retry isolation, and duplicate Worker policy. It also avoids an always-ready Function baseline before evidence says cold start is the remaining constraint.

## Governing docs

- `docs/prd/pegasus-product.md`: add the operator outcome and measurable quality/cost boundary without prescribing Azure mechanics.
- `docs/frd/frd-02-intake-and-source-identity.md`: retain its existing durable commit, identifier-only queue, Worker-only processing, idempotency, and fail-closed case rules; add exact trigger, state, recovery, and latency behaviour.
- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: retain approved-mailbox/cursor/classification rules; add callback validation, subscription lifecycle, delta recovery, fallback timing, and truthful sender projection.
- `docs/adr/0032-near-real-time-durable-intake-triggering.md`: record the durable architecture choice. It partially supersedes only ADR-0002's polling and timer-first dispatch choice; all other ADR-0002 decisions remain current.
- `docs/capabilities.md`: register INT-33 and point its canonical owner to FRD-02/FRD-08.

The user explicitly approved modifying these governing documents in the preceding Kanmer plan.

## Ordered steps

1. Create a ticket worktree from `origin/dev` and claim INTK-041.
2. Add the product outcome: truthful near-real-time intake, ordinary p95 <=10 seconds, durable recovery, and measured cost guardrail.
3. Update FRD-02 with the shared two-stage route: durable commit, immediate publication, one-minute recovery, stage telemetry, and truthful Received/Processing/Complete/Failed semantics for e-mail and manual upload.
4. Update FRD-08 with Graph basic notification validation, clientState protection, identifier-only wake messages, SQL subscription state, six-hour maintenance, 48-hour renewal margin, lifecycle/delta recovery, five-minute fallback, and neutral unresolved sender.
5. Add ADR-0032 and minimally annotate/index ADR-0002's partial supersession.
6. Add INT-33 to the capability registry and update navigation only where required.
7. Run Markdown/reference checks, targeted documentation tests if present, and `git diff --check`.
8. Run the required simplification lenses over this docs-only diff and record `n/a — docs-only` plus any scope findings.
9. Write the implementation report, commit, push, open a PR to `dev`, and move INTK-041 to Review.

## Proof

Pre-merge: validate links/frontmatter/index consistency and inspect the focused diff. Independent review checks document authority and that the plan did not smuggle runtime implementation into this ticket. After merge to `dev`, verification records the merged commit and repeats reference checks. Production proof belongs to DELIV-021.

## Risks and mitigations

- **Behaviour leaks into ADR:** keep exact states/timers/acceptance in FRDs; ADR records only the architecture mechanism.
- **ADR-0002 damage:** mark partial supersession precisely instead of rewriting unrelated decisions.
- **Duplicate normative lists:** capability registry remains a join key; timer values and state rules live only in their owning FRDs.
- **Overlap with INTK-040:** this ticket edits governing docs only; it will not touch that ticket's code files.
- **Cloud authorization:** no deployment or Azure/mailbox write is part of this ticket.
