# Files — DOCS-005

| File | Change |
| --- | --- |
| `src/Pegasus.Core/Custody/CustodyContracts.cs` | `RetainAcceptedIntakeAttachmentAsync` (default fails closed; lease-guard overload mirrors the source method) |
| `src/Pegasus.Infrastructure/Custody/BoxCaseCustody.cs` | Bound-folder create/verify loses the binding file (staging promote kept); accepted-source binding block removed; new attachment retention; dead helpers deleted; fold keeps legacy binding delete |
| `src/Pegasus.Infrastructure/Custody/LocalCaseCustody.cs` | Attachment retention parity (content-addressed like the source) |
| `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs` | After source retention, load the receipt's `Kind = attachment` assets and retain each (ordinal 2+) |
| Custody tests (`CustodyOutboxIntegrationTests`, `LocalCustodyDurabilityTests`, Box fakes) | Binding assertions removed/updated; attachment retention coverage added |

Premises verified by code read (recorded in the ticket body); the live binding-file deletion is a T10 deployment step with exact targets.
