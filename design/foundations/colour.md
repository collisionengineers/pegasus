# Colour

## Roles and semantics

Adapted brand roles are Collision red `#DB0816`, pressed red `#8F1422`, warm
charcoal `#2C2A27`, near-black `#16191D`, white, light neutral `#F5F4F2`, border
`#E6E4E1`, muted text `#6B6B6B`, and confirmed-success green `#16833B`.
Pegasus additionally retains amber incomplete/pending and navy Review
from its approved UI plan. State is never conveyed by colour alone.

## Canonical tokens/source

Canonical adapted values are recorded in [the token inventory](../tokens/README.md)
from the provided `collision-engineers-design-dev` source plus the two named
Pegasus workflow-state roles. No generated token file is created during
onboarding.

## Runtime consumers

`src/Pegasus.Web/wwwroot/css/site.css` currently consumes its own
`--ink`, `--muted`, `--paper`, `--panel`, `--line`, `--navy`, `--amber`, and
`--red` values. Its red/neutral values and 3px geometry differ from this adapted
authority; a future selected UI implementation must reconcile them rather than
claiming current parity.
