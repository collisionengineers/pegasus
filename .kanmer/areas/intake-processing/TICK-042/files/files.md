# Files — TICK-042

## Where the change lands

The research found no required source change: the INT-28 implementation is already on `dev`. These are the assessed implementation surfaces, to modify only if a concrete defect is discovered.

| Path | Why |
|---|---|
| `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` | Owns forward pairing after a qualifying read. It must retain the single eligible-candidate and contradiction/ambiguity abstention rules. |
| `src/Pegasus.Core/ImageIntake/ImageIntakeCasePairing.cs` | Owns reverse pairing on Case acceptance. It deliberately permits exact registration equality only. |
| `src/Pegasus.Core/Intake/AcceptIntake.cs` | Real caller for reverse pairing after a newly accepted Case; duplicate acceptance must not repeat pairings. |
| `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` | Defines immutable Image-intake registration, eligible pre-report candidate constraints, and separation of association history. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs` | Durable one-shot automatic association; existing associations, staff leases, and conflicting writes yield rather than overwrite. |
| `tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs` | Forward-pairing, threshold, ambiguity, and registration behavior. |
| `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeCasePairingTests.cs` | Exact-only reverse pairing, ambiguity, and per-item failure isolation. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/frd/frd-02-intake-and-source-identity.md` | Matching must be explainable, preserve both identities, fail closed on ambiguity/contradiction, and remain reasonedly reversible by staff. |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Supplies the scan-time-only one-missing-character and inserted-character rules; they never apply after registration in the reverse path. |
| `src/Pegasus.Core/ImageIntake/VrmRecognition.cs` | Contains the accepted scan-time matching primitives and explains why matching distinct from registration is policy sensitive. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Supplies the forward automation hook after durable intake processing. |
| `tests/Pegasus.IntegrationTests/ImageIntakePersistenceTests.cs` | Covers durable Image-intake identity and persistence behavior beyond Core fakes. |

## Ripple effects

- Any matching-policy change must update both forward and reverse paths only when the FRDs authorise it, and must test idempotency, ambiguity, staff-lease yielding, and retained history.
- Candidate/association changes can affect intake and Case views, persistence migrations, and operator-visible reason evidence.
- Existing process has no requested repository source or documentation modification.

## Out of scope

- Altering the 0.80 recognition bar or recognition engine.
- Case creation policy, post-report Case association, automatic resolution of ambiguous matches, and UI redesign.
- Changing `corpus/` or representing the timed-out integration subset as a passing test run.
