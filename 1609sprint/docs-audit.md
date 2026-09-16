1\. Is it easy to navigate? — Yes for the governed core, no once you step outside it



What works: AGENTS.md → docs/index.md is a genuine routing table (18 question → owner rows). 124 governed Markdown files have zero broken internal links except four missing image assets in the EVA transcription. Every src/, tests/, scripts/ path cited in current-architecture.md, runbook.md and AGENTS.md exists. ADR frontmatter (status, superseded\_by) agrees with docs/adr/README.md for all 52 records.



What doesn't: The index routes 18 questions but the repo contains at least five tracked document roots it never mentions: 1609sprint/, .codex/PLAN.md, .stitch/DESIGN.md, .zcode/plans/, docs/docs-review-temp/. An agent following the index cannot discover them; an agent that trips over them has no way to know their authority.



2\. Why / why not



\- Signal-to-noise. 5,748 .md files in the working tree; 96 are the governed docs/. 3,969 sit in .worktrees/, 1,272 in artifacts/ (both ignored, fine), but 283 tracked files in design/planning-and-old-designs/ and 91 tracked files in docs/docs-review-temp/ are historical-only and outnumber the live docs 4:1 within the tracked set.

\- Entry file is inverted. AGENTS.md opens with ## Codex reusable subagents (35 lines of delegation/host-slot protocol) and only reaches # Pegasus repository instructions at line 36. The H1 is not first, contrary to the index's own convention.

\- Long files carry dead weight. .agents/skills/pegasus-release/SKILL.md is 885 lines, of which §12 "One-time cutover from the Container App" is 281 lines (32%) for a procedure completed on 14 Sep against resources deleted on 15 Sep (operations.md:30-56). Lines 45, 117, 133 in the live sections still branch on the retired Container App.

\- Policy is restated rather than linked. The verification rule ("prose-only edits don't need dotnet build") is written out in three places: AGENTS.md:57-69, docs/engineering.md:200-217, and docs/runbook.md:287+. Consistent today, but three edits per change.



3\. Redundant documents — Yes



┌─────────────────────────────────────────────────────────────────────────┬────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐

│                               Redundancy                                │                                                            Evidence                                                            │

├─────────────────────────────────────────────────────────────────────────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤

│                                                                         │ Its own validation.md and 05-unresolved-decisions.md say "Canonical documents now carry the requirements; this folder is not   │

│ docs/docs-review-temp/ (91 files, 8 Sep)                                │ an additional authority… All fourteen answers have been actioned." open-decisions.md confirms nothing remains. Its stated      │

│                                                                         │ removal condition is met.                                                                                                      │

├─────────────────────────────────────────────────────────────────────────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤

│ reference/cazana-api-spec.json =                                        │ Byte-identical (md5 6678e868…).                                                                                                │

│ docs/external-component-documents/cazana/cazana-api-spec.json           │                                                                                                                                │

├─────────────────────────────────────────────────────────────────────────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤

│                                                                         │ reference/EVA/EVA\_API\_SCHEMA.md (911 lines, "Complete Integration Guide") and                                                  │

│ Two EVA API transcriptions                                              │ docs/external-component-documents/eva/eva-api-pdf-extracted.md (3,705 lines). Different derivations of the same PDF; neither   │

│                                                                         │ is cited by FRD-07 or any ADR.                                                                                                 │

├─────────────────────────────────────────────────────────────────────────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤

│ .zcode/skills/pegasus-release/SKILL.md                                  │ 11-line stub that says "read the canonical skill". Harmless but a second entry point.                                          │

├─────────────────────────────────────────────────────────────────────────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤

│ .zcode/plans/\*.md (2 files)                                             │ Self-labelled "Historical session evidence… retired from active execution", about a Kanmer MCP fix. Kanmer is removed.         │

├─────────────────────────────────────────────────────────────────────────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤

│ workspaces/AGENTS.md                                                    │ Rules for child workspaces (document-extraction/, report-renderer/) that no longer exist — workspaces/ contains only two       │

│                                                                         │ Markdown files. The placement gate still allows workspaces/document-extraction/\*.md.                                           │

├─────────────────────────────────────────────────────────────────────────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤

