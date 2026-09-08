# Plan — DELIV-056: Align intake regression fixtures with definitive instruction evidence

*The plan bounds a fixture-only correction; no runtime policy changes are authorized.*

## Objective

Make the existing D2 regression scenarios provide document evidence that selects the current QDOS instruction profile, so their existing assertions run, and align the one stale pre-handoff Send to Claude display assertion.

## Starting state

PR #700 run `34196369756` at merge SHA `98f4b701b0814900006a424719c17645b7288197` reported the shared fixture root cause. Current source retains legacy QDOS shorthand in the 13 listed tests. Evidence: `research/research.md`@`378643bc5e48766f`, `files/files.md`@`7dd45fe580c77176`.

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md` — **Meets** the requirement that automatic allocation is reached only through a definitive instruction evaluated under the normal policy.
- `docs/engineering.md` — **Meets** the proportionate verification and serialized-host-verifier requirements; no documentation behavior changes.

## Required changes

- In every scoped automatic-allocation fixture, replace only the obsolete shorthand with signature-complete QDOS document evidence: retain `QDOS`, add `Our Client’s Vehicle:` and use `Registration:`, preserving scenario-specific claimant, claim, asset, format, route, lifecycle and assertion data.
- In SendToAI, preserve the current absent-dialog assertion and refused unauthorised POST. Remove only the obsolete expectation that a pre-handoff Send to Claude control renders disabled and gated.
- Do not add a shared fixture helper, modify policy or production code, weaken business assertions, or update unrelated failure cohorts.

## Expected files

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Modify | `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` | Signature-complete custody fixtures. |
| Modify | `tests/Pegasus.IntegrationTests/InstructionDraftWebTests.cs` | Signature-complete draft fixtures. |
| Modify | `tests/Pegasus.IntegrationTests/ImageViewingWebTests.cs` | Signature-complete image viewing fixtures. |
| Modify | `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Signature-complete mail workspace fixture. |
| Modify | `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs` | Signature-complete QDOS Triage fixtures. |
| Modify | `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` | Signature-complete queue fixture. |
| Modify | `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs` | Signature-complete image-intake case seeds. |
| Modify | `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs` | Signature-complete mailbox fixture. |
| Modify | `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs` | Signature-complete multi-format document inputs. |
| Modify | `tests/Pegasus.IntegrationTests/RecoveryTests.cs` | Signature-complete recovery seed. |
| Modify | `tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs` | Signature-complete seed and current display assertion. |
| Modify | `tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs` | Signature-complete upload confirmation fixtures. |
| Modify | `tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs` | Signature-complete render fixture. |

## Do not modify

- `src/**`
- `docs/**`
- `scripts/**`
- `tests/Pegasus.IntegrationTests/RetainedInstructionAnalysisTests.cs`
- Any test file not listed in Expected files.
- `corpus/**`, `artifacts/**`, packages, migration files, generated snapshots, historical worktrees or Kanmer records outside this ticket.

## Constraints

- Reuse the current QDOS signature and the established local evidence shape; do not emulate old behavior.
- Preserve each test's real scenario data and non-fixture assertions. A newly exposed assertion failure is evidence to report, not a reason to weaken a test.
- The parent-designated host verifier owns all build and test commands. This worker performs only static source checks and must not start a build, test, browser, capture, package or deployment action.
- Use the isolated DELIV-056 branch/worktree only after a ready execution packet and ticket lease.

## Ordered steps

