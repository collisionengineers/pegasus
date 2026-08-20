# Research — PR-040

## Question

How should a later reclassification permit another move without rewriting arrival evidence?

## Verified findings

- The store currently rejects any message with a succeeded move and always probes/uses the retained arrival folder as source.
- Successful rows already persist exact source/destination and logical folder type; the latest success is sufficient durable current-location evidence.
- The recommendation owns the current policy/binding and can compare the approved destination with durable current location server-side.

## Implication

Remove the permanent moved flag. Resolve source from the latest success, offer confirmation only when the current approved destination differs, and preserve the retained row unchanged.
