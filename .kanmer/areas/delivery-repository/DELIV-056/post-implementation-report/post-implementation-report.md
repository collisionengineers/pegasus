# Post-implementation report — DELIV-056

## Outcome

The bounded D56 fixture correction is implemented across the final ten test files. It replaces stale QDOS route/body-token fixtures with actual definitive PDF instruction evidence, retains the original behavioral assertions, and records the two narrow amendments exposed by focused verification. No production policy, schema, runtime route, helper framework, dependency, documentation, corpus, commit, push, PR, merge, release or deployment changed.

## Changed files

| Path | Implemented result |
|---|---|
| `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` | Reuses one shared definitive QDOS PDF builder for the corrected transport fixtures. |
| `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` | Uses real attached documents for custody; separates truthful pre-classification reevaluation sources; counts attachment custody effects; gives each Audit a real notification plus original report; uses automatically recorded Audit evidence and its `ReceiptVersion` for acceptance. |
| `tests/Pegasus.IntegrationTests/InstructionDraftWebTests.cs` | Routes controlled fixture content through the actual PDF and proves two receipt identities share one case with one allocation and exact event types. |
| `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs` | Converts affected QDOS case-seed evidence to the real document path. |
| `tests/Pegasus.IntegrationTests/ImageViewingWebTests.cs` | Converts the image-viewing case seed to definitive document evidence. |
| `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs` | Converts affected mailbox intake fixtures to retained formal instruction evidence. |
| `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs` | Aligns format-path fixtures with the document-backed intake contract. |
| `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs` | Aligns QDOS/Triage fixtures with the current document evidence contract. |
| `tests/Pegasus.IntegrationTests/RecoveryTests.cs` | Aligns recovery fixture input with definitive instruction evidence. |
| `tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs` | Uses the real accepted-case seed and proves an inaccessible case has neither the Send-to-Claude dialog nor its interactive launcher while the POST remains 404. |

## Verification

- Current static check: `git diff --check` passed (exit 0; only repository LF-to-CRLF advisories).
- Current full binary diff hash: `bdbe80e82a6569a4a79ee57f44998f98f679eef2`; the final change set is 294 insertions / 108 deletions across exactly the ten paths above.
- Designated host incremental Release build at 2026-09-08T15:59:40Z passed, exit 0, with 0 warnings and 0 errors.
- The seven-method focused run at hash `bfa477f890e1da5fb570c0bdecfd0825459a3f7c` passed five unchanged-source methods, including the Send-to-Claude negative launcher assertion, and failed only the two Audit version-token methods. Its failure record is retained in `scratch/verify.md@7fc07a84d76567c5`.
- The exact two-method Audit delta at hash `bdbe80e82a6569a4a79ee57f44998f98f679eef2` passed 2/2, exit 0; TRX SHA-256: `A44267FEB86853AEBD7039F53D2231B10CD51C86211476075DAA7957830B69D7`.
- Together, every named D56 correction method has focused PASS evidence at its applicable frozen diff. This is not a broad-suite PASS. The original 269-selected / 7-failed broad attempt, the earlier exact-six 4-pass / 2-fail attempt, and the exact-seven 5-pass / 2-fail attempt remain preserved and are not represented as passing runs.

## Governing documents

- `docs/frd/frd-02-intake-and-source-identity.md`: satisfied by exercising genuine retained instruction evidence, preserving truthful pre-case material, automatic evidence-backed Audit acceptance, and idempotent one-case allocation proof.
- `docs/engineering.md`: satisfied by serialized designated-host builds/tests, exact frozen-diff evidence, and honest retention of every non-PASS result.

## Unresolved / out of scope

`UploadConfirmation` Attach and browser case-search assumptions remain unresolved current-contract work owned by [[INTK-066]], pending the user decision. They were not changed, rerun, or claimed as passing. No later D56 action absorbs that decision.

## Handoff

The implementation is ready for root publication review only. Do not commit, push, open a PR, merge, move stages, or run additional verification without the next explicit authorization.
