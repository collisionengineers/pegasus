# Box Case/PO document custody

## Goal

Remove the internal `caseId` from the Box case-file hierarchy.  One allocated
Case/PO owns one operator-legible Box case folder whose name is exactly the
safe Case/PO, with no UUID prefix or suffix.  That folder contains both
retained intake sources and application-managed document versions.  The UUID
remains an internal identity; it must not select or name a Box case folder.

## Current boundary and defect

`BoxCaseCustody` creates and validates a root named
`{Case/PO}-{caseId}`.  `BoxDocumentContentStore` independently creates a
second `cases/{caseId}/managed/{versionId}/content` tree because
`IDocumentContentStore` receives only the case and version UUIDs.  This splits
one Case's custody and exposes an implementation identifier as a Box folder
name.

## Changes

1. Change the Core content-storage contract and its use cases so the adapter
   receives the allocated immutable Case/PO identity when it resolves managed
   content.  The policy and lookup remain in Core/persistence; the Box adapter
   must not reimplement Case/PO allocation or derive it from UI input.
2. Change `BoxCaseCustody` to create, locate, and validate its root by the safe
   Case/PO folder name alone: the literal root name is the Case/PO with no UUID
   prefix or suffix.  Make `BoxDocumentContentStore` resolve managed content
   beneath that same root rather than a separate `cases/{caseId}` tree.  Keep
   document-version identity, replay behavior, SHA-256/length checks, and root
   fencing intact.
3. Make the local document-content implementation mirror the revised contract
   so local behavior cannot hide a production-only layout.  Preserve internal
   IDs as application/storage identities, not Case/PO folder names.
4. Update the affected canonical architecture/open-decision wording so it no
   longer describes a UUID-only managed-document tree or asks the operator to
   approve it.
5. Update focused Core, local durability, in-memory Box, composition, and
   custody tests.  Add a literal assertion that the Box root is the Case/PO
   folder and managed content is beneath it, while repeats, corruption checks,
   missing content, and idempotent removal retain their established outcomes.

## Boundaries

- No Box, Azure, mailbox, credential, configuration, or deployment operation.
- No production or non-production Box inspection, move, copy, rename, delete,
  or migration.  If existing remote content later requires relocation, that is
  a separate task requiring an approved target, read-only inventory, recovery
  plan, and explicit approval.
- No change to Case/PO allocation, document lifecycle authorization, File
  Requests, request-scoped uploads, sharing, or UI workflows except their
  necessary compile-time adaptation to the revised Core contract.
- Do not add a new store, runtime, project, or migration stream.

## Verification

Run the focused document/custody test set covering the revised contract and the
production composition profile, then `dotnet build --configuration Release`.
Before PR, run the repository's required verification profile and record the
exact commands/results in the PR.  These tests prove local and in-memory Box
behavior only; they do not prove a deployed caller, a live Box mutation, or
operator acceptance.

## Review questions

An independent reviewer must confirm that the implementation removes every
Case-ID-derived Box case-folder path; names each Box case root exactly by its
Case/PO (with no UUID prefix or suffix); retains one such root for both source
and managed custody; and does not broaden into a remote migration or a new
document lifecycle policy.
