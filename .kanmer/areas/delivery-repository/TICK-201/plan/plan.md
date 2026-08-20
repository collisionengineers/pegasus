## Plan — TICK-201: correct canonical documentation claims

### Governing docs

Pure documentation correction against `docs/index.md`'s own authority-routing
rules — no new product behaviour, capability, or durable technical decision to
record, so nothing to add to a PRD/FRD/ADR. `docs_todo: true` is set instead of
a governing-doc ref, matching the precedent set by TICK-197/TICK-199 (same
area, same profile, same reasoning).

### Approach

The ticket body names no specific claims (see research.md). Per the run's
targeted-pass instruction, audited `docs/current-architecture.md` and
`docs/operations.md`'s factual claims against the read-only estate facts in
`prod-diagnostics.md` (2026-08-20). Found and corrected one genuine,
well-evidenced error: `docs/operations.md`'s "Approved Box custody root"
section claimed secrets resolve "only inside the Worker," directly
contradicting the same document's own Secrets record 200 lines later (both
hosts resolve their own Box secrets since release 3) and the live diagnostics
(Web has exactly two Key Vault secret references, matching the doc's own "two
Web" grant count). Everything else checked against the diagnostics — release
facts, Box custody root, image-intake/Box boundary, VRM acceptance threshold —
was already accurate; no further changes made. Newly observed live defects
(VRM group fan-out, Unidentified items never closing, Not-Ready badge/list
mismatch, email counter, Worker SIGABRT/PollSentEvidence rejection) are not
contradicted documentation claims — no doc asserts that behaviour works — so
per the ticket's own scope (doc-claim correction, not bug-fixing) they are
recorded in research.md as candidates for a separate bug ticket rather than
papered into the architecture/operations docs.

### Steps

1. Read `docs/current-architecture.md` and `docs/operations.md` in full;
   cross-check factual claims against `prod-diagnostics.md`.
2. Correct the one found contradiction in `docs/operations.md` (Box secret
   resolution scope), minimal edit, preserving surrounding wording and voice.
3. Run `./scripts/Test-DocumentationLinks.ps1` and
   `./scripts/Test-TestMarkdownPlacement.ps1` (the CI `documentation` job's own
   two steps) to confirm the edit and its new same-file anchor reference are
   clean.
4. Commit, push, open PR (docs-only — the `documentation` CI job is the
   relevant gate).

### Verification

- `./scripts/Test-DocumentationLinks.ps1` passes: "All relative Markdown links
  resolve (205 files checked)."
- `./scripts/Test-TestMarkdownPlacement.ps1` passes: "Markdown placement
  regression tests passed."
- `git diff` limited to the one corrected paragraph in `docs/operations.md`;
  release table untouched.
- No `docs/operator-notes.md` meaning changed; nothing parked as an open
  question.

### Simplification pass — 2026-08-20

n/a — docs-only, single-paragraph factual correction, no code touched.
