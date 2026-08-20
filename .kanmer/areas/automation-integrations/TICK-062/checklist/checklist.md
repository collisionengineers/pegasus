# Checklist — MCP-05

- [x] Revalidate prerequisites and the exact existing Core/Infrastructure/Web helpers to reuse.
- [x] Implement the minimal Core contract/policy with fail-closed validation. (None needed: the delivered mail use cases already validate and authorize the Automation actor — recorded in the plan deviations.)
- [x] Implement the mailbox-scoped persistence/projection/adapter boundary with idempotency and durable evidence. (None needed: `EfRetainedMailboxMessageStore` already owns it.)
- [x] Wire the real caller without duplicating business rules. (`MailMcpTools` registered in `AutomationMcpExtensions`; shared `MailClassificationSelection` keeps one taxonomy list.)
- [x] Add focused acceptance tests for scope denial, attribution, replay/version parity, read tools and only staff-equivalent delivered mutations. (`AutomationMailIngressTests`.)
- [x] Run `dotnet restore` and `dotnet build --configuration Release`. (0 warnings, 0 errors.)
- [x] Run focused tests and the relevant full suite. (AutomationMailIngressTests 2/2; AutomationMcpIngressTests + MailWorkspaceWebTests 23/23 total.)
- [x] Run and record the four-lens simplification pass. (Plan, dated 2026-08-20.)
- [x] Update governing/current-state documentation only to the evidence tier actually reached. (Assessed: no FRD change — behaviour is unchanged Core behaviour; `docs/capabilities.md` evidence updates only after delivery/merge per the files doc.)
- [x] Write the post-implementation report with commands, results, residual risks and deployment qualification.

- [x] Inventory every user-facing email-workspace option and expose a thin Automation MCP tool over the same Core query/command; record any owning capability not yet landed as a dependency, not an omission. (Delivered surface fully exposed; undelivered inventory recorded as MAIL-capability dependencies in the plan deviations.)
- [x] Prove authorization, confirmation, exact-target, version, idempotency, attribution, failure, recovery, and destructive-action parity for every mutation tool. (One delivered mutation — classification correction — proven in `AutomationMailIngressTests`: scope denial, exact-message target, version conflict, invalid key, unclassified refusal, mcp: operation key, attributed history. No destructive tool exists.)
- [ ] After deployment, run the full inventory through the live Automation MCP client; exercise all reads and execute writes only under each owning MAIL ticket's separately recorded exact-target approval. (Post-deployment; cannot be claimed from local evidence.)
