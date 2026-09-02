# Plan — TICK-060: Return the provider's resulting Case/PO or fail

## Approach

Simplify API-01's existing authenticated result endpoint into API-03's public
contract. Keep Principal ownership and outcome policy in Core, reuse the
existing durable-intake and active Case-link queries, and expose only an empty
unfinished response, a Case/PO identifier, or a generic terminal failure.

## Steps

1. Extend `IProviderSubmissionStore` only if required to scope the existing
   submission read by authenticated Principal. Reuse the existing store and
   query; add no result projection.
2. Replace `ProviderSubmissionResult`'s detailed external vocabulary with
   three Core outcomes: unfinished, success with the actual active Case
   reference, and terminal failure. Treat completed-without-active-link as
   failure.
3. Keep `GET /api/provider/v1/submissions/{submissionId}` and map:
   unknown/foreign to 404, unfinished to an empty 202, linked success to
   `{"caseReference":"..."}`, and terminal failure to a generic 422.
   Preserve paused reads and revoked/invalid 401 behavior.
4. Update the existing Core and integration tests for ownership, unfinished,
   linked success, completed-without-link, failed work, response minimality,
   paused reads, revoked authentication, and random/cross-Principal
   nondisclosure.
5. Update FRD-09 and `docs/capabilities.md` so API-03 owns the result contract
   and API-02's detailed processing-status contract is retired.
6. Run the independent simplification lenses over this ticket's diff, apply
   behavior-preserving findings, and record every disposition here.
7. Run locked restore, Release build, and the non-Corpus solution test command.
   Open a PR to `dev`; an independent reviewer owns review and merge.
   Verification and proof run only at the exact merge SHA on `main`.

## Reuse and boundaries

The production caller is the API-01 GET route. Authentication, feature
composition, rate limiting, telemetry, durable intake state, active Case-link
authority, and test infrastructure already exist. No new abstraction, route,
store, SQL projection, database object, dependency, Azure resource, report/file
surface, list/search capability, retry detail, compatibility response, or
deployment is in scope.

## Acceptance

- Unknown, random, and foreign identifiers are indistinguishable 404.
- Owned unfinished work returns an empty 202.
- Only an actual active Case link returns 200 and `caseReference`.
- Failed or completed-without-link work returns generic 422.
- No processing state, decision, failure code, provider reference, timestamp,
  file, report, or general Case detail is returned.
- Paused reads remain allowed; revoked and invalid credentials remain 401.
- Canonical commands pass and the independent review records no unresolved
  finding.
