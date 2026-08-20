## Research — TICK-201: correct canonical documentation claims against source evidence

### Ticket body vs. concrete claims

The ticket body (unchanged since 2026-08-12) states an approach but names no
specific documentation claim. Per the run's lane instructions, this triggers the
targeted pass: check `docs/current-architecture.md` and `docs/operations.md`'s
factual claims against the read-only estate facts in the orchestrator-supplied
`prod-diagnostics.md` (production diagnostics dated 2026-08-20, release 13 =
`2325ed4a`, subscription `e6076573-...`, App Insights not capped), and fix stale
ones. The release table in `docs/operations.md` is historical record and stays
untouched, per the same instruction.

### Checks run (read-only, in worktree `../pegasus-worktrees/tick-201` on `origin/dev`)

Cross-checked, claim by claim, against `prod-diagnostics.md`'s estate facts:

1. **Release/deployment facts** — `docs/operations.md`'s release-13 row
   (`2325ed4a…`, `sha256:7efa46fd…`) matches the diagnostics header exactly.
   Worker-enabled state, Automation MCP evidence, and Key Vault consolidation
   claims all match live facts recorded in the diagnostics (App Insights not
   capped; nine Worker functions enabled). No correction needed.
2. **Box custody root** — `docs/operations.md` claims folder `405543781910`
   ("pegasus") is the production custody root. Diagnostics confirms
   `RootFolderId 405543781910`. Matches.
3. **Box secret resolution — found stale/self-contradictory claim.**
   `docs/operations.md`'s "Approved Box custody root" section (pre-fix) stated:
   *"Secret values remain resolved only inside the Worker through Key Vault
   references."* This directly contradicts the same document's own
   "Production environment § Secrets" record two hundred lines later, which
   states Box was reachable by **both hosts since release 3** and that the Web
   host holds its own `box-config-json`/`box-client-secret` Container Apps
   secrets with **"exactly six Worker and two Web `Key Vault Secrets User`
   grants"**, live-verified 2026-08-04. `prod-diagnostics.md` §5 (Box)
   independently confirms this from the live estate: *"Web secretRefs
   box-config-json/285b5c83…, box-client-secret/34b9ca84… via identity
   pegasus-prod-web-id-252ow37gij"* — exactly two Web secret references,
   matching the doc's own "two Web" grant count. **Corrected**: the claim now
   states both Worker and Web resolve their own copy of the Box secrets
   server-side (Worker via app-setting Key Vault references, Web via Key
   Vault-backed Container Apps secrets), each through its own managed
   identity, linking to the Secrets record for detail. No other wording in
   that paragraph changed.
4. **Worker health / mailbox approval** — diagnostics found a live defect
   (`PollSentEvidence` spuriously rejecting an approved mailbox that matches
   `ApprovedMailboxes` exactly) and an ongoing Functions host crash-loop
   (SIGABRT, 344 aborts/48h). Checked both `docs/current-architecture.md`
   ("Sent-evidence polling remains configuration-driven for one mailbox") and
   `docs/operations.md`'s Worker-health prose: neither claims this poll
   currently succeeds without error, or that the Worker is exception-free —
   the only "crash-loop" text in either doc is the historical release-9 event,
   which resolved when that release's package landed. **This is a live
   operational defect, not a documentation claim contradicted by evidence** —
   no doc asserts the behaviour the diagnostics show is broken. Correcting
   documentation cannot itself fix or disclose an undocumented bug without
   inventing claims the diagnostics don't fully resolve (the diagnostics author
   itself hedges several of these with "likely"/"possibly"). Recording it here
   instead of in the docs, per the ticket's own note: "Documentation
   correctness does not itself prove application callers or live behavior."
   Recommend a separate bug ticket for: VRM group-fan-out inconsistency,
   Unidentified items never closing, the Not-Ready badge/list source mismatch,
   the dashboard email counter counting `manual_upload` receipts, and the
   `PollSentEvidence`/Worker crash-loop pair — none of these are currently
   asserted as working in the two canonical docs, so there is nothing to
   "correct," only new operational facts to eventually add or fix in code.
5. **Image intake / Box** — diagnostics confirms `ImageIntakes` has no custody
   column and images never reach Box (by design, case-scoped custody only).
   Checked both docs for any claim that images get Box custody — none exists.
   No correction needed.
6. **VRM acceptance threshold** — `docs/operations.md`'s dated-evidence
   qualification records the accepted 0.80 confidence bar for **automatic
   image-registration reading** (the specific gate the 2026-08-03 evaluation
   accepted). Diagnostics' VRM group fan-out defect concerns downstream
   **group-propagation** of one confirmed reading across sibling images in the
   same upload batch, a different mechanism the docs do not describe or claim
   correct. Not a contradiction of the threshold claim itself; left as-is.

### Other canonical documents

`docs/index.md`'s routing rules were used to confirm `docs/operations.md` is
the correct owner for the Box-secrets claim (deployed/runtime state) rather
than an ADR or FRD — no ADR or FRD makes a competing claim about Box secret
resolution scope, so nothing else needed a matching edit.

`docs/operator-notes.md` was not touched — no claim reviewed here changes its
meaning; nothing required parking as an open operator question.

### Verification run

- `./scripts/Test-DocumentationLinks.ps1` — "All relative Markdown links
  resolve (205 files checked)."
- `./scripts/Test-TestMarkdownPlacement.ps1` — "Markdown placement regression
  tests passed."
