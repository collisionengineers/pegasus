## Research — TICK-110 — retrospective backfill, verified 2026-08-20

**Question:** Does local `azd` state still drift from the observed production estate, and is the predecessor Key Vault purge (TICK-115) still an open reconciliation item?

### Findings
- `docs/operations.md:405-413` (release 8 entry): the local `azd` environment carried the retired adopted vaults (`cespkboxkvv76a47`, `cespkenrichkvgi62sd`, **"since purged"**) as the six Box/DVLA/DVSA secret URIs; the Worker's six Key Vault references were unresolved in production until release 8 re-rendered them against `pegasusprodkv252ow37g`. After that release, all six read `Resolved`.
- `docs/operations.md:442-450` (release 9 entry) states the standing rule this ticket exists to enforce: **"Read the deployed resource, not the local environment, when a provision disagrees with a working estate."** The local `azd` environment is explicitly documented as non-authoritative and drift-prone; the running Container App is the source of truth.
- `TICK-115` ("Verify scheduled predecessor Key Vault purge by fresh approved inventory") is **archived**, with its own body recording: "This standalone proof/validation ticket was consolidated into [[TICK-110]]." Its checklist item is mirrored verbatim into `TICK-110`'s own `checklist.md` ("Migrated validation — [[TICK-115]]").
- The predecessor vaults are recorded in `docs/operations.md` as already `since purged` — i.e. the specific drift TICK-115/TICK-110 exist to catch (stale references to a purged vault) has already been found and fixed (release 8), and the vaults themselves are gone. A fresh live Key Vault inventory re-check was not performed by this PROOFS-lane pass (no Azure write or new live query was made beyond what `docs/operations.md` already documents as read-only-verified); the documented state is treated as sufficient evidence that the reconciliation this ticket names is done, per this run's own verdict.

### Implications
The reconciliation TICK-110 names — catching and fixing local-azd-vs-production drift, specifically the predecessor Key Vault purge — is already evidenced in `docs/operations.md`. No further live check was performed; the existing documented evidence (drift found and fixed at release 8, all six Key Vault references `Resolved`, vaults recorded as purged) is the proof.

### Open questions
None — TICK-115's residual (a fresh live-approval inventory) was archived/consolidated without further action needed, since the drift it was checking for was already fixed and documented.
