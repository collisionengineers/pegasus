# Checklist — UIIMP-012

*One independently tickable box per ordered plan step or acceptance check. Remove examples that are not applicable; append progress notes rather than rewriting.*

- [ ] Step 1 — Append `public static class TriageRecord { public const string NotesPanel = "Notes"; }` with its EPIC-011 §1.5 / D25 summary to the end of `src/Pegasus.Web/Presentation/OperatorLabels.cs`, reordering no existing member.
- [ ] Step 2 — Render `@OperatorLabels.TriageRecord.NotesPanel` in the `<h2 id="triage-history-title">` of `src/Pegasus.Web/Pages/Triage/Details.cshtml` and replace the stale divergence comment with one line; leave the section, id, `aria-labelledby`, classes and entries unchanged.
- [ ] Step 3 — In `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs` (line 477) assert `<h2 id="triage-history-title">Notes</h2>` and add `Assert.DoesNotContain("Permanent history", …)`; leave the surrounding assertions alone.
- [ ] Step 4 — Runner regenerates `docs/design/test-ui/` and the implementer commits exactly what the script writes, in the same change set as the page; the Triage artifact is expected byte-identical.
- [ ] [pre-review] `dotnet build ./Pegasus.slnx --configuration Release --no-restore` succeeds with 0 errors.
- [ ] [pre-review] `git grep -n "Permanent history" -- src tests` returns only `tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs:432`.
- [ ] [pre-review] The label string exists exactly once: no literal "Notes" heading remains in markup.
- [ ] [pre-review] Production caller named: the routed page `/Triage/{id:guid}` is the only caller of the new label; no registration, route or DI change.
- [ ] [pre-review] Runner's evidence recorded with exact commands and exit codes: Core tests, the `QdosTriageIntegrationTests` filter, the Browser lane, the capture, `-Verify -SkipCapture`, and the catalogue check — lanes needing LocalDB marked `NOT_APPLICABLE (CI-evidenced at <head sha>)`, never `PASS`.
- [ ] [pre-review] No governing document is in the diff (`docs/design/README.md`, `docs/frd/**`, `docs/adr/**` untouched — DELIV-040 / PR #643 owns them); no file outside Expected files is touched.
- [ ] [pre-review] Simplification pass run over the branch diff and recorded in `plan` under a dated `### <YYYY-MM-DD>` heading, with dispositions for any unapplied finding.
- [ ] [pre-review] PR opened to `dev` titled `Rename the Triage history panel to "Notes" and narrow D7 to uncomposed integrations (UIIMP-012)` with the footer `Kanmer: UIIMP-012`; ticket moved implementing → review.
- [ ] [pre-review] Stop at that boundary: do not merge, do not start INTK-054.
- [ ] [post-merge] CI's Test UI lane and the merged `dev` build are green; `docs/design/test-ui/` matches a fresh capture.

`[pre-review]` and `[post-merge]` are plain-text labels for humans and skills. Current gates ignore these labels; use `get_doc_gates` for live gate behaviour.

## Progress notes

Append with `set_ticket_doc(doc: "checklist", append: true)`.