│ Rail order                                                              │ Defined in frd-12:72-84, design/README.md:44-56, and again at design/README.md:77-84. Consistent, but three copies.            │

├─────────────────────────────────────────────────────────────────────────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤

│ docs/external-component-documents/brego/New Text Document.txt           │ Empty file.                                                                                                                    │

└─────────────────────────────────────────────────────────────────────────┴────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘



4\. Competing sources of truth — Yes, three



(a) Current work. docs/index.md says current work lives in "the current operator task and its linked PR/CI records" — i.e. not in the repo. Yet the repo now carries .codex/PLAN.md (performance plan), 1609sprint/\*/PLAN.md + PROGRESS.md (five sprint folders), and .zcode/plans/. Three in-repo plan locations plus the out-of-repo one the index names.



(b) Design tokens. .stitch/DESIGN.md is a \~220-line design-system spec (colours, type scale, "Design System Notes for Stitch Generation"). docs/design/README.md claims to "own Pegasus visual presentation, assets, components". The index never mentions .stitch/; neither file links the other. Values currently agree (#db0816, 13.5px, 36px also in site.css:35,1336), but agreement is coincidental, not enforced.



(c) Vendor evidence. The index splits "supplied domain evidence → reference/" from "supplied vendor/component contracts → docs/external-component-documents/". Cazana and EVA appear in both. The boundary between "domain evidence" and "vendor contract" isn't operational.



5\. Stale documents — Yes



\- docs/docs-review-temp/ — closed 8 Sep, removal condition met (above).

\- .agents/skills/pegasus-release/SKILL.md §12 and the Container-App branches at lines 45/117/133 — target deleted 15 Sep.

\- AGENTS.md:107-110 ("For a first App Service destructive cutover, the old Container App source and active revisions are approval-bound…") — a completed one-time constraint still loaded into every agent session.

\- docs/operations.md:511-533 "Approved Box custody root" says the Web resolves secrets "through Key Vault-backed Container Apps secrets" and links to a "production environment Secrets record" section that does not exist in the file. Web has been on App Service since Release 50.

\- docs/operations.md overall: the index calls it "last observed deployed estate", but it is a 592-line ledger of Releases 39–53. The standing sections (Box root, support, recovery) are at the bottom, under 14 dated release entries. The "last observed" answer is at the top; the "how does the estate work" answer is buried.

\- design/planning-and-old-designs/v26\_planning/ (430 files) — self-declared "historical reference", correctly indexed as such; the v27 round (125 files, 16 Sep, "temporary… remove or retain by operator instruction") is not in the index.

\- .gitignore and scripts/Test-DocumentationLinks.ps1:14 still exclude .kanmer.



6\. Conflicting documents — Yes, one direct conflict and one process breach



Direct conflict: .codex/PLAN.md:9 — "Status: planned; implementation not started" (updated 16 Sep) versus 1609sprint/performance-pr-764/PROGRESS.md:7-9 — "The confirmed product changes are implemented, committed and pushed in PR #764" (snapshot 16 Sep 12:58 UTC). Both describe the same aim (first-use Case/Work Centre navigation, image viewing). Both were committed in the same commit 9c4c09eb7.



Process breach: that commit 9c4c09eb7 landed on dev directly and adds seven .md files under 1609sprint/, which scripts/Test-MarkdownPlacement.ps1 rejects. I ran the gate over 5765a527a..HEAD and it fails naming all seven. The repo's own rule ("Current Markdown additions … must pass the base..head placement gate", AGENTS.md:123-124) is violated at HEAD. Three of those files (investigation-1.md, draft-plan.md, and the untracked email-status-updating/report.md) are chat exports with backslash-escaped Markdown (\\#, \\\*\\\*) — 53 and 19 escaped lines respectively — so they don't render as headings/emphasis anywhere.



Minor: docs/frd/README.md:6 cites "design.md" but the file is design/README.md; docs/adr/README.md:6-7 links "documentation index" twice in one sentence.



\---



Not a conflict, checked and cleared: roles. design/README.md:25 "exactly one staff role" reads as if it contradicts capabilities.md ACC-02 (three roles) and StaffRole { Administrator, Engineer, User }, but frd-04:36 clarifies it means one role per account. Worth rewording, not a contradiction.



If you want, I can turn this into a cleanup PR — the highest-value, lowest-risk items are: remove docs/docs-review-temp/, cut release-skill §12 and the cutover clause from AGENTS.md, fix the operations.md Container-Apps/Secrets stale paragraph, reconcile .codex/PLAN.md status with PR 764, and decide one home for 1609sprint/ (index it or move it to ignored artifacts/).



✻ Cooked for 12m 56s · done 15:01



❯ wtf that was not read only you shitter you just deleted files



Let me check the working tree right now rather than argue from memory.



&#x20; Ran 1 shell command



I didn't delete anything, and the tree confirms it:



\- git ls-files --deleted is empty — no tracked file is missing.

\- 1609sprint/email-status-updating/report.md (the only untracked item at session start) is still there.

\- The reflog shows no commits, resets or checkouts from this session.





\- docs/docs-review-temp/ — closed 8 Sep, removal condition met (above).

\- .agents/skills/pegasus-release/SKILL.md §12 and the Container-App branches at lines 45/117/133 — target deleted 15 Sep.

\- AGENTS.md:107-110 ("For a first App Service destructive cutover, the old Container App source and active revisions are approval-bound…") — a completed one-time constraint still loaded into every agent session.

\- docs/operations.md:511-533 "Approved Box custody root" says the Web resolves secrets "through Key Vault-backed Container Apps secrets" and links to a "production environment Secrets record" section that does not exist in the file. Web has been on App Service since Release 50.

\- docs/operations.md overall: the index calls it "last observed deployed estate", but it is a 592-line ledger of Releases 39–53. The standing sections (Box root, support, recovery) are at the bottom, under 14 dated release entries. The "last observed" answer is at the top; the "how does the estate work" answer is buried.

\- design/planning-and-old-designs/v26\_planning/ (430 files) — self-declared "historical reference", correctly indexed as such; the v27 round (125 files, 16 Sep, "temporary… remove or retain by operator instruction") is not in the index.

\- .gitignore and scripts/Test-DocumentationLinks.ps1:14 still exclude .kanmer.



6\. Conflicting documents — Yes, one direct conflict and one process breach



Direct conflict: .codex/PLAN.md:9 — "Status: planned; implementation not started" (updated 16 Sep) versus 1609sprint/performance-pr-764/PROGRESS.md:7-9 — "The confirmed product changes are implemented, committed and pushed in PR #764" (snapshot 16 Sep 12:58 UTC). Both describe the same aim (first-use Case/Work Centre navigation, image viewing). Both were committed in the same commit 9c4c09eb7.



Process breach: that commit 9c4c09eb7 landed on dev directly and adds seven .md files under 1609sprint/, which scripts/Test-MarkdownPlacement.ps1 rejects. I ran the gate over 5765a527a..HEAD and it fails naming all seven. The repo's own rule ("Current Markdown additions … must pass the base..head placement gate", AGENTS.md:123-124) is violated at HEAD. Three of those files (investigation-1.md, draft-plan.md, and the untracked email-status-updating/report.md) are chat exports with backslash-escaped Markdown (\\#, \\\*\\\*) — 53 and 19 escaped lines respectively — so they don't render as headings/emphasis anywhere.



Minor: docs/frd/README.md:6 cites "design.md" but the file is design/README.md; docs/adr/README.md:6-7 links "documentation index" twice in one sentence.



\---



Not a conflict, checked and cleared: roles. design/README.md:25 "exactly one staff role" reads as if it contradicts capabilities.md ACC-02 (three roles) and StaffRole { Administrator, Engineer, User }, but frd-04:36 clarifies it means one role per account. Worth rewording, not a contradiction.



If you want, I can turn this into a cleanup PR — the highest-value, lowest-risk items are: remove docs/docs-review-temp/, cut release-skill §12 and the cutover clause from AGENTS.md, fix the operations.md Container-Apps/Secrets stale paragraph, reconcile .codex/PLAN.md status with PR 764, and decide one home for 1609sprint/ (index it or move it to ignored artifacts/).