### Step 1 — Correct ordinary intake fixture evidence
- Preconditions: A ready DELIV-056 execution packet, isolated recorded worktree and active lease.
- Files: `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs`, `tests/Pegasus.IntegrationTests/InstructionDraftWebTests.cs`, `tests/Pegasus.IntegrationTests/ImageViewingWebTests.cs`, `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs`, `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs`, `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`, `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs`, `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs`, `tests/Pegasus.IntegrationTests/RecoveryTests.cs`, `tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs`, `tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs`.
- Change: Replace only obsolete QDOS shorthand in the existing automatic-allocation inputs with signature-complete QDOS document evidence.
- Preserved behaviour: Existing route, allocation, identity, asset, lifecycle, accessibility, destination and focused-render assertions.
- Forbidden: Policy, helper, product, schema, source-inventory or snapshot changes; changes to the existing Qdos Notes assertion.
- Negative cases: The scenarios must not establish QDOS identity through a filename, sender or a relaxed policy.
- Tests: Existing named test classes in these files.
- Commands: Static scoped source inspection only; host verifier runs the named existing tests after the required build.
- Expected output: No obsolete automatic-allocation shorthand remains in these scoped fixtures.
- Done when: Every Step 1 fixture contains all current required QDOS document signals while retaining its scenario fields.
- Deviation stop: Any need to change another file, a common helper, production policy, or a non-fixture assertion stops work.

### Step 2 — Correct multi-format fixture evidence
- Preconditions: Step 1 complete.
- Files: `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs`.
- Change: Replace obsolete QDOS shorthand in the synthetic DOCX, DOC, MSG, EML, PDF and limit/guard payloads with evidence that survives their format-specific transport and contains the current signature signals.
- Preserved behaviour: Format ingestion, attachment, duplicate, truncation, image-limit and malformed-content assertions.
- Forbidden: New test infrastructure, parser changes, file-format fixture redesign, or corpus use.
- Negative cases: Corrupt/limited inputs must retain their existing negative behavior; only documents intended to allocate gain the required signature.
- Tests: `MultiFormatIntakeWebTests`.
- Commands: Static scoped source inspection only; host verifier runs the existing class selection after the required build.
- Expected output: All automatic-allocation multi-format samples contain the signature, with intentional negative samples remaining negative.
- Done when: Signature substitution is limited to the existing synthetic content bodies.
- Deviation stop: A format parser or shared-fixture change is needed.

### Step 3 — Correct SendToAI seed and stale display expectation
- Preconditions: Steps 1 and 2 complete.
- Files: `tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs`.
- Change: Make the accepted-case seed signature-complete; retain no-dialog and refused-POST coverage, and remove only the expectation for a rendered disabled/gated pre-handoff Send to Claude control.
- Preserved behaviour: Principal/role authorization, inaccessible action refusal, AI job and destination assertions.
- Forbidden: Rendering the action early, changing access policy, or deleting the unauthorized-POST assertion.
- Negative cases: A disallowed user still cannot access the dialog or execute the POST.
- Tests: `SendToAiIntegrationTests`.
- Commands: Static scoped source inspection only; host verifier runs the existing class selection after the required build.
- Expected output: The test asserts the current absent-control boundary and reaches its accepted-case setup.
- Done when: No stale gated-control expectation remains and security assertions are intact.
- Deviation stop: The current page actually renders a control or requires production/UI changes.

## Acceptance checks

- Each modified automatic-allocation fixture carries the three current QDOS signature signals in its actual document content.
- Existing outcome assertions remain semantically intact; none are weakened to hide a setup failure.
- The SendToAI test reflects current absent-control behavior and preserves access-denial coverage.
- The host verifier records the exact existing 13-class selection, required sequential build result, original 44-failure inventory and any genuine newly exposed failure.

## Commands

The parent-designated host verifier, not this implementation worker, runs the required sequential build and the focused existing integration-test selection covering exactly: `CustodyOutboxIntegrationTests`, `InstructionDraftWebTests`, `ImageViewingWebTests`, `MailWorkspaceWebTests`, `QdosTriageIntegrationTests`, `TriageQueuesWebTests`, `ImageIntakeWebTests`, `MailboxIntakeIntegrationTests`, `MultiFormatIntakeWebTests`, `RecoveryTests`, `SendToAiIntegrationTests`, `UploadConfirmationWebTests`, and `TestUiFocusedRenderTests`.

## Failure and deviation rules

Stop and report an unexpected file, required policy/helper/production change, signature ambiguity, conflicting live ownership, or any verification failure. Do not run host-owned commands or conceal failures with altered assertions.

## Stop condition

After the exact fixture-only changes, static scope review, post-implementation report, commit and draft PR to `dev`, hand off to independent review. Do not merge, verify, close out, or start another ticket.
