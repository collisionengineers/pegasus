# Post-implementation report — TICK-043

## Summary

MAIL-01 now gives retained inbound mail one explicit, mailbox-scoped identity contract. The existing Graph/poll caller keeps immutable provider-item identity separate from RFC message and conversation identity. Retained mail requires RFC Message-ID and uses one canonical representation—trimmed, Unicode NFKC-normalized, and invariant-uppercase—for receipt idempotency, retained comparison, and binary-collated SQL uniqueness. Missing or contradictory identity fails closed, and a thread never crosses mailbox or folder scope. This is local implementation and test evidence only: no Outlook, Graph, Azure, deployment, or external write was performed.

## Changes

| File | Change | Why |
|---|---|---|
| `src/Pegasus.Core/Intake/MailboxIntake.cs` | Defines the shared RFC canonicalizer, requires bounded RFC identity, and hashes its canonical value into the receipt token while retaining provider identity separately | Ensures case/normalization/whitespace-equivalent redelivery cannot create another intake occurrence |
| `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs` | Adds the canonical RFC comparison key beside the raw evidence value | Separates retained evidence from equality mechanics |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Uses the shared canonical key for insert, lookup, comparison and race recovery; scopes thread reads to mailbox + folder | Makes redelivery idempotent, contradictions fail closed, and prevents cross-scope thread disclosure |
| `src/Pegasus.Infrastructure/Persistence/MailboxModelConfiguration.cs` | Configures binary collation and unique mailbox + canonical-RFC index | Makes SQL equality exactly match the Core canonical representation |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260819093019_RetainedMailboxInternetMessageIdentity.cs` and designer/snapshot | Adds/backfills the canonical column and filtered composite unique index | Carries existing retained ASCII Message-ID rows into the canonical boundary |
| `tests/Pegasus.Core.Tests/Intake/PollApprovedInboxTests.cs` | Covers missing RFC identity and equivalent-representation receipt tokens | Proves the Core fail-closed/canonical contract |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Covers real poll/EF equivalent redelivery, distinct identity, provider-ID change, contradictions, database idempotency and thread isolation | Proves receipt/work/read-model behavior through the real persistence boundary |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Owns the exact canonical equality rule and inbound identity behaviour | Keeps product behaviour in the governing FRD |
| `docs/capabilities.md` | Records locally implemented MAIL-01 evidence and deployment qualification | Keeps the registry accurate without claiming deployment |

## Governing docs

The linked `docs/frd/frd-08-email-mailbox-and-background-processing.md` states the exact identity dimensions, canonical equality rule, mailbox + RFC duplicate boundary, contradiction behaviour, and mailbox/folder-scoped thread rule implemented here. The existing Core/Infrastructure/Web architecture carries the work, so no ADR was needed. PR-004's review finding is addressed by using the same Core representation everywhere instead of relying on case-sensitive hashing, .NET comparison, and ambient SQL collation independently.

## Risks / follow-ups

- Historical non-null RFC identities are accepted transport Message-IDs and are backfilled with SQL trim + uppercase; new writes additionally apply Unicode NFKC in Core. Null historical RFC identity stays readable and is not fabricated.
- This ticket does not implement mailbox search, Case association, classification correction, folder moves, or other Outlook mutations.
- Deployment and fresh live-mailbox verification are not claimed. Any future external write still needs exact approval.

## Verification hand-off

On the merged release candidate, run:

- `dotnet restore --locked-mode`
- `dotnet build --configuration Release --no-restore` — expect 0 warnings/errors
- `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` — blocking-fix result: 618/618 passed
- `dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` — original implementation result: 96/96 passed
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~RetainedMailPersistenceTests` — blocking-fix result: 14/14 passed
- `git diff --check` — passed

Verify the migration applies through the normal integration database fixture and the real poll tests show one staged receipt/work item/retained row for equivalent RFC representations and distinct rows for distinct canonical identities. No screenshot is required.

## PR-005 review-fix addendum — 2026-08-19

Canonical output is now bounded in the shared Core canonicalizer after trim, NFKC normalization and uppercase folding. A raw value that fits 500 characters but expands beyond the 500-character persistence key fails closed as malformed before receipt or database writes. The real poll/EF acceptance test now uses Unicode-equivalent Kelvin-sign and ASCII-K Message-IDs, proves one staged receipt/work item/retained row, and asserts that the first raw transport value remains verbatim while the separate canonical column contains `<CASE@K.EXAMPLE>`. The distinct-canonical control remains. Verification: Release build clean; focused Core 22/22; focused real poll/EF 2/2; full Core 619/619; retained-mail integration 14/14; diff check clean.

## PR-008 review-fix addendum — 2026-08-19

The committed migration expectation now includes `20260819093019_RetainedMailboxInternetMessageIdentity`. The two restart/terminal-outcome theory cases retain their original missing/changed source behavior, but the later independent message now has a distinct RFC Message-ID rather than reusing the observed message's identity. No production identity rule was weakened. Verification: clean Integration build; exact former failures 3/3; full `MailboxIntakeIntegrationTests` plus `IntakePersistenceIntegrationTests` 23/23; diff check clean.
