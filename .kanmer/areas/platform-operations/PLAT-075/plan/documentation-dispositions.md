# V1 documentation correction dispositions

Checkpoint 2026-09-07; common A/B/C source and canonical documentation at
`b8d7cd325f7ce76e442706237b8a3424d84accfe`. The local and remote Platform,
Casework and Intake stream refs all resolve to that identity. This refresh
records the source-level disposition of the complete 29-row supplied register.
The canonical corrections are committed; they do not by themselves prove
external acceptance, deployment, operator acceptance, or final validation.
Those claims remain pending where named below. Exact-head CI, remaining corpus
dispositions, integration proof and the formal review attestation are still
required before PLAT-075 closeout. The board remains the authoritative workflow
record. Product source and the canonical FRD/architecture/capability files are
unchanged since the independently reviewed `886f94df9` checkpoint. Subsequent
changes correct test fixtures, bounded CI execution and generated Test UI state
selection. The supplied pinned PNG now has a separate 1 PASS / 0 SKIP result;
the six scan-only MP cases remain INCONCLUSIVE release gates. Current CI run
34126704865 and final integration proof remain pending.

| ID | Canonical files | Disposition |
| --- | --- | --- |
| A01 | AGENTS.md | Landed at `817a961c1`: ordinary workflow, managed block, named integration target, exact proof, no author self-merge or age-based cleanup, and Git/CI conventions are aligned. The explicit v1 execution exception is retained. |
| A02 | CONTEXT.md | Landed at `817a961c1`: global `T-00001`, pre-Case Triage, normal Case/PO linkage, and `Unidentified` vocabulary are corrected. Estimate and working-data acceptance belongs to the B/C combined implementation and is not claimed by this glossary edit. |
| A03 | README.md | Landed at `817a961c1`: the overview describes retired source-import provenance rather than an active workspace architecture. |
| A04 | workspaces/README.md | Landed at `817a961c1`: workspace imports are retired, while their historical evidence remains identified. |
| A05 | docs/engineering.md | Landed at `817a961c1`: DOC/MSG capability and claim procedure are corrected, and the named `NOW.md` operating-index exception replaces the absent-file claim. |
| A06 | .stitch/DESIGN.md | Landed at `817a961c1`: the historical guide is explicitly subordinate to the current design authority and supplied v3 decisions. |
| B01 | docs/current-architecture.md | Source corrections through `886f94df9` separate release-38 deployment from the v1 source assembly and describe DOC/MSG, 44 MCP tools including estimate import, custody, authorized mail attachments, Reply-To, Glass's and per-mailbox Inbox/Sent leases. Earlier A/B/C source audits and later bounded deltas found no unresolved documentation blocker; exact-head CI and the formal final review attestation remain pending. |
| B02 | docs/operations.md | Landed at `817a961c1`: deployed inventory is bound to the read-only 2026-09-06 Web revision/digest and feature evidence, with no v1 deployment claimed. A later deployment still requires a same-release refresh. |
| B03 | docs/runbook.md | Landed at `817a961c1`: runtime Chromium/fonts, mailbox onboarding, durable OAuth certificates, and operator scope are recorded separately from acceptance. Live mailbox/certificate operations remain operator-owned evidence. |
| B04 | docs/capabilities.md | Source corrections through `886f94df9` retain the sole 244-capability registry and remove fixed-signatory, single-mailbox Sent, and undelivered-password-reset claims. Manual-only EVA is explicit. Schedule allocation does not prove deployment or external acceptance. |
| B05 | docs/open-decisions.md | Source corrections landed at `817a961c1`: API, DVLA/DVSA, AI lifecycle, Glass repair-estimate scope, the deployed 0.5 GB/day cap, EVA Unknown handling, and current upload-session decisions are aligned. Exact B/C combined callers, live credentials, workload measurement, and external acceptance remain pending where the register says so. |
| C01 | docs/operator-notes.md | Landed at `817a961c1`: the explicitly authorized 6 September v1 decisions, including Triage and D29/D30 direction, are recorded while earlier operator statements remain historical evidence. Protected operator meaning was not otherwise changed; final combined implementation proof remains pending. |
| C02 | docs/frd/frd-03-triage.md | Canonical correction landed at `817a961c1`: global T reference and missing-registration `Unidentified` routing are stated without collapsing Audit, Triage, or Blocked intake. C implementation exists on its owner branch; reviewed combined proof remains pending. |
| C03 | docs/frd/frd-06-vehicle-and-engineering-evidence.md | Landed at `817a961c1`: DVLA/DVSA selection/composition, missing fields, and the distinction between source, caller, credential, and live proof are explicit. Live provider acceptance remains pending. |
| C04 | docs/frd/frd-07-eva-and-external-engineering-handoff.md | `71923b255` and `ffa20fbf0` align the approved manual-only EVA target and ADR-0038: successful first handoff advances the recorded state, staff resend is explicit, and Unknown is never automatically retried. Automatic-submission settings and claims are superseded. Local caller verification and live EVA acceptance remain distinct. |
| C05 | docs/frd/frd-09-provider-and-intermediary-routes.md | Landed at `817a961c1`: the accepted Provider API contract and actual QDOS route v4/classification v5 source versions are recorded. Named client credentials, rollout, and live caller acceptance remain pending. |
| C06 | docs/frd/frd-12-operator-experience.md | Source corrections through `886f94df9` state current Case sections, lease behavior, upload sessions/finalisation, grouped uploads, labels, Administration, authorized attachment selection, and notification-query failure isolation. Related FRD-11 states unchanged persisted Accepted/Superseded raw and printed totals. The 69-state Test UI catalogue is regenerated with authorized and current named states; exact-head CI remains pending. |
| C07 | docs/principal-rules-and-mappings/qdos.md; docs/frd/frd-01-case-identity-and-lifecycle.md | Landed at `817a961c1`: missing standalone Audit evidence withholds only the later Audit reference and does not block an otherwise eligible normal Case/PO allocation. Combined custody/allocation proof remains an implementation gate, not a prose gap. |
| D01 | docs/adr/README.md | The stable index and supersession chains are retained, including the subsequent ADR-0037 Linux release-terminal decision and ADR-0038 manual-only EVA decision. Historical decisions are not presented as current behavior. Final link/placement checks remain required. |
| D02 | docs/adr/0002-dotnet-modular-monolith-on-azure.md | Landed at `817a961c1`: historical hosting currency and supersession links are explicit without rewriting the decision record. |
| D03 | docs/adr/0004-provider-api-and-staff-mcp-authentication.md | Landed at `817a961c1`: authentication status and current successors are corrected while the stable ID/history remain. |
| D04 | docs/adr/0007-direct-terminal-azure-deployment.md | The historical Windows-only release decision is explicitly superseded by ADR-0037's accepted Linux x64 release-terminal boundary. This closeout performs Windows development validation only and makes no release or deployment claim. |
| D05 | docs/adr/0011-restrict-mcp-to-automation-actor.md; docs/adr/0014-local-to-production-deployment.md; docs/adr/0015-host-web-on-container-apps-consumption.md | Landed at `817a961c1`: actor, deployment, and hosting decisions name their accepted successors without presenting historical choices as current behavior. |
| E01 | .zcode/plans/plan-sess_18294ee2-592f-4647-910c-54c5a5e0a0ab.md | Landed at `817a961c1`: the historical development plan is marked subordinate and non-runnable. |
| E02 | .zcode/plans/plan-sess_485ec446-cbcd-46a2-bb5a-511a9255e9c5.md | Landed at `817a961c1`: the historical development plan is marked subordinate and non-runnable. |
| F01 | .agents/skills/kanmer-docs/SKILL.md; .grok/skills/kanmer-docs/SKILL.md; .opencode/skills/kanmer-docs/SKILL.md | Landed at `817a961c1`: all three installed Pegasus copies remove foreign paths/history and fixed npm gates. Upstream Kanmer publication remains a separate owner follow-up and is not claimed complete here. |
| F02 | .agents/skills/kanmer-docs/assets/doc-structure.md; .grok/skills/kanmer-docs/assets/doc-structure.md; .opencode/skills/kanmer-docs/assets/doc-structure.md | Landed at `817a961c1`: all three installed templates distinguish configured integration proof from deployed evidence. |
| F03 | .agents/skills/kanmer-execute/assets/proof-template.md; .agents/skills/kanmer-execute/assets/proof-test-template.md; .agents/skills/kanmer-execute/assets/proof-visual-template.md; .grok/skills/kanmer-execute/assets/proof-template.md; .grok/skills/kanmer-execute/assets/proof-test-template.md; .grok/skills/kanmer-execute/assets/proof-visual-template.md; .opencode/skills/kanmer-execute/assets/proof-template.md; .opencode/skills/kanmer-execute/assets/proof-test-template.md; .opencode/skills/kanmer-execute/assets/proof-visual-template.md | Landed at `817a961c1`: all nine proof assets use one attempt-preserving result contract, exact merge identity, and the integration/deployment distinction. |
| F04 | .agents/skills/kanmer-verify/SKILL.md; .grok/skills/kanmer-verify/SKILL.md; .opencode/skills/kanmer-verify/SKILL.md | Landed at `817a961c1`: all three installed verify skills allow only PASS to move Done; waiver remains a truthful non-PASS disposition and terminal retirement remains Verifying. No waiver or stage move is implied. |
