# Files — PR-040

| Path | Change |
|---|---|
| `IRetainedMailFolderMoveStore` and EF store | Query current location and use latest successful destination as the next source. |
| `GetRetainedMail`, `Message.cshtml` | Compute `CanMove` from current exact binding vs durable current location; stop suppressing all later success. |
| persistence/Web tests | Prove reclassification to a different destination enables a second confirmation and preserves arrival identity. |

Out of scope: arbitrary destinations or duplicated classification policy.
