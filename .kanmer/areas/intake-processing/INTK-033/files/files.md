# Files

## Core — policy and contracts

| File | Change |
| --- | --- |
| `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` | Name the triage-request category once: a `TriageRequestSubtype` constant and an `IsTriageRequest` predicate on `MailCategory`, plus one on `MailClassificationResult`. Collapses the two literal copies. |
| `src/Pegasus.Core/Intake/Classification/MailOperationalDestinationPolicy.cs` | Use the named constant instead of its own `"triage-request"` literal. |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | `AssessAsync`: a classified triage request is `NeedsSorting`, not `CaseCreated`, and carries one derived `AcceptedTriageMatch` evidence entry. `IsUnidentifiedEligible`: defer a triage request, exactly as image-only material is deferred. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | `CreateTriageIfQualifyingAsync` reports whether it created a Triage; `SynchronizeUnidentifiedAsync` registers a triage request as Unidentified only when it did not. |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | Delete `IIntakeTriageMatcher`, `NoAcceptedIntakeTriageMatcher`, `IntakeTriageMatch`. |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` | Drop the matcher constructor parameter, the triage evidence loop and `ValidateTriageMatch`. Add the subject registration rule to `SubjectFactLines` and stop the vehicle rule swallowing the `Vehicle Registration` label. Version 5 → 6. |

## Infrastructure

| File | Change |
| --- | --- |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Remove the `IIntakeTriageMatcher` registration and the matcher argument to `QdosInstructionExtractionPolicy`. |

Untouched by design, and that is the point: `TriageLifecycle`, `EfTriageStore`,
`CreateTriageFromIntake`, and the Triage pages already implement the downstream
behaviour correctly. This change makes the evidence they require actually
arrive.

## Tests

| File | Change |
| --- | --- |
| `tests/Pegasus.IntegrationTests/ProductionCompositionTests.cs` | Replace `ProductionProfileKeepsTheTriageMatcherInactive` with a test pinning the active route: production composes the classification policy as the triage trigger and no `IIntakeTriageMatcher` remains. |
| `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs` | The `AcceptedTriageMatchPolicy` stub stops being how triage evidence appears; the real classification path produces it. Rework to drive from a triage-request message. |
| `tests/Pegasus.IntegrationTests/QdosTriageReplayIntegrationTests.cs`, `QdosTriageCaseAssociationIntegrationTests.cs`, `AutomationIntakeParityIngressTests.cs` | Same stub; update to the real path. |
| `tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs` | Subject registration extraction, both spacings; the vehicle-description rule no longer captures the label. |
| `tests/Pegasus.Core.Tests/Intake/Qdos/QdosMailClassificationPolicyTests.cs` | The named triage predicate. |
| New Core tests | A triage request is never `CaseCreated`; it carries exactly one `AcceptedTriageMatch`; with a registration it opens a Triage and registers no Unidentified item; without one it registers Unidentified and opens no Triage. |

## Documents

| File | Change |
| --- | --- |
| `docs/open-decisions.md` | Close the triage-matcher activation paragraph — the predicates it waited on are accepted (`qdos_mail_classification` v4) and the matcher is retired. |
| `docs/frd/frd-03-triage.md` | State the automatic route: an accepted route classification of triage-request opens a Triage when a registration is known, and holds the material in Unidentified until it is. |
| `docs/frd/frd-09-provider-and-intermediary-routes.md` | Record the accepted QDOS triage predicates beside the accepted case-association ones. |
| `docs/principal-rules-and-mappings/qdos.md` | §2 and §5: the triage tells now drive Triage creation; the subject registration fact; policy version 6. |
| `docs/capabilities.md` | Canonical-owner row if the triage capability's owner moves. Check, do not assume. |

`docs/operator-notes.md` is **not** edited — it already says the rule.
