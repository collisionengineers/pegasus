# Plan — DELIV-009 (release 10)

Same route as [[DELIV-008]] (release 9), reusing every existing script; the
release-9 lessons applied up front (azd env copied into the release worktree
with the corrected `pegasusprodkv252ow37g` secret URIs; artifacts copied out
before the worktree is removed; Worker via `config-zip`; `efbundle` not needed
because no migration is pending — verified by `git diff` of the Migrations
folder between the deployed SHA and the candidate).

1. After AUTO-002 merges: preflight (`merge-base --is-ancestor origin/main
   origin/dev`; SHA == PR head with all checks SUCCESS); operator `MERGE AUTH
   GRANTED` for that SHA; atomic lease-checked push; readback; main-push run
   green (guard step).
2. Release worktree at the SHA: build; `Test-AzureDeploymentPlan -Mode Local`;
   `Build-ReleaseArtifacts`; `-Mode Artifact`; manifest SHA-256; `azd env
   refresh`; `-Mode PreUpload`; `oras cp`; digest check; `-Mode PreProvision`
   (worker still `approved-live-worker`); set `PEGASUS_WEB_IMAGE_DIGEST`,
   `PEGASUS_WEB_REVISION_SUFFIX`; (`AUTOMATION_MCP_REDIRECT_URIS` defaults via
   parameters.json); `azd provision --preview` (expect the new revision with the
   added `AutomationMcp__RedirectUris` env only, plus the usual what-if noise);
   `azd provision`; readback; `az functionapp deployment source config-zip`;
   `Invoke-ProductionSmoke.ps1`.
3. Live evidence: discovery document advertises `authorization_endpoint` /
   `code_challenge_methods_supported`; unauthenticated `GET /authorize?…` →
   sign-in redirect; then the operator connects the Claude.ai connector (or the
   HTTP flow is driven with the seeded Administrator up to the redirect).
4. Docs: `docs/operations.md` release-10 row + "serves release 10"; connector
   flow note already landed with AUTO-002; PR to `dev`; review; merge.
5. Copy `artifacts/releases/<version>` to the main checkout's ignored
   `artifacts/releases/` before removing the worktree.

## Simplification pass — n/a (release execution and docs).
