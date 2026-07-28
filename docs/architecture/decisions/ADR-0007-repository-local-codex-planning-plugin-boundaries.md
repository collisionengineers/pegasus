# ADR-0007: Repository-local Codex planning plugin boundaries

- Status: Superseded by ADR-0008
- Date: 2026-07-24
- Owners: Alex and the Pegasus development team

## Context

Pegasus needs a repeatable planning workflow that can persist research,
decisions, independent review, open questions, and an implementation-ready plan
pack across Codex sessions. The workflow is repository tooling, not application
runtime behavior.

The existing `.codex/` boundary owns repository agent and host configuration.
It is not an installable plugin distribution boundary, and placing generated
flow state there would mix durable configuration with local working data. The
existing `repoplugin/` directory is a reference-only holding area containing
source material that must remain outside the installed package. It is not a
runtime boundary and is not suitable as the distributable plugin root.

The Codex plugin and marketplace contracts require a stable package root and a
marketplace entry. The planning flow also needs ignored, resumable local state
that can be validated without committing working notes or sensitive payloads.

## Decision

1. Add tracked `plugins/repoplugin/` as the repository-local distribution
   boundary for the Pegasus Repository Planning plugin. It owns the
   plugin manifest, nine focused skills, deterministic PowerShell scripts,
   templates, and advisory command hooks.
2. Add tracked `.agents/plugins/marketplace.json` as the repository-local
   marketplace index for that package. It contains package discovery metadata
   only; it does not install, enable, or trust the plugin.
3. Reserve ignored `.repoplugin/` for local planning flow state. It may contain
   briefs, evidence handoffs, options, drafts, reviews, open questions,
   immutable plan-pack generations, and implementation evidence. It must not
   contain customer material, corpus content, secrets, credentials, full tool
   payloads, or full transcripts.
4. The accountable planning or implementation lead owns mutable flow state.
   Read-only research and review agents return structured handoffs for that lead
   to persist. Parallel writers require disjoint file ownership; hooks never
   write critical state or sequence stages.
5. Repository validation covers plugin/marketplace relationships, skill
   metadata, PowerShell syntax and fixtures, safe paths, state transitions,
   hashes, review gates, and immutable plan generations. Installing or trusting
   the plugin remains a separate user-authorized host action.
6. Completed flows are retained locally by default. No automated cleanup,
   publication, upload, or migration into product data is introduced.

## Consequences

- The repository gains three explicit tooling boundaries: tracked plugin
  source, tracked marketplace metadata, and ignored local flow state.
- The plugin can be reviewed and statically validated without changing user
  configuration. Installed and end-to-end runtime evidence must be reported
  separately and requires explicit authorization for installation and hook
  trust.
- `.codex/` remains the owner of repository agent/configuration policy;
  `repoplugin/` remains an unchanged source-material holding area.
- Concurrent planning is safe only through unique flow IDs and single-writer
  ownership of mutable state. Immutable plan generations are never rewritten.
- Removing the capability requires first disabling/uninstalling any explicitly
  installed copy, then removing only the tracked marketplace entry and plugin
  package. Local `.repoplugin/` flows remain recoverable until the user
  explicitly requests their removal.

## Limits

This decision adds no application project, endpoint, database schema, Azure
resource, deployment unit, product feature, MCP server, connector, credential,
or external side effect. It does not authorize plugin installation, hook trust,
Azure mutation, deployment, publication, or deletion of local flow history.

## Supersession

ADR-0008 replaces the single-package workflow, mutable flow-state engine, hooks,
and immutable-generation machinery with focused plugins that exchange a small
repository-local task-folder contract. ADR-0007 remains historical evidence for
the tracked marketplace boundary, ignored local working area, and the separation
between source registration and user-authorized host installation.
