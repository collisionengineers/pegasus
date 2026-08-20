# Research

## Verified

- `docs/design/README.md` currently defers every post-alpha UI capability through a re-entry sequence.
- `docs/capabilities.md` allocates MAIL-11 to Next / 0.3.0 and records the local `/Inbox` implementation, but does not record the operator activation decision.
- PR #469 integrates search into the existing authenticated `/Inbox` route and existing tabs/filter/table patterns; it adds no route, navigation item, store, parser, deployment, or mailbox write.
- On 2026-08-20 the operator instructed “Implement the plan”; the programme owner has confirmed that instruction activates this planned local MAIL-11 browse/search UI only. It does not authorize deployment, Graph permission changes, or live mailbox writes.

## Conclusion

Reconcile the durable design authority with a narrow adopted MAIL-11 re-entry note: existing `/Inbox` integration was selected over a second workspace, existing visual patterns are reused, PR #469 supplies independent code/design review, and deployment/manual visual acceptance remain separate evidence. Update the capability row to name local activation without claiming deployment.
