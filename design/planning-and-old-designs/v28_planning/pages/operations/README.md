# Operations

- **Mockup route:** `pegasus_search_operations_v28.html` (`so-area=operations`) in [`../../current/`](../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Operations/Index.cshtml`

- [**How it works**](how-it-works.md)

## Screenshots

- [s04-operations-jobs-1580.png](../../current/v28-shots/s04-operations-jobs-1580.png) · [1440](../../current/v28-shots/s04-operations-jobs-1440.png) · [760](../../current/v28-shots/s04-operations-jobs-760.png)
- [s05-operations-attention-1580.png](../../current/v28-shots/s05-operations-attention-1580.png) · [1440](../../current/v28-shots/s05-operations-attention-1440.png) · [760](../../current/v28-shots/s05-operations-attention-760.png)

## Notes

Retry/Cancel/Complete/Send-to-AI forms are captured as real forms with the
exact fields and confirmation-reason inputs the live page uses, but their
submit handlers are demo-only (`data-mock-action`) — they show a toast rather
than performing a real state transition, since this round captures structure
and states, not a live backend.
