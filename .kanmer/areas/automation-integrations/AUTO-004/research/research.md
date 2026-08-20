# Research — AUTO-004: Automation Actor access to Unidentified material

## Question

Why can the Automation Actor not reach Unidentified material, does that reveal duplicated staff/automation implementations or other unreachable approved functions, and what exact correction belongs in AUTO-004?

## Findings

- FRD-10 already requires Automation to list and look up Unidentified items by exact U-reference and permits resolution through the same Core command as Web. FRD-02 separately requires the original source bytes, receipt identity, custody, and group membership to remain available. Sources: `docs/frd/frd-10-mcp-automation-and-actor-boundary.md`, `docs/frd/frd-02-intake-and-source-identity.md`.
- `src/Pegasus.Web/Mcp/UnidentifiedMcpTools.cs` already defines `pegasus_unidentified_list`, `pegasus_unidentified_get`, and `pegasus_unidentified_resolve`, but `AutomationMcpExtensions.AddPegasusAutomationMcp` registers only `CaseMcpTools`, `IntakeMcpTools`, `DocumentMcpTools`, and `AssessmentMcpTools`. Therefore none of the Unidentified tools is discoverable or callable through `/mcp`.
- The approved-inventory test encodes the same defect. `AutomationMcpIngressTests.ExpectedTools` omits all three Unidentified tools, so `tools/list` passes precisely because the runtime and the expected list agree on the incomplete inventory. There is no HTTP success, denial, validation, or action-history test for an Unidentified tool.
- A repository-wide registration census found five classes marked `[McpServerToolType]`; four are registered and only `UnidentifiedMcpTools` is orphaned. There is no second orphaned implemented tool class.
- Registration alone would not satisfy the requested use case. `pegasus_unidentified_get` returns the Unidentified aggregate and history only. It does not return the retained receipt evidence and it cannot download the original material. Staff detail uses the existing Core `IGetIntake` query for a Receipt origin, and the staff source endpoint uses `IDownloadIntakeSource` for integrity-checked bytes. Both Core use cases authorize `ActionActor.Automation` through the existing `PerformCasework` boundary, so no new business implementation or authorization model is needed.
- Submission-group origins need a plural projection. The existing `IIntakeSubmissionGroupStore` can load the group and its members; each member retains its own receipt/file identity. Treating a group as one receipt would violate FRD-02. The actor result must enumerate members and retrieve an exact member, with the same bounded inline-content convention already used by `DocumentMcpTools`.
- Staff and Automation do share the mutation owner: both `Unidentified/Details.cshtml.cs` and `UnidentifiedMcpTools.ResolveAsync` call `IResolveUnidentified`. Reads are less clean: both callers access `IUnidentifiedStore` directly and independently shape their result, while only Web enriches it through `IGetIntake`. This is not a second business-policy implementation, but it is duplicated caller orchestration and caused the observable parity drift.
- The other currently committed Automation Actor surfaces are registered: Case, intake submission/list, case-document, and assessment tools all appear in composition and the exact `tools/list` inventory. No other missing registration was found within MCP-01–04/MCP-06. The broader classified-mail gap is already tracked by [[AUTO-003]] and is intentionally waiting for its Core owners.
- Triage is entirely unreachable through MCP: there is no Triage tool class, registration, inventory entry, or FRD-10 Triage contract. That is not an Unidentified defect—Triage is a distinct pre-case workflow with different states and completion evidence. It has been split to [[AUTO-005]] so AUTO-004 does not silently broaden actor authority or collapse the two workflows.
- History explains how the defect escaped:
  - Commit `abd8a923` introduced the whole Unidentified aggregate, Web surface, FRD text, migration, and the MCP class in one 49-file intake change, but did not touch MCP composition or Automation integration tests.
  - Commit `94f99b95` corrected two local methods inside the orphan class, still without proving an HTTP caller.
  - INTK-007's report claimed “MCP lookup/resolution” and called the aggregate reachable while its verification hand-off still asked for future MCP exercise.
  - TICK-025 later closed retrospectively by checking that `UnidentifiedMcpTools.cs` existed at production SHA `2325ed4a`; its own inventory test expected only the 15 registered non-Unidentified tools. This confused source presence with runtime exposure despite FRD-10 explicitly saying an endpoint file or registration is not proof.
- The design did not create separate staff and automation business policy. The failure is composition/proof drift plus a too-thin Automation read projection. The existing exact inventory test is useful, but its expected list was manually curated from the registration rather than derived from the governing capability, so it could ratify an omission.

## Implications

AUTO-004 should expand beyond “add a retrieval tool.” Its bounded implementation scope is:

1. Register the existing `UnidentifiedMcpTools` class and add its three existing names to the approved inventory.
2. Make exact U-reference detail expose the same retained receipt/group context that staff uses, through existing Core/store ports.
3. Add an exact retained-source retrieval operation for a Receipt origin and exact-member retrieval for a SubmissionGroup origin, reusing `IDownloadIntakeSource`, existing group membership, Automation actor attribution, `automation.intake` scope, integrity checks, and the existing bounded-inline response convention.
4. Keep `IResolveUnidentified` as the one mutation owner; do not copy validation, version, target, reference, or idempotency rules into MCP.
5. Add real `/mcp` tests for discovery, success, wrong scope, invalid reference/member/version, history attribution, receipt content, grouped-member selection, and integrity failure. The inventory assertion must explicitly include the governed tools.
6. Reconcile `docs/capabilities.md`, `docs/current-architecture.md`, and `docs/operations.md` claims only when caller evidence exists; source-file presence is not exposure or deployment proof.

Do not add a generic material abstraction or merge Case documents, intake sources, Unidentified, and Triage into one tool. The existing ports and typed tool classes are sufficient. Triage remains [[AUTO-005]]; classified mail remains [[AUTO-003]].

## Open questions

None for AUTO-004. Exact wire naming and response records are planning details constrained by the existing typed-tool and bounded-inline conventions. Triage authority is deliberately separated to [[AUTO-005]].

## Parity correction — 2026-08-20

The operator clarified that [[AUTO-005]] is in the same task/worktree and that staff/Automation capability parity is already policy. The governing ADR review confirms this:

- ADR-0011 requires MCP tools to call the same Core use cases as Web/Worker and forbids a second policy engine.
- ADR-0021 grants `ActorKind.Automation` exactly `PerformCasework` and records the requirement for a comprehensive toolset with logging parity.
- `StaffAuthorization` implements Staff/Automation parity for `PerformCasework`, while retaining explicit denial of staff-application identity, administration, credentials, system work, and request-upload authority.

Therefore the earlier implication that Triage lacked an approved Automation contract was too narrow. AUTO-004 is the umbrella implementation plan for both tickets. It must register/complete Unidentified access and add a separate typed Triage surface over the existing Core queries/commands. Triage remains a distinct domain/tool class, but not a deferred authority decision.

The Triage inventory and exclusions are recorded in [[AUTO-005]] research. Staff “Assign to me” is not transferable because the Automation Actor cannot impersonate a staff GUID; this explicit identity constraint does not weaken ordinary-casework parity.
