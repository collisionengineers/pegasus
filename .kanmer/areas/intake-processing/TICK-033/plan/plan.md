# Plan — TICK-033: reconcile INT-31 evidence with the implemented request-scoped upload caller

## Approach

Use the existing, Core-owned request-upload implementation and its real Web caller as the single source of implementation evidence. Make a narrowly scoped correction to the INT-31 inventory boundary text, because source history proves the prior Box File Request UI/persistence is removed. This avoids duplicating already-implemented policy and explicitly does not represent local tests as live deployment or operator acceptance.

## Governing docs

- **Meets `docs/frd/frd-02-intake-and-source-identity.md`** — retain its established semantics: authenticated creation, temporary/revocable token, request-local public result, custody, retry, limits, cross-request isolation and non-disclosing failures. The documentation correction makes no behavioural change.
- **Does not modify the FRD.** The discovered implementation and existing source caller align with its contract. The capability inventory remains the schedule/boundary registry, so only its stale predecessor-removal wording changes.

## Steps

1. Update the INT-31 row in `docs/capabilities.md` to state that the obsolete Box File Request path is removed in source while live activation, deployment and acceptance remain separate.
2. Run focused request-upload caller/custody integration tests and a Release build; retain their output as local verification evidence only.
3. Inspect the resulting diff against the FRD and current architecture, run the required simplification pass (n/a — docs-only), commit, push and open the review PR.

## Verification

Run:
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~DocumentCustodyDurabilityTests"`
- `dotnet build --configuration Release`

Review that the inventory wording neither claims a deployment nor operator acceptance, and that the source caller remains `/Uploads/{token}`.

## Risks / open questions

- Local tests cannot establish real browser accessibility, production custody, or operator acceptance. Those require separately approved exact-target live evidence and remain outside this ticket.
- No user decision is required for the source-truth correction; the work does not change product behaviour.
