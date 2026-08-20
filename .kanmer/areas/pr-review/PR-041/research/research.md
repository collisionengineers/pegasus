# Research — PR-041

## Question

What is the smallest existing route that keeps moved retained mail findable?

## Verified findings

- The canonical retained query applies Inbox exclusion before its existing search predicate, so moved rows cannot be found by any retained search.
- The workspace has no logical-destination tabs; adding them would duplicate MAIL-11’s folder vocabulary and broaden UI scope.
- FRD-08 accepts destination-folder scope or search. The existing retained search can include moved rows while ordinary Inbox browsing continues to exclude them.

## Implication

Make a non-empty retained search span retained current locations, using the same query/store/paging/mailbox filters. Project the latest logical folder from the existing successful operation for current-location evidence; add no second store or taxonomy.
