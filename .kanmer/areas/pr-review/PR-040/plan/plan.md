# Plan — PR-040

## Approach

1. Add a narrow current-location comparison on the existing move store.
2. Resolve each new operation’s source from latest successful destination; reject only when it already equals the current approved destination.
3. Let `CanMove` alone control the confirmation so reclassification to a different binding becomes actionable.
4. Prove two sequential moves use destination-one as source-two while the retained arrival folder never changes.

## Governing docs

FRD-08 explicitly requires a separate confirmation after reclassification changes the designated folder. The plan reuses MAIL-05/MAIL-23 policy and bindings rather than copying them.

## Risks

Binding changes within the same logical folder are handled by exact folder identity comparison, not only logical type.
