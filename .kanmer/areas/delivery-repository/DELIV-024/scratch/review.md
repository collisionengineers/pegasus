# Independent docs-only review — PR #555 / DELIV-024 — 2026-08-26

## Findings

1. **BLOCKING — operations does not record the exact source SHA or image digest required by the ticket.** The new release row stores only `cfb3e6cf…` and `sha256:bac866ee…`, and the evidence paragraph shortens them again. Those prefixes agree with the supplied evidence, but they are not the exact deployed values `cfb3e6cfd838dfdcf7ffa64aa9164bfdc2bc9223` and `sha256:bac866eeb11215c2b0dbaf949e769280aefef246c34f6cbf9436d28a486274bf`. The plan and ticket explicitly require exact values.

2. **BLOCKING — current architecture remains stale after the release and incorrectly says the topology is unchanged.** The deployed release changed the ordinary intake route from “Worker dispatcher publishes the staged receipt id” to immediate post-commit publication by the committing Web/Worker caller, with the Worker timer retained only for recovery. `docs/current-architecture.md` still states the old timer/Worker-dispatch route at lines 193-203 and changes only the release-number sentence to say topology was rechecked and unchanged. Repository safety rails require the as-built current architecture to match the deployed reality in the same release task.

## Evidence that does match

- Web revision is exactly `pegasus-prod-web-252ow37gij--cfb3e6cfd838`.
- Migration head is explicitly unchanged at `20260825145216_MailboxImageIntake`.
- The release evidence states strict source/version smoke passed, all exact nine Worker functions are enabled, `PendingWorkRecoverySchedule` is exactly `0 * * * * *`, the sole healthy revision has 100% traffic, and the immutable digest read-back matched.
- Operator acceptance is not overclaimed: both documents retain “operator acceptance remains outstanding,” and operations explicitly says the release does not prove fresh mailbox/manual-upload speed or displayed states.
- Docs-only simplification is honest and proportional: only the two canonical current-state documents are changed, with no duplicate/new Markdown file.

## Verdict

**FAIL / NEEDS CHANGES.** Record the full exact SHA and digest in the release-32 evidence, and update the stale current intake flow to immediate commit-then-publish plus Worker recovery before claiming release-32 topology verification. No implementation edit or merge was performed.
