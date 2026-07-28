# Real, non-collisionengineers.co.uk emails found in repo scan

Scan date: 2026-07-27. Excludes any `@collisionengineers.co.uk` addresses and obviously fake
test addresses (`example.com/.org/.test/.invalid`, `provider.example`, etc).

## Third-party provider / legal contacts (database seeds)

`database/seeds/916_provider_domain_corrections.sql`
- info@fairwaylegal.co.uk
- sa@tenlegal.co.uk
- engineersinspections@ax-uk.com
- u.ibrahim@bakercoleman.co.uk
- a.nawaz@bakercoleman.co.uk
- gary.laiolo@dfd-solicitors.co.uk
- claims@blackstone-legal.co.uk
- office@parkhouseassist.com (lines 25, 125)

`database/seeds/915_corpus_email_address_match.sql`
- networkhduk@gmail.com (lines 10, 49)

## Hardcoded in application code

- k.garner@robertjameslaw.co.uk — `services/engine/cedocumentmapper_v2/src/cedocumentmapper_v2/exporters/rjs_docx.py:107`
  (and duplicated in `services/functions/ocr/...` and `services/functions/parser/...` copies of the same file)

## Test fixture, flagged as possibly real/unredacted PII

- ajmal.cheema@yahoo.com — `services/engine/cedocumentmapper_v2/tests/fixtures/expected/CDQ_DOCX_01.expected.json:11`
  (also echoed in a code comment in `engine.py:1382`, duplicated across engine/ocr/parser copies)

## Repairer contact seed data

`database/seeds/data/repairer-matches.csv`
- csgarage365@gmail.com
- gordon@gmbodyshop.co.uk
- gibbys_spraying_services@yahoo.com
- info@glasgowsmartrepaircentreltd.co.uk
- lpbcarrepairs@gmail.com
- markmcshane1@hotmail.com
- scottgibsonbodyshop@outlook.com
- undentit@yahoo.com

## Off-limits mailbox (referenced, not to be touched)

- info@carclaims.co.uk — `AGENTS.md:42`

## Box service account

- AutomationUser_2600067_2iVu7x1fXA@boxdevedition.com — `docs/tickets/verify/TKT-146-box-upload-event-classify/evidence/upload-receipt.json:59`
  (Box-generated synthetic dev-edition service account login, not a person)

## Working case correspondence (raw .eml archive, not code)

`emailevals/` (to-sort/ and received/) — large volume of real third-party sender/recipient
addresses in raw email headers/bodies, including but not limited to:
- jmoseley@qdosassist.co.uk
- sberlyne@qdosassist.co.uk
- sclark@qdosassist.co.uk
- nduncombe@qdosassist.co.uk
- SettlementClaims@ax-uk.com

This directory was not exhaustively enumerated for individual addresses — it is raw case data,
not provider logic, and likely contains many more real addresses (claimants, solicitors, garages)
across its .eml files.
