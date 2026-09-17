Full sweep done — three read-only passes (sprint + design tree, docs/reference/skills, source/infra/artifacts/GitHub), with the important claims verified by hand. Nothing was modified. The full detail is in the plan file; here's the consolidated picture.



Headline: docs/open-decisions.md is empty and accurate — but that register only covers product/technical choices. The real open work lives in 1609sprint/, the uncommitted v27 design round, the 11 Sep audit backlog, and five open PRs.



Decisions only you can make



1\. v27 Case-record mockup — nothing approved yet. current/README.md:84 says so, and the whole folder is uncommitted (13 modified + 3 new screenshots). Open sign-offs: A–F (baseline/fixture/scope/branch/build-inputs/reference file), G, G1–G7 (damage selector variant and its seven sub-questions), H (logo), I (nineteen reference switches, each carry/change/reject), I1/I2 (need a settled-rule change: D29/D30, FRD-11/ADR-0050), I3 (ticks/signoff/reportdate, new today), plus the ten-item §14 "needs a decision first" list that conflicts with settled FRD/ADR rules.

2\. Audit prefix ap. → a. (1609sprint/audit-reference-change/): two investigations, no ruling. New-only vs retrospective renumber; retire the legacy Engineer-Finding path; does assessment still gate Audit creation; UI assessment indicator; fixture/historical-doc treatment; replay-fingerprint handling.

3\. Frontend audit "Decisions for Alex" (frontend-code-audit.md:195): H1 (User-role can generate reports via non-page routes — close?), H5 (Triage chaser stops offering under-configured mailboxes — accept?), H2 (approve AutomationScope shape), H4 (may automation null an estimate-owned field?). Note: the actual report was meant to land in artifacts/audits/2026-09-16/ — that directory doesn't exist; the file in 1609sprint/ is an escaped chat export of the plan.

4\. Cost/estate decisions from the 11 Sep audit — all still undecided: D1–D5/D9 savings, App Insights cap, SQL LTR (recommend enable), zone redundancy (recommend decline + record), budget £75/mo vs \~£96–102 actual so the alert fires every month, maxInactiveRevisions, Key Vault secret expiry, cert URI index before next MCP rotation, dead external-work\* queues (ADR-0033), EF retry. Also: Document Intelligence processed 0 pages in 30 days.

5\. Housekeeping calls: where 1609sprint/ lives (commit 9c4c09eb7 put seven .md files on dev that fail the placement gate); .codex/PLAN.md:9 says "not started" while performance-pr-764/PROGRESS.md:7 says "implemented in #764"; authorize the cleanup PR proposed at docs-audit.md:143; reference-evidence/astra\_output/ still says three questions are "awaiting" (they look answered by TRI-01/ACC-08/EXT-04); docs-audit.md:151 has a pasted mid-session exchange in it.



Go-aheads on worked-but-unstarted fixes



\- Email status updating — diagnosed and designed, no branch cut. Side question at report.md:315.

\- Import test and fix — draft-plan.md awaits approval; optional calc.pdf oracle row is operator-owned.

\- 1609sprint/estimate-generator/evaestimate.pdf appeared at 15:17 today with no plan — task not yet written up.

\- PR #764 release needs fresh promotion/Azure approval (PLAN.md step 6).

\- BL-01 (wipe-classification near-miss) / BL-02 (EVA Function has no .Disabled census entry) — approval to schedule.



GitHub state



┌─────────────────────────────────────┬───────────────────────────────────────────────────────────────┐

│                 PR                  │                            Blocker                            │

├─────────────────────────────────────┼───────────────────────────────────────────────────────────────┤

│ #767 case-linking (stacked on #766) │ sql-integration (1)(2) red                                    │

├─────────────────────────────────────┼───────────────────────────────────────────────────────────────┤

│ #766 U50 PDF/image intake           │ documentation job red                                         │

├─────────────────────────────────────┼───────────────────────────────────────────────────────────────┤

│ #765 audit original report (draft)  │ sql-integration (3) red                                       │

├─────────────────────────────────────┼───────────────────────────────────────────────────────────────┤

│ #764 perf first-use                 │ shards cancelled/timed out; performance acceptance still open │

├─────────────────────────────────────┼───────────────────────────────────────────────────────────────┤

│ #731 unidentified next steps        │ no CI run at all, untouched since 12 Sep — close or revive?   │

└─────────────────────────────────────┴───────────────────────────────────────────────────────────────┘



Six-shard CI split has no review/build/run yet and is blocked on a host-slot grant; one grouping question (SendPageRendersItsChoiceInReviewAndWithEngineer Custody vs Workflow).



One security item (act, not decide)



src/Pegasus.Web/appsettings.json:8-14 still ships the claudeuiverification plaintext bootstrap Administrator marked TEMPORARY. The 15 Sep reset removed the live account, but this config recreates it on the next Web start.



Deferred live checks



Release 53 authenticated Work Centre/Case-image check (explicitly skipped 15 Sep); Release 51 MCP client reconnections; several Release 41–49 "remains for Alex" walk-throughs never marked done; leftover pegasus-worktrees/release-e8efb197.



Checked and not open



No TODO/FIXME/Obsolete in src/tests; all \~20 skipped tests are env-gated; workspaces/ is intentional provenance; every superseded ADR has a successor; no draft FRDs; CI has nothing disabled; ci-six-shards/PLAN.md is already approved.

