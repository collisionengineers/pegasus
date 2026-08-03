# QDOS sender-domain inventory and accepted additions

Supporting evidence for [`qdos-email-classification`](qdos-email-classification.md).
Domain-level only: the operator asked for the second part of each address, not
the full addresses, so local parts are counted but not reproduced here.

## Sources read

| Source | Contribution |
| --- | --- |
| `docs/reference/workproviders-and-repairers/initial.xlsx` | Provider code to address-group map; 11 provider codes, 129 addresses, 16 distinct domains. The only domain-authoritative source found. |
| `docs/reference/workproviders-and-repairers/providers.xlsx` | None. A case dataset (Case ID, Vehicle Reg, Insured Name, Claim No, inspection location) with no address column. Its one domain-shaped token, `Onecall.com`, is the company name `Onecall.com Ltd`. |
| `docs/reference/workproviders-and-repairers/email_addresses.csv` | 32 QDOS-family addresses across the same three domains; a subset of `initial.xlsx`. |
| `docs/reference/workproviders-and-repairers/emailevalsaddresses.md` | Per-address occurrence counts from the emailevals corpus; corroborating volume evidence only. |

Both workbooks were copied to a scratch directory and read there. Neither
tracked file was modified.

## QDOS principal domains

`initial.xlsx` groups these three domains under the single provider code
`QDOS`, covering 38 addresses. That grouping is the operator-supplied evidence
that all three belong to one principal.

| Domain | Addresses | Status in code today | Action |
| --- | ---: | --- | --- |
| `qdosassist.co.uk` | 36 | Accepted (`AcceptedDirectDomain`) | Keep |
| `qdoslaw.co.uk` | 1 | Not matched; evaluates to `NoMatch` | **Add** |
| `qdosassists.co.uk` | 1 | Not matched; evaluates to `NoMatch` | **Add** |

Operator decision, 2026-08-03, recorded in this task's prompt: include both
additions. `qdosassists.co.uk` may be a transcription artifact of
`qdosassist.co.uk` — it carries the same local part as a real address on the
correct domain — but the operator accepted the inclusion regardless, on the
basis that accepting a near-identical sibling domain cannot misroute work away
from the QDOS principal. `qdoslaw.co.uk` is a distinct legal entity name but is
grouped under `QDOS` by the same operator source.

## Code change this implies

`QdosInstructionExtractionPolicy` currently holds one constant,
`AcceptedDirectDomain = "qdosassist.co.uk"`, compared by equality at the
`direct.qdos-domain` predicate. The change is a set membership test over three
accepted domains, keeping exact whole-domain equality per domain — no suffix
widening, no wildcard, no subdomain match. `mail.qdosassist.co.uk` must still
fail.

The `direct.qdos-domain` predicate keeps its name and continues to record which
domain matched in its reason text, so a decision record shows the exact
accepted domain rather than only that some domain matched.

`MailRouteVersion` increments from 2 to 3: the accepted route set changes, and
a version bump is what keeps historical decisions readable under the policy
they were made with rather than silently reinterpreted.

## Required companion documentation edit

`docs/open-decisions.md:94` currently states QDOS direct sender identity is
"the exact `@qdosassist.co.uk` suffix". The operator decision above supersedes
that single-domain statement, so the task PR updates that line to the accepted
three-domain set and keeps the rest of the clause intact — the suffix still
does not classify message type, associate a case, or apply to an identified
intermediary. This edit rides with the task PR, not a maintenance push.

## Non-QDOS provider domains found

Recorded as inventory only. None is activated by this task: additional
provider routes are `INT-04`, allocated `Next` / `0.2.0`, and
[DATA-01](../capabilities.md) states a domain snapshot is not principal
identity or route activation. `intermediary.accepted-policy` stays hardcoded
`false`.

| Provider code | Addresses | Domains |
| --- | ---: | --- |
| PCH | 24 | `pch-ltd.com`, `connexus.co.uk`, `ensurance-claims.co.uk` |
| AX | 14 | `ax-uk.com` |
| OAK | 14 | `oakwoodsolicitors.co.uk`, `oakwoodscotland.co.uk` |
| KBS | 13 | `knightsbridgesolicitors.co.uk` |
| RJS | 11 | `robertjameslaw.co.uk` |
| BLACK | 6 | `blackstone-legal.co.uk` |
| DFD | 3 | `dfd-solicitors.co.uk` |
| QCL | 2 | `qc-law.co.uk` |
| FW | 2 | `fairwaylegal.co.uk` |
| MP | 2 | `montrealprestige.co.uk` |

Two observations worth keeping for the `INT-04` cohort, neither acted on here:

- PCH spans three unrelated domains, and several people appear on more than one
  of them. A one-domain-per-principal assumption does not hold generally, so
  the accepted-domain set must be per principal, not per domain.
- `providers.xlsx` Case IDs carry provider-code prefixes (`qdos…`, `fw…`,
  `qcl…`, `parkers…`). `parkers` has no entry in `initial.xlsx`, so the
  provider-code space is wider than the eleven mapped groups.

## Verification

- A test asserting the accepted set is exactly the three QDOS domains, so
  adding a fourth requires an explicit change.
- Per-domain acceptance tests for all three, direct and staff-forwarded.
- A rejection test for `mail.qdosassist.co.uk` and for
  `qdosassist.co.uk.example.com`, proving no suffix or subdomain widening.
- A test that the recorded predicate reason names the matched domain.
- A test that no non-QDOS provider domain from the table above is accepted.
