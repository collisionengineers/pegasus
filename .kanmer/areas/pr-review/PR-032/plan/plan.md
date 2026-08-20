# Plan — PR-032: render null-classification unavailable recommendation

## Approach

Preserve the existing classified/ambiguous recommendation rendering and add one adjacent, separately labelled unavailable evidence section only for the null-dossier branch. This is the smallest caller fix: it reuses the Core result, avoids changing the established classified layout, and does not justify a new partial for two definition-list rows. Add one exact Web test using the existing retained-message fixture.

## Governing docs

- **Meets** docs/frd/frd-08-email-mailbox-and-background-processing.md: the exact-message staff caller visibly fails closed before classification, while classification, recommendation, and later move remain separate.
- No governing document changes and no ADR are needed.

## Steps

1. Add a null-dossier sibling branch that renders the existing unavailable recommendation reason and policy in an accessible evidence section, preserving classified/ambiguous markup and adding no controls.
2. Add an authenticated Web test for a retained message whose classification dossier is null, asserting the labelled unavailable reason and absence of move control.
3. Run Release build and the focused MailWorkspaceWebTests; inspect the two-file diff through reuse, simplification, efficiency, and altitude lenses.
4. Update PR-032 and TICK-047 implementation reports, commit/push to PR #474, and move PR-032 to Review.

## Verification

Run the Release solution build and focused MailWorkspaceWebTests against LocalDB. Existing classified and ambiguous cases must remain green. CI provides replacement broad-suite evidence.

## Risks / open questions

The only risk is accidentally hiding classified recommendation output while moving markup; the focused class covers configured, ambiguous, and null-dossier states. No open question remains.
