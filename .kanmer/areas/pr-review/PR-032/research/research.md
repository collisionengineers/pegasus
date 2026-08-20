# Research — PR-032

## Question

How can /Inbox/{id} expose the existing Core unavailable folder recommendation when no classification dossier exists, without broadening MAIL-05?

## Verified findings

- PR #474 commit 75c33641 makes GetRetainedMail.RecommendFolderAsync return a non-null RetainedMailFolderRecommendation with reason “This message has no current classification decision…” when detail.Classification is null.
- Message.cshtml renders all folder-recommendation markup only inside the classification-dossier condition; therefore the null-dossier result is dropped by the real staff caller.
- MailWorkspaceWebTests.MessageDetailShowsTheBodyAttachmentsThreadOutcomeAndTheWayBack already seeds a retained message with no classification, but asserts no folder recommendation.
- The smallest correction is Razor-only: render the existing Core result in a separate recommendation evidence section, available or unavailable. This also better preserves FRD-08's separation of classification and folder recommendation.
- No Core policy/store, page model, persistence, Graph, operation identity, mutation, governing-doc behavior, or MCP contract changes.

## Implication

Move the current folder recommendation markup out of the dossier-only block without copying policy/mapping logic, and add one exact authenticated null-classification Web assertion. Existing classified and ambiguous tests remain regression evidence.

## Open questions

None. The blocker and requested outcome are exact.
