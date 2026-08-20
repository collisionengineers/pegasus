# Open questions — PLAT-002

## How broad should this cleanup be?

The ticket names eight places to consolidate, but the same staff-actor lookup
currently appears across 20 Web files. Its existing verification says the lookup
must remain in only one file, which the eight-place change cannot achieve.

Choose one:

- [ ] **Complete consolidation (recommended):** move every Web staff-actor
  lookup to one shared owner and remove every duplicate operation-key generator
  that fits the same rule. This is a larger but still mechanical Web-only change.
  It makes the ticket title, simplicity goal, and one-root verification all true.
  The public anonymous upload page would reuse only neutral key generation; it
  would not become a staff page.
- [ ] **Narrow consolidation:** change only the two bases and six pages named in
  the ticket. This is smaller, but deliberately leaves other copies in place.
  The verification and ticket wording must be narrowed so the result is not
  described as one Web-wide root.

## Parked (explicitly deferred)

None.
