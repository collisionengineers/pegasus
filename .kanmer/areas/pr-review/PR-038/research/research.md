# Research — PR-038

## Question

How can two different confirmation keys be prevented from both reaching the provider?

## Verified findings

- The store checks active operations before insert, but the database uniquely constrains only `OperationKey`; two contexts can both pass the query.
- The operation row is persisted before the provider call, so a filtered unique constraint on `RetainedMailboxMessageId` while `Outcome` is pending or uncertain is the existing boundary that can serialize eligible calls.
- Terminal failed and succeeded rows must not occupy that constraint: a deliberate retry after failure and a later reclassification move remain valid.

## Implication

Add one filtered unique index to the existing entity/migration and prove concurrent different-key claims result in one provider call. Keep operation-key uniqueness for replay/conflict.
