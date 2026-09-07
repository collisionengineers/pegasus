# Current work

Pegasus v1 is under development in three coordinated streams:

| Owner | Branch | Scope |
| --- | --- | --- |
| PLAT-075 | `task/pegasus-v1-platform` | Platform, shared contracts, integration |
| CASE-047 | `task/pegasus-v1-casework` | Case engineering, estimates, reports |
| INTK-060 | `task/pegasus-v1-intake` | Intake, directories, shell |

The controller now owns all three streams on this host and is the sole heavy
verifier. The owners
record implementation and verification in their Kanmer tickets. Shared
Foundation corrections are consumed as identical commits; each stream keeps
its own PR to `dev`. Reviewed and verified work is integrated into `dev`;
the resulting PR to `main` must remain unmerged.

[Repository instructions](AGENTS.md#approved-v1-three-stream-exception)
own the scoped Git exception. [Operator authority](docs/operator-notes.md)
owns the accepted v1 decisions, [documentation](docs/index.md) routes current
questions, and [Operations](docs/operations.md) owns deployed evidence.
Branch implementation does not establish a release or live acceptance.
