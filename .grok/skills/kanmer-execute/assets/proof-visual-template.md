# Visual proof — <ticket id>

*The visual proof. Not a description — a reader must be able to **see** the result without running anything.*

For a `proof:visual` requirement. Gathered on the configured integration branch after review and merge.

## Screenshots

Put the image files **under `proof/`** in the ticket folder and reference them
here. Core emits a soft warning when a `proof:visual` requirement finds no image
files beneath `proof/` — it will not block the move, but an unanswered warning
means nobody can see what you saw.

| What it shows | File |
|---|---|
| the state being demonstrated | `proof/<name>.png` |

## Where it was taken

Environment, and the exact route to reproduce the view — a screenshot with no
route is unverifiable.

## What to look at

Point at the part of the image that is the evidence. A screenshot of a whole
screen proves nothing on its own.

## Not shown

Any state, theme or viewport deliberately not captured.

## Verification identity and attempts

- Integration branch: resolve from get_status.delivery.integrationBranch.
- Exact SHA, environment, command exit codes and evidence paths.
- Retain every failed or inconclusive attempt and its disposition.
- Deployment and live operator acceptance are separate proof.
- PASS is required for ordinary Done; this template grants no waiver.
