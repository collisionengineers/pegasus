# Research — DELIV-055: self-contained migration-host recipe

## Question

What process environment must the manifest-bound migration bundle receive after
PR #702 removed the runbook heading the release reference named, and what
release behaviour must remain unchanged?

## Findings

- The existing migration reference directs the operator to a now-absent
  `docs/runbook.md` heading instead of supplying the environment itself
  (`.agents/skills/pegasus-release/references/database-migration.md`,
  lines 21–31).
- The Production Web host rejects every missing or whitespace-only value in its
  required-key list: SQL, Web identity, transport/custody/queue, Graph, Box and
  EVA configuration (`src/Pegasus.Web/Program.cs`, lines 136–174). It also
  validates the custody Blob and intake Queue URI shapes (lines 176–217).
- `infra/modules/platform.bicep` is the current deployed configuration map:
  it supplies storage/identity values and service URIs (lines 482–489), Graph
  configuration, Box endpoints/root and secret-backed values (lines 493–501),
  and EVA values (lines 507–512). `infra/main.parameters.json` names the
  approved azd inputs for Box holding and EVA public settings/secret URIs.
- `scripts/Invoke-ProductionAdministratorBootstrap.ps1` already demonstrates
  the manifest-bound azd map and the SQL, identity and storage derivations
  (lines 29–36 and 66–72); its values provide a repository-local precedent,
  not a second migration route.
- Box and EVA factories are deferred until use
  (`src/Pegasus.Web/Program.cs`, lines 688–706). A migration bundle builds
  the host but does not start the Graph webhook; actual Key Vault secret values
  are neither needed nor permitted in a process recipe. The existing reference
  nevertheless requires shape-valid Box JWT JSON, so its replacement retains
  that conservative requirement.
- The release skill requires migrations and runtime grants to finish before Web
  provision or Worker deployment (`.agents/skills/pegasus-release/SKILL.md`,
  lines 179–185). This ticket must preserve that ordering.
- `AGENTS.md` already sends release work to the existing release skill
  (lines 145–147). A compact link to its migration reference is the only
  permitted repository-instruction change.
- There is no documented Web quiescence control. The active deployment is
  single-revision, external and 100% traffic
  (`infra/modules/platform.bicep`, lines 408–424). This repair cannot resolve
  PLAT-046’s old-Web/Worker containment question.

## Implications

The repair belongs in the existing database-migration reference, using one
process-local PowerShell recipe. It must derive only non-secret values from the
approved azd environment, derive account service URIs from those names, retain
the configured public endpoint/settings values, and use explicit inert
host-only placeholders for secret-backed Graph, Box and EVA values. It must not
add a wrapper, secret retrieval, deployment command, migration-order branch,
or runtime/infrastructure change.

## Open questions

None for this bounded documentation repair. Web/Worker containment remains
explicitly outside DELIV-055 and requires separate operator/architecture
resolution.
