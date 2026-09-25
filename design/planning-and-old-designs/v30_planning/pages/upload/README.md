# Upload

Live routes: `/Upload`, `/Upload/Group/{id}`, `/Upload/Status/{id}`. Review
[how it works](how-it-works.md), [proposed behavior](how-it-should-work.md),
[states](states/README.md), [panels](panels/README.md), and
[dialogs](dialogs/README.md).

## Screenshots

The [screenshot folder](../../current/v30-shots/) contains every combination
of option `a`–`e`, state `select`, `chosen`, `storing`, `processing`,
`decision`, `registered`, `mixed`, `single`, `failed`, or `discarded`, and width
`1580`, `1440`, or `760`. Names follow `a-decision-1580.png`.
Representative comparisons:

| Option | Decision | Processing | Narrow decision |
| --- | --- | --- | --- |
| A | [1580](../../current/v30-shots/a-decision-1580.png) | [1580](../../current/v30-shots/a-processing-1580.png) | [760](../../current/v30-shots/a-decision-760.png) |
| B | [1580](../../current/v30-shots/b-decision-1580.png) | [1580](../../current/v30-shots/b-processing-1580.png) | [760](../../current/v30-shots/b-decision-760.png) |
| C | [1580](../../current/v30-shots/c-decision-1580.png) | [1580](../../current/v30-shots/c-processing-1580.png) | [760](../../current/v30-shots/c-decision-760.png) |
| D | [1580](../../current/v30-shots/d-decision-1580.png) | [1580](../../current/v30-shots/d-processing-1580.png) | [760](../../current/v30-shots/d-decision-760.png) |
| E | [1580](../../current/v30-shots/e-decision-1580.png) | [1580](../../current/v30-shots/e-processing-1580.png) | [760](../../current/v30-shots/e-decision-760.png) |

These are synthetic, offline design captures, not application evidence.

## Case-first states

Additional captures for every variant at 1580, 1440 and 760 px:

- [No match](../../current/v30-shots/a-no-match-1580.png): Case lookup.
- [Multiple](../../current/v30-shots/a-multiple-1580.png): explicit Case choice.
- [Attached](../../current/v30-shots/a-attached-1580.png): confirmed destination.

Use b–e in the capture name for the other layouts.
