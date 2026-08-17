# Research — SIMPLI-008: queued receipt status

## Question

How can staff identify a durably staged upload while Worker processing is still pending, and how can the eventual destination be exposed without making Web a processor?

## Findings

- UploadModel currently redirects back to Upload only when inline processing returns Queued; the staged receipt identifier is not included in that destination.
- IIntakeWorkStore already exposes the work item and completed evaluation by staged receipt ID.
- Persisted work states can be projected without exposing leases or storage details.
- A completed evaluation identifies the processed receipt; the receipt identifies its current case, if one exists.
- /Received/{id} is keyed by processed receipt ID, so it cannot represent work before evaluation completes.
- The user selected one combined SIMPLI-008/SIMPLI-009 PR.

## Implications

Add a bounded Core query keyed by staged receipt ID and an authenticated /Upload/Status/{id} page. Project internal states to Received, Processing, Complete, or Failed. Refresh only nonterminal states and link completed work to Case Details or the retained-receipt page.

## Open questions

None. The combined plan fixes the implementation decisions.
