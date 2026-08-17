# Plan — TICK-009: MAIL-21 classification foundation

*The plan. Not the checklist — this is the **reasoning**; the checklist is the executable distillation of it.*

Written FROM the ticket's `research` and `files` documents — if either is missing or stale, fix that first.

## Approach

Keep the existing QDOS v3 policy and persistence. Close the remaining MAIL-21 *local* evidence gap: the cohort harness only recognises a labelled `extraction-corpus` / `emailevals` tree, so this machine's flat `corpus/*.eml` dump never runs. Teach volume discovery to include the corpus root when it contains `.eml` files, skip labelled accuracy when those folders are absent, run the volume cohort read-only, and write a content-safe dated observation. Do not add predicates, deploy, or invent labels. Alternatives rejected: (1) re-implement the policy — already on `dev`; (2) restructure `corpus/` — immutable; (3) claim acceptance from volume counts — that is the INT-21-style labelled+holdout gate, parked.

## Governing docs

**Required.** How this plan meets each linked PRD/FRD/ADR (`refs`). For each:
- **Meets** — which requirement/acceptance-criterion each step satisfies; or
- **Modifies** (only with explicit user authorization) — what changes in the doc and why; or
- **New ADR** — the design decision this introduces, written via `kanmer-docs` and linked.

`kanmer-review` checks this section holds against the diff.

- **FRD-08** (`docs/frd/frd-08-email-mailbox-and-background-processing.md`) — **Meets** the QDOS classification policy already recorded (versioned rules, per-message predicate evidence, explicit ambiguity, fail-closed unclassified). This ticket does not change that contract. It supplies the missing *acceptance-cohort evidence state* the capability row still names, as a local volume run that records exact outcome counts without inventing a winner or a destination.
- **ADR-0008** (not in `refs`, cited for the cohort requirement) — **Meets** "a route is activated only when genuine evidence establishes its predicates and an acceptance cohort proves positive, negative, ambiguous, retry, and version-pinning behavior" at the *local volume* tier only. Version-pinning and retry remain the existing persist/replay tests; this ticket does not weaken them.
- No FRD/ADR is modified. No new ADR.

## Steps

1. In `QdosEmailCohortTests`, treat `corpus/` itself as a volume root when it contains `*.eml` files (top-level or nested). Keep the existing labelled and `emailevals` paths when those directories exist.
2. Make `QdosCorpus.IsPresent` true when any of those roots exist and contain at least one `.eml`.
3. Skip `LabelledWorkTypeEmailsNeverMisclassifyAcrossFamilies` and `LabelledClaimTokensNeverCollideAcrossCaseFolders` when no labelled folder exists (do not fail on `processed == 0`).
4. Keep volume-cohort assertions as exact counts only; still write `artifacts/evaluation/qdos-classification/cohort-results.csv`.
5. Run `QdosEmailCohortTests` locally against this machine's corpus. Confirm labelled tests skip and volume test processes the flat dump.
6. Append a dated, content-safe observation to `docs/operations.md` § Dated evidence qualifications (counts and outcome tallies; no filenames or PII). Qualify: this machine, this layout, not operator acceptance, not deployment, not live verification.
7. Update the MAIL-21 activation note in `docs/capabilities.md` so it names local volume-cohort evidence separately from labelled holdout, deployment, and live verification.
8. Run focused tests: `QdosMailClassificationPolicyTests`, `QdosEmailCohortTests`, and `ProcessIntakeTests` classification facts.

## Verification

- Focused `dotnet test` on the three test classes above (`Release`).
- Volume cohort `processed > 0` on this machine; labelled facts skipped.
- `operations.md` observation contains only counts.
- No `corpus/` writes; no policy-version bump.

## Risks / open questions

- Risk: a machine with both a flat dump *and* `emailevals` could double-count. Mitigation: volume roots stay a distinct list; if the corpus root is already covered by a more specific tree, do not add it twice (skip the root when a known subtree exists, or enumerate unique paths).
- Risk: reviewers treat volume counts as acceptance. Mitigation: operations note states the opposite.
- Open questions resolved in `open-questions` (no deploy, no invented labels, no new predicates).
