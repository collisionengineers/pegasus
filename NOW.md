# NOW — updated 2026-08-02

(Anything here older than 14 days is stale: delete it, don't investigate it.)

## Doing (max 1)

- Decide the Box production custody boundary (open-decisions: enterprise, service user, root folder, File Request template) — blocks Path step 3.

## Next (max 3)

- Assemble the operator-reviewed extraction cohort + untouched holdout and accept the per-field thresholds (open-decisions) — blocks Path step 4.
- Fix `ProjectReferencesFollowTheModularMonolithDirection` backslash parsing so architecture tests also pass on Linux.

## Waiting (max 3 — each line names its unblock condition)

- Isolated RPO/RTO recovery exercise — blocks any second production release ([operations](docs/operations.md#production-recovery)).
- Obsolete predecessor vault purge — platform-scheduled 2026-08-09, no action unless it fails.

## Path (decided 2026-08-02: full QDOS cutover — every new QDOS instruction is worked in Pegasus through to the EVA handoff; EVA keeps engineering and reports)

1. Green `main` (the two failing integration tests above).
2. Approve the Box production custody root — until then production cases cannot file evidence (DOC-01/02).
3. Prove the spine on one genuine QDOS email in production: mailbox intake → custody → extraction draft → principal → Case/PO minted → Box folder (INT-02/08/09/19/22/25, CASE-07, DOC-01/02).
4. Accept extraction thresholds from the reviewed cohort + holdout (INT-21); zero false case creation.
5. Staff review path live: completeness gates and Review/Not ready/Held queues (CASE-13/14/15/16, UI-02/08).
6. EVA bundle from a real case: exact 13-key JSON + images + SHA-256 manifest (EXT-03), first `Sent to Engineer` (CASE-21), operator accepts every field mapping via a real drag-and-drop run.
7. Chasing live: due-by, 7-day chase schedule, copyable chasers (CASE-17/18, MAIL-18).
8. Cutover date: all new QDOS instructions enter Pegasus; watch alerts and telemetry (OPS-07/08) daily for the first week.
9. Record operator acceptance and management approval (OPS-23, OPS-25) — this is what closes `0.1.0-alpha.1`.
10. RPO/RTO recovery exercise (OPS-09) — the gate before any second release.

Explicitly NOT on the path (allocated but non-blocking): MCP-01–04, INT-17 VRM reading, INT-31 upload links, the EVAL evaluator cluster, live DVLA/DVSA adapters (approved replay/`Unavailable` is fine), and MAIL-14/16 report-sent detection (post-report tracking starts manual via CASE-24).

---

Roadmap: [docs/capabilities.md](docs/capabilities.md) · Questions: [docs/open-decisions.md](docs/open-decisions.md) · How-to: [docs/operations.md](docs/operations.md)

Rules: this file is touched only in the commit that starts or finishes a piece of work; the caps are enforced by deleting lines, not growing sections; the date is bumped whenever it is touched. No other file may contain a "current status" or "next steps" section.
