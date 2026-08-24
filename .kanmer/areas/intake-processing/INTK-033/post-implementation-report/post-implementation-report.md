# Post-implementation report

**Branch** `task/intk-033-triage-from-intake` · **PR** [#525](https://github.com/collisionengineers/pegasus/pull/525) → `dev`
**Commits** `7b43ab17` (the change) · `04be01a7` (simplification-pass fixes)

## What was wrong, in one line

The inbox label was real and the work behind it was not: a QDOS triage request
was classified correctly, then sent into automatic case allocation anyway, which
failed closed for want of a case type a triage request does not carry — leaving
no case, no Triage and no Unidentified item.

## What landed

| Fault | Fix |
| --- | --- |
| Triage request reached case allocation | A classified triage request is `NeedsSorting`, following the standalone-Audit precedent already in `AssessAsync`. Allocation untouched — it was failing closed correctly. |
| The Triage gate could never pass | The `AcceptedTriageMatch` evidence is derived from the accepted route classification decision. `IIntakeTriageMatcher`, `NoAcceptedIntakeTriageMatcher` and `IntakeTriageMatch` are **deleted**. |
| No registration read from a triage request | `SubjectFactLines` reads the `Vehicle Registration` label in both recorded spacings; the vehicle-description rule no longer swallows that label. |

Both branches of the operator's rule are wired, reusing the image-only deferral
mechanism rather than inventing a second one: a known registration opens the
Triage; no known registration registers the material as Unidentified.

## The decision worth defending

Writing a real `QdosIntakeTriageMatcher` was the obvious fix and the wrong one.
FRD-03 says Triage begins when *"the exact accepted route policy classifies a
provider request as an assessment request"*, and ADR-0008 makes that policy the
only owner of message-type classification. A second abstraction asking the same
question is a duplicate owner — a stop condition, not a style preference. The
matcher's only implementation was ever the null one, so deleting it removes a
closed composition gate rather than shipping behind one.

The downstream contract — `CreateTriageIfQualifyingAsync`, `TriageLifecycle`,
`EfTriageStore` and its uniqueness re-check — is untouched. It simply starts
receiving evidence.

## Three things a reviewer must weigh, not skim

**A composition pin changed meaning.**
`ProductionProfileKeepsTheTriageMatcherInactive` existed so the matcher could
never be activated as a side effect of composition, and `open-decisions.md` said
activation needed the predicates accepted. They now are — as
`qdos_mail_classification` v4 ([[MAIL-012]], shipped in release 26), with
exclusions and ambiguity outcome. The test pins the *active* classification
policy, its key and its version, so the protection points at the real mechanism
instead of being deleted. `open-decisions.md` asserted the opposite of what
ships and is closed.

**A test's invariant was overruled by the operator's notes.**
`ClassificationIsRecordedOnlyAndNeverChangesTheIntakeDecision` pinned "a
classification never changes the decision" using a *triage* message. It was
already qualified before this branch (the standalone-Audit rule has downgraded
`CaseCreated` on a classification fact for some time), and FRD-01 forbids only
classification mutating *Case* state, which this does not. It now uses an
automatic reply, where the invariant genuinely holds; the triage exception is
its own named test.

**A plan step was deliberately not taken.** See the checklist note: four
integration suites stayed on their `AcceptedTriageMatchPolicy` stub, because
they test the downstream contract and that contract is unchanged. A new suite
drives the real path instead.

## Simplification pass — 2026-08-24

Run over this branch's own diff before the PR, with an independent lens rather
than by hand. **Two correctness findings**, both verified independently before
being accepted, both fixed in `04be01a7`:

- **A quadratic subject regex** (applied). `\s*[:.]?\s*` — two unbounded
  whitespace runs either side of an optional character — costs O(k²) to fail:
  343 ms at 4,000 spaces, 1,366 ms at 8,000, **6,905 ms at 16,000**, a clean 4×
  per doubling, on a subject header an approved sender controls and the reader
  retains untruncated. The 100 ms match timeout holds it to denial-of-processing
  for one message rather than a hang, which is why it is a defect and not an
  incident. Replaced with one bounded class `[\s:.]{1,10}`: 1.12 ms at the same
  width, and identical output across 13 recorded and plausible subject shapes.

  This is the release-26 lesson repeating. The comment above that line claimed
  bounded quantifiers and pointed at the value group, which was never the
  problem, and my own reasoning while writing it dismissed the seam as harmless.
  It was not.

- **Missing fault handling on a step that had just become reachable** (applied).
  `CreateTriageIfQualifyingAsync` was the only step after
  `CompleteProcessingAsync` with no `catch`; its four neighbours all have one and
  say so. Invisible while the gate could never pass. An escaping fault would
  leave the receipt Completed, throw to the host, and throw identically on every
  redelivery — a poison loop. It now fails closed to `false`, which registers the
  material as Unidentified: a queue somebody works, rather than nowhere.

**Quality findings, all applied:** one shared `IsDeferredForAutomation`
predicate (the deferral rule had ended up written twice, in two files, in
opposite polarity); the decision override asks `IsTriageRequest` directly rather
than inferring it from a non-null evidence object; thirteen files had picked up
a spurious UTF-8 BOM from the editing scripts, stripped and verified
byte-for-byte against `origin/dev`; a stray blank line.

**Verified clean and recorded as skips:** the Detail-length cap is structurally
unreachable (a triage classification excludes the three long predicate keys, so
~163 characters against a 500 cap); the modified vehicle-description regex is
linear (measured 0.96 ms at 32,000 characters); no null or empty value flows
into a consumer that assumes presence; no QDOS knowledge leaked into generic
Core. Two suggestions were dismissed with reasons rather than applied: the
subject registration rule keeps its own shape-bounded capture (validation
happens outside the pattern, so a generic token would swallow the subject), and
the timeout stays on one regex rather than being spread to five as a new
convention.

## Verification

| Check | Result |
| --- | --- |
| `dotnet build --configuration Release` | green, no warnings |
| `dotnet test tests/Pegasus.Core.Tests` | **945 passed** |
| `dotnet test tests/Pegasus.ArchitectureTests` | **99 passed** |
| `dotnet test tests/Pegasus.IntegrationTests --filter "Category!=Corpus"` | **956 passed** on `7b43ab17` (21 m 52 s); re-running on `04be01a7` |

Three new end-to-end tests drive the real pipeline with no stubbed policy:
subject-template triage opens a Triage, body-template triage opens a Triage, and
a triage request with no registration lands in Unidentified with no Triage.
Green on first run.

## Not done, deliberately

- **Images on a triage request.** Both templates attach client damage photos.
  Retaining them as Triage evidence is real work with no operator instruction
  behind it, and a Triage record has no evidence surface today.
- **Claim reference from the subject.** `TriageRecord` stores only the
  registration; a claim reference has nowhere to go.
- **Unidentified → Triage promotion** once a registration is later learned.
  `UnidentifiedResolutionTargetKind` has no Triage member, and the operator's
  rule says the material *waits*; it does not say the promotion is automatic.

Each is named here rather than left for a reader to notice.
