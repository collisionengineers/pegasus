# Current work

Pegasus v1 is under development in three coordinated streams:

| Owner | Branch | Scope |
| --- | --- | --- |
| PLAT-075 | `task/pegasus-v1-platform` | Platform, shared contracts, integration |
| CASE-047 | `task/pegasus-v1-casework` | Case engineering, estimates, reports |
| INTK-060 | `task/pegasus-v1-intake` | Intake, directories, shell |

The Stream A controller is the sole heavy verifier on its host. The owners
record implementation and verification in their Kanmer tickets. Shared
Foundation corrections are consumed as identical commits; each stream keeps
its own PR to `dev`. All three PRs remain open and unmerged at handoff.

[Repository instructions](AGENTS.md#approved-v1-three-stream-exception)
own the scoped Git exception. [Operator authority](docs/operator-notes.md)
owns the accepted v1 decisions, [documentation](docs/index.md) routes current
questions, and [Operations](docs/operations.md) owns deployed evidence.
Branch implementation does not establish a release or live acceptance.
