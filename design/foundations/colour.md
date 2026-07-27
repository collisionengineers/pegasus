# Colour

## Roles and semantics

The planned internal-app roles are warm off-white background, white panels,
near-black/warm-charcoal text, CE-red primary/urgent, amber incomplete/pending,
navy Review, and green only for confirmed completion. State is never conveyed
by colour alone.

## Canonical tokens/source

The exercised values are the `:root` custom properties in
`src/CollisionSpike.Web/wwwroot/css/site.css`: `--ink`, `--muted`, `--paper`,
`--panel`, `--line`, `--navy`, `--amber`, and `--red`. No separate generated
token file exists.

## Runtime consumers

`src/CollisionSpike.Web/wwwroot/css/site.css` consumes the variables directly.
The planned green confirmed-completion role has no exercised runtime token yet
and must not be invented during onboarding.
